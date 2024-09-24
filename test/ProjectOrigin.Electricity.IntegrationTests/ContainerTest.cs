using Xunit;
using System.Threading.Tasks;
using System;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using System.Text;
using Grpc.Net.Client;
using ProjectOrigin.TestCommon;
using System.IO;
using DotNet.Testcontainers.Images;
using Xunit.Abstractions;
using Docker.DotNet;

namespace ProjectOrigin.Electricity.IntegrationTests;

public class ContainerTest : AbstractFlowTest, IAsyncLifetime
{
    private const string DockerfilePath = "Electricity.Dockerfile";
    private const int GrpcPort = 5000;

    private readonly ITestOutputHelper _outputHelper;
    private readonly IFutureDockerImage _image;
    private readonly IContainer _container;
    private readonly ModifiedDockerfile _modifiedDockerfile;

    public ContainerTest(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        var solutionDirectory = CommonDirectoryPath.GetSolutionDirectory().DirectoryPath;

        // Testcontainers doesn't support some functionality in Dockerfiles
        _modifiedDockerfile = new ModifiedDockerfile(Path.Combine(solutionDirectory, DockerfilePath), content => content
            .Replace(" --platform=$BUILDPLATFORM", "") // not supported by Testcontainers
            .Replace("-jammy-chiseled-extra", "")); // not supported by Testcontainers because of user permissions

        _image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(solutionDirectory)
            .WithDockerfile(_modifiedDockerfile.FileName)
            .WithLogger(new CustomActionLogger("ImageBuilder", outputHelper.WriteLine))
            .Build();

        var configFile = TempFile.WriteAllText($"""
        registries:
          {Registry}:
            url: http://localhost:5000
        areas:
          {Area}:
            issuerKeys:
              - publicKey: "{Convert.ToBase64String(Encoding.UTF8.GetBytes(IssuerKey.PublicKey.ExportPkixText()))}"
        """, ".yaml");

        _container = new ContainerBuilder()
                .WithImage(_image)
                .WithPortBinding(GrpcPort, true)
                .WithResourceMapping(configFile, $"/app/tmp/")
                .WithEnvironment("Network__ConfigurationUri", $"file:///app/tmp/{Path.GetFileName(configFile)}")
                .WithCommand("--serve")
                .WithWaitStrategy(
                    Wait.ForUnixContainer()
                        .UntilPortIsAvailable(GrpcPort)
                    )
                .WithLogger(new CustomActionLogger("Container", outputHelper.WriteLine))
                .Build();
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _image.CreateAsync();
            await _container.StartAsync();
        }
        catch (DockerApiException)
        {
            var log = await _container.GetLogsAsync();
            _outputHelper.WriteLine("\nContainer stdOut:\n" + log.Stdout.Replace("\n", "    " + Environment.NewLine));
            _outputHelper.WriteLine("\nContainer stdErr:\n" + log.Stderr.Replace("\n", "    " + Environment.NewLine));
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        _modifiedDockerfile.Dispose();
        await _container.StopAsync();
    }

    protected override GrpcChannel GetChannel()
    {
        var address = $"http://localhost:{_container.GetMappedPublicPort(GrpcPort)}";
        return GrpcChannel.ForAddress(address);
    }
}


using Xunit;
using System.Threading.Tasks;
using System;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using System.Text;
using Grpc.Net.Client;
using ProjectOrigin.TestCommon;
using System.IO;
using ProjectOrigin.Electricity.IntegrationTests.Fixtures;

namespace ProjectOrigin.Electricity.IntegrationTests;

public class ContainerTest : AbstractFlowTest, IAsyncLifetime, IClassFixture<ContainerImageFixture>
{
    private const int GrpcPort = 5000;
    private readonly IContainer _container;

    public ContainerTest(ContainerImageFixture imageFixture)
    {
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
                .WithImage(imageFixture.Image)
                .WithPortBinding(GrpcPort, true)
                .WithResourceMapping(configFile, $"/app/tmp/")
                .WithEnvironment("Network__ConfigurationUri", $"file:///app/tmp/{Path.GetFileName(configFile)}")
                .WithCommand("--serve")
                .WithWaitStrategy(
                    Wait.ForUnixContainer()
                        .UntilPortIsAvailable(GrpcPort)
                    )
                .Build();
    }

    protected override GrpcChannel GetChannel()
    {
        var address = $"http://localhost:{_container.GetMappedPublicPort(GrpcPort)}";
        return GrpcChannel.ForAddress(address);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
    }
}

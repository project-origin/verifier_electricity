using Xunit;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Microsoft.Extensions.Logging;

namespace ProjectOrigin.Electricity.IntegrationTests;

public class ContainerTest
{
    private const string DockerfilePath = "ProjectOrigin.Registry.Server/Dockerfile";

    [Fact]
    public async Task TestLatencyParallel()
    {

        TestcontainersSettings.Logger = LoggerFactory.Create(x =>
            {
                x.AddConsole();
                x.SetMinimumLevel(LogLevel.Debug);
            }).CreateLogger<ContainerTest>();

        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), string.Empty)
            .WithDockerfile(DockerfilePath)
            .WithBuildArgument("BUILDPLATFORM", "linux/arm64")
            .Build();

        await image.CreateAsync().ConfigureAwait(false);
    }
}

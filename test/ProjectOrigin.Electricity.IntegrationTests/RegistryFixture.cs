using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using Grpc.Net.Client;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.TestCommon.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace ProjectOrigin.Electricity.IntegrationTests;

public class RegistryFixture : IAsyncLifetime
{
    protected const int GrpcPort = 5000;
    private const int RabbitMqHttpPort = 15672;

    private const string RegistryAlias = "registry-container";
    private const string VerifierAlias = "verifier-container";
    private const string VerifierPostgresAlias = "verifier-postgres-container";
    private const string RabbitMqAlias = "rabbitmq-container";

    private readonly IContainer registryContainer;
    private readonly IContainer verifierContainer;
    private readonly Testcontainers.RabbitMq.RabbitMqContainer rabbitMqContainer;
    private readonly PostgreSqlContainer registryPostgresContainer;
    protected readonly INetwork Network;
    private readonly IFutureDockerImage rabbitMqImage;
    public IPrivateKey Dk1IssuerKey { get; init; }
    public IPrivateKey Dk2IssuerKey { get; init; }

    private ModifiedDockerfile _modifiedDockerfile;
    private IFutureDockerImage _verifierImage;

    public string ConfigFile { get; init; }
    public string RegistryName { get; } = "TestRegistry";
    public GrpcChannel RegistryChannel =>
        GrpcChannel.ForAddress($"http://localhost:{registryContainer.GetMappedPublicPort(5000)}");


    public RegistryFixture()
    {
        // Dotnet disallows HTTP/2 plaintext, unless we force it to
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        Network = new NetworkBuilder().WithName(Guid.NewGuid().ToString()).Build();
        rabbitMqImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetProjectDirectory(), string.Empty)
            .WithDockerfile("rabbitmq.dockerfile")
            .Build();
        rabbitMqContainer = new RabbitMqBuilder()
            .WithImage(rabbitMqImage)
            .WithNetwork(Network)
            .WithNetworkAliases(RabbitMqAlias)
            .WithPortBinding(RabbitMqHttpPort, true)
            .Build();
        Dk1IssuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();
        Dk2IssuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();


        registryPostgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15")

            .WithNetwork(Network)
            .WithNetworkAliases(VerifierPostgresAlias)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        registryContainer = new ContainerBuilder()
            .WithImage("ghcr.io/project-origin/registry-server:2.1.0")
            .WithNetwork(Network)
            .WithNetworkAliases(RegistryAlias)
            .WithPortBinding(GrpcPort, true)
            .WithCommand("--migrate", "--serve")
            .WithEnvironment("RegistryName", RegistryName)
            .WithEnvironment("Otlp__Enabled", "false")
            .WithEnvironment("Verifiers__project_origin.electricity.v1", $"http://{VerifierAlias}:{GrpcPort}")
            .WithEnvironment("IMMUTABLELOG__TYPE", "log")
            .WithEnvironment("BlockFinalizer__Interval", "00:00:05")
            .WithEnvironment("cache__TYPE", "InMemory")
            .WithEnvironment("RabbitMq__Hostname", RabbitMqAlias)
            .WithEnvironment("RabbitMq__AmqpPort", RabbitMqBuilder.RabbitMqPort.ToString())
            .WithEnvironment("RabbitMq__HttpApiPort", RabbitMqHttpPort.ToString())
            .WithEnvironment("RabbitMq__Username", RabbitMqBuilder.DefaultUsername)
            .WithEnvironment("RabbitMq__Password", RabbitMqBuilder.DefaultPassword)
            .WithEnvironment("TransactionProcessor__ServerNumber", "0")
            .WithEnvironment("TransactionProcessor__Servers", "1")
            .WithEnvironment("TransactionProcessor__Threads", "5")
            .WithEnvironment("TransactionProcessor__Weight", "10")
            .WithEnvironment("ConnectionStrings__Database", registryPostgresContainer.GetLocalConnectionString(VerifierPostgresAlias))
            .WithWaitStrategy(Wait.ForUnixContainer().UntilGrpcEndpointIsReady(GrpcPort, "/"))
            .Build();


        var solutionDirectory = CommonDirectoryPath.GetSolutionDirectory().DirectoryPath;

        // Testcontainers doesn't support some functionality in Dockerfiles
        _modifiedDockerfile = new ModifiedDockerfile(Path.Combine(solutionDirectory, "Electricity.Dockerfile"), content => content
            .Replace(" --platform=$BUILDPLATFORM", "") // not supported by Testcontainers
            .Replace("-jammy-chiseled-extra", "")); // not supported by Testcontainers because of user permissions

        _verifierImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(solutionDirectory)
            .WithDockerfile(_modifiedDockerfile.FileName)
            .Build();


        ConfigFile = Path.GetTempFileName() + ".yaml";
        File.WriteAllText(ConfigFile, $"""
        registries:
          {RegistryName}:
            url: http://{RegistryAlias}:{GrpcPort}
        areas:
          DK1:
            issuerKeys:
              - publicKey: "{Convert.ToBase64String(Encoding.UTF8.GetBytes(Dk1IssuerKey.PublicKey.ExportPkixText()))}"
          DK2:
            issuerKeys:
              - publicKey: "{Convert.ToBase64String(Encoding.UTF8.GetBytes(Dk2IssuerKey.PublicKey.ExportPkixText()))}"
        """);

        verifierContainer = new ContainerBuilder()
                .WithImage(_verifierImage)
                .WithNetwork(Network)
                .WithNetworkAliases(VerifierAlias)
                .WithPortBinding(GrpcPort, true)
                .WithResourceMapping(ConfigFile, $"/app/tmp/")
                .WithEnvironment("Network__ConfigurationUri", $"file:///app/tmp/{Path.GetFileName(ConfigFile)}")
                .WithCommand("--serve")
                .WithWaitStrategy(
                    Wait.ForUnixContainer()
                        .UntilPortIsAvailable(GrpcPort)
                    )
                .Build();
    }

    public virtual async Task InitializeAsync()
    {
        await rabbitMqImage.CreateAsync();
        await Network.CreateAsync();
        await _verifierImage.CreateAsync();

        // Start RabbitMQ and Postgres
        await rabbitMqContainer.StartWithLoggingAsync();
        await registryPostgresContainer.StartWithLoggingAsync();

        // Start Verifier container first
        await verifierContainer.StartWithLoggingAsync();

        // Now start Registry
        await registryContainer.StartWithLoggingAsync();
    }

    public virtual async Task DisposeAsync()
    {
        await registryContainer.StopAsync();
        await registryPostgresContainer.StopAsync();
        await rabbitMqContainer.StopAsync();
        await verifierContainer.StopAsync();
        _modifiedDockerfile.Dispose();
        await Network.DisposeAsync();
    }
}

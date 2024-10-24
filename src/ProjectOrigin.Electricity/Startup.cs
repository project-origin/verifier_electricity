using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Options;
using ProjectOrigin.Electricity.Services;
using ProjectOrigin.Electricity.Verifiers;
using ProjectOrigin.ServiceCommon.Grpc;
using ProjectOrigin.ServiceCommon.Otlp;
using ProjectOrigin.ServiceCommon.UriOptionsLoader;

namespace ProjectOrigin.Electricity;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureDefaultOtlp(_configuration);
        services.ConfigureGrpc(_configuration);

        services.AddSingleton<IProtoDeserializer>(new ProtoDeserializer(Assembly.GetAssembly(typeof(V1.IssuedEvent))!));

        services.AddTransient<IEventVerifier<V1.IssuedEvent>, IssuedEventVerifier>();
        services.AddTransient<IEventVerifier<V1.AllocatedEvent>, AllocatedEventVerifier>();
        services.AddTransient<IEventVerifier<V1.ClaimedEvent>, ClaimedEventVerifier>();
        services.AddTransient<IEventVerifier<V1.SlicedEvent>, SlicedEventVerifier>();
        services.AddTransient<IEventVerifier<V1.TransferredEvent>, TransferredEventVerifier>();
        services.AddTransient<IEventVerifier<V1.UnclaimedEvent>, UnclaimedEventVerifier>();
        services.AddTransient<IEventVerifier<V1.WithdrawnEvent>, WithdrawEventVerifier>();

        services.AddTransient<IVerifierDispatcher, VerifierDispatcher>();
        services.AddTransient<IRemoteModelLoader, GrpcRemoteModelLoader>();
        services.AddTransient<IModelHydrater, ElectricityModelHydrater>();
        services.AddTransient<IGridAreaIssuerService, GridAreaIssuerOptionsService>();

        services.AddSingleton<IValidateOptions<NetworkOptions>, NetworkOptionsValidator>();
        services.AddHttpClient();
        services.ConfigureUriOptionsLoader<NetworkOptions>("network");
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<ElectricityVerifierService>();
            endpoints.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
        });
    }
}

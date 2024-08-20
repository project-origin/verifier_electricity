using ProjectOrigin.Electricity.Server;
using ProjectOrigin.ServiceCommon;

await new ServiceApplication<Startup>()
    .ConfigureWebApplication("--serve")
    .RunAsync(args);

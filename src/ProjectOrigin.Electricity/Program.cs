using ProjectOrigin.Electricity;
using ProjectOrigin.ServiceCommon;

await new ServiceApplication<Startup>()
    .ConfigureWebApplication("--serve")
    .RunAsync(args);

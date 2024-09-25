using System;
using ProjectOrigin.Electricity.Example;

if (args.Length < 6)
{
    await Console.Error.WriteLineAsync("Insufficient arguments");
    return 1;
}

return await new Flow
{
    Area = args[0],
    IssuerKey = args[1],
    ProdRegistryName = args[2],
    ProdRegistryAddress = args[3],
    ConsRegistryName = args[4],
    ConsRegistryAddress = args[5],
}.Run();

using System.Collections.Generic;
using Microsoft.Extensions.Options;
using ProjectOrigin.Electricity.Options;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Electricity.Tests;

public class NetworkOptionsFake : IOptions<NetworkOptions>
{
    public NetworkOptions Value { get; init; }

    public NetworkOptionsFake(string gridArea, IPrivateKey issuerKey)
    {
        Value = new NetworkOptions()
        {
            Registries = new Dictionary<string, RegistryInfo>(),
            Areas = new Dictionary<string, AreaInfo>(){
                {gridArea, new AreaInfo(){
                    IssuerKeys = new List<KeyInfo>(){
                        new KeyInfo(){
                            PublicKey = issuerKey.PublicKey
                        }
                    }
                }}
            },
        };
    }
}

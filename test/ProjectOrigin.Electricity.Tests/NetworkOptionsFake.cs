using System.Collections.Generic;
using Microsoft.Extensions.Options;
using ProjectOrigin.Electricity.Options;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Electricity.Tests;

public class NetworkOptionsFake : IOptions<NetworkOptions>
{
    public NetworkOptions Value { get; init; }

    public NetworkOptionsFake(string gridArea = "Narnia", IPrivateKey? issuerKey = null, int? daysToExpiry = null)
    {
        Value = new NetworkOptions()
        {
            DaysBeforeCertificatesExpire = daysToExpiry,
            Registries = new Dictionary<string, RegistryInfo>(),
            Areas = new Dictionary<string, AreaInfo>(){
                {gridArea, new AreaInfo(){
                    IssuerKeys = new List<KeyInfo>(){
                        new KeyInfo(){
                            PublicKey = (issuerKey ?? Algorithms.Ed25519.GenerateNewPrivateKey()).PublicKey
                        }
                    }
                }}
            },
        };
    }
}

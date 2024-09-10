using System;
using System.Collections.Generic;
using System.Text;
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
                            PublicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(issuerKey.PublicKey.ExportPkixText()))
                        }
                    }
                }}
            },
        };
    }
}

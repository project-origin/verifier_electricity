using System.Collections.Generic;

using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Electricity.Options;

public record NetworkOptions
{
    public IDictionary<string, RegistryInfo> Registries { get; init; } = new Dictionary<string, RegistryInfo>();
    public IDictionary<string, AreaInfo> Areas { get; init; } = new Dictionary<string, AreaInfo>();
    public int? DaysBeforeCertificatesExpire { get; init; }
}

public record RegistryInfo
{
    public required string Url { get; init; }
}

public record ChroniclerInfo
{
    public required string Url { get; init; }

    public required IList<KeyInfo> SignerKeys { get; set; } = new List<KeyInfo>();
}

public class AreaInfo
{
    public ChroniclerInfo? Chronicler { get; init; }
    public required IList<KeyInfo> IssuerKeys { get; set; }
}

public record KeyInfo
{
    public required IPublicKey PublicKey { get; init; }
}

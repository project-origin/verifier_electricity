using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Options;
using ProjectOrigin.Electricity.Server.Interfaces;
using ProjectOrigin.Electricity.Server.Options;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Electricity.Server.Services;

public class GridAreaIssuerOptionsService : IGridAreaIssuerService
{
    private readonly IOptionsMonitor<NetworkOptions> _options;

    public GridAreaIssuerOptionsService(IOptionsMonitor<NetworkOptions> options)
    {
        _options = options;
    }

    public IEnumerable<IPublicKey> GetAreaPublicKey(string area)
    {
        if (_options.CurrentValue.Areas.TryGetValue(area, out var areaInfo))
        {
            return areaInfo.IssuerKeys.Select(x =>
            {
                var keyText = Encoding.UTF8.GetString(Convert.FromBase64String(x.PublicKey));
                return Algorithms.Ed25519.ImportPublicKeyText(keyText);
            });
        }
        return Enumerable.Empty<IPublicKey>();
    }
}

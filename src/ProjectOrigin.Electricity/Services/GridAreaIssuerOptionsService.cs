using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Options;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Options;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Electricity.Services;

public class GridAreaIssuerOptionsService : IGridAreaIssuerService
{
    private readonly IOptions<NetworkOptions> _options;

    public GridAreaIssuerOptionsService(IOptions<NetworkOptions> options)
    {
        _options = options;
    }

    public IEnumerable<IPublicKey> GetAreaPublicKey(string area)
    {
        if (_options.Value.Areas.TryGetValue(area, out var areaInfo))
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

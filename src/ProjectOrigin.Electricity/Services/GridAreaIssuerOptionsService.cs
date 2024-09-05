using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Options;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Electricity.Services;

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
            return areaInfo.IssuerKeys.Select(x => x.PublicKey);
        }
        return Enumerable.Empty<IPublicKey>();
    }
}

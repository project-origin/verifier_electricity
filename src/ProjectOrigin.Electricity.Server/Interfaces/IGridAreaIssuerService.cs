using System.Collections.Generic;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Electricity.Server.Interfaces;

public interface IGridAreaIssuerService
{
    IEnumerable<IPublicKey> GetAreaPublicKey(string area);
}

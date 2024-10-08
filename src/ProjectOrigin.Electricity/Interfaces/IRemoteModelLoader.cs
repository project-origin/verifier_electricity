using System.Threading.Tasks;
using ProjectOrigin.Common.V1;

namespace ProjectOrigin.Electricity.Interfaces;

public interface IRemoteModelLoader
{
    Task<T?> GetModel<T>(FederatedStreamId federatedStreamId) where T : class;
}

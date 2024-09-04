using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using ProjectOrigin.Common.V1;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Options;

namespace ProjectOrigin.Electricity.Services;

public class GrpcRemoteModelLoader : IRemoteModelLoader
{
    private readonly IModelHydrater _modelHydrater;
    private readonly IProtoDeserializer _protoDeserializer;
    private readonly IOptionsMonitor<NetworkOptions> _options;

    public GrpcRemoteModelLoader(IModelHydrater modelHydrater, IProtoDeserializer protoDeserializer, IOptionsMonitor<NetworkOptions> options)
    {
        _modelHydrater = modelHydrater;
        _protoDeserializer = protoDeserializer;
        _options = options;
    }

    public GrpcChannel GetChannel(string registryName)
    {
        if (_options.CurrentValue.Registries.TryGetValue(registryName, out var registryInfo))
        {
            return GrpcChannel.ForAddress(registryInfo.Url);
        }
        else
        {
            throw new KeyNotFoundException($"Registry ”{registryName}” not known");
        }
    }

    public async Task<T?> GetModel<T>(FederatedStreamId federatedStreamId) where T : class
    {
        using (var channel = GetChannel(federatedStreamId.Registry))
        {
            var client = new Registry.V1.RegistryService.RegistryServiceClient(channel);

            var stream = await client.GetStreamTransactionsAsync(new Registry.V1.GetStreamTransactionsRequest
            {
                StreamId = federatedStreamId.StreamId,
            });

            var deserializedStream = stream.Transactions.Select(x => _protoDeserializer.Deserialize(x.Header.PayloadType, x.Payload)).ToList();

            return _modelHydrater.HydrateModel<T>(deserializedStream);
        }
    }
}

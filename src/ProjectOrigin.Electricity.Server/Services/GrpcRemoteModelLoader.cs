using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using ProjectOrigin.Common.V1;
using ProjectOrigin.Electricity.Server.Interfaces;
using ProjectOrigin.Electricity.Server.Options;

namespace ProjectOrigin.Electricity.Server.Services;

public class GrpcRemoteModelLoader : IRemoteModelLoader
{
    private readonly IModelHydrater _modelHydrater;
    private readonly IProtoDeserializer _protoDeserializer;
    private readonly RegistryOptions _registryOptions;

    public GrpcRemoteModelLoader(IModelHydrater modelHydrater, IProtoDeserializer protoDeserializer, IOptions<RegistryOptions> registryOptions)
    {
        _modelHydrater = modelHydrater;
        _protoDeserializer = protoDeserializer;
        _registryOptions = registryOptions.Value;
    }

    public GrpcChannel GetChannel(string registryName)
    {
        if (_registryOptions.Registries.TryGetValue(registryName, out var registryInfo))
        {
            return GrpcChannel.ForAddress(registryInfo.Address);
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

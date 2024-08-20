using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProjectOrigin.Electricity.Server;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.Verifier.V1;
using Google.Protobuf;
using FluentAssertions;
using System.Security.Cryptography;
using Xunit;
using System.Text;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.Electricity.Server.Interfaces;
using ProjectOrigin.Electricity.Server.Services;
using ProjectOrigin.TestCommon.Fixtures;
using ProjectOrigin.TestCommon;

namespace ProjectOrigin.Electricity.IntegrationTests;

public class ExceptionTest : IClassFixture<TestServerFixture<Startup>>
{
    private const string Area = "TestArea";
    private const string Registry = "test-registry";

    private readonly IPrivateKey _issuerKey;
    private readonly TestServerFixture<Startup> _serviceFixture;

    public ExceptionTest(TestServerFixture<Startup> serviceFixture)
    {
        _issuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();
        _serviceFixture = serviceFixture;

        var configFile = TempFile.WriteAllText($"""
        registries:
          {Registry}:
            url: http://localhost:5000
        areas:
          {Area}:
            issuerKeys:
              - publicKey: "{Convert.ToBase64String(Encoding.UTF8.GetBytes(_issuerKey.PublicKey.ExportPkixText()))}"
        """, ".yaml");

        serviceFixture.ConfigureHostConfiguration(new Dictionary<string, string?>()
        {
            {"network:ConfigurationUri", "file://" + configFile},
        });

        serviceFixture.ConfigureTestServices += (services) =>
        {
            services.RemoveAll<IRemoteModelLoader>();
            services.AddTransient<IRemoteModelLoader, GrpcRemoteModelLoader>();
        };
    }

    [Fact]
    public async Task NoVerifierForType_ReturnInvalid()
    {
        var client = new VerifierService.VerifierServiceClient(_serviceFixture.Channel);

        IMessage @event = new Common.V1.Uuid();
        var request = CreateInvalidSignedEvent(_issuerKey, @event, @event.Descriptor.FullName);

        var result = await client.VerifyTransactionAsync(request);

        result.ErrorMessage.Should().Be($"No verifier implemented for payload type ”{@event.Descriptor.FullName}”");
        result.Valid.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidData_ReturnInvalid()
    {
        var client = new VerifierService.VerifierServiceClient(_serviceFixture.Channel);

        IMessage @event = new Common.V1.Uuid
        {
            Value = Guid.NewGuid().ToString()
        };

        var otherTypeName = V1.AllocatedEvent.Descriptor.FullName;
        var request = CreateInvalidSignedEvent(_issuerKey, @event, otherTypeName);

        var result = await client.VerifyTransactionAsync(request);

        result.ErrorMessage.Should().Be("Could not deserialize invalid payload of type ”project_origin.electricity.v1.AllocatedEvent”");
        result.Valid.Should().BeFalse();
    }

    [Fact]
    public async Task UnknownType_ReturnInvalid()
    {
        var client = new VerifierService.VerifierServiceClient(_serviceFixture.Channel);

        IMessage @event = new Common.V1.Uuid
        {
            Value = Guid.NewGuid().ToString()
        };

        var request = CreateInvalidSignedEvent(_issuerKey, @event, "SomeRandomName");

        var result = await client.VerifyTransactionAsync(request);

        result.ErrorMessage.Should().Be("Could not deserialize unknown type ”SomeRandomName”");
        result.Valid.Should().BeFalse();
    }

    private static VerifyTransactionRequest CreateInvalidSignedEvent(IPrivateKey signerKey, IMessage @event, string type)
    {
        var header = new Registry.V1.TransactionHeader()
        {
            FederatedStreamId = new Common.V1.FederatedStreamId()
            {
                Registry = Registry,
                StreamId = new Common.V1.Uuid
                {
                    Value = Guid.NewGuid().ToString()
                }
            },
            PayloadType = type,
            PayloadSha512 = ByteString.CopyFrom(SHA512.HashData(@event.ToByteArray())),
            Nonce = Guid.NewGuid().ToString(),
        };

        var transaction = new Registry.V1.Transaction()
        {
            Header = header,
            HeaderSignature = ByteString.CopyFrom(signerKey.Sign(header.ToByteArray())),
            Payload = @event.ToByteString()
        };

        return new VerifyTransactionRequest
        {
            Transaction = transaction
        };
    }
}


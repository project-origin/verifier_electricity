using Xunit;
using System.Threading.Tasks;
using System;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using Grpc.Net.Client;
using FluentAssertions;
using Google.Protobuf;
using System.Security.Cryptography;
using ProjectOrigin.Verifier.V1;
using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.Common.V1;

namespace ProjectOrigin.Electricity.IntegrationTests;

public abstract class AbstractFlowTest
{
    protected const string Area = "TestArea";
    protected const string Registry = "test-registry";
    private const int GrpcPort = 5000;

    protected IPrivateKey IssuerKey { get; init; }
    protected abstract GrpcChannel GetChannel();

    public AbstractFlowTest()
    {
        // The same key is used for all tests. A dynamic key caused issued when used together when used
        // with IClassFixture<TestServerFixture<Startup>> which reuses configuration between tests.
        IssuerKey = Algorithms.Ed25519.ImportPrivateKeyText("-----BEGIN PRIVATE KEY-----MC4CAQAwBQYDK2VwBCIEIGMlF8WEyXOy8ZSRkfq+TlNSnFdtjs9ONkBDf2fsHMA6-----END PRIVATE KEY-----");
    }

    [Fact]
    public async Task IssueConsumptionCertificate_Success()
    {
        // Arrange
        var @event = CreateIssuedEvent();

        // Act
        var result = await SignEventAndVerify(@event.CertificateId, @event, IssuerKey);

        // Assert
        result.ErrorMessage.Should().BeEmpty();
        result.Valid.Should().BeTrue();
    }

    [Fact]
    public async Task WithdrawCertificate_Success()
    {
        // Arrange
        var @event = CreateIssuedEvent();
        var issueCertificateTransaction = SignEvent(@event.CertificateId, @event, IssuerKey);

        // Act
        var result = await SignEventAndVerify(@event.CertificateId, new WithdrawnEvent(), IssuerKey, new[] { issueCertificateTransaction });

        // Assert
        result.ErrorMessage.Should().BeEmpty();
        result.Valid.Should().BeTrue();
    }

    [Fact]
    public async Task UnclaimCertificate_FailedWithMissingCertificateSlice()
    {
        // Arrange
        var issueCertificateEvent = CreateIssuedEvent();
        var issueCertificateTransaction = SignEvent(issueCertificateEvent.CertificateId, issueCertificateEvent, IssuerKey);

        // Act
        var result = await SignEventAndVerify(issueCertificateEvent.CertificateId, CreateUnclaimEvent(), IssuerKey, new[] { issueCertificateTransaction });

        // Assert
        result.Valid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Certificate claim does not exist");
    }

    protected static V1.IssuedEvent CreateIssuedEvent()
    {
        var owner = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var commitmentInfo = new SecretCommitmentInfo(250);
        var certId = Guid.NewGuid().ToString();

        return new V1.IssuedEvent
        {
            CertificateId = CreateId(certId),
            Type = GranularCertificateType.Consumption,
            Period = new V1.DateInterval
            {
                Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2023, 1, 1, 12, 0, 0, 0, TimeSpan.Zero)),
                End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2023, 1, 1, 13, 0, 0, 0, TimeSpan.Zero))
            },
            GridArea = Area,
            AssetIdHash = ByteString.Empty,
            QuantityCommitment = new Electricity.V1.Commitment
            {
                Content = ByteString.CopyFrom(commitmentInfo.Commitment.C),
                RangeProof = ByteString.CopyFrom(commitmentInfo.CreateRangeProof(certId))
            },
            OwnerPublicKey = new Electricity.V1.PublicKey
            {
                Content = ByteString.CopyFrom(owner.PublicKey.Export())
            }
        };
    }

    protected static FederatedStreamId CreateId(string certId)
    {
        return new FederatedStreamId()
        {
            Registry = Registry,
            StreamId = new Uuid()
            {
                Value = certId
            }
        };
    }

    protected async Task<VerifyTransactionResponse> SignEventAndVerify(Common.V1.FederatedStreamId streamId, IMessage @event, IPrivateKey key, IEnumerable<Registry.V1.Transaction>? stream = null)
    {
        var client = new Verifier.V1.VerifierService.VerifierServiceClient(GetChannel());
        var request = new Verifier.V1.VerifyTransactionRequest
        {
            Transaction = SignEvent(streamId, @event, key)
        };

        if (stream is not null)
            request.Stream.AddRange(stream);

        var result = await client.VerifyTransactionAsync(request);
        return result;
    }

    protected static UnclaimedEvent CreateUnclaimEvent()
    {
        return new UnclaimedEvent
        {
            AllocationId = new Uuid() { Value = Guid.NewGuid().ToString() },
        };
    }

    protected static Registry.V1.Transaction SignEvent(Common.V1.FederatedStreamId streamId, IMessage @event, IPrivateKey signerKey)
    {
        var header = new Registry.V1.TransactionHeader()
        {
            FederatedStreamId = streamId,
            PayloadType = @event.Descriptor.FullName,
            PayloadSha512 = ByteString.CopyFrom(SHA512.HashData(@event.ToByteArray())),
            Nonce = Guid.NewGuid().ToString(),
        };

        var transaction = new Registry.V1.Transaction()
        {
            Header = header,
            HeaderSignature = ByteString.CopyFrom(signerKey.Sign(header.ToByteArray())),
            Payload = @event.ToByteString()
        };

        return transaction;
    }
}

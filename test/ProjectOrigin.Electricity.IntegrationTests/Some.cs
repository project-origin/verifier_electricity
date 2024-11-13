using System;
using System.Security.Cryptography;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Registry.V1;

namespace ProjectOrigin.Electricity.IntegrationTests;
public static class Some
{
    public static SendTransactionsRequest TransactionsRequest(Common.V1.FederatedStreamId streamId, IMessage @event, IPrivateKey signerKey)
    {
        var request = new SendTransactionsRequest();
        request.Transactions.Add(Transaction(streamId, @event, signerKey));
        return request;
    }
    public static SecretCommitmentInfo SecretCommitmentInfo(uint value = 250)
    {
        return new SecretCommitmentInfo(value);
    }

    public static Transaction Transaction(Common.V1.FederatedStreamId streamId, IMessage @event, IPrivateKey signerKey)
    {
        var header = new TransactionHeader()
        {
            FederatedStreamId = streamId,
            PayloadType = @event.Descriptor.FullName,
            PayloadSha512 = ByteString.CopyFrom(SHA512.HashData(@event.ToByteArray())),
            Nonce = Guid.NewGuid().ToString(),
        };

        return new Transaction()
        {
            Header = header,
            HeaderSignature = ByteString.CopyFrom(signerKey.Sign(header.ToByteArray())),
            Payload = @event.ToByteString()
        };
    }

    public static IHDPrivateKey Owner()
    {
        return Algorithms.Secp256k1.GenerateNewPrivateKey();
    }

    public static IssuedEvent IssuedEvent(IPublicKey publicOwnerKey, string registry, string area, GranularCertificateType type = GranularCertificateType.Consumption, SecretCommitmentInfo? commitmentInfo = null)
    {
        commitmentInfo ??= new SecretCommitmentInfo(250);
        var certId = Guid.NewGuid().ToString();

        return new IssuedEvent()
        {
            CertificateId = new Common.V1.FederatedStreamId
            {
                Registry = registry,
                StreamId = new Common.V1.Uuid
                {
                    Value = certId
                },
            },
            Type = type,
            Period = new DateInterval
            {
                Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2023, 1, 1, 12, 0, 0, 0, TimeSpan.Zero)),
                End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2023, 1, 1, 13, 0, 0, 0, TimeSpan.Zero))
            },
            GridArea = area,
            AssetIdHash = ByteString.Empty,
            QuantityCommitment = new V1.Commitment
            {
                Content = ByteString.CopyFrom(commitmentInfo.Commitment.C),
                RangeProof = ByteString.CopyFrom(commitmentInfo.CreateRangeProof(certId))
            },
            OwnerPublicKey = new V1.PublicKey()
            {
                Content = ByteString.CopyFrom(publicOwnerKey.Export()),
                Type = KeyType.Secp256K1
            }
        };
    }
    public static AllocatedEvent AllocatedEvent(IssuedEvent certC, IssuedEvent certP, SecretCommitmentInfo commitmentC, SecretCommitmentInfo commitmentP)
    {
        var allocationId = Guid.NewGuid();
        var proof = PedersenCommitment.SecretCommitmentInfo.CreateEqualityProof(commitmentP, commitmentC, allocationId.ToString());
        return new AllocatedEvent()
        {
            AllocationId = new Common.V1.Uuid() { Value = allocationId.ToString() },
            ConsumptionCertificateId = certC.CertificateId,
            ConsumptionSourceSliceHash = ByteString.CopyFrom(SHA256.HashData(commitmentC.Commitment.C)),
            ProductionCertificateId = certP.CertificateId,
            ProductionSourceSliceHash = ByteString.CopyFrom(SHA256.HashData(commitmentP.Commitment.C)),
            EqualityProof = ByteString.CopyFrom(proof)
        };
    }

    public static ClaimedEvent ClaimedEvent(IssuedEvent issuedEvent, Common.V1.Uuid allocationId)
    {
        return new ClaimedEvent
        {
            AllocationId = allocationId,
            CertificateId = issuedEvent.CertificateId,
        };
    }

    public static UnclaimedEvent UnclaimedEvent(Common.V1.Uuid allocationId)
    {
        return new UnclaimedEvent
        {
            AllocationId = allocationId
        };
    }
}

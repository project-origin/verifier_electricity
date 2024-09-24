using System;
using AutoFixture;
using Google.Protobuf;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.PedersenCommitment;
using Xunit;

namespace ProjectOrigin.Electricity.Tests;

public class CertificateApplyTests
{
    private Fixture _fix = new Fixture();

    private Common.V1.FederatedStreamId CreateId()
    {
        var registry = _fix.Create<string>();
        var streamId = Guid.NewGuid();

        return new Common.V1.FederatedStreamId
        {
            Registry = registry,
            StreamId = new Common.V1.Uuid
            {
                Value = streamId.ToString()
            }
        };
    }

    private (GranularCertificate, SecretCommitmentInfo) Create()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var area = _fix.Create<string>();
        return FakeRegister.ProductionIssued(ownerKey.PublicKey, _fix.Create<uint>(), area);
    }

    [Fact]
    public void Certificate_Apply_SliceEvent()
    {
        var allocationId = Guid.NewGuid().ToProto();
        var (cert, slice0) = Create();

        var @event = new V1.SlicedEvent()
        {
            CertificateId = cert.Id,
            SourceSliceHash = slice0.ToSliceId(),
        };

        var slice1 = new SecretCommitmentInfo(_fix.Create<uint>());
        var owner1 = Algorithms.Secp256k1.GenerateNewPrivateKey();
        @event.NewSlices.Add(new V1.SlicedEvent.Types.Slice
        {
            Quantity = slice1.ToProtoCommitment(cert.Id.StreamId.Value),
            NewOwner = owner1.PublicKey.ToProto()
        });

        var slice2 = new SecretCommitmentInfo(_fix.Create<uint>());
        var owner2 = Algorithms.Secp256k1.GenerateNewPrivateKey();
        @event.NewSlices.Add(new V1.SlicedEvent.Types.Slice
        {
            Quantity = slice2.ToProtoCommitment(cert.Id.StreamId.Value),
            NewOwner = owner2.PublicKey.ToProto()
        });

        cert.Apply(@event);

        Assert.Null(cert.GetCertificateSlice(slice0.ToSliceId()));
        Assert.NotNull(cert.GetCertificateSlice(slice1.ToSliceId()));
        Assert.NotNull(cert.GetCertificateSlice(slice2.ToSliceId()));
    }

    [Fact]
    public void Certificate_Apply_TransferEvent()
    {
        var allocationId = Guid.NewGuid().ToProto();
        var (cert, slice0) = Create();

        var newOwner = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var @event = new V1.TransferredEvent()
        {
            CertificateId = cert.Id,
            SourceSliceHash = slice0.ToSliceId(),
            NewOwner = newOwner.PublicKey.ToProto()
        };

        var slice = cert.GetCertificateSlice(slice0.ToSliceId());
        Assert.NotNull(slice);
        Assert.NotEqual(newOwner.PublicKey.ToProto(), slice!.Owner);

        cert.Apply(@event);
        slice = cert.GetCertificateSlice(slice0.ToSliceId());
        Assert.NotNull(slice);
        Assert.Equal(newOwner.PublicKey.ToProto(), slice!.Owner);
    }

    [Fact]
    public void Certificate_Apply_ClaimedEvent()
    {
        var allocationId = Guid.NewGuid().ToProto();
        var consumptionId = CreateId();
        var (cert, prodQuantity) = Create();
        var consQuantity = new SecretCommitmentInfo(_fix.Create<uint>());

        var allocationEvent = new V1.AllocatedEvent()
        {
            AllocationId = allocationId,
            ProductionCertificateId = cert.Id,
            ConsumptionCertificateId = consumptionId,
            ProductionSourceSliceHash = prodQuantity.ToSliceId(),
            ConsumptionSourceSliceHash = consQuantity.ToSliceId(),
            EqualityProof = ByteString.CopyFrom(SecretCommitmentInfo.CreateEqualityProof(consQuantity, prodQuantity, allocationId.Value))
        };
        cert.Apply(allocationEvent);

        var claimedEvent = new V1.ClaimedEvent
        {
            AllocationId = allocationId,
            CertificateId = cert.Id,
        };
        cert.Apply(claimedEvent);

        Assert.Null(cert.GetCertificateSlice(consQuantity.ToSliceId()));
        Assert.Null(cert.GetAllocation(allocationId));
        Assert.False(cert.HasAllocation(allocationId));
        Assert.True(cert.HasClaim(allocationId));
    }

    [Fact]
    public void Certificate_Apply_WithdrawnEvent()
    {
        var (cert, _) = Create();
        var @event = new V1.WithdrawnEvent();

        cert.Apply(@event);

        Assert.True(cert.IsCertificateWithdrawn);
    }

    [Fact]
    public void Certificate_Default_WithdrawnIsFalse()
    {
        var (cert, _) = Create();

        Assert.False(cert.IsCertificateWithdrawn);
    }

    [Fact]
    public void Certificate_Apply_UnclaimedEvent()
    {
        var allocationId = Guid.NewGuid().ToProto();
        var consumptionId = CreateId();
        var (cert, prodQuantity) = Create();
        var consQuantity = new SecretCommitmentInfo(_fix.Create<uint>());

        // Allocate
        var allocationEvent = new V1.AllocatedEvent()
        {
            AllocationId = allocationId,
            ProductionCertificateId = cert.Id,
            ConsumptionCertificateId = consumptionId,
            ProductionSourceSliceHash = prodQuantity.ToSliceId(),
            ConsumptionSourceSliceHash = consQuantity.ToSliceId(),
            EqualityProof = ByteString.CopyFrom(SecretCommitmentInfo.CreateEqualityProof(consQuantity, prodQuantity, allocationId.Value))
        };
        cert.Apply(allocationEvent);

        // Claim
        var claimedEvent = new V1.ClaimedEvent
        {
            AllocationId = allocationId,
            CertificateId = cert.Id,
        };
        cert.Apply(claimedEvent);

        // Unclaim
        var unclaimedEvent = new V1.UnclaimedEvent
        {
            AllocationId = allocationId
        };
        cert.Apply(unclaimedEvent);


        Assert.NotNull(cert.GetCertificateSlice(prodQuantity.ToSliceId()));
        Assert.Null(cert.GetAllocation(allocationId));
        Assert.Null(cert.GetClaim(allocationId));
    }
}

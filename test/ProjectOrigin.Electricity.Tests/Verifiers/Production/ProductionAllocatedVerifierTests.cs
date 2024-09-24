using System;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using Moq;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Verifiers;
using ProjectOrigin.HierarchicalDeterministicKeys;
using MsOptions = Microsoft.Extensions.Options.Options;
using Xunit;
using Google.Protobuf.WellKnownTypes;
using ProjectOrigin.Electricity.Options;

namespace ProjectOrigin.Electricity.Tests;

public class ProductionAllocatedVerifierTests
{
    private AllocatedEventVerifier _verifier;
    private GranularCertificate? _otherCertificate;

    public ProductionAllocatedVerifierTests()
    {
        var modelLoaderMock = new Mock<IRemoteModelLoader>();
        modelLoaderMock.Setup(obj => obj.GetModel<GranularCertificate>(It.IsAny<Common.V1.FederatedStreamId>()))
            .Returns(() => Task.FromResult(_otherCertificate));
        _verifier = new AllocatedEventVerifier(modelLoaderMock.Object, MsOptions.Create(new NetworkOptions { }));
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_Valid()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: consCert.Period.Clone());
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertValid();
    }


    [Fact]
    public async Task Verifier_InvalidProductionSlice_SliceNotFound()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, consParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Production slice does not exist");
    }

    [Fact]
    public async Task Verifier_WrongKey_InvalidSignature()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var otherKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, otherKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Invalid signature for slice");
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_ConsCertNotFould()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = null;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("ConsumptionCertificate does not exist");
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_ValidPeriod_EnclosingStart()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 15, 0, TimeSpan.Zero))
        });
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_ValidPeriod_EnclosingEnd()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 45, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_ValidPeriod_EnclosingWithin()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 30, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 35, 0, TimeSpan.Zero))
        });
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_InvalidPeriod_DifferentPeriods()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 14, 0, 0, TimeSpan.Zero))
        });

        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Periods are not overlapping");
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_InvalidPeriod_Before()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 30, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 30, 0, TimeSpan.Zero))
        });

        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Periods are not overlapping");
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_InvalidPeriod_After()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 11, 30, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 30, 0, TimeSpan.Zero))
        });

        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Periods are not overlapping");
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_AllowCrossArea()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, area: "DK1");
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, area: "DK2");
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task Verifier_WrongConsumptionSlice_SliceNotFound()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, prodParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Consumption slice does not exist");
    }

    [Fact]
    public async Task Verifier_RandomProofData_InvalidEqualityProof()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams, overwrideEqualityProof: new Fixture().CreateMany<byte>(64).ToArray());
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Invalid Equality proof");
    }

    [Fact]
    public async Task Verifier_AllocationCerticate_InvalidCertificateIsWithdrawnInvalidEqualityProof()
    {
        // Arrange
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams, overwrideEqualityProof: new Fixture().CreateMany<byte>(64).ToArray());
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        // Act
        prodCert.Withdrawn();
        var result = await _verifier.Verify(transaction, prodCert, @event);

        // Assert
        result.AssertInvalid("Certificate is withdrawn");
    }
}

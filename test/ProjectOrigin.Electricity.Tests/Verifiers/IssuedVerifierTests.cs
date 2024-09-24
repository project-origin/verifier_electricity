using System;
using System.Threading.Tasks;
using AutoFixture;
using Google.Protobuf.WellKnownTypes;
using ProjectOrigin.Electricity.Extensions;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.Electricity.Services;
using ProjectOrigin.Electricity.Verifiers;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using Xunit;

namespace ProjectOrigin.Electricity.Tests;

public class IssuedVerifierTests
{
    const string IssuerArea = "DK1";
    private IPrivateKey _issuerKey;
    private IssuedEventVerifier _verifier;

    public IssuedVerifierTests()
    {
        _issuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        var options = new NetworkOptionsFake(IssuerArea, _issuerKey);
        var issuerService = new GridAreaIssuerOptionsService(options);

        _verifier = new IssuedEventVerifier(issuerService);
    }

    [Fact]
    public async Task IssuedVerifier_IssueCertificate_Success()
    {
        var @event = FakeRegister.CreateProductionIssuedEvent();
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, _issuerKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task IssuedVerifier_IssueCertificateWithPublicQuantity_Success()
    {
        var @event = FakeRegister.CreateProductionIssuedEvent(publicQuantity: true);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, _issuerKey);

        var a = transaction.IsSignatureValid(_issuerKey.PublicKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task IssuedVerifier_CertificateExists_Fail()
    {
        var @event = FakeRegister.CreateProductionIssuedEvent();
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, _issuerKey);
        var certifcate = new GranularCertificate(@event);

        var result = await _verifier.Verify(transaction, certifcate, @event);

        result.AssertInvalid($"Certificate with id ”{@event.CertificateId.StreamId}” already exists");
    }

    [Fact]
    public async Task IssuedVerifier_QuantityCommitmentInvalid_Fail()
    {
        var @event = FakeRegister.CreateProductionIssuedEvent(quantityCommitmentOverride: FakeRegister.InvalidCommitment());
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, _issuerKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("Invalid range proof for Quantity commitment");
    }

    [Fact]
    public async Task IssuedVerifier_PeriodInvalid_ToLong()
    {
        var @event = FakeRegister.CreateProductionIssuedEvent(periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 14, 0, 0, TimeSpan.Zero))
        });
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, _issuerKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("Invalid period, maximum period is 1 hour");
    }


    [Fact]
    public async Task IssuedVerifier_PeriodInvalid_ToSmall()
    {
        var @event = FakeRegister.CreateProductionIssuedEvent(periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero))
        });
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, _issuerKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("Invalid period, minimum period is 1 minute");
    }

    [Fact]
    public async Task IssuedVerifier_InvalidOwner_Fail()
    {
        var randomOwnerKeyData = new V1.PublicKey
        {
            Content = Google.Protobuf.ByteString.CopyFrom(new Fixture().Create<byte[]>())
        };

        var @event = FakeRegister.CreateProductionIssuedEvent(ownerKeyOverride: randomOwnerKeyData);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, _issuerKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("Invalid owner key, not a valid publicKey");
    }

    [Fact]
    public async Task IssuedVerifier_InvalidSignature_Fail()
    {
        var invalidKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        var @event = FakeRegister.CreateProductionIssuedEvent();
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, invalidKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("Invalid issuer signature for GridArea ”DK1”");
    }

    [Fact]
    public async Task IssuedVerifier_NoIssuerForArea_Fail()
    {
        var @event = FakeRegister.CreateProductionIssuedEvent(gridAreaOverride: "DK2");
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, _issuerKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("No issuer found for GridArea ”DK2”");
    }
}

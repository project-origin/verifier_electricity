using System;
using System.Threading.Tasks;
using Moq;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.Electricity.Verifiers;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using Xunit;

namespace ProjectOrigin.Electricity.Tests;

public class UnclaimedVerifierTests
{
    private UnclaimedEventVerifier _verifier;

    private IHDPrivateKey _ownerKey;

    private Guid _allocationId;
    private GranularCertificate _certificate;
    private SecretCommitmentInfo _commitment;
    private GranularCertificate _oppositeCertificate;
    private SecretCommitmentInfo _oppositeCommitment;
    private Mock<IRemoteModelLoader> _modelLoaderMock;


    public UnclaimedVerifierTests()
    {
        _allocationId = Guid.NewGuid();
        _ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        (_certificate, _commitment) = FakeRegister.ProductionIssued(_ownerKey.PublicKey, 250);
        (_oppositeCertificate, _oppositeCommitment) = FakeRegister.ConsumptionIssued(_ownerKey.PublicKey, 250);

        _modelLoaderMock = new Mock<IRemoteModelLoader>();
        _modelLoaderMock.Setup(x => x.GetModel<GranularCertificate>(_oppositeCertificate.Id)).ReturnsAsync(_oppositeCertificate);
        _modelLoaderMock.Setup(x => x.GetModel<GranularCertificate>(_certificate.Id)).ReturnsAsync(_certificate);

        _verifier = new UnclaimedEventVerifier(_modelLoaderMock.Object);
    }

    [Fact]
    public async Task UnclaimedVerifier_ValidUnclaimForProduction_Valid()
    {
        // Arrange
        // Allocate, Claim and Withdraw
        _certificate.Allocated(_oppositeCertificate, _commitment, _oppositeCommitment, _allocationId);
        _certificate.Claimed(_allocationId);
        _oppositeCertificate.Withdrawn();

        var @event = FakeRegister.CreateUnclaimedEvent(_allocationId);
        var transaction = FakeRegister.SignTransaction(_certificate.Id, @event, _ownerKey);

        // Act
        var result = await _verifier.Verify(transaction, _certificate, @event);

        // Assert
        result.AssertValid();
    }

    [Fact]
    public async Task UnclaimedVerifier_NullCertificate_CertificateNotExists()
    {
        // Arrange
        var @event = FakeRegister.CreateUnclaimedEvent(_allocationId);
        var transaction = FakeRegister.SignTransaction(_certificate.Id, @event, _ownerKey);

        // Act
        var result = await _verifier.Verify(transaction, null, @event);

        // Assert
        result.AssertInvalid("Certificate does not exist");
    }

    [Fact]
    public async Task UnclaimedVerifier_CertificateIsWithdrawn_InvalidValid()
    {
        // Verify that the certificate is not withdrawn (but the opposite certificate must be withdrawn which is validated in another test)
        // Arrange
        var @event = FakeRegister.CreateUnclaimedEvent(_allocationId);
        var transaction = FakeRegister.SignTransaction(_certificate.Id, @event, _ownerKey);

        // Act
        _certificate.Withdrawn();
        var result = await _verifier.Verify(transaction, _certificate, @event);

        // Assert
        result.AssertInvalid("Certificate is withdrawn");
    }

    [Fact]
    public async Task UnclaimedVerifier_NoClaim_InvalidBecauseOfMissingClaim()
    {
        // Arrange
        var @event = FakeRegister.CreateUnclaimedEvent(_allocationId);
        var transaction = FakeRegister.SignTransaction(_certificate.Id, @event, _ownerKey);

        // Act
        var result = await _verifier.Verify(transaction, _certificate, @event);

        // Assert
        result.AssertInvalid("Certificate claim does not exist");
    }

    [Fact]
    public async Task UnclaimedVerifier_WrongOwner_InvalidClaimSignature()
    {
        // Arrange
        var invalidOwner = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var @event = FakeRegister.CreateUnclaimedEvent(_allocationId);
        var transaction = FakeRegister.SignTransaction(_certificate.Id, @event, invalidOwner);

        _certificate.Allocated(_oppositeCertificate, _commitment, _oppositeCommitment, _allocationId);
        _certificate.Claimed(_allocationId);

        // Act
        var result = await _verifier.Verify(transaction, _certificate, @event);

        // Assert
        result.AssertInvalid("Invalid signature for claim");
    }

    [Fact]
    public async Task UnclaimedVerifier_NoOppositeCertificate_Invalid()
    {
        // Arrange
        var @event = FakeRegister.CreateUnclaimedEvent(_allocationId);
        var transaction = FakeRegister.SignTransaction(_certificate.Id, @event, _ownerKey);
        // Use a new certificate which is not in the model loader
        var (missingCertificate, missingCommitment) = FakeRegister.ConsumptionIssued(_ownerKey.PublicKey, 250);

        _certificate.Allocated(missingCertificate, _commitment, missingCommitment, _allocationId);
        _certificate.Claimed(_allocationId);

        // Act
        var result = await _verifier.Verify(transaction, _certificate, @event);

        // Assert
        result.AssertInvalid("Opposite certificate does not exist");
    }

    [Fact]
    public async Task UnclaimedVerifier_OppositeCertificateIsNotWithdrawn_Invalid()
    {
        // Arrange
        var @event = FakeRegister.CreateUnclaimedEvent(_allocationId);
        var transaction = FakeRegister.SignTransaction(_certificate.Id, @event, _ownerKey);

        _certificate.Allocated(_oppositeCertificate, _commitment, _oppositeCommitment, _allocationId);
        _certificate.Claimed(_allocationId);

        // Act
        var result = await _verifier.Verify(transaction, _certificate, @event);

        // Assert
        result.AssertInvalid("Opposite certificate is NOT withdrawn");
    }


    [Fact]
    public async Task UnclaimedVerifier_ValidUnclaimForConsumption_Valid()
    {
        // Arrange (We swap certificates and commitments to test unclaim on a consumption certificate)
        var certificate = _oppositeCertificate;
        var oppositeCertificate = _certificate;
        var commitment = _oppositeCommitment;
        var oppositeCommitment = _commitment;
        // Allocate, Claim and Withdraw
        certificate.Allocated(oppositeCertificate, oppositeCommitment, commitment, _allocationId);
        certificate.Claimed(_allocationId);
        oppositeCertificate.Withdrawn();

        var @event = FakeRegister.CreateUnclaimedEvent(_allocationId);
        var transaction = FakeRegister.SignTransaction(certificate.Id, @event, _ownerKey);

        // Act
        var result = await _verifier.Verify(transaction, certificate, @event);

        // Assert
        result.AssertValid();
    }
}

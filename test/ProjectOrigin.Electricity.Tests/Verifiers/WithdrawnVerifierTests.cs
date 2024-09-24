using System.Threading.Tasks;
using ProjectOrigin.Electricity.Options;
using ProjectOrigin.Electricity.Services;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.Electricity.Verifiers;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using Xunit;

namespace ProjectOrigin.Electricity.Tests;

public class WithdrawnVerifierTests
{
    private WithdrawEventVerifier _verifier;
    private IPrivateKey _issuerKey;
    const string IssuerArea = "DK1";

    public WithdrawnVerifierTests()
    {
        _issuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        var optionsFake = new NetworkOptionsFake(IssuerArea, _issuerKey);
        var issuerService = new GridAreaIssuerOptionsService(optionsFake);

        _verifier = new WithdrawEventVerifier(issuerService);
    }

    [Fact]
    public async Task WithdrawnEventVerifier_WithdrawnCertificate_Valid()
    {
        // Arrange
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (cert, _) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);

        var @event = new WithdrawnEvent();
        var transaction = FakeRegister.SignTransaction(cert.Id, @event, _issuerKey);

        // Act
        var result = await _verifier.Verify(transaction, cert, @event);

        // Assert
        result.AssertValid();
    }

    [Fact]
    public async Task WithdrawnEventVerifier_WithdrawnWhenAlreadyWithdrawn_Invalid()
    {
        // Arrange
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (cert, sourceParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);

        var @event = new WithdrawnEvent();
        var transaction = FakeRegister.SignTransaction(cert.Id, @event, _issuerKey);

        // Act
        cert.Withdrawn();
        var result = await _verifier.Verify(transaction, cert, @event);

        // Assert
        result.AssertInvalid("Certificate is already withdrawn");
    }

    [Fact]
    public async Task WithdrawnEventVerifier_NullCertificate_Invalid()
    {
        // Arrange
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (cert, _) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);

        var @event = new WithdrawnEvent();
        var transaction = FakeRegister.SignTransaction(cert.Id, @event, _issuerKey);

        // Act
        var result = await _verifier.Verify(transaction, null, @event);

        // Assert
        result.AssertInvalid("Certificate does not exist");
    }

    [Fact]
    public async Task WithdrawnEventVerifier_NoIssuerInNetwork_Invalid()
    {
        // Arrange
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (cert, _) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);

        var @event = new WithdrawnEvent();
        var transaction = FakeRegister.SignTransaction(cert.Id, @event, _issuerKey);

        // Create verifier with no network options
        var optionsEmpty = Microsoft.Extensions.Options.Options.Create(new NetworkOptions());
        var issuerService = new GridAreaIssuerOptionsService(optionsEmpty);
        var verifier = new WithdrawEventVerifier(issuerService);

        // Act
        var result = await verifier.Verify(transaction, cert, @event);

        // Assert
        result.AssertInvalid($"No issuer found for GridArea ”{cert.GridArea}”");
    }

    [Fact]
    public async Task WithdrawnEventVerifier_NoValidIssuerSignature_Invalid()
    {
        // Arrange
        var invalidIssuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (cert, _) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);

        var @event = new WithdrawnEvent();
        var transaction = FakeRegister.SignTransaction(cert.Id, @event, invalidIssuerKey);

        // Act
        var result = await _verifier.Verify(transaction, cert, @event);

        // Assert
        result.AssertInvalid($"Invalid issuer signature for GridArea ”{cert.GridArea}”");
    }
}

using System.Threading.Tasks;
using Moq;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Verifiers;
using ProjectOrigin.HierarchicalDeterministicKeys;
using Xunit;

namespace ProjectOrigin.Electricity.Tests;

public class ClaimedVerifierTests
{
    private ClaimedEventVerifier _verifier;


    public ClaimedVerifierTests()
    {
        var modelLoaderMock = new Mock<IRemoteModelLoader>();

        _verifier = new ClaimedEventVerifier(modelLoaderMock.Object);
    }

    [Fact]
    public async Task ClaimedVerifier_Invalid_CertificateNotExists()
    {
        // Arrange
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);

        var @event = FakeRegister.CreateClaimedEvent(allocationId, consCert.Id);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, ownerKey);

        // Act
        var result = await _verifier.Verify(transaction, null, @event);

        // Assert
        result.AssertInvalid("Certificate does not exist");
    }

    [Fact]
    public async Task ClaimedVerifier_CertificateWithdrawn_Invalid()
    {
        // Arrange
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);

        var @event = FakeRegister.CreateClaimedEvent(allocationId, prodCert.Id);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, ownerKey);

        // Act
        prodCert.Withdrawn();
        var result = await _verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Certificate is withdrawn");
    }
}

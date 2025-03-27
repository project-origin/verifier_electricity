using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf;
using ProjectOrigin.Common.V1;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using Xunit;
using static ProjectOrigin.Registry.V1.RegistryService;

namespace ProjectOrigin.Electricity.IntegrationTests;

[Collection("VerifierImageCollection")]
public class UnclaimFlowTest : IClassFixture<RegistryFixture>
{
    private readonly RegistryServiceClient _registryClient;
    private readonly string _registryName;
    private readonly IPrivateKey _issuerPrivateKeyDk1;
    private readonly IHDPrivateKey _ownerPrivateKey;

    public UnclaimFlowTest(RegistryFixture registryFixture, VerifierImageFixture verifierImageFixture)
    {
        _registryClient = new RegistryServiceClient(registryFixture.RegistryChannel);
        _registryName = registryFixture.RegistryName;
        _issuerPrivateKeyDk1 = registryFixture.Dk1IssuerKey;
        _ownerPrivateKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldUnclaimCertificateSliceAfterOppositeIsWithdrawn(bool isProductionWithdrawn)
    {
        // Issue certificates
        var (certP, commitmentP) = await IssueCertificate(GranularCertificateType.Production);
        var (certC, commitmentC) = await IssueCertificate(GranularCertificateType.Consumption);

        // Allocate (The production certificate must be allocated first)
        var allocate = Some.AllocatedEvent(certC, certP, commitmentC, commitmentP);
        await SendTransactionRequest(certP.CertificateId, allocate, _ownerPrivateKey);
        await SendTransactionRequest(certC.CertificateId, allocate, _ownerPrivateKey);

        // Claim (The production certificate must be claimed first)
        var claimP = Some.ClaimedEvent(certP, allocate.AllocationId);
        var claimC = Some.ClaimedEvent(certC, allocate.AllocationId);

        await SendTransactionRequest(claimP.CertificateId, claimP, _ownerPrivateKey);
        await SendTransactionRequest(claimC.CertificateId, claimC, _ownerPrivateKey);

        // Withdraw one certificate
        var withdraw = new WithdrawnEvent();
        if (isProductionWithdrawn)
            await SendTransactionRequest(certP.CertificateId, withdraw, _issuerPrivateKeyDk1);
        else
            await SendTransactionRequest(certC.CertificateId, withdraw, _issuerPrivateKeyDk1);


        // Unclaim on the other cerificate
        var unclaim = Some.UnclaimedEvent(allocate.AllocationId);
        if (isProductionWithdrawn)
        {
            await SendTransactionRequest(certC.CertificateId, unclaim, _ownerPrivateKey);
        }
        else
        {
            await SendTransactionRequest(certP.CertificateId, unclaim, _ownerPrivateKey);
        }
    }

    public async Task<(IssuedEvent, SecretCommitmentInfo)> IssueCertificate(GranularCertificateType type)
    {
        var commitment = Some.SecretCommitmentInfo();
        var certificate = Some.IssuedEvent(_ownerPrivateKey.PublicKey, _registryName, "DK1", type, commitment);
        await SendTransactionRequest(certificate.CertificateId, certificate, _issuerPrivateKeyDk1);
        return (certificate, commitment);
    }

    private async Task<Registry.V1.GetTransactionStatusResponse> SendTransactionRequest(FederatedStreamId certificateId, IMessage @event, IPrivateKey signerKey)
    {
        var transaction = Some.TransactionsRequest(certificateId, @event, signerKey);
        await _registryClient.SendTransactionsAsync(transaction);
        return await RepeatUntilOrTimeout(
            () => GetStatus(transaction.Transactions[0]),
            result =>
            {
                if (result.Status == Registry.V1.TransactionState.Failed)
                    throw new Exception("The transaction failed with the message: '" + result.Message + "' for the event: " + @event.Descriptor.FullName);
                return result.Status == Registry.V1.TransactionState.Committed;
            },
            TimeSpan.FromSeconds(60));
    }

    public async Task<Registry.V1.GetTransactionStatusResponse> GetStatus(Registry.V1.Transaction transaction)
    {
        return await _registryClient.GetTransactionStatusAsync(new Registry.V1.GetTransactionStatusRequest
        {
            Id = Convert.ToBase64String(SHA256.HashData(transaction.ToByteArray()))
        });
    }

    public static async Task<TResult> RepeatUntilOrTimeout<TResult>(Func<Task<TResult>> getResultFunc, Func<TResult, bool> isValidFunc, TimeSpan timeout)
    {
        var began = DateTimeOffset.UtcNow;
        while (true)
        {
            var result = await getResultFunc();
            if (isValidFunc(result))
                return result;

            await Task.Delay(100);

            if (began + timeout < DateTimeOffset.UtcNow)
            {
                throw new TimeoutException();
            }
        }
    }



}

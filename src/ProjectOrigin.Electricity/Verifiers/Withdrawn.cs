using System.Linq;
using System.Threading.Tasks;
using ProjectOrigin.Electricity.Extensions;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Models;

namespace ProjectOrigin.Electricity.Verifiers;

public class WithdrawEventVerifier : IEventVerifier<V1.WithdrawnEvent>
{
    private readonly IGridAreaIssuerService _gridAreaIssuerService;
    private readonly IExpiryChecker _expiryChecker;

    public WithdrawEventVerifier(IGridAreaIssuerService gridAreaIssuerService, IExpiryChecker expiryChecker)
    {
        _gridAreaIssuerService = gridAreaIssuerService;
        _expiryChecker = expiryChecker;
    }

    public Task<VerificationResult> Verify(Registry.V1.Transaction transaction, GranularCertificate? certificate, V1.WithdrawnEvent payload)
    {
        if (certificate is null)
            return new VerificationResult.Invalid("Certificate does not exist");

        if (certificate.IsCertificateWithdrawn)
            return new VerificationResult.Invalid("Certificate is already withdrawn");

        if (_expiryChecker.IsExpired(certificate))
            return new VerificationResult.Invalid("Certificate has expired");

        var areaPublicKeys = _gridAreaIssuerService.GetAreaPublicKey(certificate.GridArea);
        if (!areaPublicKeys.Any())
            return new VerificationResult.Invalid($"No issuer found for GridArea ”{certificate.GridArea}”");

        if (!areaPublicKeys.Any(transaction.IsSignatureValid))
            return new VerificationResult.Invalid($"Invalid issuer signature for GridArea ”{certificate.GridArea}”");

        return new VerificationResult.Valid();
    }
}

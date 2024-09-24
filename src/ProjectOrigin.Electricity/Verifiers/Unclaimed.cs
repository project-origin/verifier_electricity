using System.Threading.Tasks;
using ProjectOrigin.Electricity.Extensions;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Models;

namespace ProjectOrigin.Electricity.Verifiers;

public class UnclaimedEventVerifier : IEventVerifier<V1.UnclaimedEvent>
{
    private readonly IRemoteModelLoader _remoteModelLoader;

    public UnclaimedEventVerifier(IRemoteModelLoader remoteModelLoader)
    {
        _remoteModelLoader = remoteModelLoader;
    }

    public async Task<VerificationResult> Verify(Registry.V1.Transaction transaction, GranularCertificate? certificate, V1.UnclaimedEvent payload)
    {
        if (certificate is null)
            return new VerificationResult.Invalid("Certificate does not exist");

        if (certificate.IsCertificateWithdrawn)
            return new VerificationResult.Invalid("Certificate is withdrawn");

        var claim = certificate.GetClaim(payload.AllocationId);

        if (claim is null)
            return new VerificationResult.Invalid("Certificate claim does not exist");

        if (!transaction.IsSignatureValid(claim.Owner))
            return new VerificationResult.Invalid($"Invalid signature for claim");

        var oppostiteCertificateId = claim.ProductionCertificateId == certificate.Id ? claim.ConsumptionCertificateId : claim.ProductionCertificateId;

        var oppositeCertificate = await _remoteModelLoader.GetModel<GranularCertificate>(oppostiteCertificateId);

        if (oppositeCertificate is null)
            return new VerificationResult.Invalid("Opposite certificate does not exist");

        if (!oppositeCertificate.IsCertificateWithdrawn)
            return new VerificationResult.Invalid("Opposite certificate is NOT withdrawn");

        return new VerificationResult.Valid();
    }

}

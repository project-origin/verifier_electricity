using ProjectOrigin.Electricity.Extensions;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.Registry.V1;
using System.Threading.Tasks;
using System;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.Electricity.Interfaces;

namespace ProjectOrigin.Electricity.Verifiers;

public class ClaimedEventVerifier : IEventVerifier<V1.ClaimedEvent>
{
    private readonly IRemoteModelLoader _remoteModelLoader;
    private readonly IExpiryChecker _expiryChecker;

    public ClaimedEventVerifier(IRemoteModelLoader remoteModelLoader, IExpiryChecker expiryChecker)
    {
        _remoteModelLoader = remoteModelLoader;
        _expiryChecker = expiryChecker;
    }

    public async Task<VerificationResult> Verify(Transaction transaction, GranularCertificate? certificate, ClaimedEvent payload)
    {
        if (certificate is null)
            return new VerificationResult.Invalid("Certificate does not exist");

        if (certificate.IsCertificateWithdrawn)
            return new VerificationResult.Invalid("Certificate is withdrawn");

        if (_expiryChecker.IsExpired(certificate))
            return new VerificationResult.Invalid("Certificate has expired");

        var slice = certificate.GetAllocation(payload.AllocationId);
        if (slice is null)
            return new VerificationResult.Invalid("Allocation does not exist");

        if (!transaction.IsSignatureValid(slice.Owner))
            return new VerificationResult.Invalid($"Invalid signature for slice");

        if (certificate.Type == V1.GranularCertificateType.Production)
        {
            // Verify that the consumption certificate exists and that the allocation is valid
            var otherCertificate = await _remoteModelLoader.GetModel<GranularCertificate>(slice.ConsumptionCertificateId);
            if (otherCertificate is null)
                return new VerificationResult.Invalid("ConsumptionCertificate does not exist");

            if (otherCertificate.IsCertificateWithdrawn)
                return new VerificationResult.Invalid("ConsumptionCertificate is withdrawn");

            if (otherCertificate.Type != V1.GranularCertificateType.Consumption)
                return new VerificationResult.Invalid("ConsumptionCertificate is not a consumption certificate");

            if (!otherCertificate.HasAllocation(payload.AllocationId))
                return new VerificationResult.Invalid("Consumption not allocated");
        }
        else if (certificate.Type == V1.GranularCertificateType.Consumption)
        {
            // Verify that the production certificate exists and that the allocation has been claimed
            var otherCertificate = await _remoteModelLoader.GetModel<GranularCertificate>(slice.ProductionCertificateId);
            if (otherCertificate is null)
                return new VerificationResult.Invalid("ProductionCertificate does not exist");

            if (otherCertificate.IsCertificateWithdrawn)
                return new VerificationResult.Invalid("ProductionCertificate is withdrawn");

            if (otherCertificate.Type != V1.GranularCertificateType.Production)
                return new VerificationResult.Invalid("ProductionCertificate is not a production certificate");

            if (!otherCertificate.HasClaim(payload.AllocationId))
                return new VerificationResult.Invalid("Production not claimed");
        }
        else
            throw new NotSupportedException($"Certificate type ”{certificate.Type}” is not supported");

        return new VerificationResult.Valid();
    }
}

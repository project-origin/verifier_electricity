using ProjectOrigin.Electricity.Extensions;
using ProjectOrigin.Registry.V1;
using System.Threading.Tasks;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.Electricity.Interfaces;

namespace ProjectOrigin.Electricity.Verifiers;

public class TransferredEventVerifier : IEventVerifier<V1.TransferredEvent>
{
    private readonly IExpiryChecker _expiryChecker;

    public TransferredEventVerifier(IExpiryChecker expiryChecker)
    {
        _expiryChecker = expiryChecker;
    }
    
    public Task<VerificationResult> Verify(Transaction transaction, GranularCertificate? certificate, V1.TransferredEvent payload)
    {
        if (certificate is null)
            return new VerificationResult.Invalid("Certificate does not exist");

        if (certificate.IsCertificateWithdrawn)
            return new VerificationResult.Invalid("Certificate is withdrawn");

        if (_expiryChecker.IsExpired(certificate))
            return new VerificationResult.Invalid("Certificate has expired");

        var certificateSlice = certificate.GetCertificateSlice(payload.SourceSliceHash);
        if (certificateSlice is null)
            return new VerificationResult.Invalid("Slice not found");

        if (!transaction.IsSignatureValid(certificateSlice.Owner))
            return new VerificationResult.Invalid($"Invalid signature for slice");

        if (!payload.NewOwner.TryToModel(out _))
            return new VerificationResult.Invalid("Invalid NewOwner key, not a valid publicKey");

        return new VerificationResult.Valid();
    }
}

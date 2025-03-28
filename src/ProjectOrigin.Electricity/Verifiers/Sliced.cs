using ProjectOrigin.Electricity.Extensions;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Registry.V1;
using System.Threading.Tasks;
using System.Linq;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.Electricity.Interfaces;

namespace ProjectOrigin.Electricity.Verifiers;

public class SlicedEventVerifier : IEventVerifier<V1.SlicedEvent>
{
    private readonly IExpiryChecker _expiryChecker;

    public SlicedEventVerifier(IExpiryChecker expiryChecker)
    {
        _expiryChecker = expiryChecker;
    }

    public Task<VerificationResult> Verify(Transaction transaction, GranularCertificate? certificate, V1.SlicedEvent payload)
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

        foreach (var slice in payload.NewSlices)
        {
            if (!slice.NewOwner.TryToModel(out _))
                return new VerificationResult.Invalid("Invalid NewOwner key, not a valid publicKey");

            if (!slice.Quantity.VerifyCommitment(payload.CertificateId.StreamId.Value))
                return new VerificationResult.Invalid("Invalid range proof for Quantity commitment");
        }

        if (!Commitment.VerifyEqualityProof(
                payload.SumProof.ToByteArray(),
                certificateSlice.Commitment.ToModel(),
                payload.NewSlices.Select(slice => slice.Quantity.ToModel()).Aggregate((left, right) => left + right),
                payload.CertificateId.StreamId.Value))
            return new VerificationResult.Invalid($"Invalid sum proof");

        return new VerificationResult.Valid();
    }
}

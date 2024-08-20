using ProjectOrigin.Electricity.Extensions;
using ProjectOrigin.Registry.V1;
using System.Threading.Tasks;
using ProjectOrigin.Electricity.Server.Models;
using ProjectOrigin.Electricity.Server.Interfaces;
using System;
using System.Linq;

namespace ProjectOrigin.Electricity.Server.Verifiers;

public class IssuedEventVerifier : IEventVerifier<V1.IssuedEvent>
{
    private readonly IGridAreaIssuerService _gridAreaIssuerService;

    public IssuedEventVerifier(IGridAreaIssuerService gridAreaIssuerService)
    {
        _gridAreaIssuerService = gridAreaIssuerService;
    }

    public Task<VerificationResult> Verify(Transaction transaction, GranularCertificate? certificate, V1.IssuedEvent payload)
    {
        if (certificate is not null)
            return new VerificationResult.Invalid($"Certificate with id ”{payload.CertificateId.StreamId}” already exists");

        if (!payload.QuantityCommitment.VerifyCommitment(payload.CertificateId.StreamId.Value))
            return new VerificationResult.Invalid("Invalid range proof for Quantity commitment");

        if (payload.Type == V1.GranularCertificateType.Invalid)
            return new VerificationResult.Invalid("Invalid certificate type");

        if (!payload.OwnerPublicKey.TryToModel(out _))
            return new VerificationResult.Invalid("Invalid owner key, not a valid publicKey");

        if (payload.Period.GetTimeSpan() > TimeSpan.FromHours(1))
            return new VerificationResult.Invalid("Invalid period, maximum period is 1 hour");

        if (payload.Period.GetTimeSpan() < TimeSpan.FromMinutes(1))
            return new VerificationResult.Invalid("Invalid period, minimum period is 1 minute");

        var areaPublicKeys = _gridAreaIssuerService.GetAreaPublicKey(payload.GridArea);
        if (!areaPublicKeys.Any())
            return new VerificationResult.Invalid($"No issuer found for GridArea ”{payload.GridArea}”");

        if (!areaPublicKeys.Any(transaction.IsSignatureValid))
            return new VerificationResult.Invalid($"Invalid issuer signature for GridArea ”{payload.GridArea}”");

        return new VerificationResult.Valid();
    }
}

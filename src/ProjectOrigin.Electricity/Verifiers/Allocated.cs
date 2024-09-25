using ProjectOrigin.Electricity.Extensions;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Registry.V1;
using System.Threading.Tasks;
using ProjectOrigin.Electricity.Models;
using System;
using ProjectOrigin.Electricity.Interfaces;
using Microsoft.Extensions.Options;
using ProjectOrigin.Electricity.Options;
using System.Linq;
using Google.Protobuf;

namespace ProjectOrigin.Electricity.Verifiers;

public class AllocatedEventVerifier : IEventVerifier<V1.AllocatedEvent>
{
    private readonly IRemoteModelLoader _remoteModelLoader;
    private readonly IOptions<NetworkOptions> _options;

    public AllocatedEventVerifier(IRemoteModelLoader remoteModelLoader, IOptions<NetworkOptions> options)
    {
        _remoteModelLoader = remoteModelLoader;
        _options = options;
    }

    public async Task<VerificationResult> Verify(Transaction transaction, GranularCertificate? certificate, V1.AllocatedEvent payload)
    {
        if (certificate is null)
            return new VerificationResult.Invalid("Certificate does not exist");

        if (certificate.IsCertificateWithdrawn)
            return new VerificationResult.Invalid("Certificate is withdrawn");

        switch (certificate.Type)
        {
            case V1.GranularCertificateType.Production:
                return await VerifyProduction(transaction, certificate, payload);
            case V1.GranularCertificateType.Consumption:
                return await VerifyConsumption(transaction, certificate, payload);
            default:
                throw new NotSupportedException($"Certificate type ”{certificate.Type}” is not supported");
        }
    }

    private async Task<VerificationResult> VerifyProduction(Transaction transaction, GranularCertificate certificate, V1.AllocatedEvent payload)
    {
        var validSliceResult = HasValidSlice(transaction, certificate, payload.ProductionSourceSliceHash, payload.ChroniclerSignature, out var slice);
        if (validSliceResult is not VerificationResult.Valid)
            return validSliceResult;

        var otherCertificate = await _remoteModelLoader.GetModel<GranularCertificate>(payload.ConsumptionCertificateId);
        if (otherCertificate is null)
            return new VerificationResult.Invalid("ConsumptionCertificate does not exist");

        if (otherCertificate.Type != V1.GranularCertificateType.Consumption)
            return new VerificationResult.Invalid("ConsumptionCertificate is not a consumption certificate");

        if (!otherCertificate.Period.IsEnclosingOrEnclosed(certificate.Period))
            return new VerificationResult.Invalid("Periods are not overlapping");

        var otherSlice = otherCertificate.GetCertificateSlice(payload.ConsumptionSourceSliceHash);
        if (otherSlice is null)
            return new VerificationResult.Invalid("Consumption slice does not exist");

        if (!Commitment.VerifyEqualityProof(
            payload.EqualityProof.ToByteArray(),
            slice.Commitment.ToModel(),
            otherSlice.Commitment.ToModel(),
            payload.AllocationId.Value))
            return new VerificationResult.Invalid("Invalid Equality proof");

        return new VerificationResult.Valid();
    }

    private async Task<VerificationResult> VerifyConsumption(Transaction transaction, GranularCertificate certificate, V1.AllocatedEvent payload)
    {
        var validSliceResult = HasValidSlice(transaction, certificate, payload.ConsumptionSourceSliceHash, payload.ChroniclerSignature, out var _);
        if (validSliceResult is not VerificationResult.Valid)
            return validSliceResult;

        var otherCertificate = await _remoteModelLoader.GetModel<GranularCertificate>(payload.ProductionCertificateId);
        if (otherCertificate is null)
            return new VerificationResult.Invalid("ProductionCertificate does not exist");

        if (!otherCertificate.HasAllocation(payload.AllocationId))
            return new VerificationResult.Invalid("Production not allocated");

        return new VerificationResult.Valid();
    }

    private VerificationResult HasValidSlice(Transaction transaction, GranularCertificate certificate, ByteString sourceSliceHash, ByteString chroniclerSignature, out CertificateSlice slice)
    {
        slice = certificate.GetCertificateSlice(sourceSliceHash)!;
        if (slice is null)
            return new VerificationResult.Invalid("Certificate slice does not exist");

        if (!transaction.IsSignatureValid(slice.Owner))
            return new VerificationResult.Invalid($"Invalid signature for slice");

        AreaInfo areaInfo;
        if (!_options.Value.Areas.TryGetValue(certificate.GridArea, out areaInfo!)
            || areaInfo is null)
            return new VerificationResult.Invalid("Area does not exist");

        if (areaInfo.Chronicler is not null)
        {
            var claimIntent = new Chronicler.V1.ClaimIntent
            {
                CertificateId = certificate.Id,
                Commitment = slice.Commitment.Content,
            };

            if (!areaInfo.Chronicler.SignerKeys.Any(x => x.PublicKey.Verify(claimIntent.ToByteArray(), chroniclerSignature.Span)))
                return new VerificationResult.Invalid("Not signed by Chronicler");
        }

        return new VerificationResult.Valid();
    }
}

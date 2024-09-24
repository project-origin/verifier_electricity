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

        GranularCertificate? otherCertificate;

        if (certificate.Type == V1.GranularCertificateType.Production)
        {
            var productionSlice = certificate.GetCertificateSlice(payload.ProductionSourceSliceHash);
            if (productionSlice is null)
                return new VerificationResult.Invalid("Production slice does not exist");

            if (!transaction.IsSignatureValid(productionSlice.Owner))
                return new VerificationResult.Invalid($"Invalid signature for slice");

            otherCertificate = await _remoteModelLoader.GetModel<GranularCertificate>(payload.ConsumptionCertificateId);
            if (otherCertificate is null)
                return new VerificationResult.Invalid("ConsumptionCertificate does not exist");

            if (otherCertificate.Type != V1.GranularCertificateType.Consumption)
                return new VerificationResult.Invalid("ConsumptionCertificate is not a consumption certificate");

            if (!otherCertificate.Period.IsEnclosingOrEnclosed(certificate.Period))
                return new VerificationResult.Invalid("Periods are not overlapping");

            var consumptionSlice = otherCertificate.GetCertificateSlice(payload.ConsumptionSourceSliceHash);
            if (consumptionSlice is null)
                return new VerificationResult.Invalid("Consumption slice does not exist");

            if (!Commitment.VerifyEqualityProof(
                payload.EqualityProof.ToByteArray(),
                productionSlice.Commitment.ToModel(),
                consumptionSlice.Commitment.ToModel(),
                payload.AllocationId.Value))
                return new VerificationResult.Invalid("Invalid Equality proof");
        }
        else if (certificate.Type == V1.GranularCertificateType.Consumption)
        {
            var consumptionSlice = certificate.GetCertificateSlice(payload.ConsumptionSourceSliceHash);
            if (consumptionSlice is null)
                return new VerificationResult.Invalid("Consumption slice does not exist");

            AreaInfo areaInfo;
            if (!_options.Value.Areas.TryGetValue(certificate.GridArea, out areaInfo!)
                || areaInfo is null)
                return new VerificationResult.Invalid("Area does not exist");

            if (areaInfo.Chronicler is not null)
            {
                var claimIntent = new Chronicler.V1.ClaimIntent
                {
                    CertificateId = certificate.Id,
                    Commitment = consumptionSlice.Commitment.ToByteString(),
                };

                if (!areaInfo.Chronicler.SignerKeys.Any(x => x.PublicKey.Verify(claimIntent.ToByteArray(), payload.ConsumptionSourceSliceHash.Span)))
                    return new VerificationResult.Invalid("Not signed by Chronicler");
            }

            if (!transaction.IsSignatureValid(consumptionSlice.Owner))
                return new VerificationResult.Invalid($"Invalid signature for slice");

            var productionCertificate = await _remoteModelLoader.GetModel<GranularCertificate>(payload.ProductionCertificateId);
            if (productionCertificate is null)
                return new VerificationResult.Invalid("ProductionCertificate does not exist");

            if (!productionCertificate.HasAllocation(payload.AllocationId))
                return new VerificationResult.Invalid("Production not allocated");
        }
        else
            throw new NotSupportedException($"Certificate type ”{certificate.Type.ToString()}” is not supported");

        return new VerificationResult.Valid();
    }
}

using Google.Protobuf;
using NSec.Cryptography;
using ProjectOrigin.Electricity.Consumption;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.Register.StepProcessor.Models;

namespace ProjectOrigin.Electricity.Production.Requests;

internal class ProductionClaimedEventVerifier : IEventVerifier<ProductionCertificate, V1.ClaimedEvent>
{
    public Task<VerificationResult> Verify(VerificationRequest<ProductionCertificate, ClaimedEvent> request)
    {
        var hydrator = new ModelHydrater();

        if (request.Model is null)
            return new VerificationResult.Invalid("Certificate does not exist");

        var allocationId = request.Event.AllocationId.ToModel();

        var slice = request.Model.GetAllocation(allocationId);
        if (slice is null)
            return new VerificationResult.Invalid("Allocation does not exist");

        if (!Ed25519.Ed25519.Verify(slice.Owner, request.Event.ToByteArray(), request.Signature))
            return new VerificationResult.Invalid($"Invalid signature for slice");

        if (request.AdditionalStreams.TryGetValue(slice.ConsumptionCertificateId, out var events)
            && !hydrator.HydrateModel<ConsumptionCertificate>(events).HasAllocation(allocationId))
            return new VerificationResult.Invalid("Consumption not allocated");

        return new VerificationResult.Valid();
    }
}

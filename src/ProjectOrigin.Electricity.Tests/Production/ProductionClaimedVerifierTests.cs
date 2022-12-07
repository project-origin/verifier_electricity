// using NSec.Cryptography;
// using ProjectOrigin.Electricity.Consumption;
// using ProjectOrigin.Electricity.Production.Requests;
// using ProjectOrigin.Register.StepProcessor.Interfaces;
// using ProjectOrigin.Register.StepProcessor.Models;

// namespace ProjectOrigin.Electricity.Tests;

// public class ProductionClaimedVerifierTests
// {
//     private ProductionClaimedEventVerifier Verifier(ConsumptionCertificate? pc)
//     {
//         return new ProductionClaimedEventVerifier();
//     }

//     [Fact]
//     public async Task Verifier_AllocateCertificate_Valid()
//     {
//         var ownerKey = Key.Create(SignatureAlgorithm.Ed25519);
//         var quantity = FakeRegister.Group.Commit(150);
//         var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
//         var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 300);
//         var (allocationId, _) = prodCert.Allocated(prodParams, consCert.Id, quantity);
//         consCert.Allocated(allocationId, consParams, prodCert.Id, quantity);

//         var verifier = Verifier(consCert);
//         var request = FakeRegister.CreateProductionClaim(prodCert.Id, allocationId, ownerKey);
//         var result = await verifier.Verify(request, prodCert);

//         Assert.IsType<VerificationResult.Valid>(result);
//     }

//     [Fact]
//     public async Task Verifier_AllocateCertificate_CertNotFound()
//     {
//         var ownerKey = Key.Create(SignatureAlgorithm.Ed25519);
//         var quantity = FakeRegister.Group.Commit(150);
//         var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
//         var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 300);
//         var (allocationId, _) = prodCert.Allocated(prodParams, consCert.Id, quantity);
//         consCert.Allocated(allocationId, consParams, prodCert.Id, quantity);

//         var verifier = Verifier(consCert);
//         var request = FakeRegister.CreateProductionClaim(consCert.Id, allocationId, ownerKey);
//         var result = await verifier.Verify(request, null);

//         var invalid = result as VerificationResult.Invalid;
//         Assert.NotNull(invalid);
//         Assert.Equal("Certificate does not exist", invalid!.ErrorMessage);
//     }

//     [Fact]
//     public async Task Verifier_AllocateCertificate_ConsumptionNotAllocated()
//     {
//         var ownerKey = Key.Create(SignatureAlgorithm.Ed25519);
//         var quantity = FakeRegister.Group.Commit(150);
//         var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
//         var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 300);
//         var (allocationId, _) = prodCert.Allocated(prodParams, consCert.Id, quantity);

//         var verifier = Verifier(consCert);
//         var request = FakeRegister.CreateProductionClaim(prodCert.Id, allocationId, ownerKey);
//         var result = await verifier.Verify(request, prodCert);

//         var invalid = result as VerificationResult.Invalid;
//         Assert.NotNull(invalid);
//         Assert.Equal("Consumption not allocated", invalid!.ErrorMessage);
//     }

//     [Fact]
//     public async Task Verifier_AllocateCertificate_ProdNotAllocated()
//     {
//         var ownerKey = Key.Create(SignatureAlgorithm.Ed25519);
//         var quantity = FakeRegister.Group.Commit(150);
//         var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
//         var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 300);
//         var allocationId = Guid.NewGuid();
//         consCert.Allocated(allocationId, consParams, prodCert.Id, quantity);

//         var verifier = Verifier(consCert);
//         var request = FakeRegister.CreateProductionClaim(prodCert.Id, allocationId, ownerKey);
//         var result = await verifier.Verify(request, prodCert);

//         var invalid = result as VerificationResult.Invalid;
//         Assert.NotNull(invalid);
//         Assert.Equal("Allocation does not exist", invalid!.ErrorMessage);
//     }

//     [Fact]
//     public async Task Verifier_AllocateCertificate_InvalidSignature()
//     {
//         var ownerKey = Key.Create(SignatureAlgorithm.Ed25519);
//         var otherKey = Key.Create(SignatureAlgorithm.Ed25519);
//         var quantity = FakeRegister.Group.Commit(150);
//         var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
//         var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 300);
//         var (allocationId, _) = prodCert.Allocated(prodParams, consCert.Id, quantity);
//         consCert.Allocated(allocationId, consParams, prodCert.Id, quantity);

//         var verifier = Verifier(consCert);
//         var request = FakeRegister.CreateProductionClaim(prodCert.Id, allocationId, otherKey);
//         var result = await verifier.Verify(request, prodCert);

//         var invalid = result as VerificationResult.Invalid;
//         Assert.NotNull(invalid);
//         Assert.Equal("Invalid signature", invalid!.ErrorMessage);
//     }
// }

using System;
using System.Threading.Tasks;
using Moq;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Verifiers;
using ProjectOrigin.HierarchicalDeterministicKeys;
using MsOptions = Microsoft.Extensions.Options;
using Xunit;
using ProjectOrigin.Electricity.Options;
using System.Collections.Generic;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Electricity.Tests;

public class ConsumptionAllocatedVerifierTests
{
    private readonly IPrivateKey _issuerKey;
    private readonly MsOptions.IOptions<NetworkOptions> _defaultOptions;
    private GranularCertificate? _otherCertificate;
    private Mock<IRemoteModelLoader> _modelLoaderMock;

    public ConsumptionAllocatedVerifierTests()
    {
        _issuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();
        _modelLoaderMock = new Mock<IRemoteModelLoader>();
        _modelLoaderMock.Setup(obj => obj.GetModel<GranularCertificate>(It.IsAny<Common.V1.FederatedStreamId>()))
            .Returns(() => Task.FromResult(_otherCertificate));

        _defaultOptions = MsOptions.Options.Create(new NetworkOptions
        {
            Areas = new Dictionary<string, AreaInfo>
            {
                {
                    "DK1", new AreaInfo
                    {
                        IssuerKeys = new List<KeyInfo>
                        {
                            new KeyInfo
                            {
                                PublicKey = _issuerKey.PublicKey
                            }
                        }
                    }
                }
            }
        });
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_Valid()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);
        _otherCertificate = prodCert;

        var @event = FakeRegister.CreateAllocationEvent(allocationId, prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ConsumptionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, consCert, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task Verifier_CertNotFound_Invalid()
    {
        var _verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);
        _otherCertificate = prodCert;

        var @event = FakeRegister.CreateAllocationEvent(allocationId, prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ConsumptionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("Certificate does not exist");
    }

    [Fact]
    public async Task Verifier_SliceNotFound_Invalid()
    {
        var _verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);
        _otherCertificate = prodCert;

        var @event = FakeRegister.CreateAllocationEvent(allocationId, prodCert.Id, consCert.Id, prodParams, prodParams);
        var transaction = FakeRegister.SignTransaction(@event.ConsumptionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, consCert, @event);

        result.AssertInvalid("Consumption slice does not exist");
    }

    [Fact]
    public async Task Verifier_InvalidSignatureForSlice_Invalid()
    {
        var _verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var otherKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);
        _otherCertificate = prodCert;

        var @event = FakeRegister.CreateAllocationEvent(allocationId, prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ConsumptionCertificateId, @event, otherKey);

        var result = await _verifier.Verify(transaction, consCert, @event);

        result.AssertInvalid("Invalid signature for slice");
    }

    [Fact]
    public async Task Verifier_ProductionNotFound_Invalid()
    {
        var _verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);
        _otherCertificate = null;

        var @event = FakeRegister.CreateAllocationEvent(allocationId, prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ConsumptionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, consCert, @event);

        result.AssertInvalid("ProductionCertificate does not exist");
    }

    [Fact]
    public async Task Verifier_ProdNotAllocated_Invalid()
    {
        var _verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = prodCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ConsumptionCertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, consCert, @event);

        result.AssertInvalid("Production not allocated");
    }

    [Fact]
    public async Task Verifier_Chronicler_Enabled()
    {
        // Arrange
        var chroniclerKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        var _verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, MsOptions.Options.Create(new NetworkOptions
        {
            Areas = new Dictionary<string, AreaInfo>
            {
                {
                    "DK1", new AreaInfo
                    {
                        Chronicler = new ChroniclerInfo
                        {
                            Url = "http://chronicler",
                            SignerKeys = new List<KeyInfo>
                            {
                                new KeyInfo
                                {
                                    PublicKey = chroniclerKey.PublicKey
                                }
                            }
                        },
                        IssuerKeys = new List<KeyInfo>
                        {
                            new KeyInfo
                            {
                                PublicKey = _issuerKey.PublicKey
                            }
                        }
                    }
                }
            }
        }));

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);
        _otherCertificate = prodCert;

        var @event = FakeRegister.CreateAllocationEvent(allocationId, prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ConsumptionCertificateId, @event, ownerKey);

        // Act
        var result = await _verifier.Verify(transaction, consCert, @event);

        // Assert
        result.AssertInvalid("Not signed by Chronicler");
    }
}

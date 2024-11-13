using System;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using Moq;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Verifiers;
using ProjectOrigin.HierarchicalDeterministicKeys;
using MsOptions = Microsoft.Extensions.Options;
using Xunit;
using Google.Protobuf.WellKnownTypes;
using ProjectOrigin.Electricity.Options;
using Google.Protobuf;
using System.Collections.Generic;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Electricity.Tests;

public class ProductionAllocatedVerifierTests
{
    private readonly IPrivateKey _issuerKey;
    private readonly MsOptions.IOptions<NetworkOptions> _defaultOptions;
    private readonly IExpiryChecker _expiryChecker;
    private readonly Mock<IRemoteModelLoader> _modelLoaderMock;
    private GranularCertificate? _otherCertificate;

    public ProductionAllocatedVerifierTests()
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
        _expiryChecker = new ExpiryCheckerFake();
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_Valid()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: consCert.Period.Clone());
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertValid();
    }


    [Fact]
    public async Task Verifier_InvalidProductionSlice_SliceNotFound()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, consParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Certificate slice does not exist");
    }

    [Fact]
    public async Task Verifier_WrongKey_InvalidSignature()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var otherKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, otherKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Invalid signature for slice");
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_ConsCertNotFould()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = null;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("ConsumptionCertificate does not exist");
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_ValidPeriod_EnclosingStart()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 15, 0, TimeSpan.Zero))
        });
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_ValidPeriod_EnclosingEnd()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 45, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_ValidPeriod_EnclosingWithin()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 30, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 35, 0, TimeSpan.Zero))
        });
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_InvalidPeriod_DifferentPeriods()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 14, 0, 0, TimeSpan.Zero))
        });

        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Periods are not overlapping");
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_InvalidPeriod_Before()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 30, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 30, 0, TimeSpan.Zero))
        });

        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Periods are not overlapping");
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_InvalidPeriod_After()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 0, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 13, 0, 0, TimeSpan.Zero))
        });
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, periodOverride: new V1.DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 11, 30, 0, TimeSpan.Zero)),
            End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2022, 09, 25, 12, 30, 0, TimeSpan.Zero))
        });

        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Periods are not overlapping");
    }

    [Fact]
    public async Task Verifier_AllocateCertificate_AllowCrossArea()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250, area: "DK2");
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250, area: "DK1");
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task Verifier_WrongConsumptionSlice_SliceNotFound()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, prodParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Consumption slice does not exist");
    }

    [Fact]
    public async Task Verifier_RandomProofData_InvalidEqualityProof()
    {
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams, overwrideEqualityProof: new Fixture().CreateMany<byte>(64).ToArray());
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        var result = await verifier.Verify(transaction, prodCert, @event);

        result.AssertInvalid("Invalid Equality proof");
    }

    [Fact]
    public async Task Verifier_AllocationCerticate_InvalidCertificateIsWithdrawnInvalidEqualityProof()
    {

        // Arrange
        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, _defaultOptions, _expiryChecker);
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams, overwrideEqualityProof: new Fixture().CreateMany<byte>(64).ToArray());
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        // Act
        prodCert.Withdrawn();
        var result = await verifier.Verify(transaction, prodCert, @event);

        // Assert
        result.AssertInvalid("Certificate is withdrawn");
    }


    [Fact]
    public async Task Verifier_Chronicler_Enabled_InvalidSignature()
    {
        // Arrange
        var chroniclerKey = Algorithms.Ed25519.GenerateNewPrivateKey();
        var networkOptions = MsOptions.Options.Create(new NetworkOptions
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
        });

        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, networkOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = consCert;

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        // Act
        var result = await verifier.Verify(transaction, consCert, @event);

        // Assert
        result.AssertInvalid("Not signed by Chronicler");
    }

    [Fact]
    public async Task Verifier_Chronicler_Enable_ValidSignature()
    {
        // Arrange
        var chroniclerKey = Algorithms.Ed25519.GenerateNewPrivateKey();
        var networkOptions = MsOptions.Options.Create(new NetworkOptions
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
        });

        var verifier = new AllocatedEventVerifier(_modelLoaderMock.Object, networkOptions, _expiryChecker);

        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        _otherCertificate = consCert;

        var claimIntent = new Chronicler.V1.ClaimIntent
        {
            CertificateId = prodCert.Id,
            Commitment = ByteString.CopyFrom(prodParams.Commitment.C),
        };
        var signature = chroniclerKey.Sign(claimIntent.ToByteArray()).ToArray();

        var @event = FakeRegister.CreateAllocationEvent(Guid.NewGuid(), prodCert.Id, consCert.Id, prodParams, consParams, chroniclerSignature: signature);
        var transaction = FakeRegister.SignTransaction(@event.ProductionCertificateId, @event, ownerKey);

        // Act
        var result = await verifier.Verify(transaction, prodCert, @event);

        // Assert
        result.AssertValid();
    }
}

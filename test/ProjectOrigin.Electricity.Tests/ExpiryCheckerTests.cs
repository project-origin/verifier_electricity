using System;
using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.Electricity.Services;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys;
using Xunit;

namespace ProjectOrigin.Electricity.Tests;
public class ExpiryCheckerTests()
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Should_Expire_When_CertificateIsVeryOld()
    {
        // Arrange
        var threeYearsAgo = _now.AddYears(-3);
        var certificate = CreateCertificate(threeYearsAgo);
        int daysToExpiry = 100;
        var expiryChecker = CreateExpiryChecker(daysToExpiry);

        // Act
        var isExpired = expiryChecker.IsExpired(certificate);

        // Assert
        isExpired.Should().BeTrue();
    }

    [Fact]
    public void Should_NotExpire_When_CertificateIsFromYesterday()
    {
        // Arrange
        var yesterday = _now.AddDays(-1);
        var certificate = CreateCertificate(yesterday);
        int daysToExpiry = 100;
        var expiryChecker = CreateExpiryChecker(daysToExpiry);


        // Act
        var isExpired = expiryChecker.IsExpired(certificate);

        // Assert
        isExpired.Should().BeFalse();
    }

    [Fact]
    public void Should_NotExpire_When_CertificateIsVeryNew()
    {
        // Arrange
        var certificate = CreateCertificate(_now);
        int? daysToExpiry = 100;
        var expiryChecker = CreateExpiryChecker(daysToExpiry);

        // Act
        var isExpired = expiryChecker.IsExpired(certificate);

        // Assert
        isExpired.Should().BeFalse();
    }

    [Fact]
    public void Should_NotExpire_When_DaysToExpiryIsNotConfiguredInNetworkOptions()
    {
        // Arrange
        int? daysToExpiry = null;
        var expiryChecker = CreateExpiryChecker(daysToExpiry);
        var certificate = CreateCertificate(_now.AddYears(-100));

        // Act
        var isExpired = expiryChecker.IsExpired(certificate);

        // Assert
        isExpired.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(23, false)]
    [InlineData(23.5, false)]
    [InlineData(24.5, true)]
    [InlineData(25, true)]
    public void Should_WorkForCornerCases_WhenExpiresAfterOneDay(double cerficateAgeInHours, bool expectedIsExpired)
    {
        // Arrange
        var daysToExpiry = 1;
        var expiryChecker = CreateExpiryChecker(daysToExpiry);
        var certificate = CreateCertificate(_now.AddHours(-cerficateAgeInHours));

        // Act
        var isExpired = expiryChecker.IsExpired(certificate);

        // Assert
        expectedIsExpired.Should().Be(isExpired);
    }

    private static ExpiryChecker CreateExpiryChecker(int? daysToExpiry)
    {
        var networkOptions = new NetworkOptionsFake("Narnia", Algorithms.Ed25519.GenerateNewPrivateKey(), daysToExpiry);
        return new ExpiryChecker(networkOptions);
    }

    private static GranularCertificate CreateCertificate(DateTimeOffset periodEnd)
    {
        var dateInterval = new DateInterval()
        {
            Start = Timestamp.FromDateTimeOffset(periodEnd.AddHours(-1)),
            End = Timestamp.FromDateTimeOffset(periodEnd),
        };
        var (certificate, _) = FakeRegister.ProductionIssued(Algorithms.Ed25519.GenerateNewPrivateKey().PublicKey, 250, "Narnia", dateInterval);
        return certificate;

    }
}

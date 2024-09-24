using System;
using System.Text;
using System.Text.Json;
using ProjectOrigin.Electricity.Converters;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using Xunit;
using FluentAssertions;

namespace ProjectOrigin.Electricity.Tests.Converters;

public class JsonPublicKeyConverterTests
{
    private readonly JsonSerializerOptions _options;

    public JsonPublicKeyConverterTests()
    {
        _options = new JsonSerializerOptions
        {
            Converters = { new JsonPublicKeyConverter() }
        };
    }

    [Fact]
    public void Read_ValidPublicKey_ReturnsPublicKey()
    {
        // Arrange
        var publicKey = Algorithms.Ed25519.GenerateNewPrivateKey().PublicKey;
        var publicKeyText = publicKey.ExportPkixText();
        var bytes = Encoding.UTF8.GetBytes(publicKeyText);
        var base64 = Convert.ToBase64String(bytes);
        var json = $"\"{base64}\"";

        // Act
        var result = JsonSerializer.Deserialize<IPublicKey>(json, _options);

        // Assert
        result.Should().NotBeNull();
        result!.ExportPkixText().Should().Be(publicKeyText);
    }

    [Fact]
    public void Read_InvalidPublicKey_ThrowsInvalidConfigurationException()
    {
        // Arrange
        var json = "\"InvalidBase64\"";

        // Act
        Action act = () => JsonSerializer.Deserialize<IPublicKey>(json, _options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Write_ValidPublicKey_WritesBase64String()
    {
        // Arrange
        var publicKey = Algorithms.Ed25519.GenerateNewPrivateKey().PublicKey;
        var publicKeyText = publicKey.ExportPkixText();
        var bytes = Encoding.UTF8.GetBytes(publicKeyText);
        var base64 = Convert.ToBase64String(bytes);

        // Act
        var json = JsonSerializer.Serialize(publicKey, _options);

        // Assert
        json.Should().Be($"\"{base64}\"");
    }
}

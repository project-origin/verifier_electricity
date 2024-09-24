using System;
using Xunit;
using ProjectOrigin.Electricity.Converters;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Text;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using AutoFixture;
using YamlDotNet.Core;

namespace ProjectOrigin.Electricity.Tests.Converters;

public class YamlPublicKeyConverterTests
{
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    public YamlPublicKeyConverterTests()
    {
        var yamlPublicKeyConverter = new YamlPublicKeyConverter();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(yamlPublicKeyConverter)
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(yamlPublicKeyConverter)
            .Build();
    }

    [Fact]
    public void WriteYaml_ShouldSerializePublicKey()
    {
        // Arrange
        var publicKey = Algorithms.Ed25519.GenerateNewPrivateKey().PublicKey;
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(publicKey.ExportPkixText()));

        // Act
        var yaml = _serializer.Serialize(publicKey);

        // Assert
        Assert.Contains(expectedBase64, yaml);
    }

    [Fact]
    public void ReadYaml_ShouldDeserializePublicKey()
    {
        // Arrange
        var publicKey = Algorithms.Ed25519.GenerateNewPrivateKey().PublicKey;
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(publicKey.ExportPkixText()));

        // Act
        var result = _deserializer.Deserialize<IPublicKey>(base64);

        // Assert
        Assert.Equal(publicKey.ExportPkixText(), result.ExportPkixText());
    }

    [Fact]
    public void ReadYaml_ShouldRaiseException()
    {
        // Arrange
        var fixture = new Fixture();
        var base64 = Convert.ToBase64String(fixture.Create<byte[]>());

        // Assert
        Assert.Throws<YamlException>(() => { _deserializer.Deserialize<IPublicKey>(base64); });
    }
}

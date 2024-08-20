using System.Collections.Generic;
using ProjectOrigin.Electricity.Server;
using Xunit;
using AutoFixture;
using Microsoft.Extensions.Options;
using System;
using System.Text;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.TestCommon.Fixtures;
using ProjectOrigin.TestCommon;

namespace ProjectOrigin.Electricity.IntegrationTests;

public class MisconfigurationTest
{
    private const string Area = "TestArea";

    private readonly Fixture _fixture;
    private readonly TestServerFixture<Startup> _serviceFixture;

    public MisconfigurationTest()
    {
        _fixture = new Fixture();
        _serviceFixture = new TestServerFixture<Startup>();
    }

    [Fact]
    public void OptionsValidationException_IfInvalidKeyFormat()
    {
        ConfigureNetwork($"""
        registries:
        areas:
          {Area}:
            issuerKeys:
              - publicKey: {Convert.ToBase64String(_fixture.Create<byte[]>())}
        """);

        var ex = Assert.Throws<OptionsValidationException>(() => { var channel = _serviceFixture.Channel; });
        Assert.Equal($"A issuer key ”{Area}” is a invalid format.", ex.Message);
    }

    [Fact]
    public void OptionsValidationException_InvalidSecp256k1_NotSupported_AsIssuer()
    {
        var issuerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();

        ConfigureNetwork($"""
        registries:
        areas:
          {Area}:
            issuerKeys:
              - publicKey: {Convert.ToBase64String(Encoding.UTF8.GetBytes(issuerKey.PublicKey.ExportPkixText()))}
        """);

        var ex = Assert.Throws<OptionsValidationException>(() => { var channel = _serviceFixture.Channel; });
        Assert.Equal($"A issuer key ”{Area}” is a invalid format.", ex.Message);
    }

    [Fact]
    public void OptionsValidationException_IfNoAreaIssuerDefined()
    {
        ConfigureNetwork($"""
        registries:
        areas:
        """);

        var ex = Assert.Throws<OptionsValidationException>(() => { var channel = _serviceFixture.Channel; });
        Assert.Equal("No Issuer areas configured.", ex.Message);
    }

    private void ConfigureNetwork(string yamlConfig)
    {
        var configFile = TempFile.WriteAllText(yamlConfig, ".yaml");

        _serviceFixture.ConfigureHostConfiguration(new Dictionary<string, string?>()
        {
         {"network:ConfigurationUri", "file://" + configFile},
        });
    }

    [Fact]
    public void OptionsValidationException_InvalidUrl()
    {
        var issuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        ConfigureNetwork($"""
        registries:
          MyRegistry:
            url: This is not a url
        areas:
          {Area}:
            issuerKeys:
              - publicKey: {Convert.ToBase64String(Encoding.UTF8.GetBytes(issuerKey.PublicKey.ExportPkixText()))}
        """);

        var ex = Assert.Throws<OptionsValidationException>(() => { var channel = _serviceFixture.Channel; });
        Assert.Equal("Invalid URL address specified for registry ”MyRegistry”", ex.Message);
    }
}

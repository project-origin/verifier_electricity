using System.Collections.Generic;
using Xunit;
using AutoFixture;
using Microsoft.Extensions.Options;
using System;
using System.Text;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.TestCommon.Fixtures;
using ProjectOrigin.TestCommon;
using YamlDotNet.Core;

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
    public void ValidYaml()
    {
        var issuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        ConfigureNetwork($"""
        registries:
          MyRegistry:
            url: https://example.com
        areas:
          {Area}:
            issuerKeys:
              - publicKey: {Convert.ToBase64String(Encoding.UTF8.GetBytes(issuerKey.PublicKey.ExportPkixText()))}
        """);

        var channel = _serviceFixture.Channel;

        Assert.NotNull(channel);
    }

    [Fact]
    public void ValidJson()
    {
        var issuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        ConfigureNetwork(string.Format(@"
        {{
            ""Registries"": {{
                ""MyRegistry"": {{
                    ""Url"": ""https://example.com""
                }}
            }},
            ""Areas"": {{
                ""{0}"": {{
                    ""IssuerKeys"": [
                        {{
                            ""PublicKey"": ""{1}""
                        }}
                    ]
                }}
            }}
        }}", Area, Convert.ToBase64String(Encoding.UTF8.GetBytes(issuerKey.PublicKey.ExportPkixText()))), ".json");

        var channel = _serviceFixture.Channel;

        Assert.NotNull(channel);
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

        var ex = Assert.Throws<YamlException>(() => { var channel = _serviceFixture.Channel; });
        Assert.Equal("Invalid public key format.", ex.InnerException?.Message);
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

        var ex = Assert.Throws<YamlException>(() => { var channel = _serviceFixture.Channel; });
        Assert.Equal("Invalid public key format.", ex.InnerException?.Message);
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

    [Fact]
    public void DependencyInjection_Services_ThatVerifiersAreAdded()
    {
        ConfigureValidNetwork();

        _serviceFixture.GetRequiredService<IEventVerifier<IssuedEvent>>();
        _serviceFixture.GetRequiredService<IEventVerifier<AllocatedEvent>>();
        _serviceFixture.GetRequiredService<IEventVerifier<ClaimedEvent>>();
        _serviceFixture.GetRequiredService<IEventVerifier<SlicedEvent>>();
        _serviceFixture.GetRequiredService<IEventVerifier<TransferredEvent>>();
        _serviceFixture.GetRequiredService<IEventVerifier<WithdrawnEvent>>();
        _serviceFixture.GetRequiredService<IEventVerifier<UnclaimedEvent>>();
    }

    private void ConfigureValidNetwork()
    {
        var issuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();
        ConfigureNetwork($"""
                          registries:
                            MyRegistry:
                              url: http://localhost:5000
                          areas:
                            {Area}:
                              issuerKeys:
                                - publicKey: {Convert.ToBase64String(Encoding.UTF8.GetBytes(issuerKey.PublicKey.ExportPkixText()))}
                          """);
    }

    private void ConfigureNetwork(string yamlConfig, string extension = ".yaml")
    {
        var configFile = TempFile.WriteAllText(yamlConfig, extension);

        _serviceFixture.ConfigureHostConfiguration(new Dictionary<string, string?>()
        {
         {"network:ConfigurationUri", "file://" + configFile},
        });
    }

}

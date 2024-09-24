using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using System.Text;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Services;
using ProjectOrigin.TestCommon.Fixtures;
using ProjectOrigin.TestCommon;
using Grpc.Net.Client;

namespace ProjectOrigin.Electricity.IntegrationTests;

public class FlowTests : AbstractFlowTest, IClassFixture<TestServerFixture<Startup>>
{
    private readonly TestServerFixture<Startup> _serviceFixture;

    public FlowTests(TestServerFixture<Startup> serviceFixture)
    {
        _serviceFixture = serviceFixture;

        var configFile = TempFile.WriteAllText($"""
        registries:
          {Registry}:
            url: http://localhost:5000
        areas:
          {Area}:
            issuerKeys:
              - publicKey: "{Convert.ToBase64String(Encoding.UTF8.GetBytes(IssuerKey.PublicKey.ExportPkixText()))}"
        """, ".yaml");

        serviceFixture.ConfigureHostConfiguration(new Dictionary<string, string?>()
        {
            {"network:ConfigurationUri", "file://" + configFile},
        });

        serviceFixture.ConfigureTestServices += (services) =>
        {
            services.RemoveAll<IRemoteModelLoader>();
            services.AddTransient<IRemoteModelLoader, GrpcRemoteModelLoader>();
        };
    }

    protected override GrpcChannel GetChannel()
    {
        return _serviceFixture.Channel;
    }
}


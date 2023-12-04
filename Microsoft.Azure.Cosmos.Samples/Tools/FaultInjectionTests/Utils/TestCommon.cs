namespace Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    internal static class TestCommon
    {
        internal static CosmosClient CreateCosmosClient(
            bool useGateway,
            FaultInjector injector,
            List<string>? preferredRegion = null,
            Action<CosmosClientBuilder> customizeClientBuilder = null)
        {
            CosmosClientBuilder cosmosClientBuilder = GetDefaultConfiguration();
            cosmosClientBuilder.WithFaultInjection(injector.GetChaosInterceptor());

            customizeClientBuilder?.Invoke(cosmosClientBuilder);

            if (useGateway)
            {
                cosmosClientBuilder.WithConnectionModeGateway();
            }

            if (preferredRegion != null)
            {
                cosmosClientBuilder.WithApplicationPreferredRegions(preferredRegion);
            }

            return cosmosClientBuilder.Build();
        }

        internal static CosmosClient CreateCosmosClient(
            bool useGateway,
            Action<CosmosClientBuilder> customizeClientBuilder = null)
        {
            CosmosClientBuilder cosmosClientBuilder = GetDefaultConfiguration();

            customizeClientBuilder?.Invoke(cosmosClientBuilder);

            if (useGateway)
            {
                cosmosClientBuilder.WithConnectionModeGateway();
            }

            return cosmosClientBuilder.Build();
        }

        internal static CosmosClientBuilder GetDefaultConfiguration(
            string accountEndpointOverride = null)
        {
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            CosmosClientBuilder clientBuilder = new CosmosClientBuilder(
                accountEndpoint: accountEndpointOverride ?? endpoint,
                authKeyOrResourceToken: authKey);

            return clientBuilder;
        }

        internal static (string endpoint, string authKey) GetAccountInfo()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];

            return (endpoint, authKey);
        }
    }
}

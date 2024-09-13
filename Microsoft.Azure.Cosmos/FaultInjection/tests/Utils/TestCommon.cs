namespace Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Fluent;

    internal static class TestCommon
    {
        public const string EndpointMultiRegion = "";
        public const string AuthKeyMultiRegion = "";
        
        internal static CosmosClient CreateCosmosClient(
            bool useGateway,
            FaultInjector injector,
            bool multiRegion,
            List<string>? preferredRegion = null,
            Action<CosmosClientBuilder>? customizeClientBuilder = null)
        {
            CosmosClientBuilder cosmosClientBuilder = GetDefaultConfiguration(multiRegion);
            cosmosClientBuilder.WithFaultInjection(injector.GetChaosInterceptorFactory());

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
            bool multiRegion,
            Action<CosmosClientBuilder>? customizeClientBuilder = null)
        {
            CosmosClientBuilder cosmosClientBuilder = GetDefaultConfiguration(multiRegion);

            customizeClientBuilder?.Invoke(cosmosClientBuilder);

            if (useGateway)
            {
                cosmosClientBuilder.WithConnectionModeGateway();
            }

            return cosmosClientBuilder.Build();
        }

        internal static CosmosClientBuilder GetDefaultConfiguration(
            bool multiRegion, 
            string? accountEndpointOverride = null)
        {
            CosmosClientBuilder clientBuilder = new CosmosClientBuilder(
                accountEndpoint: accountEndpointOverride 
                ?? EndpointMultiRegion,
                authKeyOrResourceToken: AuthKeyMultiRegion);
            
            if (!multiRegion)
            {
                return clientBuilder.WithApplicationPreferredRegions(new List<string> { "Central US" });
            }

            return clientBuilder;
        }
    }
}

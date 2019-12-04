//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Utils;

    internal static class TestCommon
    {
        public static readonly CosmosSerializer Serializer = new CosmosJsonDotNetSerializer();

        internal static (string endpoint, string authKey) GetAccountInfo()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];

            return (endpoint, authKey);
        }

        internal static CosmosClientBuilder GetDefaultConfiguration()
        {
            (string endpoint, string authKey) accountInfo = TestCommon.GetAccountInfo();

            return new CosmosClientBuilder(accountEndpoint: accountInfo.endpoint, authKeyOrResourceToken: accountInfo.authKey);
        }

        internal static CosmosClient CreateCosmosClient(Action<CosmosClientBuilder> customizeClientBuilder = null)
        {
            CosmosClientBuilder cosmosClientBuilder = GetDefaultConfiguration();
            if (customizeClientBuilder != null)
            {
                customizeClientBuilder(cosmosClientBuilder);
            }

            return cosmosClientBuilder.Build();
        }

        internal static CosmosClient CreateCosmosClient(CosmosClientOptions clientOptions, string resourceToken = null)
        {
            string authKey = resourceToken ?? ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];

            return new CosmosClient(endpoint, authKey, clientOptions);
        }

        internal static CosmosClient CreateCosmosClient(
            bool useGateway)
        {
            CosmosClientBuilder cosmosClientBuilder = GetDefaultConfiguration();
            if (useGateway)
            {
                cosmosClientBuilder.WithConnectionModeGateway();
            }

            return cosmosClientBuilder.Build();
        }
    }
}
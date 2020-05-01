//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.EmulatorTests
{
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Utils;
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using System.Text;

    internal static class TestCommon
    {
        // Passing in the a custom serializer instance will cause all the checks for internal types to validated.
        public static readonly JsonSerializer SerializerCore = new JsonSerializer();

        internal static Stream ToStream<T>(T input)
        {
            string s = JsonConvert.SerializeObject(input);
            return new MemoryStream(Encoding.UTF8.GetBytes(s));
        }

        internal static T FromStream<T>(Stream stream)
        {
            using (StreamReader sr = new StreamReader(stream))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<T>(reader);
            }
        }

        internal static MemoryStream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        private static (string endpoint, string authKey) GetAccountInfo()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];

            return (endpoint, authKey);
        }

        internal static CosmosClientBuilder GetClientBuilder(string resourceToken)
        {
            (string endpoint, string authKey) accountInfo = TestCommon.GetAccountInfo();
            CosmosClientBuilder clientBuilder = new CosmosClientBuilder(
                accountEndpoint: accountInfo.endpoint, 
                authKeyOrResourceToken: resourceToken ?? accountInfo.authKey);

            return clientBuilder;
        }

        internal static CosmosClient CreateCosmosClient(Action<CosmosClientBuilder> customizeClientBuilder = null)
        {
            CosmosClientBuilder cosmosClientBuilder = GetClientBuilder(resourceToken: null);
            customizeClientBuilder?.Invoke(cosmosClientBuilder);

            return cosmosClientBuilder.Build();
        }

        internal static CosmosClient CreateCosmosClient(string resourceToken, Action<CosmosClientBuilder> customizeClientBuilder = null)
        {
            CosmosClientBuilder cosmosClientBuilder = GetClientBuilder(resourceToken);
            customizeClientBuilder?.Invoke(cosmosClientBuilder);

            return cosmosClientBuilder.Build();
        }

        internal static CosmosClient CreateCosmosClient(
            bool useGateway)
        {
            CosmosClientBuilder cosmosClientBuilder = GetClientBuilder(resourceToken: null);
            if (useGateway)
            {
                cosmosClientBuilder.WithConnectionModeGateway();
            }

            return cosmosClientBuilder.Build();
        }
    }
}
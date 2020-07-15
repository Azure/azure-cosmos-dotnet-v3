//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Fluent;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class TestCommon
    {
        internal static byte[] GenerateRandomByteArray()
        {
            Random random = new Random();
            byte[] b = new byte[10];
            random.NextBytes(b);
            return b;
        }

        internal static byte[] EncryptData(byte[] plainText)
        {
            return plainText.Select(b => (byte)(b + 1)).ToArray();
        }

        internal static byte[] DecryptData(byte[] cipherText)
        {
            return cipherText.Select(b => (byte)(b - 1)).ToArray();
        }
        
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

        private static JObject ParseStream(Stream stream)
        {
            return JObject.Load(new JsonTextReader(new StreamReader(stream)));
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
        internal class TestDoc
        {
            public static List<string> PathsToEncrypt { get; } = new List<string>() { "/SensitiveStr", "/SensitiveInt" };

            [JsonProperty("id")]
            public string Id { get; set; }

            public string PK { get; set; }

            public string NonSensitive { get; set; }

            public string SensitiveStr { get; set; }

            public int SensitiveInt { get; set; }

            public TestDoc()
            {
            }

            public TestDoc(TestDoc other)
            {
                this.Id = other.Id;
                this.PK = other.PK;
                this.NonSensitive = other.NonSensitive;
                this.SensitiveStr = other.SensitiveStr;
                this.SensitiveInt = other.SensitiveInt;
            }

            public override bool Equals(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.PK == doc.PK
                       && this.NonSensitive == doc.NonSensitive
                       && this.SensitiveInt == doc.SensitiveInt
                       && this.SensitiveStr == this.SensitiveStr;
            }

            public override int GetHashCode()
            {
                int hashCode = 1652434776;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.PK);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.NonSensitive);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.SensitiveStr);
                hashCode = (hashCode * -1521134295) + EqualityComparer<int>.Default.GetHashCode(this.SensitiveInt);
                return hashCode;
            }

            public static TestDoc Create(string partitionKey = null)
            {
                return new TestDoc()
                {
                    Id = Guid.NewGuid().ToString(),
                    PK = partitionKey ?? Guid.NewGuid().ToString(),
                    NonSensitive = Guid.NewGuid().ToString(),
                    SensitiveStr = Guid.NewGuid().ToString(),
                    SensitiveInt = new Random().Next()
                };
            }

            public Stream ToStream()
            {
                return TestCommon.ToStream(this);
            }
        }
    }
}

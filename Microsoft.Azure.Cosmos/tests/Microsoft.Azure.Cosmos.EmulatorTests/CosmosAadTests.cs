//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Documents.Client;
    using global::Azure.Core;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosAadTests
    {
        [TestMethod]
        public async Task AadMockTest()
        {
            string databaseId = Guid.NewGuid().ToString();
            string containerId = Guid.NewGuid().ToString();
            using (CosmosClient cosmosClient = TestCommon.CreateCosmosClient())
            {
                Database database = await cosmosClient.CreateDatabaseAsync(databaseId);
                await database.CreateContainerAsync(containerId, "/id");
            }

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            AadSimpleEmulatorTokenCredential simpleEmulatorTokenCredential = new AadSimpleEmulatorTokenCredential(authKey);
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Gateway,
                ConnectionProtocol = Protocol.Https
            };

            using CosmosClient client = new CosmosClient(endpoint, simpleEmulatorTokenCredential, clientOptions);
            DatabaseResponse responseDatabase = await client.GetDatabase(databaseId).ReadAsync();
            //Assert.AreEqual(HttpStatusCode.NotFound, responseDatabase.StatusCode);
            // AccountProperties response = await client.ReadAccountAsync();

        }

        public class AadSimpleEmulatorTokenCredential : TokenCredential
        {
            private const string AAD_HEADER_COSMOS_EMULATOR =
                "{\"typ\":\"JWT\",\"alg\":\"RS256\",\"x5t\":\"CosmosEmulatorPrimaryMaster\",\"kid\":\"CosmosEmulatorPrimaryMaster\"}";

            private static readonly string AadHeaderPart1Base64Encoded = Base64Encode(AAD_HEADER_COSMOS_EMULATOR);

            private readonly string emulatorKeyBase64Encoded;

            public AadSimpleEmulatorTokenCredential(string emulatorKey)
            {
                if (string.IsNullOrWhiteSpace(emulatorKey))
                {
                    throw new ArgumentNullException(nameof(emulatorKey));
                }

                this.emulatorKeyBase64Encoded = AadSimpleEmulatorTokenCredential.Base64Encode(emulatorKey);
            }

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext,
                CancellationToken cancellationToken)
            {
                return new ValueTask<AccessToken>(this.GetEmulatorKeyBasedAadString());
            }

            public override AccessToken GetToken(TokenRequestContext requestContext,
                CancellationToken cancellationToken)
            {
                return this.GetEmulatorKeyBasedAadString();
            }

            private AccessToken GetEmulatorKeyBasedAadString()
            {
                string epochSecond = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
                DateTimeOffset expireDateTimeOffset = DateTimeOffset.UtcNow.Add(TimeSpan.FromHours(1));
                string expireEpochSecond = expireDateTimeOffset.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
                string part2 =
                    $"{{\"aud\":\"https://localhost.localhost\",\"iss\":\"https://sts.fake-issuer.net/7b1999a1-dfd7-440e-8204-00170979b984\",\"iat\":{epochSecond},\"nbf\":{epochSecond},\"exp\":{expireEpochSecond},\"aio\":\"\",\"appid\":\"localhost\",\"appidacr\":\"1\",\"idp\":\"https://localhost:8081/\",\"oid\":\"96313034-4739-43cb-93cd-74193adbe5b6\",\"rh\":\"\",\"sub\":\"localhost\",\"tid\":\"EmulatorFederation\",\"uti\":\"\",\"ver\":\"1.0\",\"scp\":\"user_impersonation\",\"groups\":[\"7ce1d003-4cb3-4879-b7c5-74062a35c66e\",\"e99ff30c-c229-4c67-ab29-30a6aebc3e58\",\"5549bb62-c77b-4305-bda9-9ec66b85d9e4\",\"c44fd685-5c58-452c-aaf7-13ce75184f65\",\"be895215-eab5-43b7-9536-9ef8fe130330\"]}}";
                string part2Encoded = Base64Encode(part2);
                string token = AadHeaderPart1Base64Encoded + "." + part2Encoded + "." + this.emulatorKeyBase64Encoded;
                return new AccessToken(token, expireDateTimeOffset);
            }

            public static string Base64Encode(string plainText)
            {
                byte[] plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                return System.Convert.ToBase64String(plainTextBytes);
            }
        }
    }
}

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosAuthorizationTests
    {
        public CosmosAuthorizationTests()
        {
        }

        [TestMethod]
        public async Task ResourceTokenAsync()
        {
            AuthorizationTokenProvider cosmosAuthorization = new AuthorizationTokenProviderResourceToken("VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc=");

            { 
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test",
                    ResourceType.Database.ToResourceTypeString(),
                    "GET",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual("VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc%3d", token);
                Assert.IsNull(payload);
            }

            {
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test\\colls\\abc",
                    ResourceType.Collection.ToResourceTypeString(),
                    "PUT",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual("VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc%3d", token);
                Assert.IsNull(payload);
            }

            {
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test\\colls\\abc\\docs\\1234",
                    ResourceType.Document.ToResourceTypeString(),
                    "GET",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual("VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc%3d", token);
                Assert.IsNull(payload);
            }
        }

        [TestMethod]
        public async Task TokenAuthAsync()
        {
            LocalEmulatorTokenCredential simpleEmulatorTokenCredential = new LocalEmulatorTokenCredential(
                "VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc=",
                defaultDateTime: new DateTime(2020, 9, 21, 9, 9, 9));

            AuthorizationTokenProvider cosmosAuthorization = new AuthorizationTokenProviderTokenCredential(
                simpleEmulatorTokenCredential,
                "https://localhost:8081",
                backgroundTokenCredentialRefreshInterval: TimeSpan.FromSeconds(1));

            {
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test",
                    ResourceType.Database.ToResourceTypeString(),
                    "GET",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual(
                    "type%3daad%26ver%3d1.0%26sig%3dew0KICAgICAgICAgICAgICAgICJhbGciOiJSUzI1NiIsDQogICAgICAgICAgICAgICAgImtpZCI6InhfOUtTdXNLVTVZY0hmNCIsDQogICAgICAgICAgICAgICAgInR5cCI6IkpXVCINCiAgICAgICAgICAgIH0.ew0KICAgICAgICAgICAgICAgICJvaWQiOiI5NjMxMzAzNC00NzM5LTQzY2ItOTNjZC03NDE5M2FkYmU1YjYiLA0KICAgICAgICAgICAgICAgICJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLA0KICAgICAgICAgICAgICAgICJncm91cHMiOlsNCiAgICAgICAgICAgICAgICAgICAgIjdjZTFkMDAzLTRjYjMtNDg3OS1iN2M1LTc0MDYyYTM1YzY2ZSIsDQogICAgICAgICAgICAgICAgICAgICJlOTlmZjMwYy1jMjI5LTRjNjctYWIyOS0zMGE2YWViYzNlNTgiLA0KICAgICAgICAgICAgICAgICAgICAiNTU0OWJiNjItYzc3Yi00MzA1LWJkYTktOWVjNjZiODVkOWU0IiwNCiAgICAgICAgICAgICAgICAgICAgImM0NGZkNjg1LTVjNTgtNDUyYy1hYWY3LTEzY2U3NTE4NGY2NSIsDQogICAgICAgICAgICAgICAgICAgICJiZTg5NTIxNS1lYWI1LTQzYjctOTUzNi05ZWY4ZmUxMzAzMzAiDQogICAgICAgICAgICAgICAgXSwNCiAgICAgICAgICAgICAgICAibmJmIjoxNjAwNzA0NTQ5LA0KICAgICAgICAgICAgICAgICJleHAiOjE2MDA3MDgxNDksDQogICAgICAgICAgICAgICAgImlhdCI6MTU5NjU5MjMzNSwNCiAgICAgICAgICAgICAgICAiaXNzIjoiaHR0cHM6Ly9zdHMuZmFrZS1pc3N1ZXIubmV0LzdiMTk5OWExLWRmZDctNDQwZS04MjA0LTAwMTcwOTc5Yjk4NCIsDQogICAgICAgICAgICAgICAgImF1ZCI6Imh0dHBzOi8vbG9jYWxob3N0LmxvY2FsaG9zdCINCiAgICAgICAgICAgIH0.VkdocGN5QnBjeUJoSUhOaGJYQnNaU0J6ZEhKcGJtYz0"
                    , token);
                Assert.IsNull(payload);
            }

            {
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test\\colls\\abc",
                    ResourceType.Collection.ToResourceTypeString(),
                    "PUT",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual(
                    "type%3daad%26ver%3d1.0%26sig%3dew0KICAgICAgICAgICAgICAgICJhbGciOiJSUzI1NiIsDQogICAgICAgICAgICAgICAgImtpZCI6InhfOUtTdXNLVTVZY0hmNCIsDQogICAgICAgICAgICAgICAgInR5cCI6IkpXVCINCiAgICAgICAgICAgIH0.ew0KICAgICAgICAgICAgICAgICJvaWQiOiI5NjMxMzAzNC00NzM5LTQzY2ItOTNjZC03NDE5M2FkYmU1YjYiLA0KICAgICAgICAgICAgICAgICJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLA0KICAgICAgICAgICAgICAgICJncm91cHMiOlsNCiAgICAgICAgICAgICAgICAgICAgIjdjZTFkMDAzLTRjYjMtNDg3OS1iN2M1LTc0MDYyYTM1YzY2ZSIsDQogICAgICAgICAgICAgICAgICAgICJlOTlmZjMwYy1jMjI5LTRjNjctYWIyOS0zMGE2YWViYzNlNTgiLA0KICAgICAgICAgICAgICAgICAgICAiNTU0OWJiNjItYzc3Yi00MzA1LWJkYTktOWVjNjZiODVkOWU0IiwNCiAgICAgICAgICAgICAgICAgICAgImM0NGZkNjg1LTVjNTgtNDUyYy1hYWY3LTEzY2U3NTE4NGY2NSIsDQogICAgICAgICAgICAgICAgICAgICJiZTg5NTIxNS1lYWI1LTQzYjctOTUzNi05ZWY4ZmUxMzAzMzAiDQogICAgICAgICAgICAgICAgXSwNCiAgICAgICAgICAgICAgICAibmJmIjoxNjAwNzA0NTQ5LA0KICAgICAgICAgICAgICAgICJleHAiOjE2MDA3MDgxNDksDQogICAgICAgICAgICAgICAgImlhdCI6MTU5NjU5MjMzNSwNCiAgICAgICAgICAgICAgICAiaXNzIjoiaHR0cHM6Ly9zdHMuZmFrZS1pc3N1ZXIubmV0LzdiMTk5OWExLWRmZDctNDQwZS04MjA0LTAwMTcwOTc5Yjk4NCIsDQogICAgICAgICAgICAgICAgImF1ZCI6Imh0dHBzOi8vbG9jYWxob3N0LmxvY2FsaG9zdCINCiAgICAgICAgICAgIH0.VkdocGN5QnBjeUJoSUhOaGJYQnNaU0J6ZEhKcGJtYz0"
                    , token);
                Assert.IsNull(payload);
            }

            {
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test\\colls\\abc\\docs\\1234",
                    ResourceType.Document.ToResourceTypeString(),
                    "GET",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual(
                    "type%3daad%26ver%3d1.0%26sig%3dew0KICAgICAgICAgICAgICAgICJhbGciOiJSUzI1NiIsDQogICAgICAgICAgICAgICAgImtpZCI6InhfOUtTdXNLVTVZY0hmNCIsDQogICAgICAgICAgICAgICAgInR5cCI6IkpXVCINCiAgICAgICAgICAgIH0.ew0KICAgICAgICAgICAgICAgICJvaWQiOiI5NjMxMzAzNC00NzM5LTQzY2ItOTNjZC03NDE5M2FkYmU1YjYiLA0KICAgICAgICAgICAgICAgICJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLA0KICAgICAgICAgICAgICAgICJncm91cHMiOlsNCiAgICAgICAgICAgICAgICAgICAgIjdjZTFkMDAzLTRjYjMtNDg3OS1iN2M1LTc0MDYyYTM1YzY2ZSIsDQogICAgICAgICAgICAgICAgICAgICJlOTlmZjMwYy1jMjI5LTRjNjctYWIyOS0zMGE2YWViYzNlNTgiLA0KICAgICAgICAgICAgICAgICAgICAiNTU0OWJiNjItYzc3Yi00MzA1LWJkYTktOWVjNjZiODVkOWU0IiwNCiAgICAgICAgICAgICAgICAgICAgImM0NGZkNjg1LTVjNTgtNDUyYy1hYWY3LTEzY2U3NTE4NGY2NSIsDQogICAgICAgICAgICAgICAgICAgICJiZTg5NTIxNS1lYWI1LTQzYjctOTUzNi05ZWY4ZmUxMzAzMzAiDQogICAgICAgICAgICAgICAgXSwNCiAgICAgICAgICAgICAgICAibmJmIjoxNjAwNzA0NTQ5LA0KICAgICAgICAgICAgICAgICJleHAiOjE2MDA3MDgxNDksDQogICAgICAgICAgICAgICAgImlhdCI6MTU5NjU5MjMzNSwNCiAgICAgICAgICAgICAgICAiaXNzIjoiaHR0cHM6Ly9zdHMuZmFrZS1pc3N1ZXIubmV0LzdiMTk5OWExLWRmZDctNDQwZS04MjA0LTAwMTcwOTc5Yjk4NCIsDQogICAgICAgICAgICAgICAgImF1ZCI6Imh0dHBzOi8vbG9jYWxob3N0LmxvY2FsaG9zdCINCiAgICAgICAgICAgIH0.VkdocGN5QnBjeUJoSUhOaGJYQnNaU0J6ZEhKcGJtYz0"
                    , token);
                Assert.IsNull(payload);
            }
        }
    }
}

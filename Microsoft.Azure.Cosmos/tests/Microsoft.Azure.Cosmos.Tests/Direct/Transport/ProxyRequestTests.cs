// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ProxyRequestTests
    {
        [Owner("sqlx")]
        [TestMethod]
        public void TestConstructorAndReset()
        {
            RntbdConstants.RequestIdentifiers[] expectedFirstTokens = new[]
            {
                RntbdConstants.RequestIdentifiers.EffectivePartitionKey,
                RntbdConstants.RequestIdentifiers.StartEpkHash,
                RntbdConstants.RequestIdentifiers.EndEpkHash,
                RntbdConstants.RequestIdentifiers.GlobalDatabaseAccountName,
                RntbdConstants.RequestIdentifiers.RegionalDatabaseAccountName,
                RntbdConstants.RequestIdentifiers.DatabaseName,
                RntbdConstants.RequestIdentifiers.CollectionName,
                RntbdConstants.RequestIdentifiers.CollectionRid,
                RntbdConstants.RequestIdentifiers.ResourceId,
                RntbdConstants.RequestIdentifiers.PayloadPresent,
                RntbdConstants.RequestIdentifiers.DocumentName,
                RntbdConstants.RequestIdentifiers.AuthorizationToken,
                RntbdConstants.RequestIdentifiers.Date,
            };

            RntbdConstants.RequestIdentifiers[] expectedTokensToMakeOptional = new[]
            {
                RntbdConstants.RequestIdentifiers.ReplicaPath,
                RntbdConstants.RequestIdentifiers.TransportRequestID
            };

            Func<RntbdConstants.Request, RntbdToken>[] tokensMadeOptionalGetters = new Func<RntbdConstants.Request, RntbdToken>[]
            {
                (request) => request.replicaPath,
                (request) => request.transportRequestID
            };

            ProxyRequest proxyRequest = new();
            RntbdConstants.Request request = proxyRequest.RntbdRequest;
            RntbdConstants.Request defaultRequest = new();

            // Assert that the created request has the same tokens as the default request
            AssertSameTokenSet(request, defaultRequest);

            // Assert that the first tokens are in the expected positions
            AssertExpectedFirstTokens(expectedFirstTokens, request);

            // Assert required and optional tokens
            AssertRequiredAndOptionalTokens(expectedTokensToMakeOptional, request, defaultRequest);

            // Assert that the fields for tokens made optional were update (not only the array)
            AssertOptionalTokenFields(proxyRequest.RntbdRequest, tokensMadeOptionalGetters);

            proxyRequest.Reset();
            AssertSameTokenSet(proxyRequest.RntbdRequest, defaultRequest);
            AssertExpectedFirstTokens(expectedFirstTokens, proxyRequest.RntbdRequest);
            AssertRequiredAndOptionalTokens(expectedTokensToMakeOptional, proxyRequest.RntbdRequest, defaultRequest);
            AssertOptionalTokenFields(proxyRequest.RntbdRequest, tokensMadeOptionalGetters);
        }

        private static void AssertOptionalTokenFields(RntbdConstants.Request request, Func<RntbdConstants.Request, RntbdToken>[] getterTokenFieldsMadeOptional)
        {
            foreach (Func<RntbdConstants.Request, RntbdToken> getter in getterTokenFieldsMadeOptional)
            {
                RntbdToken token = getter(request);
                Assert.IsFalse(token.IsRequired());
            }
        }

        private static void AssertSameTokenSet(RntbdConstants.Request request, RntbdConstants.Request defaultRequest)
        {
            Assert.AreEqual(defaultRequest.tokens.Length, request.tokens.Length);
            HashSet<RntbdConstants.RequestIdentifiers> expectedTokens = GetRequestIdentifiers(defaultRequest);
            HashSet<RntbdConstants.RequestIdentifiers> actualTokens = GetRequestIdentifiers(request);
            Assert.AreEqual(expectedTokens.Count, actualTokens.Count);
            foreach (RntbdConstants.RequestIdentifiers token in expectedTokens)
            {
                Assert.IsTrue(actualTokens.Contains(token));
            }
        }

        private static void AssertExpectedFirstTokens(RntbdConstants.RequestIdentifiers[] expectedFirstTokens, RntbdConstants.Request request)
        {
            for (int i = 0; i < expectedFirstTokens.Length; i++)
            {
                Assert.AreEqual(request.tokens[i].GetTokenIdentifier(), (ushort)expectedFirstTokens[i]);
            }
        }

        private static void AssertRequiredAndOptionalTokens(RntbdConstants.RequestIdentifiers[] expectedTokensToMakeOptional, RntbdConstants.Request request, RntbdConstants.Request defaultRequest)
        {
            HashSet<RntbdConstants.RequestIdentifiers> tokensToMakeOptionalSet = new(expectedTokensToMakeOptional);
            Dictionary<RntbdConstants.RequestIdentifiers, int> indexes = CreateIndexMap(request);
            foreach (RntbdToken token in defaultRequest.tokens)
            {
                if (token == null)
                {
                    continue;
                }

                RntbdToken otherToken = request.tokens[indexes[(RntbdConstants.RequestIdentifiers)token.GetTokenIdentifier()]];
                Assert.AreEqual(token.GetTokenIdentifier(), otherToken.GetTokenIdentifier());
                Assert.AreEqual(token.GetTokenType(), otherToken.GetTokenType());
                if (tokensToMakeOptionalSet.Contains((RntbdConstants.RequestIdentifiers)token.GetTokenIdentifier()))
                {
                    Assert.IsFalse(otherToken.IsRequired());
                }
                else
                {
                    Assert.IsTrue(otherToken.IsRequired() == token.IsRequired());
                }
            }
        }

        private static HashSet<RntbdConstants.RequestIdentifiers> GetRequestIdentifiers(RntbdConstants.Request request)
        {
            HashSet<RntbdConstants.RequestIdentifiers> tokens = new();
            foreach (RntbdToken token in request.tokens)
            {
                if (token != null)
                {
                    tokens.Add((RntbdConstants.RequestIdentifiers)token.GetTokenIdentifier());
                }
            }

            return tokens;
        }

        private static Dictionary<RntbdConstants.RequestIdentifiers, int> CreateIndexMap(RntbdConstants.Request request)
        {
            Dictionary<RntbdConstants.RequestIdentifiers, int> indexes = new();
            for (int i = 0; i < request.tokens.Length; i++)
            {
                if (request.tokens[i] == null)
                {
                    continue;
                }

                RntbdConstants.RequestIdentifiers tokenType = (RntbdConstants.RequestIdentifiers)request.tokens[i].GetTokenIdentifier();
                indexes[tokenType] = i;
            }

            return indexes;
        }

        private static ushort[] CreateTestArray(params RntbdConstants.RequestIdentifiers[] identifiers)
        {
            return Array.ConvertAll(identifiers, x => (ushort)x);
        }

        private static RntbdConstants.RequestIdentifiers[] ConvertToRequestIdentifiers(ushort[] identifiers)
        {
            return Array.ConvertAll(identifiers, x => (RntbdConstants.RequestIdentifiers)x);
        }
    }
}
//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryRequestOptionsUniTests
    {
        [TestMethod]
        public void StatelessTest()
        {
            QueryRequestOptions requestOption = new QueryRequestOptions();

            RequestMessage testMessage = new RequestMessage();
            requestOption.PopulateRequestOptions(testMessage);

            Assert.IsNull(testMessage.Headers.ContinuationToken);
        }

        [TestMethod]
        public void EmbeddingGenerator_DefaultsToNull()
        {
            QueryRequestOptions requestOptions = new QueryRequestOptions();

            Assert.IsNull(requestOptions.EmbeddingGenerator);
        }

        [TestMethod]
        public void EmbeddingGenerator_RoundTripsAssignedInstance()
        {
            // Verifies that EmbeddingGenerator is declared on QueryRequestOptions and that it
            // delegates to BaseEmbeddingGenerator (mirrors ReadConsistencyStrategy → BaseReadConsistencyStrategy).
            ICosmosEmbeddingGenerator generator = new TestEmbeddingGenerator();
            QueryRequestOptions requestOptions = new QueryRequestOptions
            {
                EmbeddingGenerator = generator,
            };

            Assert.AreSame(generator, requestOptions.EmbeddingGenerator,
                "EmbeddingGenerator getter should return the assigned instance");
            Assert.AreSame(generator, requestOptions.BaseEmbeddingGenerator,
                "Setting EmbeddingGenerator should populate BaseEmbeddingGenerator");
        }

        [TestMethod]
        public void EmbeddingGenerator_RequestLevelOverridesClientLevel()
        {
            ICosmosEmbeddingGenerator clientGenerator = new TestEmbeddingGenerator();
            ICosmosEmbeddingGenerator requestGenerator = new TestEmbeddingGenerator();

            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                EmbeddingGenerator = clientGenerator,
            };

            QueryRequestOptions requestOptions = new QueryRequestOptions
            {
                EmbeddingGenerator = requestGenerator,
            };

            // Request-level should take precedence
            ICosmosEmbeddingGenerator effective = requestOptions.EmbeddingGenerator ?? clientOptions.EmbeddingGenerator;
            Assert.AreSame(requestGenerator, effective,
                "Request-level EmbeddingGenerator should take precedence over client-level");
        }

        [TestMethod]
        public void EmbeddingGenerator_FallsBackToClientLevel()
        {
            ICosmosEmbeddingGenerator clientGenerator = new TestEmbeddingGenerator();

            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                EmbeddingGenerator = clientGenerator,
            };

            QueryRequestOptions requestOptions = new QueryRequestOptions(); // no generator set

            // Should fall back to client level
            ICosmosEmbeddingGenerator effective = requestOptions.EmbeddingGenerator ?? clientOptions.EmbeddingGenerator;
            Assert.AreSame(clientGenerator, effective,
                "Client-level EmbeddingGenerator should be used when request-level is null");
        }

        private sealed class TestEmbeddingGenerator : ICosmosEmbeddingGenerator
        {
            public Task<IEnumerable<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
                IEnumerable<string> text,
                CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }
    }
}
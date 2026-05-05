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
            IEmbeddingGenerator generator = new TestEmbeddingGenerator();
            QueryRequestOptions requestOptions = new QueryRequestOptions
            {
                EmbeddingGenerator = generator,
            };

            Assert.AreSame(generator, requestOptions.EmbeddingGenerator);
        }

        [TestMethod]
        public void EmbeddingGenerator_DelegatesToBaseEmbeddingGenerator()
        {
            // EmbeddingGenerator property should be backed by BaseEmbeddingGenerator,
            // mirroring how ReadConsistencyStrategy delegates to BaseReadConsistencyStrategy.
            IEmbeddingGenerator generator = new TestEmbeddingGenerator();
            QueryRequestOptions requestOptions = new QueryRequestOptions();

            requestOptions.EmbeddingGenerator = generator;

            Assert.AreSame(generator, requestOptions.BaseEmbeddingGenerator,
                "Setting EmbeddingGenerator should update BaseEmbeddingGenerator");
            Assert.AreSame(generator, requestOptions.EmbeddingGenerator,
                "Getting EmbeddingGenerator should read from BaseEmbeddingGenerator");
        }

        [TestMethod]
        public void EmbeddingGenerator_RequestLevelOverridesClientLevel()
        {
            IEmbeddingGenerator clientGenerator = new TestEmbeddingGenerator();
            IEmbeddingGenerator requestGenerator = new TestEmbeddingGenerator();

            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                EmbeddingGenerator = clientGenerator,
            };

            QueryRequestOptions requestOptions = new QueryRequestOptions
            {
                EmbeddingGenerator = requestGenerator,
            };

            // Request-level should take precedence
            IEmbeddingGenerator effective = requestOptions.EmbeddingGenerator ?? clientOptions.EmbeddingGenerator;
            Assert.AreSame(requestGenerator, effective,
                "Request-level EmbeddingGenerator should take precedence over client-level");
        }

        [TestMethod]
        public void EmbeddingGenerator_FallsBackToClientLevel()
        {
            IEmbeddingGenerator clientGenerator = new TestEmbeddingGenerator();

            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                EmbeddingGenerator = clientGenerator,
            };

            QueryRequestOptions requestOptions = new QueryRequestOptions(); // no generator set

            // Should fall back to client level
            IEmbeddingGenerator effective = requestOptions.EmbeddingGenerator ?? clientOptions.EmbeddingGenerator;
            Assert.AreSame(clientGenerator, effective,
                "Client-level EmbeddingGenerator should be used when request-level is null");
        }

        private sealed class TestEmbeddingGenerator : IEmbeddingGenerator
        {
            public Task<IEnumerable<ReadOnlyMemory<double>>> GenerateEmbeddingsAsync(
                IEnumerable<string> text,
                CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }
    }
}
//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.AI.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.AI;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AzureOpenAIEmbeddingGeneratorTests
    {
        private const int DefaultDimensions = 256;

        // ------------------------------------------------------------------ //
        //  Constructor validation — explicit parameters
        // ------------------------------------------------------------------ //

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullEndpoint_Throws()
        {
            _ = new AzureOpenAIEmbeddingGenerator(null, "deployment", DefaultDimensions, "apiKey");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_EmptyEndpoint_Throws()
        {
            _ = new AzureOpenAIEmbeddingGenerator(string.Empty, "deployment", DefaultDimensions, "apiKey");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WhitespaceEndpoint_Throws()
        {
            _ = new AzureOpenAIEmbeddingGenerator("   ", "deployment", DefaultDimensions, "apiKey");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullDeploymentName_Throws()
        {
            _ = new AzureOpenAIEmbeddingGenerator("https://my.openai.azure.com", null, DefaultDimensions, "apiKey");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullApiKey_Throws()
        {
            _ = new AzureOpenAIEmbeddingGenerator("https://my.openai.azure.com", "deployment", DefaultDimensions, (string)null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor_ZeroDimensions_Throws()
        {
            _ = new AzureOpenAIEmbeddingGenerator("https://my.openai.azure.com", "deployment", 0, "apiKey");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor_NegativeDimensions_Throws()
        {
            _ = new AzureOpenAIEmbeddingGenerator("https://my.openai.azure.com", "deployment", -1, "apiKey");
        }

        // ------------------------------------------------------------------ //
        //  Constructor validation — EmbeddingSource overloads
        // ------------------------------------------------------------------ //

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullSource_Throws()
        {
            _ = new AzureOpenAIEmbeddingGenerator((EmbeddingSource)null, DefaultDimensions, "apiKey");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Constructor_SourceMissingEndpoint_Throws()
        {
            EmbeddingSource source = new EmbeddingSource
            {
                Endpoint = null,
                DeploymentName = "deployment",
            };
            _ = new AzureOpenAIEmbeddingGenerator(source, DefaultDimensions, "apiKey");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Constructor_SourceMissingDeploymentName_Throws()
        {
            EmbeddingSource source = new EmbeddingSource
            {
                Endpoint = "https://my.openai.azure.com",
                DeploymentName = null,
            };
            _ = new AzureOpenAIEmbeddingGenerator(source, DefaultDimensions, "apiKey");
        }

        // ------------------------------------------------------------------ //
        //  GenerateEmbeddingsAsync — input validation
        // ------------------------------------------------------------------ //

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GenerateEmbeddingsAsync_NullInput_Throws()
        {
            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator();
            _ = await generator.GenerateEmbeddingsAsync(null);
        }

        [TestMethod]
        public async Task GenerateEmbeddingsAsync_EmptyInput_ReturnsEmpty()
        {
            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator();
            IEnumerable<ReadOnlyMemory<float>> result = await generator.GenerateEmbeddingsAsync(Array.Empty<string>());
            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task GenerateEmbeddingsAsync_NullElementInInput_Throws()
        {
            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator();
            _ = await generator.GenerateEmbeddingsAsync(new[] { "hello", null, "world" });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task GenerateEmbeddingsAsync_WhitespaceElementInInput_Throws()
        {
            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator();
            _ = await generator.GenerateEmbeddingsAsync(new[] { "hello", "   ", "world" });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task GenerateEmbeddingsAsync_EmptyStringElementInInput_Throws()
        {
            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator();
            _ = await generator.GenerateEmbeddingsAsync(new[] { "hello", string.Empty });
        }

        // ------------------------------------------------------------------ //
        //  GenerateEmbeddingsAsync — happy path (single batch)
        // ------------------------------------------------------------------ //

        [TestMethod]
        public async Task GenerateEmbeddingsAsync_SingleBatch_ReturnsSameOrder()
        {
            int callCount = 0;
            float[] vec1 = { 0.1f, 0.2f };
            float[] vec2 = { 0.3f, 0.4f };
            float[] vec3 = { 0.5f, 0.6f };

            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator(
                (inputs, dims, ct) =>
                {
                    callCount++;
                    List<ReadOnlyMemory<float>> list = new List<ReadOnlyMemory<float>>();
                    foreach (string _ in inputs)
                    {
                        int idx = list.Count;
                        list.Add(idx == 0 ? vec1 : idx == 1 ? vec2 : vec3);
                    }

                    return Task.FromResult<IReadOnlyList<ReadOnlyMemory<float>>>(list);
                });

            IEnumerable<ReadOnlyMemory<float>> result = await generator.GenerateEmbeddingsAsync(
                new[] { "text1", "text2", "text3" });

            Assert.AreEqual(1, callCount, "Expected a single API call for ≤2048 inputs.");
            List<ReadOnlyMemory<float>> resultList = result.ToList();
            Assert.AreEqual(3, resultList.Count);
            Assert.AreEqual(0.1f, resultList[0].Span[0]);
            Assert.AreEqual(0.3f, resultList[1].Span[0]);
            Assert.AreEqual(0.5f, resultList[2].Span[0]);
        }

        [TestMethod]
        public async Task GenerateEmbeddingsAsync_PassesDimensionsToFunc()
        {
            const int expectedDimensions = 1536;
            int receivedDimensions = 0;

            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator(
                dimensions: expectedDimensions,
                func: (inputs, dims, ct) =>
                {
                    receivedDimensions = dims;
                    IReadOnlyList<ReadOnlyMemory<float>> r = inputs.Select(_ => new ReadOnlyMemory<float>(new float[dims])).ToList();
                    return Task.FromResult(r);
                });

            _ = await generator.GenerateEmbeddingsAsync(new[] { "hello" });

            Assert.AreEqual(expectedDimensions, receivedDimensions);
        }

        [TestMethod]
        public async Task GenerateEmbeddingsAsync_PassesCancellationToken()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken passedToken = CancellationToken.None;

            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator(
                (inputs, dims, ct) =>
                {
                    passedToken = ct;
                    IReadOnlyList<ReadOnlyMemory<float>> r = inputs.Select(_ => new ReadOnlyMemory<float>(new float[dims])).ToList();
                    return Task.FromResult(r);
                });

            _ = await generator.GenerateEmbeddingsAsync(new[] { "hello" }, cts.Token);

            Assert.AreEqual(cts.Token, passedToken);
        }

        // ------------------------------------------------------------------ //
        //  GenerateEmbeddingsAsync — batching (>2048 inputs)
        // ------------------------------------------------------------------ //

        [TestMethod]
        public async Task GenerateEmbeddingsAsync_LargeInput_SplitsIntoBatches()
        {
            const int totalInputs = 5000;
            int callCount = 0;
            List<int> batchSizes = new List<int>();

            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator(
                (inputs, dims, ct) =>
                {
                    callCount++;
                    batchSizes.Add(inputs.Count);
                    IReadOnlyList<ReadOnlyMemory<float>> r = inputs.Select(_ => new ReadOnlyMemory<float>(new float[dims])).ToList();
                    return Task.FromResult(r);
                });

            string[] allInputs = Enumerable.Range(0, totalInputs).Select(i => $"text{i}").ToArray();
            IEnumerable<ReadOnlyMemory<float>> result = await generator.GenerateEmbeddingsAsync(allInputs);

            Assert.AreEqual(3, callCount, "5000 inputs should require 3 batches (2048+2048+904).");
            Assert.AreEqual(2048, batchSizes[0]);
            Assert.AreEqual(2048, batchSizes[1]);
            Assert.AreEqual(totalInputs - 2 * 2048, batchSizes[2]);
            Assert.AreEqual(totalInputs, result.Count());
        }

        [TestMethod]
        public async Task GenerateEmbeddingsAsync_BatchBoundary_ExactlyMaxBatch_SingleCall()
        {
            int callCount = 0;

            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator(
                (inputs, dims, ct) =>
                {
                    callCount++;
                    IReadOnlyList<ReadOnlyMemory<float>> r = inputs.Select(_ => new ReadOnlyMemory<float>(new float[dims])).ToList();
                    return Task.FromResult(r);
                });

            string[] allInputs = Enumerable.Range(0, 2048).Select(i => $"text{i}").ToArray();
            _ = await generator.GenerateEmbeddingsAsync(allInputs);

            Assert.AreEqual(1, callCount, "Exactly 2048 inputs should fit in one batch.");
        }

        [TestMethod]
        public async Task GenerateEmbeddingsAsync_MultiBatch_PreservesOrder()
        {
            const int totalInputs = 2050;

            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator(
                (inputs, dims, ct) =>
                {
                    // Return a unique marker value at index 0 of each vector
                    // based on the first character of the first input.
                    IReadOnlyList<ReadOnlyMemory<float>> r = inputs
                        .Select((text, i) =>
                        {
                            // Use the text's number suffix as the embedding value
                            float marker = float.Parse(text.Substring("text".Length));
                            return new ReadOnlyMemory<float>(new[] { marker });
                        })
                        .ToList();
                    return Task.FromResult(r);
                });

            string[] allInputs = Enumerable.Range(0, totalInputs).Select(i => $"text{i}").ToArray();
            List<ReadOnlyMemory<float>> result = (await generator.GenerateEmbeddingsAsync(allInputs)).ToList();

            Assert.AreEqual(totalInputs, result.Count);
            for (int i = 0; i < totalInputs; i++)
            {
                Assert.AreEqual((float)i, result[i].Span[0], $"Embedding at index {i} has wrong value.");
            }
        }

        // ------------------------------------------------------------------ //
        //  GenerateEmbeddingsAsync — error propagation
        // ------------------------------------------------------------------ //

        [TestMethod]
        [ExpectedException(typeof(TaskCanceledException))]
        public async Task GenerateEmbeddingsAsync_CancellationRequested_Propagates()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator(
                (inputs, dims, ct) => Task.FromCanceled<IReadOnlyList<ReadOnlyMemory<float>>>(ct));

            _ = await generator.GenerateEmbeddingsAsync(new[] { "hello" }, cts.Token);
        }

        [TestMethod]
        public async Task GenerateEmbeddingsAsync_FuncThrows_PropagatesException()
        {
            using AzureOpenAIEmbeddingGenerator generator = MakeGenerator(
                (inputs, dims, ct) => Task.FromException<IReadOnlyList<ReadOnlyMemory<float>>>(new InvalidOperationException("mock failure")));

            try
            {
                _ = await generator.GenerateEmbeddingsAsync(new[] { "hello" });
                Assert.Fail("Expected exception was not thrown.");
            }
            catch (InvalidOperationException ex)
            {
                StringAssert.Contains(ex.Message, "mock failure");
            }
        }

        // ------------------------------------------------------------------ //
        //  Dispose
        // ------------------------------------------------------------------ //

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public async Task GenerateEmbeddingsAsync_AfterDispose_Throws()
        {
            AzureOpenAIEmbeddingGenerator generator = MakeGenerator();
            generator.Dispose();
            _ = await generator.GenerateEmbeddingsAsync(new[] { "hello" });
        }

        [TestMethod]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            AzureOpenAIEmbeddingGenerator generator = MakeGenerator();
            generator.Dispose();
            generator.Dispose(); // should be safe
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private static AzureOpenAIEmbeddingGenerator MakeGenerator(
            Func<IReadOnlyList<string>, int, CancellationToken, Task<IReadOnlyList<ReadOnlyMemory<float>>>> func = null,
            int dimensions = DefaultDimensions)
        {
            Func<IReadOnlyList<string>, int, CancellationToken, Task<IReadOnlyList<ReadOnlyMemory<float>>>> batchFunc =
                func ?? DefaultFunc;

            return new AzureOpenAIEmbeddingGenerator(batchFunc, dimensions);
        }

        private static Task<IReadOnlyList<ReadOnlyMemory<float>>> DefaultFunc(
            IReadOnlyList<string> inputs,
            int dimensions,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ReadOnlyMemory<float>> result = inputs
                .Select(_ => new ReadOnlyMemory<float>(new float[dimensions]))
                .ToList();
            return Task.FromResult(result);
        }
    }
}

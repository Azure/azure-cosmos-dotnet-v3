//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.AI
{
    using System;
    using System.ClientModel;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.AI.OpenAI;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos;
    using OpenAI.Embeddings;

    /// <summary>
    /// An <see cref="ICosmosEmbeddingGenerator"/> backed by Azure OpenAI that generates
    /// floating-point embedding vectors for text inputs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this class when your Cosmos DB container is configured with a vector embedding
    /// policy and you want the SDK to automatically embed query literals before execution.
    /// </para>
    /// <para>
    /// You can construct an instance directly from an <see cref="EmbeddingSource"/> (obtained
    /// from <see cref="Embedding.EmbeddingSource"/> on the container's
    /// <see cref="VectorEmbeddingPolicy"/>) or by supplying endpoint, deployment name, and
    /// dimensions explicitly.
    /// </para>
    /// <para>
    /// <b>Thread safety:</b> this class is safe to use concurrently. The underlying
    /// <c>EmbeddingClient</c> is thread-safe and a single instance may be shared
    /// across parallel queries.
    /// </para>
    /// <para>
    /// Dispose the instance when done to release the underlying HTTP connection pool.
    /// </para>
    /// </remarks>
    public sealed class AzureOpenAIEmbeddingGenerator : ICosmosEmbeddingGenerator, IDisposable
    {
        /// <summary>Azure OpenAI hard limit on inputs per embeddings API call.</summary>
        private const int MaxBatchSize = 2048;

        private readonly int dimensions;
        private readonly string endpoint;
        private readonly string deploymentName;

        // Real path: non-null when constructed from public constructors.
        private readonly EmbeddingClient embeddingClient;

        // Test path: non-null when injected via the internal test constructor.
        private readonly Func<IReadOnlyList<string>, int, CancellationToken, Task<IReadOnlyList<ReadOnlyMemory<float>>>> batchFunc;

        private bool disposed;

        // ------------------------------------------------------------------ //
        //  Public constructors from EmbeddingSource (VectorEmbeddingPolicy)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOpenAIEmbeddingGenerator"/> class
        /// using an <see cref="EmbeddingSource"/> and an API key.
        /// </summary>
        /// <param name="source">
        /// The <see cref="EmbeddingSource"/> from <see cref="Embedding.EmbeddingSource"/>;
        /// supplies the endpoint and deployment name.
        /// </param>
        /// <param name="dimensions">
        /// Expected vector dimensionality — must match <see cref="Embedding.Dimensions"/>
        /// on the enclosing <see cref="Embedding"/> entry.
        /// </param>
        /// <param name="apiKey">Azure OpenAI API key.</param>
        public AzureOpenAIEmbeddingGenerator(EmbeddingSource source, int dimensions, string apiKey)
            : this(ValidateSource(source).Endpoint, source.DeploymentName, dimensions, apiKey)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOpenAIEmbeddingGenerator"/> class
        /// using an <see cref="EmbeddingSource"/> and an Entra (Azure AD) credential.
        /// </summary>
        /// <param name="source">
        /// The <see cref="EmbeddingSource"/> from <see cref="Embedding.EmbeddingSource"/>.
        /// </param>
        /// <param name="dimensions">
        /// Expected vector dimensionality — must match <see cref="Embedding.Dimensions"/>
        /// on the enclosing <see cref="Embedding"/> entry.
        /// </param>
        /// <param name="credential">Azure token credential for Entra authentication.</param>
        public AzureOpenAIEmbeddingGenerator(EmbeddingSource source, int dimensions, TokenCredential credential)
            : this(ValidateSource(source).Endpoint, source.DeploymentName, dimensions, credential)
        {
        }

        // ------------------------------------------------------------------ //
        //  Public constructors with explicit parameters
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOpenAIEmbeddingGenerator"/> class
        /// with explicit connection parameters and an API key.
        /// </summary>
        /// <param name="endpoint">Azure OpenAI endpoint URL (e.g. <c>https://my-resource.openai.azure.com</c>).</param>
        /// <param name="deploymentName">Name of the embedding model deployment.</param>
        /// <param name="dimensions">Expected output vector dimensionality.</param>
        /// <param name="apiKey">Azure OpenAI API key.</param>
        public AzureOpenAIEmbeddingGenerator(string endpoint, string deploymentName, int dimensions, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (string.IsNullOrWhiteSpace(deploymentName))
            {
                throw new ArgumentNullException(nameof(deploymentName));
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey));
            }

            if (dimensions <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dimensions), "Dimensions must be greater than zero.");
            }

            this.endpoint = endpoint.TrimEnd('/');
            this.deploymentName = deploymentName;
            this.dimensions = dimensions;

            AzureOpenAIClient client = new AzureOpenAIClient(new Uri(this.endpoint), new AzureKeyCredential(apiKey));
            this.embeddingClient = client.GetEmbeddingClient(deploymentName);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOpenAIEmbeddingGenerator"/> class
        /// with explicit connection parameters and an Entra credential.
        /// </summary>
        /// <param name="endpoint">Azure OpenAI endpoint URL.</param>
        /// <param name="deploymentName">Name of the embedding model deployment.</param>
        /// <param name="dimensions">Expected output vector dimensionality.</param>
        /// <param name="credential">Azure token credential for Entra authentication.</param>
        public AzureOpenAIEmbeddingGenerator(string endpoint, string deploymentName, int dimensions, TokenCredential credential)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (string.IsNullOrWhiteSpace(deploymentName))
            {
                throw new ArgumentNullException(nameof(deploymentName));
            }

            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            if (dimensions <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dimensions), "Dimensions must be greater than zero.");
            }

            this.endpoint = endpoint.TrimEnd('/');
            this.deploymentName = deploymentName;
            this.dimensions = dimensions;

            AzureOpenAIClient client = new AzureOpenAIClient(new Uri(this.endpoint), credential);
            this.embeddingClient = client.GetEmbeddingClient(deploymentName);
        }

        // ------------------------------------------------------------------ //
        //  Internal test constructor
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOpenAIEmbeddingGenerator"/> class
        /// with an injected batch function for unit testing.
        /// Not intended for production use.
        /// </summary>
        /// <param name="batchFunc">
        /// A delegate that replaces the real Azure OpenAI call.
        /// Receives (inputs, dimensions, cancellationToken) and returns the embedding vectors.
        /// </param>
        /// <param name="dimensions">Expected output vector dimensionality.</param>
        /// <param name="endpoint">Endpoint string used in error messages.</param>
        /// <param name="deploymentName">Deployment name used in error messages.</param>
        internal AzureOpenAIEmbeddingGenerator(
            Func<IReadOnlyList<string>, int, CancellationToken, Task<IReadOnlyList<ReadOnlyMemory<float>>>> batchFunc,
            int dimensions,
            string endpoint = "https://test.openai.azure.com",
            string deploymentName = "test-deployment")
        {
            this.batchFunc = batchFunc ?? throw new ArgumentNullException(nameof(batchFunc));
            this.dimensions = dimensions;
            this.endpoint = endpoint;
            this.deploymentName = deploymentName;
        }

        // ------------------------------------------------------------------ //
        //  ICosmosEmbeddingGenerator
        // ------------------------------------------------------------------ //

        /// <inheritdoc/>
        public async Task<IEnumerable<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
            IEnumerable<string> text,
            CancellationToken cancellationToken = default)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            this.ThrowIfDisposed();

            List<string> inputs = new List<string>(text);

            if (inputs.Count == 0)
            {
                return Array.Empty<ReadOnlyMemory<float>>();
            }

            this.ValidateInputs(inputs);

            List<ReadOnlyMemory<float>> results = new List<ReadOnlyMemory<float>>(inputs.Count);

            if (inputs.Count <= MaxBatchSize)
            {
                IReadOnlyList<ReadOnlyMemory<float>> batch = await this.InvokeBatchAsync(inputs, cancellationToken);
                results.AddRange(batch);
            }
            else
            {
                for (int offset = 0; offset < inputs.Count; offset += MaxBatchSize)
                {
                    int count = Math.Min(MaxBatchSize, inputs.Count - offset);
                    List<string> chunk = inputs.GetRange(offset, count);
                    IReadOnlyList<ReadOnlyMemory<float>> batchResult = await this.InvokeBatchAsync(chunk, cancellationToken);
                    results.AddRange(batchResult);
                }
            }

            return results;
        }

        /// <summary>
        /// Releases resources held by this instance.
        /// </summary>
        public void Dispose()
        {
            this.disposed = true;
        }

        // ------------------------------------------------------------------ //
        //  Private helpers
        // ------------------------------------------------------------------ //
        private static EmbeddingSource ValidateSource(EmbeddingSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (string.IsNullOrWhiteSpace(source.Endpoint))
            {
                throw new ArgumentException("EmbeddingSource.Endpoint must not be null or empty.", nameof(source));
            }

            if (string.IsNullOrWhiteSpace(source.DeploymentName))
            {
                throw new ArgumentException("EmbeddingSource.DeploymentName must not be null or empty.", nameof(source));
            }

            return source;
        }

        private Task<IReadOnlyList<ReadOnlyMemory<float>>> InvokeBatchAsync(
            IReadOnlyList<string> batch,
            CancellationToken cancellationToken)
        {
            if (this.batchFunc != null)
            {
                return this.batchFunc(batch, this.dimensions, cancellationToken);
            }

            return this.CallEmbeddingApiAsync(batch, cancellationToken);
        }

        private async Task<IReadOnlyList<ReadOnlyMemory<float>>> CallEmbeddingApiAsync(
            IReadOnlyList<string> batch,
            CancellationToken cancellationToken)
        {
            // Create a new options instance per call — the Azure SDK may mutate internal
            // fields during serialization when called concurrently.
            EmbeddingGenerationOptions options = new EmbeddingGenerationOptions
            {
                Dimensions = this.dimensions,
            };

            ClientResult<OpenAIEmbeddingCollection> result;
            try
            {
                result = await this.embeddingClient.GenerateEmbeddingsAsync(batch, options, cancellationToken);
            }
            catch (RequestFailedException ex)
            {
                throw this.WrapRequestFailedException(ex);
            }

            OpenAIEmbeddingCollection collection = result.Value;
            if (collection == null || collection.Count != batch.Count)
            {
                int received = collection?.Count ?? 0;
                throw new CosmosException(
                    $"Azure OpenAI embedding call to '{this.endpoint}' (deployment '{this.deploymentName}') " +
                    $"returned {received} embedding(s) for {batch.Count} input(s). " +
                    "Expected a 1:1 correspondence.",
                    HttpStatusCode.ServiceUnavailable,
                    subStatusCode: 0,
                    activityId: string.Empty,
                    requestCharge: 0);
            }

            List<ReadOnlyMemory<float>> vectors = new List<ReadOnlyMemory<float>>(collection.Count);
            foreach (OpenAIEmbedding embedding in collection)
            {
                vectors.Add(embedding.ToFloats());
            }

            return vectors;
        }

        private CosmosException WrapRequestFailedException(RequestFailedException ex)
        {
            return new CosmosException(
                $"Azure OpenAI embedding call to '{this.endpoint}' (deployment '{this.deploymentName}') " +
                $"failed with status {ex.Status}: {ex.Message}",
                HttpStatusCode.ServiceUnavailable,
                subStatusCode: ex.Status,
                activityId: string.Empty,
                requestCharge: 0);
        }

        private void ValidateInputs(List<string> inputs)
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(inputs[i]))
                {
                    throw new ArgumentException(
                        $"Input at index {i} is null, empty, or whitespace. " +
                        "Azure OpenAI embeddings require non-empty text inputs.",
                        paramName: "text");
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(AzureOpenAIEmbeddingGenerator));
            }
        }
    }
}

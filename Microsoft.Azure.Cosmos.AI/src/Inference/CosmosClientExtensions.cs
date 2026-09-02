//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.AI.Inference;

using System.Runtime.CompilerServices;

/// <summary>
/// Extension methods for the <see cref="CosmosClient"/> class related to Semantic Inference.
/// </summary>
public static class CosmosClientExtensions
{
    private static readonly ConditionalWeakTable<CosmosClient, InferenceService> inferenceServices = new ConditionalWeakTable<CosmosClient, InferenceService>();
    private static readonly ConditionalWeakTable<CosmosClient, InferenceConfigData> inferenceConfigData = new ConditionalWeakTable<CosmosClient, InferenceConfigData>();
    
    /// <summary>
    /// Rerank a list of documents using semantic reranking.
    /// This method uses a semantic reranker to score and reorder the provided documents
    /// based on their relevance to the given reranking context.
    /// 
    /// The sematic reranking requests will not use the regular request flow and not use the default SDK retry policies.
    /// </summary>
    /// <param name="client"> The <see cref="CosmosClient"/> instance to use for the reranking operation.</param>
    /// <param name="rerankContext"> The context (ex: query string) to use for reranking the documents.</param>
    /// <param name="documents"> A list of documents to be reranked</param>
    /// <param name="options"> (Optional) The options for the semantic reranking request.</param>
    /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns> The reranking results, typically including the reranked documents and their scores. </returns>
    public static async Task<SemanticRerankResult> SemanticRerankAsync(
        this CosmosClient client,
        string rerankContext,
        IEnumerable<string> documents,
        IDictionary<string, object> options = null,
        CancellationToken cancellationToken = default)
    {
        if (!inferenceConfigData.TryGetValue(client, out InferenceConfigData configData))
        {
            throw new InvalidOperationException($"Semantic reranking is not enabled for this CosmosClient. Please call {nameof(EnableSemanticReranking)}() first.");
        }

        InferenceService inferenceService = inferenceServices.GetValue(client, c => new InferenceService(c, configData.InferenceEndpoint, configData.Options));
        return await inferenceService.SemanticRerankAsync(rerankContext, documents, options, cancellationToken);
    }


    /// <summary>
    /// Enables semantic reranking for the specified <see cref="CosmosClient"/> instance.
    /// This method configures the client to use the specified inference endpoint and options for semantic reranking operations.
    /// </summary>
    /// <param name="cosmosClient">The <see cref="CosmosClient"/> instance to enable semantic reranking for.</param>
    /// <param name="inferenceEndpoint">The inference endpoint URL to be used for semantic reranking.</param>
    /// <param name="options">The options for configuring semantic reranking operations.</param>
    public static void EnableSemanticReranking(this CosmosClient cosmosClient, string inferenceEndpoint, SemanticRankingOptions options)
    {
        inferenceConfigData.Add(cosmosClient, new InferenceConfigData
        {
            InferenceEndpoint = inferenceEndpoint,
            Options = options
        });
    }

    private class InferenceConfigData 
    {
        public string InferenceEndpoint { get; set; }
        public SemanticRankingOptions Options { get; set; }
    }
}

/// <summary>
/// Represents the options for configuring semantic reranking operations in the Cosmos DB Inference Service.
/// </summary>
public class SemanticRankingOptions
{

    internal static readonly TimeSpan DefaultInferenceRequestTimeout = TimeSpan.FromSeconds(5);
    /// <summary>
    /// Gets or sets the request timeout for inference service operations (e.g., semantic reranking).
    /// This is a single-attempt timeout with no retries; if the request does not complete
    /// within the specified duration, a <see cref="CosmosException"/> with status 408 (Request Timeout) is thrown.
    /// </summary>
    /// <value>Default value is 5 seconds.</value>
    public TimeSpan RequestTimeout { get; set; } = DefaultInferenceRequestTimeout;

    /// <summary>
    /// Gets or sets the maximum number of concurrent connections to the inference service.
    /// </summary>
    public int MaxConnectionLimit { get; set; } = 50;
}

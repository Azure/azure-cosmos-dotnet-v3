//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the result of a semantic reranking operation, including rerank scores,
    /// latency, token usage, and HTTP response headers.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif

    class SemanticRerankResult
    {
        /// <summary>
        /// Gets the HTTP response headers associated with the rerank operation.
        /// </summary>
        public HttpResponseHeaders Headers { get; }

        /// <summary>
        /// Gets the list of rerank scores for the documents.
        /// </summary>
        public IReadOnlyList<RerankScore> RerankScores { get; }

        /// <summary>
        /// Gets the latency information for the rerank operation.
        /// </summary>
        public Dictionary<string, object> Latency { get; }

        /// <summary>
        /// Gets the token usage information for the rerank operation.
        /// </summary>
        public Dictionary<string, object> TokenUseage { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SemanticRerankResult"/> class.
        /// </summary>
        /// <param name="rerankScores">The list of rerank scores.</param>
        /// <param name="latency">The latency information.</param>
        /// <param name="tokenUseage">The token usage information.</param>
        /// <param name="headers">The HTTP response headers.</param>
        private SemanticRerankResult(
            IReadOnlyList<RerankScore> rerankScores,
            Dictionary<string, object> latency,
            Dictionary<string, object> tokenUseage,
            HttpResponseHeaders headers)
        {
            this.RerankScores = rerankScores;
            this.Latency = latency;
            this.TokenUseage = tokenUseage;
            this.Headers = headers;
        }

        /// <summary>
        /// Deserializes a <see cref="SemanticRerankResult"/> from an HTTP response message asynchronously.
        /// </summary>
        /// <param name="responseMessage">The HTTP response message containing the rerank result.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the deserialized <see cref="SemanticRerankResult"/>.</returns>
        internal static async Task<SemanticRerankResult> DeserializeSemanticRerankResultAsync(HttpResponseMessage responseMessage)
        {
            Stream content = await responseMessage.Content.ReadAsStreamAsync();

            using (content)
            {
                using (JsonDocument doc = await JsonDocument.ParseAsync(content))
                {
                    JsonElement root = doc.RootElement;

                    // Parse Scores
                    List<RerankScore> rerankScores = new List<RerankScore>();
                    if (root.TryGetProperty("Scores", out JsonElement scoresElement) && scoresElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in scoresElement.EnumerateArray())
                        {
                            object document = null;
                            if (item.TryGetProperty("document", out JsonElement docElement))
                            {
                                // Try to deserialize as an object
                                switch (docElement.ValueKind)
                                {
                                    case JsonValueKind.Object:
                                        document = JsonSerializer.Deserialize<Dictionary<string, object>>(docElement.GetRawText());
                                        break;
                                    case JsonValueKind.Null:
                                        document = null;
                                        break;
                                }
                            }

                            double score = item.TryGetProperty("score", out JsonElement scoreElement) && scoreElement.TryGetDouble(out double s) ? s : 0.0;
                            int index = item.TryGetProperty("index", out JsonElement indexElement) && indexElement.TryGetInt32(out int i) ? i : -1;

                            rerankScores.Add(new RerankScore(document, score, index));
                        }
                    }

                    // Parse latency
                    Dictionary<string, object> latency = null;
                    if (root.TryGetProperty("latency", out JsonElement latencyElement) && latencyElement.ValueKind == JsonValueKind.Object)
                    {
                        latency = JsonSerializer.Deserialize<Dictionary<string, object>>(latencyElement.GetRawText());
                    }

                    // Parse token_usage
                    Dictionary<string, object> tokenUsage = null;
                    if (root.TryGetProperty("token_usage", out JsonElement tokenUsageElement) && tokenUsageElement.ValueKind == JsonValueKind.Object)
                    {
                        tokenUsage = JsonSerializer.Deserialize<Dictionary<string, object>>(tokenUsageElement.GetRawText());
                    }

                    return new SemanticRerankResult(
                        rerankScores,
                        latency,
                        tokenUsage,
                        responseMessage.Headers);
                }
            }
        }
    }
}

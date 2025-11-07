//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
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

            // Deserialize the JSON content into a dictionary.
            Dictionary<string, object> responseJson;
            using (StreamReader streamReader = new StreamReader(content))
            using (Newtonsoft.Json.JsonTextReader jsonReader = new Newtonsoft.Json.JsonTextReader(streamReader))
            {
                Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                responseJson = serializer.Deserialize<Dictionary<string, object>>(jsonReader);
            }

            // Parse the rerank scores, latency, and token usage from the response.
            return new SemanticRerankResult(
                ParseRerankScores(responseJson["Scores"]),
                responseJson.ContainsKey("latency") ? Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(responseJson["latency"].ToString()) : null,
                responseJson.ContainsKey("token_usage") ? Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(responseJson["token_usage"].ToString()) : null,
                responseMessage.Headers);
        }

        /// <summary>
        /// Parses the rerank scores from the provided object.
        /// </summary>
        /// <param name="rerankScoresObj">The object containing rerank scores, expected to be a JArray.</param>
        /// <returns>A read-only list of <see cref="RerankScore"/> objects.</returns>
        private static IReadOnlyList<RerankScore> ParseRerankScores(object rerankScoresObj)
        {
            List<RerankScore> rerankScores = new List<RerankScore>();
            if (rerankScoresObj is Newtonsoft.Json.Linq.JArray rerankScoresArray)
            {
                foreach (Newtonsoft.Json.Linq.JToken item in rerankScoresArray)
                {
                    // Extract document, score, and index from each item.
                    object document = item["document"];
                    double score = item["score"] != null ? item.Value<double>("score") : 0.0;
                    int index = item["index"] != null ? item.Value<int>("index") : -1;
                    RerankScore rerankScore = new RerankScore(document, score, index);
                    rerankScores.Add(rerankScore);
                }
            }
            return rerankScores;
        }
    }
}

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.QueryAdvisor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Newtonsoft.Json;

    /// <summary>
    /// Query advice in the Azure Cosmos database service.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    sealed class QueryAdvice
    {
        /// <summary>
        /// Initializes a new instance of the Query Advice class.
        /// </summary>
        /// <param name="queryAdviceEntries">the array of query advice entries</param>
        [JsonConstructor]
        public QueryAdvice(IReadOnlyList<QueryAdviceEntry> queryAdviceEntries)
        {
            this.QueryAdviceEntries = (queryAdviceEntries ?? Enumerable.Empty<QueryAdviceEntry>()).Where(item => item != null).ToList();
        }

        public IReadOnlyList<QueryAdviceEntry> QueryAdviceEntries { get; }

        public static bool TryCreateFromString(string responseHeader, out QueryAdvice result)
        {
            if (responseHeader == null)
            {
                result = null;
                return false;
            }

            try
            {
                // Decode and deserialize the response string
                string decodedString = System.Web.HttpUtility.UrlDecode(responseHeader, Encoding.UTF8);

                List<QueryAdviceEntry> queryAdviceEntries = JsonConvert.DeserializeObject<List<QueryAdviceEntry>>(decodedString, new JsonSerializerSettings()
                {
                    // Allowing null values to be resilient to Json structure change
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,

                    // Ignore parsing error encountered in deserialization
                    Error = (sender, parsingErrorEvent) => parsingErrorEvent.ErrorContext.Handled = true
                }) ?? null;

                result = new QueryAdvice(queryAdviceEntries);
                return true;
            }
            catch (JsonException)
            {
                result = null;
                return false;
            }
        }

        public override string ToString()
        {
            if (this.QueryAdviceEntries == null)
            {
                return null;
            }

            StringBuilder stringBuilder = new StringBuilder();
            foreach (QueryAdviceEntry entry in this.QueryAdviceEntries)
            {
                stringBuilder.AppendLine(entry.ToString());
            }

            return stringBuilder.ToString();
        }
    }
}

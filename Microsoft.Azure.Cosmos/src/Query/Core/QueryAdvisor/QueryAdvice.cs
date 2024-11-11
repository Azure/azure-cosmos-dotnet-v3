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
        [JsonConstructor]
        public QueryAdvice(IReadOnlyList<SingleQueryAdvice> queryAdvices)
        {
            this.QueryAdvices = (queryAdvices ?? Enumerable.Empty<SingleQueryAdvice>()).Where(item => item != null).ToList();
        }

        public IReadOnlyList<SingleQueryAdvice> QueryAdvices { get; }

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

                List<SingleQueryAdvice> queryAdvices = JsonConvert.DeserializeObject<List<SingleQueryAdvice>>(decodedString, new JsonSerializerSettings()
                {
                    // Allowing null values to be resilient to Json structure change
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,

                    // Ignore parsing error encountered in deserialization
                    //Error = (sender, parsingErrorEvent) => parsingErrorEvent.ErrorContext.Handled = true
                }) ?? null;

                result = new QueryAdvice(queryAdvices);
                return true;
            }
            catch (JsonException)
            {
                result = null;
                return false;
            }
        }

        public static string ToString(QueryAdvice advice)
        {
            if (advice == null)
            {
                return String.Empty;
            }

            string result = String.Empty;
            foreach (SingleQueryAdvice singleQueryAdvice in advice.QueryAdvices)
            {
                string singleQueryAdviceString = SingleQueryAdvice.ToString(singleQueryAdvice);
                result += singleQueryAdviceString + "\n";
            }

            return result;
        }
    }
}

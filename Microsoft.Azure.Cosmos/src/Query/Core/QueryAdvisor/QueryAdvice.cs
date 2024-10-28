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
        /// <param name="id">The rule id</param>
        /// <param name="parameters">The parameters associated with the rule id</param>
        [JsonConstructor]
        public QueryAdvice(
             string id,
             IReadOnlyList<string> parameters)
        {
            this.Id = id;
            this.Parameters = parameters;
        }

        [JsonPropertyAttribute("Id")]
        public string Id { get; }

        [JsonPropertyAttribute("Params")]
        public IReadOnlyList<string> Parameters { get; }

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

                result = JsonConvert.DeserializeObject<QueryAdvice>(decodedString, new JsonSerializerSettings()
                {
                    // Allowing null values to be resilient to Json structure change
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,

                    // Ignore parsing error encountered in deserialization
                    Error = (sender, parsingErrorEvent) => parsingErrorEvent.ErrorContext.Handled = true
                }) ?? null;

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
                return string.Empty;
            }

            // Load the rule document
            XDocument ruleDocument = XDocument.Load("QueryAdviceRuleDocumentation.xml");

            // Find the corresponding rule in the document
            XElement rule = ruleDocument.Descendants("Rule")
                .Where(r => r.Attribute("Id").Value == advice.Id)
                .FirstOrDefault();

            if (rule == null)
            {
                return string.Empty;
            }

            // Format message with parameters if available
            string message = rule.Element("Message").Value;
            if (advice.Parameters == null || advice.Parameters.Count == 0)
            {
                return message;
            }
            else
            {
                return string.Format(message, advice.Parameters.ToArray());
            }
        }
    }
}

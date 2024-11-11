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
    class SingleQueryAdvice
    {
        /// <summary>
        /// Initializes a new instance of the Query Advice class. 
        /// </summary>
        /// <param name="id">The rule id</param>
        /// <param name="parameters">The parameters associated with the rule id</param>
        [JsonConstructor]
        public SingleQueryAdvice(
             string id,
             IReadOnlyList<string> parameters)
        {
            this.Id = id;
            this.Parameters = parameters;
        }

        [JsonProperty("Id")]
        public string Id { get; }

        [JsonProperty("Params")]
        public IReadOnlyList<string> Parameters { get; }

        public static string ToString(SingleQueryAdvice advice)
        {
            if (advice == null)
            {
                return string.Empty;
            }

            // Load the rule document
            XDocument ruleDocument = XDocument.Load("Query\\Core\\QueryAdvisor\\QueryAdviceRuleDocumentation.xml");

            // Find the corresponding rule in the document
            XElement rule = ruleDocument.Descendants("Rule")
                .Where(r => r.Attribute("Id").Value == advice.Id)
                .FirstOrDefault();

            if (rule == null)
            {
                return string.Empty;
            }

            // Generate the help link
            string helpLink = " For more information, please visit " + ruleDocument.Descendants("UrlPrefix").First().Value + advice.Id;

            // Format message with parameters if available
            string message = advice.Id + ": " + rule.Element("Message").Value.Replace("[CDATA[\"", String.Empty).Replace("\"]]", String.Empty); 
            if (advice.Parameters == null || advice.Parameters.Count == 0)
            {
                return message + helpLink;
            }
            else
            {
                return string.Format(message, advice.Parameters.ToArray()) + helpLink;
            }
        }
    }
}

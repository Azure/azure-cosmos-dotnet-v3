//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.QueryAdvisor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Xml.Linq;

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
    class QueryAdviceEntry
    {
        /// <summary>
        /// Initializes a new instance of the Query Advice class. 
        /// </summary>
        /// <param name="id">The rule id</param>
        /// <param name="parameters">The parameters associated with the rule id</param>
        [JsonConstructor]
        public QueryAdviceEntry(
             string id,
             IReadOnlyList<string> parameters)
        {
            this.Id = id;
            this.Parameters = parameters;
        }

        [JsonPropertyName("Id")]
        public string Id { get; }

        [JsonPropertyName("Params")]
        public IReadOnlyList<string> Parameters { get; }

        public override string ToString()
        {
            if (this.Id == null)
            {
                return null;
            }

            // Load the rule document
            XDocument ruleDocument = XDocument.Load("Query\\Core\\QueryAdvisor\\QueryAdviceRuleDocumentation.xml");

            // Find the corresponding rule in the document
            XElement rule = ruleDocument.Descendants("Rule")
                .Where(r => r.Attribute("Id").Value == this.Id)
                .FirstOrDefault();

            if (rule == null)
            {
                return null;
            }

            StringBuilder stringBuilder = new StringBuilder();

            // Message format is as follow
            // <id>>: <message>. For more information, please visit <urlprefix><id>

            stringBuilder.Append(this.Id);
            stringBuilder.Append(": ");

            string message = rule.Element("Message").Value.Replace("[CDATA[\"", String.Empty).Replace("\"]]", String.Empty); // removing the CDATA tags

            // Format message with parameters if available
            if (this.Parameters == null || this.Parameters.Count == 0)
            {
                stringBuilder.Append(message);
            }
            else
            {
                stringBuilder.AppendFormat(message, this.Parameters);
            }

            // Generate the help link
            stringBuilder.Append(" For more information, please visit ");
            stringBuilder.Append(ruleDocument.Descendants("UrlPrefix").First().Value);
            stringBuilder.Append(this.Id);

            return stringBuilder.ToString();
        }
    }
}

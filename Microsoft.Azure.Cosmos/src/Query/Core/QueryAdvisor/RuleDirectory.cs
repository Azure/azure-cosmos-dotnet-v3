//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.QueryAdvisor
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Xml.Linq;

    internal class RuleDirectory
    {
        // the Lazy default constructor is thread-safe so no need for locking
        private static readonly Lazy<RuleDirectory> Singleton = new Lazy<RuleDirectory>(() => new RuleDirectory());

        public static RuleDirectory Instance => Singleton.Value;

        private readonly XDocument RuleDocument;

        public readonly string UrlMessagePrefix;

        private RuleDirectory()
        {
            // Load the rule document
            this.RuleDocument = XDocument.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Azure.Cosmos.Query.Core.QueryAdvisor.QueryAdviceRuleDocumentation.xml"));
            this.UrlMessagePrefix = " For more information, please visit " + this.RuleDocument.Descendants("UrlPrefix").First().Value;
        }

        public string GetRuleMessage(string ruleId)
        {
            // Find the corresponding rule in the document
            XElement rule = this.RuleDocument.Descendants("Rule")
                .Where(r => r.Attribute("Id").Value == ruleId)
                .FirstOrDefault();

            if (rule == null)
            {
                return null;
            }

            return rule.Element("Message")?.Value.Replace("[CDATA[\"", String.Empty).Replace("\"]]", String.Empty); // removing the CDATA tags
        }
    }
}

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class IdentifierSqlParserBaselineTests : SqlParserBaselineTests
    {
        [TestMethod]
        public void Unicode()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", identifier: "a"),
                CreateInput(description: "Basic Capitalized", identifier: "A"),
                CreateInput(description: "Underscore In Front", identifier: "_a"),
                CreateInput(description: "Number then Letter", identifier: "_12a"),
                CreateInput(description: "Letter then Number", identifier: "ab12"),
                CreateInput(description: "Number then Letter then Number", identifier: "_12ab34"),
                CreateInput(description: "Letter then Number then Letter", identifier: "ab12cd"),

                // Negative
                CreateInput(description: "Number In Front", identifier: "12a"),
                CreateInput(description: "Special Character", identifier: "ab-cd"),
                CreateInput(description: "Special Character 2", identifier: "ab:cd"),
                CreateInput(description: "Special Character 3", identifier: "ab{cd}"),
                CreateInput(description: "Special Character 4", identifier: "ab(cd)"),
            };

            this.ExecuteTestSuite(inputs);
        }

        public static SqlParserBaselineTestInput CreateInput(string description, string identifier)
        {
            return new SqlParserBaselineTestInput(description, $"SELECT c.{identifier} FROM c");
        }
    }
}
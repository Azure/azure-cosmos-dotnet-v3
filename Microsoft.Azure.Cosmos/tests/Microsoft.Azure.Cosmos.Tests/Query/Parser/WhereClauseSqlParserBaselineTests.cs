//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class WhereClauseSqlParserBaselineTests : SqlParserBaselineTests
    {
        [TestMethod]
        public void Tests()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", whereClause: "WHERE true"),
                CreateInput(description: "Case Insensitive", whereClause: "WhErE true"),

                // Negative
                CreateInput(description: "wrong keyword", whereClause: "WHEE true"),
                CreateInput(description: "Invalid extra stuff", whereClause: "WHERE true, true"),
            };

            this.ExecuteTestSuite(inputs);
        }

        public static SqlParserBaselineTestInput CreateInput(string description, string whereClause)
        {
            return new SqlParserBaselineTestInput(description, $"SELECT * {whereClause}");
        }
    }
}
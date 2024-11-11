//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class FromClauseSqlParserBaselineTests : SqlParserBaselineTests
    {
        private static readonly string baseInputPathExpression = "c";
        private static readonly string recursiveInputPathExpression = "c.age";
        private static readonly string numberPathExpression = "c.arr[5]";
        private static readonly string stringPathExpression = "c.blah['asdf']";
        private static readonly string[] pathExpressions = new string[]
        {
            baseInputPathExpression,
            recursiveInputPathExpression,
            numberPathExpression,
            stringPathExpression,
        };

        private static readonly string[] inputPathCollections = pathExpressions;
        private static readonly string subqueryCollection = "(SELECT * FROM c)";
        private static readonly string[] collections = new string[]
        {
            subqueryCollection,
        }.Concat(inputPathCollections).ToArray();

        [TestMethod]
        public void AliasedCollection()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>();
            foreach (string collection in collections)
            {
                foreach (bool useAlias in new bool[] { false, true })
                {
                    inputs.Add(CreateInput(
                        description: $"collection: {collection} + useAlias with AS: {useAlias}",
                        fromClause: $"FROM {collection} {(useAlias ? "AS asdf" : string.Empty)}"));

                    inputs.Add(CreateInput(
                        description: $"collection: {collection} + useAlias without AS: {useAlias}",
                        fromClause: $"FROM {collection} {(useAlias ? " asdf" : string.Empty)}"));
                }
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void ArrayIteratorCollection()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>();
            foreach (string collection in collections)
            {
                inputs.Add(CreateInput(
                    description: $"collection: {collection}",
                    fromClause: $"FROM item IN {collection}"));
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void JoinCollection()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>
            {
                CreateInput(
                    description: $"Basic",
                    fromClause: $"FROM c JOIN d in c.children")
            };

            this.ExecuteTestSuite(inputs);
        }

        public static SqlParserBaselineTestInput CreateInput(string description, string fromClause)
        {
            return new SqlParserBaselineTestInput(description, $"SELECT * {fromClause}");
        }
    }
}
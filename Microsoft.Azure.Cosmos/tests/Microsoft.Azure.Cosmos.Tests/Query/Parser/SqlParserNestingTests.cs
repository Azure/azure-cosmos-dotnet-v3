//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class SqlParserNestingTests
    {
        [TestMethod]
        [DataRow("(", "1", ")")]
        [DataRow("[", "1", "]")]
        [DataRow("-", "a", "")]
        [DataRow("NOT ", "a", "")]
        [DataRow("(SELECT VALUE ", "1", ")")]
        public void DeepNestingFailsWithoutOverflowingTheStack(string open, string inner, string close)
        {
            // A stack overflow would terminate the test host, the parser must report a failure instead.
            string query = "SELECT VALUE " + string.Concat(Enumerable.Repeat(open, 100_000)) + inner + string.Concat(Enumerable.Repeat(close, 100_000));
            TryCatch<SqlQuery> result = default;
            Thread thread = new Thread(() => result = SqlQueryParser.Monadic.Parse(query), maxStackSize: 1024 * 1024);
            thread.Start();
            thread.Join();

            Assert.IsTrue(result.Failed);
            Assert.IsInstanceOfType(result.Exception.InnerException ?? result.Exception, typeof(ParseException));
        }

        [TestMethod]
        public void TooDeepNestingIsReported()
        {
            string query = "SELECT VALUE " + new string('(', 100_000) + "1" + new string(')', 100_000);
            TryCatch<SqlQuery> result = SqlQueryParser.Monadic.Parse(query);

            Assert.IsTrue(result.Failed);
            StringAssert.Contains((result.Exception.InnerException ?? result.Exception).Message, "or is nested too deeply");
        }

        [TestMethod]
        public void SyntaxErrorDoesNotMentionNesting()
        {
            TryCatch<SqlQuery> result = SqlQueryParser.Monadic.Parse("SELECT (1 FROM c");

            Assert.IsTrue(result.Failed);
            Assert.IsFalse((result.Exception.InnerException ?? result.Exception).Message.Contains("nested"));
        }

        // These chains are read in loops, so they parse at any length and the tree is as deep as the chain is long.
        [TestMethod]
        [DataRow(" + 1")]
        [DataRow(" * 1")]
        [DataRow(" AND a")]
        [DataRow(" OR a")]
        [DataRow(" ?? a")]
        [DataRow(" ? a : a")]
        [DataRow(".a")]
        [DataRow("[0]")]
        public void LongChainsCanBeInspectedWithoutOverflowingTheStack(string link)
        {
            // A stack overflow would terminate the test host, the detector must report a failure instead.
            string query = "SELECT VALUE a" + string.Concat(Enumerable.Repeat(link, 200_000));
            TryCatch<SqlQuery> result = default;
            bool inspected = true;
            Thread thread = new Thread(
                () =>
                {
                    result = SqlQueryParser.Monadic.Parse(query);
                    if (result.Succeeded)
                    {
                        inspected = CosmosQueryExecutionContextFactory.AggregateProjectionDetector.TryHasAggregate(
                            result.Result.SelectClause.SelectSpec,
                            out _);
                    }
                },
                maxStackSize: 1024 * 1024);
            thread.Start();
            thread.Join();

            Assert.IsTrue(result.Succeeded);
            Assert.IsFalse(inspected);
        }

        [TestMethod]
        public void ModerateChainCanBeInspected()
        {
            SqlQuery query = SqlQueryParser.Parse("SELECT VALUE 1" + string.Concat(Enumerable.Repeat(" + 1", 1_000)));

            Assert.IsTrue(CosmosQueryExecutionContextFactory.AggregateProjectionDetector.TryHasAggregate(query.SelectClause.SelectSpec, out bool hasAggregate));
            Assert.IsFalse(hasAggregate);
        }

        [TestMethod]
        public void ModerateNestingSucceeds()
        {
            string query = "SELECT VALUE " + new string('(', 200) + "1" + new string(')', 200);
            TryCatch<SqlQuery> result = default;
            Thread thread = new Thread(() => result = SqlQueryParser.Monadic.Parse(query), maxStackSize: 1024 * 1024);
            thread.Start();
            thread.Join();

            Assert.IsTrue(result.Succeeded);
        }
    }
}

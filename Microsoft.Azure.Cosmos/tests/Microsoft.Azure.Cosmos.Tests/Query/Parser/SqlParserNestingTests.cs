//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System;
    using System.Linq;
    using System.Threading;
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
            Exception exception = result.Exception;
            while (exception is not ParseException && exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            Assert.IsInstanceOfType(exception, typeof(ParseException));
            Assert.AreEqual("The query is nested too deeply to be parsed.", exception.Message);
            Assert.IsInstanceOfType(exception.InnerException, typeof(InsufficientExecutionStackException));
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

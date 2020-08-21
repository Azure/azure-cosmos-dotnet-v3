//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System;
    using System.Xml;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public abstract class SqlParserBaselineTests : BaselineTests<SqlParserBaselineTestInput, SqlParserBaselineTestOutput>
    {
        public override SqlParserBaselineTestOutput ExecuteTest(SqlParserBaselineTestInput input)
        {
            TryCatch<SqlQuery> parseQueryMonad = SqlQueryParser.Monadic.Parse(input.Query);
            if (parseQueryMonad.Succeeded)
            {
                // Addtional round trip for extra validation
                TryCatch<SqlQuery> parseQueryMonad2 = SqlQueryParser.Monadic.Parse(parseQueryMonad.Result.ToString());
                Assert.IsTrue(parseQueryMonad2.Succeeded);
                Assert.AreEqual(parseQueryMonad.Result, parseQueryMonad2.Result);
            }

            return new SqlParserBaselineTestOutput(parseQueryMonad);
        }
    }

    public sealed class SqlParserBaselineTestInput : BaselineTestInput
    {
        public SqlParserBaselineTestInput(string description, string query)
            : base(description)
        {
            this.Query = query;
        }

        public string Query { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement($"{nameof(this.Description)}");
            xmlWriter.WriteCData(this.Description);
            xmlWriter.WriteEndElement();

            xmlWriter.WriteStartElement($"{nameof(this.Query)}");
            xmlWriter.WriteCData(this.Query);
            xmlWriter.WriteEndElement();
        }
    }

    public sealed class SqlParserBaselineTestOutput : BaselineTestOutput
    {
        internal SqlParserBaselineTestOutput(TryCatch<SqlQuery> parseQueryMonad)
        {
            this.ParseQueryMonad = parseQueryMonad;
        }

        internal TryCatch<SqlQuery> ParseQueryMonad { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            if (this.ParseQueryMonad.Succeeded)
            {
                xmlWriter.WriteStartElement($"ParsedQuery");
                xmlWriter.WriteCData(this.ParseQueryMonad.Result.ToString());
                xmlWriter.WriteEndElement();
            }
            else
            {
                Exception ex = this.ParseQueryMonad.Exception;
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                }

                xmlWriter.WriteStartElement($"Exception");
                xmlWriter.WriteCData(ex.Message);
                xmlWriter.WriteEndElement();
            }
        }
    }
}

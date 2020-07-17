//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System;
    using System.Xml;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.Azure.Cosmos.Query.Core.HandwrittenParser;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Generic;

    [TestClass]
    public class ParserBaslineTests : BaselineTests<ParserBaselineInput, ParserBaselineOutput>
    {
        [TestMethod]
        public void Tests()
        {
            List<ParserBaselineInput> inputs = new List<ParserBaselineInput>()
            {
                // new ParserBaselineInput(description: "Simple", "SELECT * FROM c WHERE c.name = 'John' GROUP BY c.age ORDER BY c.height OFFSET 5 LIMIT 10"),
                new ParserBaselineInput(description: "Simple", "SELECT * FROM c WHERE c.number IN (1, 2, 3)"),
                // Array Create Scalar Expressions
                CreateScalarExpressionInput(description: "Empty Array", "[]"),
                CreateScalarExpressionInput(description: "Single Item Array", "[1]"),
                CreateScalarExpressionInput(description: "Multi Item Array", "[1, 2, 3]"),
            };

            this.ExecuteTestSuite(inputs);
        }

        public override ParserBaselineOutput ExecuteTest(ParserBaselineInput input)
        {
            SqlQuery sqlQuery = Parser.Parse(input.Query.AsSpan());

            ParserBaselineOutput parserBaselineOutput;
            try
            {
                string parsedQuery = sqlQuery.ToString();
                parserBaselineOutput = new ParserPositiveOutput(parsedQuery);

                Assert.AreEqual(parsedQuery, Parser.Parse(parsedQuery.AsSpan()).ToString());
            }
            catch (Exception ex)
            {
                parserBaselineOutput = new ParserNegativeOutput(ex);
            }

            return parserBaselineOutput;
        }

        private static ParserBaselineInput CreateScalarExpressionInput(string description, string scalarExpression)
        {
            return new ParserBaselineInput(description, $"SELECT VALUE {scalarExpression}");
        }
    }

    public sealed class ParserBaselineInput : BaselineTestInput
    {
        public ParserBaselineInput(string description, string query)
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

    public abstract class ParserBaselineOutput : BaselineTestOutput
    {
    }

    public sealed class ParserPositiveOutput : ParserBaselineOutput
    {
        private readonly string parsedQuery;

        public ParserPositiveOutput(string parsedQuery)
        {
            this.parsedQuery = parsedQuery;
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement($"{nameof(this.parsedQuery)}");
            xmlWriter.WriteCData(this.parsedQuery);
            xmlWriter.WriteEndElement();
        }
    }

    public sealed class ParserNegativeOutput : ParserBaselineOutput
    {
        private readonly Exception exception;
        public ParserNegativeOutput(Exception ex)
        {
            this.exception = ex;
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement($"{nameof(this.exception)}");
            xmlWriter.WriteCData(this.exception.ToString());
            xmlWriter.WriteEndElement();
        }
    }
}

//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using System.Xml;
    using static LinqAggregateFunctionBaselineTests;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("Quarantine")]
    public class LinqAggregateFunctionBaselineTests : BaselineTests<LinqAggregateInput, LinqAggregateOutput>
    {
        private static DocumentClient client;
        private static Uri databaseUri;
        private static Func<bool, IQueryable<Data>> getQuery;
        private static Func<bool, IQueryable<Family>> getQueryFamily;
        private static IQueryable lastExecutedScalarQuery;

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            client = TestCommon.CreateClient(false, defaultConsistencyLevel: ConsistencyLevel.Session);

            // Set a callback to get the handle of the last executed query to do the verification
            // This is neede because aggregate queries return type is a scalar so it can't be used 
            // to verify the translated LINQ directly as other queries type.
            client.OnExecuteScalarQueryCallback = q => LinqAggregateFunctionBaselineTests.lastExecutedScalarQuery = q;

            string databaseName = $"{nameof(LinqAggregateFunctionBaselineTests)}-{Guid.NewGuid().ToString("N")}";
            databaseUri = UriFactory.CreateDatabaseUri(databaseName);
            CosmosDatabaseSettings testDb = client.CreateDatabaseAsync(new CosmosDatabaseSettings() { Id = databaseName }).Result;

            CosmosContainerSettings collection;
            getQuery = LinqTestsCommon.GenerateSimpleData(client, testDb, out collection);
            getQueryFamily = LinqTestsCommon.GenerateFamilyData(client, testDb, out collection);
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            client.DeleteDatabaseAsync(databaseUri).Wait();
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestAggregateMax()
        {
            List<LinqAggregateInput> inputs = new List<LinqAggregateInput>();
            inputs.Add(new LinqAggregateInput("Max on doc", b => getQuery(b).Max()));
            inputs.Add(new LinqAggregateInput("Max w/ doc mapping", b => getQuery(b).Max(doc => doc)));
            inputs.Add(new LinqAggregateInput("Max w/ doc mapping to number", b => getQuery(b).Max(doc => doc.Number)));
            inputs.Add(new LinqAggregateInput("Filter true flag -> Max w/ doc mapping to number", b => getQuery(b).Where(doc => doc.Flag).Max(doc => doc.Number)));
            inputs.Add(new LinqAggregateInput("Filter false flag -> Max w/ doc mapping to number", b => getQuery(b).Where(doc => !doc.Flag).Max(doc => doc.Number)));
            inputs.Add(new LinqAggregateInput("Select number -> Max", b => getQuery(b).Select(doc => doc.Number).Max()));
            inputs.Add(new LinqAggregateInput("Select number -> Max w/ mapping", b => getQuery(b).Select(doc => doc.Number).Max(num => num)));
            inputs.Add(new LinqAggregateInput("Select many -> Filter -> Select -> Max", b => getQuery(b).SelectMany(doc => doc.Multiples.Where(m => m % 3 == 0).Select(m => m)).Max()));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestAggregateMin()
        {
            List<LinqAggregateInput> inputs = new List<LinqAggregateInput>();
            inputs.Add(new LinqAggregateInput("Min on doc", b => getQuery(b).Min()));
            inputs.Add(new LinqAggregateInput("Min w/ doc mapping", b => getQuery(b).Min(doc => doc)));
            inputs.Add(new LinqAggregateInput("Min w/ doc mapping to number", b => getQuery(b).Min(doc => doc.Number)));
            inputs.Add(new LinqAggregateInput("Filter true flag -> Min w/ doc mapping to number", b => getQuery(b).Where(doc => doc.Flag).Min(doc => doc.Number)));
            inputs.Add(new LinqAggregateInput("Filter false flag -> Min w/ doc mapping to number", b => getQuery(b).Where(doc => !doc.Flag).Min(doc => doc.Number)));
            inputs.Add(new LinqAggregateInput("Select number -> Min", b => getQuery(b).Select(doc => doc.Number).Min()));
            inputs.Add(new LinqAggregateInput("Select number -> Min w/ mapping", b => getQuery(b).Select(doc => doc.Number).Min(num => num)));
            inputs.Add(new LinqAggregateInput("Select many -> Filter -> Select -> Min", b => getQuery(b).SelectMany(doc => doc.Multiples.Where(m => m % 3 == 0).Select(m => m)).Min()));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestAggregateSum()
        {
            List<LinqAggregateInput> inputs = new List<LinqAggregateInput>();
            inputs.Add(new LinqAggregateInput("Sum number property", b => getQuery(b).Sum(doc => doc.Number)));
            inputs.Add(new LinqAggregateInput("Filter true flag -> Sum w/ mapping", b => getQuery(b).Where(doc => doc.Flag).Sum(doc => doc.Number)));
            inputs.Add(new LinqAggregateInput("Filter false flag -> Sum w/ mapping", b => getQuery(b).Where(doc => !doc.Flag).Sum(doc => doc.Number)));
            inputs.Add(new LinqAggregateInput("Select number -> Sum", b => getQuery(b).Select(doc => doc.Number).Sum()));
            inputs.Add(new LinqAggregateInput("Select number -> Sum w/ mapping", b => getQuery(b).Select(doc => doc.Number).Sum(num => num)));
            inputs.Add(new LinqAggregateInput("Select many -> Filter -> Select -> Sum", b => getQuery(b).SelectMany(doc => doc.Multiples.Where(m => m % 3 == 0).Select(m => m)).Sum()));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestAggregateCount()
        {
            List<LinqAggregateInput> inputs = new List<LinqAggregateInput>();
            inputs.Add(new LinqAggregateInput("Count", b => getQuery(b).Count()));
            inputs.Add(new LinqAggregateInput("Filter true flag -> Count", b => getQuery(b).Where(doc => doc.Flag).Count()));
            inputs.Add(new LinqAggregateInput("Filter false flag -> Count", b => getQuery(b).Where(doc => !doc.Flag).Count()));
            inputs.Add(new LinqAggregateInput("Select number -> Count", b => getQuery(b).Select(doc => doc.Number).Count()));
            inputs.Add(new LinqAggregateInput("Select many -> Filter -> Select -> Count", b => getQuery(b).SelectMany(doc => doc.Multiples.Where(m => m % 3 == 0).Select(m => m)).Count()));
            inputs.Add(new LinqAggregateInput("Count w/ boolean filter", b => getQuery(b).Count(doc => doc.Flag)));
            inputs.Add(new LinqAggregateInput("Count w/ operator filter", b => getQuery(b).Count(doc => doc.Number < -7)));
            inputs.Add(new LinqAggregateInput("Select number -> Count w/ operator filter", b => getQuery(b).Select(doc => doc.Number).Count(num => num < -13)));
            inputs.Add(new LinqAggregateInput("Select(Select) -> Count(Sum)", b => getQuery(b).Select(doc => doc.Multiples).Count(array => array.Sum() > 5)));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestAny()
        {
            List<LinqAggregateInput> inputs = new List<LinqAggregateInput>();

            // ----------------------
            // Any at top level
            // ----------------------

            inputs.Add(new LinqAggregateInput(
                "Any", b => getQuery(b)
                .Any(), 
                ErrorMessages.CrossPartitionQueriesOnlySupportValueAggregateFunc));

            inputs.Add(new LinqAggregateInput(
                "Filter true flag -> Any", b => getQuery(b)
                .Where(doc => doc.Flag).Any(), 
                ErrorMessages.CrossPartitionQueriesOnlySupportValueAggregateFunc));

            inputs.Add(new LinqAggregateInput(
                "Filter false flag -> Any", b => getQuery(b)
                .Where(doc => !doc.Flag).Any(), 
                ErrorMessages.CrossPartitionQueriesOnlySupportValueAggregateFunc));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Any", b => getQuery(b)
                .Select(doc => doc.Number).Any(), 
                ErrorMessages.CrossPartitionQueriesOnlySupportValueAggregateFunc));

            inputs.Add(new LinqAggregateInput(
                "Select many -> Filter -> Select -> Any", b => getQuery(b)
                .SelectMany(doc => doc.Multiples.Where(m => m % 3 == 0).Select(m => m)).Any(), 
                ErrorMessages.CrossPartitionQueriesOnlySupportValueAggregateFunc));

            inputs.Add(new LinqAggregateInput(
                "Any w/ boolean filter", b => getQuery(b)
                .Any(doc => doc.Flag), 
                ErrorMessages.CrossPartitionQueriesOnlySupportValueAggregateFunc));

            inputs.Add(new LinqAggregateInput(
                "Any w/ operator filter", b => getQuery(b)
                .Any(doc => doc.Number < -7), 
                ErrorMessages.CrossPartitionQueriesOnlySupportValueAggregateFunc));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Any w/ operator filter", b => getQuery(b)
                .Select(doc => doc.Number).Any(num => num < -13), 
                ErrorMessages.CrossPartitionQueriesOnlySupportValueAggregateFunc));

            inputs.Add(new LinqAggregateInput(
                "Select(Select) -> Any(Sum)", b => getQuery(b)
                .Select(doc => doc.Multiples).Any(array => array.Sum() > 5), 
                ErrorMessages.CrossPartitionQueriesOnlySupportValueAggregateFunc));

            inputs.Add(new LinqAggregateInput(
                "Select(Where) -> Any(Sum(map))", b => getQueryFamily(b)
                .Select(f => f.Children.Where(c => c.Pets.Count() > 0)).Any(children => children.Sum(c => c.Grade) > 150), 
                ErrorMessages.CrossPartitionQueriesOnlySupportValueAggregateFunc));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestAggregateAvg()
        {
            List<LinqAggregateInput> inputs = new List<LinqAggregateInput>();
            inputs.Add(new LinqAggregateInput("Avg number", b => getQuery(b).Average(doc => doc.Number)));
            inputs.Add(new LinqAggregateInput("Filter true flag -> Avg w/ mapping", b => getQuery(b).Where(doc => doc.Flag).Average(doc => doc.Number)));
            inputs.Add(new LinqAggregateInput("Filter false flag -> Avg w/ mapping", b => getQuery(b).Where(doc => !doc.Flag).Average(doc => doc.Number)));
            inputs.Add(new LinqAggregateInput("Select number -> Avg", b => getQuery(b).Select(doc => doc.Number).Average()));
            inputs.Add(new LinqAggregateInput("Select number -> Avg w/ mapping", b => getQuery(b).Select(doc => doc.Number).Average(num => num)));
            inputs.Add(new LinqAggregateInput("Select many -> Filter -> Select -> Avg", b => getQuery(b).SelectMany(doc => doc.Multiples.Where(m => m % 3 == 0).Select(m => m)).Average()));
            inputs.Add(new LinqAggregateInput("Select(Where) -> Avg(Sum(map))", b => getQueryFamily(b).Select(f => f.Children.Where(c => c.Grade > 80)).Average(children => children.Sum(c => c.Grade))));
            this.ExecuteTestSuite(inputs);
        }

        public override LinqAggregateOutput ExecuteTest(LinqAggregateInput input)
        {
            LinqAggregateFunctionBaselineTests.lastExecutedScalarQuery = null;
            var compiledQuery = input.expression.Compile();

            string errorMessage = null;
            bool failed = false;
            string query = string.Empty;
            try
            {
                object queryResult;
                try
                {
                    queryResult = compiledQuery(true);
                }
                finally
                {
                    query = JObject
                        .Parse(LinqAggregateFunctionBaselineTests.lastExecutedScalarQuery.ToString())
                        .GetValue("query", StringComparison.Ordinal)
                        .ToString();
                }
                
                Assert.IsNotNull(LinqAggregateFunctionBaselineTests.lastExecutedScalarQuery, "lastExecutedScalarQuery is not set");

                try
                {
                    var dataResult = compiledQuery(false);
                    Assert.AreEqual(dataResult, queryResult);
                }
                catch (ArgumentException)
                {
                    // Min and Max operations cannot be done on Document type
                    // In this case, the queryResult should be null
                    Assert.AreEqual(null, queryResult);
                }

                if (input.ErrorMessage != null)
                {
                    errorMessage = $"Expecting error message containing [[{input.ErrorMessage}]]. Actual: <no error>";
                    failed = true;
                }
            }
            catch (Exception e)
            {
                while (!(e is DocumentClientException) && e.InnerException != null)
                {
                    e = e.InnerException;
                }

                errorMessage = $"{e.GetType().Name}: {e.Message}";
                if ((input.ErrorMessage != null && !errorMessage.Contains(input.ErrorMessage)))
                {
                    errorMessage = $"Expecting error message containing [[{input.ErrorMessage}]]. Actual: {errorMessage}";
                    failed = true;
                }
                else if (input.ErrorMessage == null)
                {
                    failed = true;
                }
            }

            return new LinqAggregateOutput(query, errorMessage, failed);
        }

        public sealed class LinqAggregateOutput : BaselineTestOutput
        {
            public string SqlQuery { get; }

            public string ErrorMessage { get; }

            public bool Failed { get; }

            public LinqAggregateOutput(string sqlQuery, string errorMessage = null, bool failed = false)
            {
                this.SqlQuery = sqlQuery;
                this.ErrorMessage = errorMessage;
                this.Failed = failed;
            }

            public override void SerializeAsXML(XmlWriter xmlWriter)
            {
                xmlWriter.WriteStartElement(nameof(SqlQuery));
                xmlWriter.WriteCData(LinqTestOutput.FormatSql(this.SqlQuery));
                xmlWriter.WriteEndElement();
                if (this.ErrorMessage != null)
                {
                    xmlWriter.WriteStartElement(nameof(ErrorMessage));
                    xmlWriter.WriteCData(LinqTestOutput.FormatErrorMessage(this.ErrorMessage));
                    xmlWriter.WriteEndElement();
                }
                if (this.Failed)
                {
                    xmlWriter.WriteElementString(nameof(Failed), this.Failed.ToString());
                }
            }
        }

        public sealed class LinqAggregateInput : BaselineTestInput
        {
            internal Expression<Func<bool, object>> expression { get; }

            public string ErrorMessage { get; }

            internal LinqAggregateInput(string description, Expression<Func<bool, object>> expr, string errorMessage = null)
                : base(description)
            {
                if (expr == null)
                {
                    throw new ArgumentNullException($"{nameof(expr)} must not be null.");
                }

                this.expression = expr;
                this.ErrorMessage = errorMessage;
            }

            public override void SerializeAsXML(XmlWriter xmlWriter)
            {
                if (xmlWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(xmlWriter)} cannot be null.");
                }

                var expressionBody = expression.Body.ToString();
                var expressionString = new StringBuilder(expressionBody);
                expressionString.Remove(0, expressionBody.IndexOfNth('.', 2)).Insert(0, "query");

                xmlWriter.WriteStartElement("Description");
                xmlWriter.WriteCData(this.Description);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Expression");
                xmlWriter.WriteCData(expressionString.ToString());
                xmlWriter.WriteEndElement();
                if (this.ErrorMessage != null)
                {
                    xmlWriter.WriteStartElement(nameof(ErrorMessage));
                    xmlWriter.WriteCData(this.ErrorMessage);
                    xmlWriter.WriteEndElement();
                }
            }
        }
    }
}

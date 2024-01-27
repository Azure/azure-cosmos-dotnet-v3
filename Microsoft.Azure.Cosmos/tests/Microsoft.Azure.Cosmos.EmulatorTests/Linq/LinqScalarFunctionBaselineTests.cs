//-----------------------------------------------------------------------
// <copyright file="LinqScalarFunctionBaselineTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using System.Xml;
    using TestCommon = Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestCommon;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// LINQ tests for Non aggregate scalar functions such as FirstOrDefault
    /// </summary>
    [TestClass]
    public class LinqScalarFunctionBaselineTests : BaselineTests<LinqScalarFunctionInput, LinqScalarFunctionOutput>
    {
        private static CosmosClient client;
        private static Cosmos.Database testDb;
        private static Func<bool, IQueryable<Data>> getQuery;
        private static Func<bool, IQueryable<Family>> getQueryFamily;
        private static IQueryable lastExecutedScalarQuery;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            client = TestCommon.CreateCosmosClient(true);

            // Set a callback to get the handle of the last executed query to do the verification
            // This is neede because aggregate queries return type is a scalar so it can't be used 
            // to verify the translated LINQ directly as other queries type.
            client.DocumentClient.OnExecuteScalarQueryCallback = q => lastExecutedScalarQuery = q;

            string dbName = $"{nameof(LinqAggregateFunctionBaselineTests)}-{Guid.NewGuid().ToString("N")}";
            testDb = await client.CreateDatabaseAsync(dbName);

            getQuery = LinqTestsCommon.GenerateSimpleCosmosData(testDb, useRandomData: false);
            getQueryFamily = LinqTestsCommon.GenerateFamilyCosmosData(testDb, out _);
        }

        [TestMethod]
        [Owner("adityasa")]
        public void TestFirstOrDefault()
        {
            List<LinqScalarFunctionInput> inputs = new List<LinqScalarFunctionInput>();

            ///////////////////////////////////////////////////
            // Positive cases - With at least one result
            ///////////////////////////////////////////////////

            inputs.Add(new LinqScalarFunctionInput(
                "FirstOrDefault",
                b => getQuery(b)
                    .FirstOrDefault()));

            inputs.Add(new LinqScalarFunctionInput(
                "Select -> FirstOrDefault 1",
                b => getQuery(b)
                    .Select(data => data.Flag)
                    .FirstOrDefault()));

            inputs.Add(new LinqScalarFunctionInput(
                "Select -> FirstOrDefault 2",
                b => getQuery(b)
                    .Select(data => data.Multiples)
                    .FirstOrDefault()));

            inputs.Add(new LinqScalarFunctionInput(
                "Where -> FirstOrDefault 1",
                b => getQuery(b)
                    .Where(data => data.Id == "1")
                    .FirstOrDefault()));

            inputs.Add(new LinqScalarFunctionInput(
                "Where -> FirstOrDefault 2",
                b => getQuery(b)
                    .Where(data => data.Flag)
                    .FirstOrDefault()));

            inputs.Add(new LinqScalarFunctionInput(
                "Select -> Where -> FirstOrDefault",
                b => getQuery(b)
                    .Select(data => data.Flag)
                    .Where(flag => flag)
                    .FirstOrDefault()));

            inputs.Add(new LinqScalarFunctionInput(
                "OrderBy -> Select -> FirstOrDefault",
                b => getQuery(b)
                    .OrderBy(data => data.Id)
                    .Select(data => data.Flag)
                    .FirstOrDefault()));

            inputs.Add(new LinqScalarFunctionInput(
                "SelectMany -> FirstOrDefault",
                b => getQuery(b)
                    .SelectMany(data => data.Multiples)
                    .FirstOrDefault()));

            ///////////////////////////////////////////////////
            // Positive cases - With no results
            ///////////////////////////////////////////////////

            inputs.Add(new LinqScalarFunctionInput(
                "FirstOrDefault (default)",
                b => getQuery(b)
                    .Where(data => data.Flag && !data.Flag)
                    .FirstOrDefault()));

            /////////////////
            // Negative cases
            /////////////////

            // Unsupported
            inputs.Add(new LinqScalarFunctionInput(
                "Select (FirstOrDefault) -> Min 1",
                b => getQuery(b)
                    .Select(data => data.Multiples.FirstOrDefault())
                    .Min()));

            // Unsupported
            inputs.Add(new LinqScalarFunctionInput(
                "Select (FirstOrDefault) -> Min 2",
                b => getQuery(b)
                    .Select(data => new List<int> { 1, 2, 3 }.FirstOrDefault())
                    .Min()));

            // ERROR - A TOP cannot be used in the same query or subquery as an OFFSET.
            inputs.Add(new LinqScalarFunctionInput(
                "Select -> Skip -> Take -> FirstOrDefault",
                b => getQuery(b)
                    .Select(data => data)
                    .Skip(5)
                    .Take(5)
                    .FirstOrDefault()));

            this.ExecuteTestSuite(inputs);
        }

        public override LinqScalarFunctionOutput ExecuteTest(LinqScalarFunctionInput input)
        {
            lastExecutedScalarQuery = null;
            Func<bool, object> compiledQuery = input.Expression.Compile();

            string errorMessage = null;
            string query = string.Empty;
            object queryResult = null;
            try
            {
                try
                {
                    queryResult = compiledQuery(true);
                }
                finally
                {
                    Assert.IsNotNull(lastExecutedScalarQuery, "lastExecutedScalarQuery is not set");

                    query = JObject
                        .Parse(lastExecutedScalarQuery.ToString())
                        .GetValue("query", StringComparison.Ordinal)
                        .ToString();
                }

                try
                {
                    object dataResult = compiledQuery(false);
                    Assert.IsTrue(AreEqual(dataResult, queryResult));
                }
                catch (ArgumentException)
                {
                    // Min and Max operations cannot be done on Document type
                    // In this case, the queryResult should be null
                    Assert.AreEqual(null, queryResult);
                }
            }
            catch (Exception e)
            {
                errorMessage = LinqTestsCommon.BuildExceptionMessageForTest(e);
            }

            string serializedResults = JsonConvert.SerializeObject(
                queryResult,
                new JsonSerializerSettings { Formatting = Newtonsoft.Json.Formatting.Indented });

            return new LinqScalarFunctionOutput(query, errorMessage, serializedResults);
        }

        private static bool AreEqual(object obj1, object obj2)
        {
            bool equals = obj1 == obj2;
            if (equals)
            {
                return true;
            }

            if (obj1 is int[] intArray1 && obj2 is int[] intArray2)
            {
                return intArray1.SequenceEqual(intArray2);
            }

            return obj1.Equals(obj2);
        }
    }

    public sealed class LinqScalarFunctionOutput : BaselineTestOutput
    {
        public string SqlQuery { get; }

        public string ErrorMessage { get; }

        public string SerializedResults { get; }

        public LinqScalarFunctionOutput(string sqlQuery, string errorMessage, string serializedResults)
        {
            this.SqlQuery = sqlQuery;
            this.ErrorMessage = errorMessage;
            this.SerializedResults = serializedResults;
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(this.SqlQuery));
            xmlWriter.WriteCData(LinqTestOutput.FormatSql(this.SqlQuery));
            xmlWriter.WriteEndElement();
            if (this.ErrorMessage != null)
            {
                xmlWriter.WriteStartElement(nameof(this.ErrorMessage));
                xmlWriter.WriteCData(LinqTestOutput.FormatErrorMessage(this.ErrorMessage));
                xmlWriter.WriteEndElement();
            }

            if (this.SerializedResults != null)
            {
                xmlWriter.WriteStartElement(nameof(this.SerializedResults));
                xmlWriter.WriteCData(LinqTestOutput.FormatErrorMessage(this.SerializedResults));
                xmlWriter.WriteEndElement();
            }
        }
    }

    public sealed class LinqScalarFunctionInput : BaselineTestInput
    {
        internal LinqScalarFunctionInput(string description, Expression<Func<bool, object>> expression)
            : base(description)
        {
            if (expression == null)
            {
                throw new ArgumentNullException($"{nameof(expression)} must not be null.");
            }

            this.Expression = expression;
        }

        internal Expression<Func<bool, object>> Expression { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            if (xmlWriter == null)
            {
                throw new ArgumentNullException($"{nameof(xmlWriter)} cannot be null.");
            }

            string expressionString = LinqTestInput.FilterInputExpression(this.Expression.Body.ToString());

            xmlWriter.WriteStartElement("Description");
            xmlWriter.WriteCData(this.Description);
            xmlWriter.WriteEndElement();
            xmlWriter.WriteStartElement("Expression");
            xmlWriter.WriteCData(expressionString);
            xmlWriter.WriteEndElement();
        }
    }
}

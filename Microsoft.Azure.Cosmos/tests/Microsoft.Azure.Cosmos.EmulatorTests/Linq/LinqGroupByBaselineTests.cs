//-----------------------------------------------------------------------
// <copyright file="LinqGroupByBaselineTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Xml;
    using static LinqGroupByBaselineTests;

    using BaselineTest;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Documents;
    using System.Threading.Tasks;
    using System.Net;
    using Newtonsoft.Json;

    using Microsoft.Azure.Cosmos.EmulatorTests.FeedRanges;
    using Microsoft.Azure.Cosmos.SqlObjects;

    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public class LinqGroupByBaselineTests : BaselineTests<LinqGroupByInput, LinqGroupByOutput>
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
            // This is needed because aggregate queries return type is a scalar so it can't be used 
            // to verify the translated LINQ directly as other queries type.
            client.DocumentClient.OnExecuteScalarQueryCallback = q => lastExecutedScalarQuery = q;

            string dbName = $"{nameof(LinqGroupByBaselineTests)}-{Guid.NewGuid():N}";
            testDb = await client.CreateDatabaseAsync(dbName);

            getQuery = LinqTestsCommon.GenerateSimpleCosmosData(testDb);
            getQueryFamily = LinqTestsCommon.GenerateFamilyCosmosData(testDb, out _);
        }

        [ClassCleanup]
        public async static Task CleanUp()
        {
            if (testDb != null)
            {
                await testDb.DeleteStreamAsync();
            }

            client?.Dispose();
        }

        [TestMethod]
        public void TestGroupByTranslation()
        {
            List<LinqGroupByInput> inputs = new List<LinqGroupByInput>();
            inputs.Add(new LinqGroupByInput("GroupBy Single Value Select Key", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => key /*return the group by key */)));
            inputs.Add(new LinqGroupByInput("GroupBy Single Value Select Key Alias", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (stringField, values) => stringField /*return the group by key */)));
            inputs.Add(new LinqGroupByInput("GroupBy Single Value With Min", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => values.Min() /*return the Min of each group */)));
            inputs.Add(new LinqGroupByInput("GroupBy Single Value With Max", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => values.Max() /*return the Max of each group */)));

            inputs.Add(new LinqGroupByInput("GroupBy Single Value With Min", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => values.Min(value => value.Multiples) /*return the Min of each group */)));
            inputs.Add(new LinqGroupByInput("GroupBy Single Value With Max", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => values.Max(value => value.Multiples) /*return the Max of each group */)));
            inputs.Add(new LinqGroupByInput("GroupBy Single Value With Count", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => values.Count() /*return the Count of each group */)));

            this.ExecuteTestSuite(inputs);
        }

        public sealed class LinqGroupByInput : BaselineTestInput
        {
            internal Expression<Func<bool, IQueryable>> Expression { get; }

            internal LinqGroupByInput(string description, Expression<Func<bool, IQueryable>> expr)
                : base(description)
            {
                this.Expression = expr ?? throw new ArgumentNullException($"{nameof(expr)} must not be null.");
            }

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

        public sealed class LinqGroupByOutput : BaselineTestOutput
        {
            public string SqlQuery { get; }

            public string ErrorMessage { get; }

            public LinqGroupByOutput(string sqlQuery, string errorMessage = null)
            {
                this.SqlQuery = sqlQuery;
                this.ErrorMessage = errorMessage;
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
            }
        }

        public override LinqGroupByOutput ExecuteTest(LinqGroupByInput input)
        {
            lastExecutedScalarQuery = null;
            Func<bool, IQueryable> compiledQuery = input.Expression.Compile();

            string errorMessage = null;
            string query = string.Empty;
            try
            {
                IQueryable queryResult;

                try
                {
                    queryResult = compiledQuery(true);

                    query = JObject.Parse(queryResult.ToString()).GetValue("query", StringComparison.Ordinal).ToString();
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
                    IQueryable dataResult = compiledQuery(false);
                    //Assert.AreEqual(dataResult, queryResult);
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

            return new LinqGroupByOutput(query, errorMessage);
        }
    }
}

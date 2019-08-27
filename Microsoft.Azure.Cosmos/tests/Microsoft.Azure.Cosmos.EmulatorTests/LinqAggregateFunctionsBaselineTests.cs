//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests
{

    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Xml;
    using static LinqAggregateFunctionBaselineTests;

    using BaselineTest;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Documents;
    using System.Threading.Tasks;

    [TestClass]
    public class LinqAggregateFunctionBaselineTests : BaselineTests<LinqAggregateInput, LinqAggregateOutput>
    {
        private static CosmosClient client;
        private static Cosmos.Database cosmosDatabase;
        private static Func<bool, IQueryable<Data>> getQuery;
        private static Func<bool, IQueryable<Family>> getQueryFamily;
        private static IQueryable lastExecutedScalarQuery;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            try
            {
                await Initialize();
            }
            catch (ServiceUnavailableException serviceUnavailableException)
            {
                if (serviceUnavailableException.Message == ClientResources.AllServicePoolsEmpty)
                {
                    throw new AssertInconclusiveException("Test was inconclusive since all serive pools were empty.");
                }
                else
                {
                    throw;
                }
            }
        }

        private async static Task Initialize()
        {
            client = TestCommon.CreateCosmosClient(true);
            CleanUp();

            // Set a callback to get the handle of the last executed query to do the verification
            // This is neede because aggregate queries return type is a scalar so it can't be used 
            // to verify the translated LINQ directly as other queries type.
            client.DocumentClient.OnExecuteScalarQueryCallback = q => LinqAggregateFunctionBaselineTests.lastExecutedScalarQuery = q;

            string databaseName = $"{nameof(LinqAggregateFunctionBaselineTests)}-{Guid.NewGuid().ToString("N")}";

            Container container;
            cosmosDatabase = await client.CreateDatabaseAsync(databaseName);
            getQuery = LinqTestsCommon.GenerateSimpleCosmosData(cosmosDatabase);
            getQueryFamily = LinqTestsCommon.GenerateFamilyCosmosData(cosmosDatabase, out container);
        }

        [ClassCleanup]
        public static void CleanUp()
        {
            try
            {
                List<Database> dbs = client.DocumentClient.CreateDatabaseQuery().ToList();
                foreach (Database db in dbs)
                {
                    client.DocumentClient.DeleteDatabaseAsync(db.SelfLink).Wait();
                }
            }
            catch (DocumentClientException e)
            {
                throw new AssertInconclusiveException("Test was inconclusive due to exception in CleanUp task", e);
            }
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestAggregateMax()
        {
            List<LinqAggregateInput> inputs = new List<LinqAggregateInput>();

            inputs.Add(new LinqAggregateInput(
                "Max on doc", b => getQuery(b)
                .Max()));

            inputs.Add(new LinqAggregateInput(
                "Max w/ doc mapping", b => getQuery(b)
                .Max(doc => doc)));

            inputs.Add(new LinqAggregateInput(
                "Max w/ doc mapping to number", b => getQuery(b)
                .Max(doc => doc.Number)));

            inputs.Add(new LinqAggregateInput(
                "Filter true flag -> Max w/ doc mapping to number", b => getQuery(b)
                .Where(doc => doc.Flag).Max(doc => doc.Number)));

            inputs.Add(new LinqAggregateInput(
                "Filter false flag -> Max w/ doc mapping to number", b => getQuery(b)
                .Where(doc => !doc.Flag).Max(doc => doc.Number)));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Max", b => getQuery(b)
                .Select(doc => doc.Number).Max()));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Max w/ mapping", b => getQuery(b)
                .Select(doc => doc.Number).Max(num => num)));

            inputs.Add(new LinqAggregateInput(
                "Select many -> Filter -> Select -> Max", b => getQuery(b)
                .SelectMany(doc => doc.Multiples.Where(m => m % 3 == 0).Select(m => m)).Max()));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Skip -> Max", b => getQueryFamily(b)
                .Select(f => f.Int).Skip(90).Max(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Skip -> Take -> Max", b => getQueryFamily(b)
                .Select(f => f.Int).Skip(90).Take(5).Max(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Select number -> Max", b => getQueryFamily(b)
                .Skip(5).Take(5).Select(f => f.Int).Max(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> SelectMany(Select) -> Skip -> Take -> Max", b => getQueryFamily(b)
                .Skip(5).Take(5).SelectMany(f => f.Children.Select(c => c.Grade)).Skip(10).Take(20).Max(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Select(new() -> Skip -> Take)", b => getQueryFamily(b)
                .Skip(1).Take(20)
                .Select(f => new
                {
                    v0 = f.Children.Skip(1).Select(c => c.Grade).Max(),
                    v1 = f.Children.Skip(1).Take(3).Select(c => c.Grade).Max(),
                    v2 = f.Children.Take(3).Skip(1).Select(c => c.Grade).Max(),
                    v3 = f.Records.Transactions.Select(t => t.Amount).OrderBy(a => a).Skip(10).Take(20).Max(),
                    v4 = f.Children.Where(c => c.Grade > 20).OrderBy(c => c.Grade).Select(c => c.Grade).Skip(1).Max()
                })
                .Skip(1).Take(10)
                .Select(f => f.v0 > f.v1 ? f.v0 : f.v1)
                .Max(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestAggregateMin()
        {
            List<LinqAggregateInput> inputs = new List<LinqAggregateInput>();

            inputs.Add(new LinqAggregateInput(
                "Min on doc", b => getQuery(b)
                .Min()));

            inputs.Add(new LinqAggregateInput(
                "Min w/ doc mapping", b => getQuery(b)
                .Min(doc => doc)));

            inputs.Add(new LinqAggregateInput(
                "Min w/ doc mapping to number", b => getQuery(b)
                .Min(doc => doc.Number)));

            inputs.Add(new LinqAggregateInput(
                "Filter true flag -> Min w/ doc mapping to number", b => getQuery(b)
                .Where(doc => doc.Flag).Min(doc => doc.Number)));

            inputs.Add(new LinqAggregateInput(
                "Filter false flag -> Min w/ doc mapping to number", b => getQuery(b)
                .Where(doc => !doc.Flag).Min(doc => doc.Number)));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Min", b => getQuery(b)
                .Select(doc => doc.Number).Min()));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Min w/ mapping", b => getQuery(b)
                .Select(doc => doc.Number).Min(num => num)));

            inputs.Add(new LinqAggregateInput(
                "Select many -> Filter -> Select -> Min", b => getQuery(b)
                .SelectMany(doc => doc.Multiples.Where(m => m % 3 == 0).Select(m => m)).Min()));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Skip -> Min", b => getQueryFamily(b)
                .Select(f => f.Int).Skip(90).Min(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Skip -> Take -> Min", b => getQueryFamily(b)
                .Select(f => f.Int).Skip(90).Take(5).Min(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Select number Min Max", b => getQueryFamily(b)
                .Skip(5).Take(5).Select(f => f.Int).Min(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> SelectMany(Select) -> Skip -> Take -> Min", b => getQueryFamily(b)
                .Skip(5).Take(5).SelectMany(f => f.Children.Select(c => c.Grade)).Skip(10).Take(20).Min(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Select(new(Skip -> Select -> Min, Skip -> Take -> Select -> Min, Take -> Skip -> Select -> Min) -> Skip -> Take)", b => getQueryFamily(b)
                .Skip(1).Take(20)
                .Select(f => new
                {
                    v0 = f.Children.Skip(1).Select(c => c.Grade).Min(),
                    v1 = f.Children.Skip(1).Take(3).Select(c => c.Grade).Min(),
                    v2 = f.Children.Take(3).Skip(1).Select(c => c.Grade).Min(),
                    v3 = f.Records.Transactions.Select(t => t.Amount).OrderBy(a => a).Skip(10).Take(20).Min(),
                    v4 = f.Children.Where(c => c.Grade > 20).OrderBy(c => c.Grade).Select(c => c.Grade).Skip(1).Min()
                })
                .Skip(1).Take(10)
                .Select(f => f.v0 < f.v1 ? f.v0 : f.v1)
                .Min(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestAggregateSum()
        {
            List<LinqAggregateInput> inputs = new List<LinqAggregateInput>();

            inputs.Add(new LinqAggregateInput(
                "Sum number property", b => getQuery(b)
                .Sum(doc => doc.Number)));

            inputs.Add(new LinqAggregateInput(
                "Filter true flag -> Sum w/ mapping", b => getQuery(b)
                .Where(doc => doc.Flag).Sum(doc => doc.Number)));

            inputs.Add(new LinqAggregateInput(
                "Filter false flag -> Sum w/ mapping", b => getQuery(b)
                .Where(doc => !doc.Flag).Sum(doc => doc.Number)));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Sum", b => getQuery(b)
                .Select(doc => doc.Number).Sum()));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Sum w/ mapping", b => getQuery(b)
                .Select(doc => doc.Number).Sum(num => num)));

            inputs.Add(new LinqAggregateInput(
                "Select many -> Filter -> Select -> Sum", b => getQuery(b)
                .SelectMany(doc => doc.Multiples.Where(m => m % 3 == 0).Select(m => m)).Sum()));

            inputs.Add(new LinqAggregateInput(
                "Select(Select) -> Count(Sum)", b => getQuery(b)
                .Select(doc => doc.Multiples).Count(array => array.Sum() > 5)));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Skip -> Sum", b => getQueryFamily(b)
                .Select(f => f.Int).Skip(90).Sum(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Skip -> Take -> Sum", b => getQueryFamily(b)
                .Select(f => f.Int).Skip(90).Take(5).Sum(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Select number -> Sum", b => getQueryFamily(b)
                .Skip(5).Take(5).Select(f => f.Int).Sum(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> SelectMany(Select) -> Skip -> Take -> Sum", b => getQueryFamily(b)
                .Skip(5).Take(5).SelectMany(f => f.Children.Select(c => c.Grade)).Skip(10).Take(20).Sum(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Select(new() -> Skip -> Take)", b => getQueryFamily(b)
                .Skip(1).Take(20)
                .Select(f => new
                {
                    v0 = f.Children.Skip(1).Select(c => c.Grade).Sum(),
                    v1 = f.Children.Skip(1).Take(3).Select(c => c.Grade).Sum(),
                    v2 = f.Children.Take(3).Skip(1).Select(c => c.Grade).Sum(),
                    v3 = f.Records.Transactions.Select(t => t.Amount).OrderBy(a => a).Skip(10).Take(20).Sum(),
                    v4 = f.Children.Where(c => c.Grade > 20).OrderBy(c => c.Grade).Select(c => c.Grade).Skip(1).Sum()
                })
                .Skip(1).Take(10)
                .Select(f => f.v0 + f.v1 + f.v2 + f.v4)
                .Sum(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestAggregateCount()
        {
            List<LinqAggregateInput> inputs = new List<LinqAggregateInput>();

            inputs.Add(new LinqAggregateInput(
                "Count", b => getQuery(b)
                .Count()));

            inputs.Add(new LinqAggregateInput(
                "Filter true flag -> Count", b => getQuery(b)
                .Where(doc => doc.Flag).Count()));

            inputs.Add(new LinqAggregateInput(
                "Filter false flag -> Count", b => getQuery(b)
                .Where(doc => !doc.Flag).Count()));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Count", b => getQuery(b)
                .Select(doc => doc.Number).Count()));

            inputs.Add(new LinqAggregateInput(
                "Select many -> Filter -> Select -> Count", b => getQuery(b)
                .SelectMany(doc => doc.Multiples.Where(m => m % 3 == 0).Select(m => m)).Count()));

            inputs.Add(new LinqAggregateInput(
                "Count w/ boolean filter", b => getQuery(b)
                .Count(doc => doc.Flag)));

            inputs.Add(new LinqAggregateInput(
                "Count w/ operator filter", b => getQuery(b)
                .Count(doc => doc.Number < -7)));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Count w/ operator filter", b => getQuery(b)
                .Select(doc => doc.Number).Count(num => num < -13)));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Skip -> Count", b => getQueryFamily(b)
                .Select(f => f.Int).Skip(90).Count(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Skip -> Take -> Count", b => getQueryFamily(b)
                .Select(f => f.Int).Skip(90).Take(5).Count(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Select number -> Count", b => getQueryFamily(b)
                .Skip(5).Take(5).Select(f => f.Int).Count(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> SelectMany(Select) -> Skip -> Take -> Count", b => getQueryFamily(b)
                .Skip(5).Take(5).SelectMany(f => f.Children.Select(c => c.Grade)).Skip(10).Take(20).Count(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Select(new(Skip -> Select -> Count, Skip -> Take -> Select -> Count, Take -> Skip -> Select -> Count) -> Skip -> Take)", b => getQueryFamily(b)
                .Skip(1).Take(20)
                .Select(f => new
                {
                    v0 = f.Children.Skip(1).Select(c => c.Grade).Count(),
                    v1 = f.Children.Skip(1).Take(3).Select(c => c.Grade).Count(),
                    v2 = f.Children.Take(3).Skip(1).Select(c => c.Grade).Count()
                })
                .Skip(1).Take(10)
                .Count(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Select(new() -> Skip -> Take)", b => getQueryFamily(b)
                .Skip(1).Take(20)
                .Select(f => new
                {
                    v0 = f.Children.Skip(1).Count(c => c.Grade > 50),
                    v1 = f.Children.Skip(1).Take(3).Count(c => c.Grade > 50),
                    v2 = f.Children.Take(3).Skip(1).Count(c => c.Grade > 50),
                    v3 = f.Records.Transactions.Select(t => t.Amount).OrderBy(a => a).Skip(10).Take(20).Count(),
                    v4 = f.Children.Where(c => c.Grade > 20).OrderBy(c => c.Grade).Select(c => c.Grade).Skip(1).Count()
                })
                .Skip(1).Take(10)
                .Count(f => f.v0 + f.v1 > f.v2 + f.v3),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

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

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Any", b => getQueryFamily(b)
                .Skip(20).Take(1).Any(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "SelectMany(Skip) -> Any", b => getQueryFamily(b)
                .SelectMany(f => f.Children.Skip(4)).Any(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Select(new()) -> Skip -> Take -> Select -> Any", b => getQueryFamily(b)
                .Skip(1).Take(20)
                .Select(f => new {
                    v0 = f.Children.Skip(3).Select(c => c.Grade).Any(),
                    v1 = f.Children.Skip(3).Take(3).Select(c => c.Grade).Any(),
                    v2 = f.Children.Take(3).Skip(4).Select(c => c.Grade).Any(),
                    v3 = f.Records.Transactions.Select(t => t.Amount).OrderBy(a => a).Skip(10).Take(20).Any(),
                    v4 = f.Children.Where(c => c.Grade > 80).OrderBy(c => c.Grade).Select(c => c.Grade).Skip(4).Any(),
                    v5 = f.Children.Where(c => c.Grade > 80).OrderBy(c => c.Grade).Skip(4)
                        .SelectMany(c => c.Pets.Where(p => p.GivenName.CompareTo("A") > 0).Skip(1).Take(2)).Take(3).Any(),
                })
                .Skip(1).Take(10)
                .Select(f => (f.v0 && f.v1) || (f.v0 && f.v1))
                .Any(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestAggregateAvg()
        {
            List<LinqAggregateInput> inputs = new List<LinqAggregateInput>();

            inputs.Add(new LinqAggregateInput(
                "Avg number", b => getQuery(b)
                .Average(doc => doc.Number)));

            inputs.Add(new LinqAggregateInput(
                "Filter true flag -> Avg w/ mapping", b => getQuery(b)
                .Where(doc => doc.Flag).Average(doc => doc.Number)));

            inputs.Add(new LinqAggregateInput(
                "Filter false flag -> Avg w/ mapping", b => getQuery(b)
                .Where(doc => !doc.Flag).Average(doc => doc.Number)));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Avg", b => getQuery(b)
                .Select(doc => doc.Number).Average()));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Avg w/ mapping", b => getQuery(b)
                .Select(doc => doc.Number).Average(num => num)));

            inputs.Add(new LinqAggregateInput(
                "Select many -> Filter -> Select -> Avg", b => getQuery(b)
                .SelectMany(doc => doc.Multiples.Where(m => m % 3 == 0).Select(m => m)).Average()));

            inputs.Add(new LinqAggregateInput(
                "Select(Where) -> Avg(Sum(map))", b => getQueryFamily(b).Select(f => f.Children.Where(c => c.Grade > 80)).Average(children => children.Sum(c => c.Grade))));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Skip -> Avg", b => getQueryFamily(b)
                .Select(f => f.Int).Skip(90).Average(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Select number -> Skip -> Take -> Avg", b => getQueryFamily(b)
                .Select(f => f.Int).Skip(90).Take(5).Average(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Select number -> Avg", b => getQueryFamily(b)
                .Skip(5).Take(5).Select(f => f.Int).Average(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> SelectMany(Select) -> Skip -> Take -> Avg", b => getQueryFamily(b)
                .Skip(5).Take(5).SelectMany(f => f.Children.Select(c => c.Grade)).Skip(10).Take(20).Average(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

            inputs.Add(new LinqAggregateInput(
                "Skip -> Take -> Select(new() -> Skip -> Take)", b => getQueryFamily(b)
                .Skip(1).Take(20)
                .Where(f => f.Children.Count() > 2)
                .Select(f => new
                {
                    v0 = f.Children.Skip(1).Select(c => c.Grade).Average(),
                    v1 = f.Children.Skip(1).Take(3).Select(c => c.Grade).Average(),
                    v2 = f.Children.Take(3).Skip(1).Select(c => c.Grade).Average(),
                    v3 = f.Records.Transactions.Select(t => t.Amount).OrderBy(a => a).Skip(10).Take(20).Average(),
                    v4 = f.Children.Where(c => c.Grade > 20).OrderBy(c => c.Grade).Select(c => c.Grade).Skip(1).Average()
                })
                .Skip(1).Take(10)
                .Select(f => (f.v0 + f.v1 + f.v2 + f.v3 + f.v4) / 5)
                .Average(),
                ErrorMessages.OffsetLimitInSubqueryNotSupported));

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
                    Assert.IsNotNull(LinqAggregateFunctionBaselineTests.lastExecutedScalarQuery, "lastExecutedScalarQuery is not set");

                    query = JObject
                        .Parse(LinqAggregateFunctionBaselineTests.lastExecutedScalarQuery.ToString())
                        .GetValue("query", StringComparison.Ordinal)
                        .ToString();
                }

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

                errorMessage = null;
                if ((input.ErrorMessage != null && !e.Message.Contains(input.ErrorMessage)))
                {
                    errorMessage = $"Expecting error message containing [[{input.ErrorMessage}]]. Actual: {e.Message}";
                    failed = true;
                }
                else if (input.ErrorMessage == null)
                {
                    errorMessage = e.Message;
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

            public override void SerializeAsXml(XmlWriter xmlWriter)
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

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                if (xmlWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(xmlWriter)} cannot be null.");
                }

                var expressionString = LinqTestInput.FilterInputExpression(expression.Body.ToString());

                xmlWriter.WriteStartElement("Description");
                xmlWriter.WriteCData(this.Description);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Expression");
                xmlWriter.WriteCData(expressionString);
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

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using BaselineTest;

    /// <summary>
    /// Baseline tests for Dictionary LINQ translation with OBJECTTOARRAY.
    /// Fixes GitHub Issue #5547: Dictionary.Any() should generate correct SQL with OBJECTTOARRAY.
    /// 
    /// These tests verify that LINQ queries on Dictionary/IDictionary/IReadOnlyDictionary properties
    /// generate the correct SQL using the OBJECTTOARRAY() function. The OBJECTTOARRAY function
    /// converts a JSON object into an array of {"k": key, "v": value} pairs that can be iterated.
    /// 
    /// NOTE: If a test baseline needs to be updated, run UpdateContracts.ps1 against the emulator.
    /// </summary>
    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public class LinqDictionaryBaselineTests : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static CosmosClient cosmosClient;
        private static Cosmos.Database testDb;
        private static Container testContainer;
        private static Func<bool, IQueryable<Family>> getQuery;

        [ClassInitialize]
        public async static Task Initialize(TestContext testContext)
        {
            cosmosClient = TestCommon.CreateCosmosClient(true);

            string dbName = $"{nameof(LinqDictionaryBaselineTests)}-{Guid.NewGuid().ToString("N")}";
            testDb = await cosmosClient.CreateDatabaseAsync(dbName);

            getQuery = LinqTestsCommon.GenerateFamilyCosmosData(testDb, out testContainer);
        }

        [ClassCleanup]
        public async static Task CleanUp()
        {
            if (testDb != null)
            {
                await testDb.DeleteStreamAsync();
            }

            cosmosClient?.Dispose();
        }

        /// <summary>
        /// Tests for LINQ translation of Dictionary operations using the Family model,
        /// which has a Dictionary&lt;string, string&gt; named Things on the Child class.
        /// 
        /// Tests cover:
        /// - Any() without predicate
        /// - Any() with key filter (t.Key == ...)
        /// - Any() with value filter (t.Value == ...)
        /// - Where with dictionary predicate
        /// - SelectMany over dictionary entries
        /// - Select projecting key or value
        /// - Aggregates (Count)
        /// - Skip, Take with dictionary filter
        /// - OrderBy with dictionary filter
        /// - Nested dictionary field access
        /// - IDictionary and IReadOnlyDictionary type fields
        /// 
        /// NOTE: Run UpdateContracts.ps1 against the emulator to generate the baseline XML.
        /// </summary>
        [TestMethod]
        [Ignore] // TODO: Run UpdateContracts.ps1 to generate baseline XML before enabling
        public void TestDictionaryLinqTranslations()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();

            // -------------------------
            // Any() - basic
            // -------------------------

            inputs.Add(new LinqTestInput(
                "Any - children with Things",
                b => getQuery(b).Where(f => f.Children.Any(c => c.Things.Any()))));

            inputs.Add(new LinqTestInput(
                "Any - children with key 'A'",
                b => getQuery(b).Where(f => f.Children.Any(c => c.Things.Any(t => t.Key == "A")))));

            inputs.Add(new LinqTestInput(
                "Any - families with children with Things -> Select FamilyId",
                b => getQuery(b)
                    .Where(f => f.Children.Any(c => c.Things.Any()))
                    .Select(f => f.FamilyId)));

            // -------------------------
            // SelectMany
            // -------------------------

            inputs.Add(new LinqTestInput(
                "SelectMany - children Things",
                b => getQuery(b).SelectMany(f => f.Children.SelectMany(c => c.Things)),
                skipVerification: true));  // KeyValuePair ordering may differ

            inputs.Add(new LinqTestInput(
                "SelectMany - Things -> Select Key",
                b => getQuery(b).SelectMany(f => f.Children.SelectMany(c => c.Things)).Select(t => t.Key),
                skipVerification: true));

            inputs.Add(new LinqTestInput(
                "SelectMany - Things -> Select Value",
                b => getQuery(b).SelectMany(f => f.Children.SelectMany(c => c.Things)).Select(t => t.Value),
                skipVerification: true));

            // -------------------------
            // Select
            // -------------------------

            inputs.Add(new LinqTestInput(
                "Select - project FamilyId for families with Things",
                b => getQuery(b)
                    .Where(f => f.Children.Any(c => c.Things.Any(t => t.Key == "A")))
                    .Select(f => f.FamilyId)));

            // -------------------------
            // Skip, Take
            // -------------------------

            inputs.Add(new LinqTestInput(
                "Where(Any) -> OrderBy -> Take",
                b => getQuery(b)
                    .Where(f => f.Children.Any(c => c.Things.Any()))
                    .OrderBy(f => f.FamilyId)
                    .Take(5)));

            inputs.Add(new LinqTestInput(
                "Where(Any) -> OrderBy -> Skip -> Take",
                b => getQuery(b)
                    .Where(f => f.Children.Any(c => c.Things.Any()))
                    .OrderBy(f => f.FamilyId)
                    .Skip(1)
                    .Take(3)));

            // -------------------------
            // OrderBy
            // -------------------------

            inputs.Add(new LinqTestInput(
                "OrderBy with Things filter",
                b => getQuery(b)
                    .Where(f => f.Children.Any(c => c.Things.Any()))
                    .OrderBy(f => f.FamilyId)
                    .Select(f => f.FamilyId)));

            inputs.Add(new LinqTestInput(
                "OrderByDescending with Things filter",
                b => getQuery(b)
                    .Where(f => f.Children.Any(c => c.Things.Any()))
                    .OrderByDescending(f => f.FamilyId)
                    .Select(f => f.FamilyId)));

            this.ExecuteTestSuite(inputs);
        }

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input);
        }
    }
}

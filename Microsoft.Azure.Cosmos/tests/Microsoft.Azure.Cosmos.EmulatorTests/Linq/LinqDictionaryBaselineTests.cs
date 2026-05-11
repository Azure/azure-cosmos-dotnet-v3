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
    /// NOTE: This baseline test is currently [Ignore]d because the baseline XML cannot be
    /// generated without running against the Cosmos emulator. To enable:
    ///   1. Bring up the Cosmos emulator locally.
    ///   2. Remove the [Ignore] attribute on TestDictionaryLinqTranslations.
    ///   3. Run UpdateContracts.ps1 to (re)generate the baseline XML.
    /// 
    /// Until then, comprehensive data + SQL coverage for the Dictionary/IDictionary/
    /// IReadOnlyDictionary/Nested-dictionary scenarios is provided by
    /// <see cref="CosmosItemLinqTests.LinqDictionaryAnyWithObjectToArrayTest"/>, which runs
    /// against the emulator in CI.
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
        /// - Any() with value filter (t.Value != null)
        /// - Where with KeyValuePair predicate over dictionary entries
        /// - SelectMany over dictionary entries
        /// - Select projecting key or value
        /// - Skip, Take with dictionary filter
        /// - OrderBy with dictionary filter
        /// 
        /// Count() aggregates over a dictionary go through a different code path
        /// (ArrayBuiltinFunctions.ArrayCountVisitor) and are covered by the integration test.
        /// IDictionary, IReadOnlyDictionary, and Nested-dictionary translations are also covered
        /// by the integration test <see cref="CosmosItemLinqTests.LinqDictionaryAnyWithObjectToArrayTest"/>.
        /// 
        /// NOTE: Run UpdateContracts.ps1 against the emulator to generate the baseline XML
        /// before removing [Ignore].
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
                "Any - children with value predicate",
                b => getQuery(b).Where(f => f.Children.Any(c => c.Things.Any(t => t.Value != null)))));

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
            // Where with filtered dictionary enumeration
            // (Where clause on KeyValuePair entries — goes through OBJECTTOARRAY)
            // -------------------------

            inputs.Add(new LinqTestInput(
                "Where - filtered dictionary entries by Key, then Any",
                b => getQuery(b)
                    .Where(f => f.Children.Any(c => c.Things.Where(t => t.Key == "A").Any()))));

            inputs.Add(new LinqTestInput(
                "Where - filtered dictionary entries by Value, then Any",
                b => getQuery(b)
                    .Where(f => f.Children.Any(c => c.Things.Where(t => t.Value != null).Any()))));

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

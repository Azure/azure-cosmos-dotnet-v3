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
    using System.Linq.Dynamic;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using BaselineTest;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Scripts;
    using static Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests.LinqGeneralBaselineTests;

    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public class LinqGeneralBaselineTests : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static CosmosClient cosmosClient;
        private static Cosmos.Database testDb;
        private static Container testContainer;
        private static Func<bool, IQueryable<Family>> getQuery;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            cosmosClient = TestCommon.CreateCosmosClient(true);
            DocumentClientSwitchLinkExtension.Reset("LinqTests");

            string dbName = $"{nameof(LinqGeneralBaselineTests)}-{Guid.NewGuid().ToString("N")}";
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

        public class Address
        {
            public string State;
            public string County;
            public string City;
        }

        public class NoMemberExampleClass
        {
            public NoMemberExampleClass(string stringValue)
            {
            }
        }

        public class GuidClass : LinqTestObject
        {
            [JsonProperty(PropertyName = "id")]
            public Guid Id;
        }

        public class ListArrayClass : LinqTestObject
        {
            [JsonProperty(PropertyName = "id")]
            public string Id;

            public int[] ArrayField;
            public List<int> ListField;
        }

        [DataContract]
        public class Sport : LinqTestObject
        {
            [DataMember(Name = "id")]
            public string SportName;

            [JsonProperty(PropertyName = "json")]
            [DataMember(Name = "data")]
            public string SportType;
        }

        public class Sport2 : LinqTestObject
        {
            [DataMember(Name = "data")]
            public string id;
        }

        [TestMethod]
        public void TestSelectMany()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();

            inputs.Add(new LinqTestInput("SelectMany(SelectMany(Where -> Select))",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => pet)))));

            inputs.Add(new LinqTestInput("SelectMany(Where -> SelectMany(Where -> Select))",
                b => getQuery(b)
                .SelectMany(family => family.Children.Where(c => c.Grade > 10)
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => pet)))));

            inputs.Add(new LinqTestInput("SelectMany(SelectMany(Where -> Select new {}))",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => new
                        {
                            family = family.FamilyId,
                            child = child.GivenName,
                            pet = pet.GivenName
                        })))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select)",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select -> Select)",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName).Select(n => n.Count()))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select -> Where)",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName).Where(n => n.Count() > 10))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select) -> Select",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName)).Select(n => n.Count())));

            inputs.Add(new LinqTestInput("SelectMany()", b => getQuery(b).SelectMany(root => root.Children)));

            inputs.Add(new LinqTestInput("SelectMany -> SelectMany", b => getQuery(b).SelectMany(f => f.Children).SelectMany(c => c.Pets)));

            inputs.Add(new LinqTestInput("SelectMany -> Where -> SelectMany(Select)", b => getQuery(b).SelectMany(f => f.Children).Where(c => c.Pets.Count() > 0).SelectMany(c => c.Pets.Select(p => p.GivenName))));

            inputs.Add(new LinqTestInput("SelectMany -> Where -> SelectMany(Select new)", b => getQuery(b)
                .SelectMany(f => f.Children)
                .Where(c => c.Pets.Count() > 0)
                .SelectMany(c => c.Pets.Select(p => new { PetName = p.GivenName, OwnerName = c.GivenName }))));

            inputs.Add(new LinqTestInput("Where -> SelectMany", b => getQuery(b).Where(f => f.Children.Count() > 0).SelectMany(f => f.Children)));

            inputs.Add(new LinqTestInput("SelectMany -> Select", b => getQuery(b).SelectMany(f => f.Children).Select(c => c.FamilyName)));

            inputs.Add(new LinqTestInput("SelectMany(Select)", b => getQuery(b).SelectMany(f => f.Children.Select(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput("SelectMany(Select)", b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName))));

            inputs.Add(new LinqTestInput("SelectMany(Select -> Select)", b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName).Select(n => n.Count()))));

            inputs.Add(new LinqTestInput("SelectMany(Select -> Where)", b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName).Where(n => n.Count() > 10))));

            inputs.Add(new LinqTestInput("SelectMany(Take -> Where)", b => getQuery(b).SelectMany(f => f.Children.Take(2).Where(c => c.FamilyName.Count() > 10))));

            inputs.Add(new LinqTestInput("SelectMany(OrderBy -> Take -> Where)", b => getQuery(b).SelectMany(f => f.Children.OrderBy(c => c.Grade).Take(2).Where(c => c.FamilyName.Count() > 10))));

            inputs.Add(new LinqTestInput("SelectMany(Distinct -> Where)", b => getQuery(b).SelectMany(f => f.Children.Distinct().Where(c => c.FamilyName.Count() > 10))));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestSimpleSubquery()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();

            inputs.Add(new LinqTestInput("Select -> Select", b => getQuery(b).Select(f => f.FamilyId).Select(n => n.Count())));

            inputs.Add(new LinqTestInput("Select -> Where", b => getQuery(b).Select(f => f.FamilyId).Where(id => id.Count() > 10)));

            inputs.Add(new LinqTestInput("Select -> OrderBy -> Take -> Select -> Orderby -> Take", b => getQuery(b).Select(x => x).OrderBy(x => x).Take(10).Select(f => f.FamilyId).OrderBy(n => n.Count()).Take(5)));

            inputs.Add(new LinqTestInput("Select -> Orderby -> Take -> Select -> Orderby -> Take", b => getQuery(b).Select(f => f).OrderBy(f => f.Children.Count()).Take(3).Select(x => x).OrderBy(f => f.Parents.Count()).Take(2)));

            inputs.Add(new LinqTestInput("Orderby -> Take -> Orderby -> Take", b => getQuery(b).OrderBy(f => f.Children.Count()).Take(3).OrderBy(f => f.Parents.Count()).Take(2)));

            inputs.Add(new LinqTestInput("Take -> Orderby -> Take", b => getQuery(b).Take(10).OrderBy(f => f.FamilyId).Take(1)));

            inputs.Add(new LinqTestInput("Take -> Where -> Take -> Where -> Take -> Where", b => getQuery(b).Take(10).Where(f => f.Children.Count() > 0).Take(9).Where(f => f.Parents.Count() > 0).Take(8).Where(f => f.FamilyId.Count() > 10)));

            inputs.Add(new LinqTestInput("Take -> Where -> Distinct -> Select -> Take -> Where", b => getQuery(b).Take(10).Where(f => f.Children.Count() > 0).Distinct().Select(f => new { f }).Take(8).Where(f => f.f.FamilyId.Count() > 10)));

            inputs.Add(new LinqTestInput("Distinct -> Select -> Take -> Where -> Take -> Where", b => getQuery(b).Distinct().Select(f => new { f }).Take(10).Where(f => f.f.Children.Count() > 0).Take(9).Where(f => f.f.Parents.Count() > 0)));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestQueryFlattening()
        {
            // these queries should make more sense when combined with where and orderby
            // these tests verify the flattening part
            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("array create", b => getQuery(b).Select(f => f.Int).Select(i => new int[] { i })));
            inputs.Add(new LinqTestInput("unary operation", b => getQuery(b).Select(f => f.Int).Select(i => -i)));
            inputs.Add(new LinqTestInput("binary operation", b => getQuery(b).Select(f => f).Select(i => i.Int % 10 * i.Children.Count())));
            inputs.Add(new LinqTestInput("literal", b => getQuery(b).Select(f => f.Int).Select(i => 0)));
            inputs.Add(new LinqTestInput("function call", b => getQuery(b).Select(f => f.Parents).Select(p => p.Count())));
            inputs.Add(new LinqTestInput("object create", b => getQuery(b).Select(f => f.Parents).Select(p => new { parentCount = p.Count() })));
            inputs.Add(new LinqTestInput("conditional", b => getQuery(b).Select(f => f.Children).Select(c => c.Count() > 0 ? "have kids" : "no kids")));
            inputs.Add(new LinqTestInput("property ref + indexer", b => getQuery(b).Select(f => f)
                .Where(f => f.Children.Count() > 0 && f.Children[0].Pets.Count() > 0)
                .Select(f => f.Children[0].Pets[0].GivenName)));

            inputs.Add(new LinqTestInput("array creation -> indexer", b => getQuery(b).Select(f => new int[] { f.Int }).Select(array => array[0])));
            inputs.Add(new LinqTestInput("unary, indexer, property, function call -> function call", b => getQuery(b)
                .Where(f => f.Children.Count() > 0)
                .Select(f => -f.Children[0].Pets.Count()).Select(i => Math.Abs(i))));
            inputs.Add(new LinqTestInput("binary operation, function call -> conditional", b => getQuery(b).Select(i => i.Int % 10 * i.Children.Count()).Select(i => i > 0 ? new int[] { i } : new int[] { })));
            inputs.Add(new LinqTestInput("object creation -> conditional", b => getQuery(b)
                .Select(f => new { parentCount = f.Parents.Count(), childrenCount = f.Children.Count() })
                .Select(r => r.parentCount > 0 ? Math.Floor((double)r.childrenCount / r.parentCount) : 0)));
            inputs.Add(new LinqTestInput("indexer -> function call", b => getQuery(b).Select(f => f.Parents[0]).Select(p => string.Concat(p.FamilyName, p.GivenName))));
            inputs.Add(new LinqTestInput("conditional -> object creation", b => getQuery(b).Select(f => f.Parents.Count() > 0 ? f.Parents : new Parent[0]).Select(p => new { parentCount = p.Count() })));
            inputs.Add(new LinqTestInput("object creation -> conditional", b => getQuery(b).Select(f => new { children = f.Children }).Select(c => c.children.Count() > 0 ? c.children[0].GivenName : "no kids")));
            inputs.Add(new LinqTestInput("object creation -> conditional", b => getQuery(b).Select(f => new { family = f, children = f.Children.Count() }).Select(f => f.children > 0 && f.family.Children[0].Pets.Count() > 0 ? f.family.Children[0].Pets[0].GivenName : "no kids")));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestSubquery()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();

            // --------------------------------------
            // Subquery lambdas
            // --------------------------------------

            inputs.Add(new LinqTestInput(
                "Select(Select)", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput(
                "Select(OrderBy)", b => getQuery(b)
                .Select(f => f.Children.OrderBy(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput(
                "Select(Take)", b => getQuery(b)
                .Select(f => f.Children.Take(2))));

            inputs.Add(new LinqTestInput(
                "Select(Where)", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Pets.Count() > 0))));

            inputs.Add(new LinqTestInput(
                "Select(Distinct)", b => getQuery(b)
                .Select(f => f.Children.Distinct())));

            inputs.Add(new LinqTestInput(
                "Select(Count)", b => getQuery(b)
                .Select(f => f.Children.Count(c => c.Grade > 80))));

            inputs.Add(new LinqTestInput(
                "Select(Sum)", b => getQuery(b)
                .Select(f => f.Children.Sum(c => c.Grade))));

            inputs.Add(new LinqTestInput(
                "Where(Count)", b => getQuery(b)
                .Where(f => f.Children.Count(c => c.Pets.Count() > 0) > 0)));

            inputs.Add(new LinqTestInput(
                "Where(Sum)", b => getQuery(b)
                .Where(f => f.Children.Sum(c => c.Grade) > 100)));

            inputs.Add(new LinqTestInput(
                "OrderBy(Select)", b => getQuery(b)
                .OrderBy(f => f.Children.Select(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput(
                "OrderBy(Sum)", b => getQuery(b)
                .OrderBy(f => f.Children.Sum(c => c.Grade))));

            inputs.Add(new LinqTestInput(
                "OrderBy(Count)", b => getQuery(b)
                .OrderBy(f => f.Children.Count(c => c.Grade > 90))));

            // -------------------------------------------------------------
            // Mutilpe-transformations subquery lambdas
            // -------------------------------------------------------------

            inputs.Add(new LinqTestInput(
                "Select(Select -> Distinct -> Count)", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Gender).Distinct().Count())));

            inputs.Add(new LinqTestInput(
                "Select(Select -> Sum)", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Grade).Sum())));

            inputs.Add(new LinqTestInput(
                "Select(Select -> OrderBy -> Take)", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.GivenName).OrderBy(n => n.Length).Take(1))));

            inputs.Add(new LinqTestInput(
                "Select(SelectMany -> Select)", b => getQuery(b)
                .Select(f => f.Children.SelectMany(c => c.Pets).Select(c => c.GivenName.Count()))));

            inputs.Add(new LinqTestInput(
                "Select(Where -> Count)", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Grade > 50).Count())));

            inputs.Add(new LinqTestInput(
                "Select(Where -> OrderBy -> Take)", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Grade > 50).OrderBy(c => c.Pets.Count()).Take(3))));

            inputs.Add(new LinqTestInput(
                "Select(Where -> Select -> Take)", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Grade > 50).Select(c => c.Pets.Count()).Take(3))));

            inputs.Add(new LinqTestInput(
                "Select(Where -> Select(array) -> Take)", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Grade > 50).Select(c => c.Pets).Take(3))));

            inputs.Add(new LinqTestInput(
                "Select(where -> Select -> Distinct)", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Grade > 50 && c.Pets.Count() > 0).Select(c => c.Gender).Distinct())));

            inputs.Add(new LinqTestInput(
                "Select(OrderBy -> Take -> Select)", b => getQuery(b)
                .Select(f => f.Children.OrderBy(c => c.Grade).Take(1).Select(c => c.Gender))));

            inputs.Add(new LinqTestInput(
                "Select(OrderBy -> Take -> Select -> Average)", b => getQuery(b)
                .Select(f => f.Children.OrderBy(c => c.Pets.Count()).Take(2).Select(c => c.Grade).Average())));

            inputs.Add(new LinqTestInput(
                "Where(Select -> Count)", b => getQuery(b)
                .Where(f => f.Children.Select(c => c.Pets.Count()).Count() > 0)));

            inputs.Add(new LinqTestInput(
                "Where(Where -> Count)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Pets.Count() > 0).Count() > 0)));

            inputs.Add(new LinqTestInput(
                "Where(Where -> Sum)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Pets.Count() > 0).Sum(c => c.Grade) < 200)));

            inputs.Add(new LinqTestInput(
                "Where(Where -> OrderBy -> Take -> Select)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Pets.Count() > 0).OrderBy(c => c.Grade).Take(1).Where(c => c.Grade > 80).Count() > 0)));

            inputs.Add(new LinqTestInput(
                "OrderBy(Select -> Where)", b => getQuery(b)
                .OrderBy(f => f.Children.Select(c => c.Pets.Count()).Where(x => x > 1))));

            inputs.Add(new LinqTestInput(
                "OrderBy(Where -> Count)", b => getQuery(b)
                .OrderBy(f => f.Children.Where(c => c.Pets.Count() > 3).Count())));

            inputs.Add(new LinqTestInput(
                "OrderBy(Select -> Sum)", b => getQuery(b)
                .OrderBy(f => f.Children.Select(c => c.Grade).Sum())));

            inputs.Add(new LinqTestInput(
                "OrderBy(OrderBy -> Take -> Sum)", b => getQuery(b)
                .OrderBy(f => f.Children.OrderBy(c => c.Pets.Count()).Take(2).Sum(c => c.Grade))));

            // ---------------------------------------------------------
            // Scalar and Built-in expressions with subquery lambdas
            // ---------------------------------------------------------

            // Unary

            inputs.Add(new LinqTestInput(
                "Where(unary (Where -> Count))", b => getQuery(b)
                .Where(f => -f.Children.Where(c => c.Grade < 20).Count() == 0)));

            // Binary

            inputs.Add(new LinqTestInput(
                "Select(binary with Count)", b => getQuery(b)
                .Select(f => 5 + f.Children.Count(c => c.Pets.Count() > 0))));

            inputs.Add(new LinqTestInput(
                "Select(constant + Where -> Count)", b => getQuery(b)
                .Select(f => 5 + f.Children.Where(c => c.Pets.Count() > 0).Count())));

            inputs.Add(new LinqTestInput(
                "Where((Where -> Count) % constant)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Pets.Count() > 0).Count() % 2 == 1)));

            // Conditional

            inputs.Add(new LinqTestInput(
                "Select(conditional Any ? Select : Select)", b => getQuery(b)
                .Select(f => f.Children.Any() ? f.Children.Select(c => c.GivenName) : f.Parents.Select(p => p.GivenName))));

            inputs.Add(new LinqTestInput(
                "Select(conditional Any(filter) ? Max : Sum)", b => getQuery(b)
                .Select(f => f.Children.Any(c => c.Grade > 97) ? f.Children.Max(c => c.Grade) : f.Children.Sum(c => c.Grade))));

            // New array

            inputs.Add(new LinqTestInput(
                "Select(new array)", b => getQuery(b)
                .Select(f => new int[] { f.Children.Count(), f.Children.Sum(c => c.Grade) })));

            // New + member init

            inputs.Add(new LinqTestInput(
                "Select(new)", b => getQuery(b)
                .Select(f => new int[] { f.Children.Count(), f.Children.Sum(c => c.Grade) })));

            inputs.Add(new LinqTestInput(
               "Select(Select new)", b => getQuery(b)
               .Select(f => new { f.FamilyId, ChildrenPetCount = f.Children.Select(c => c.Pets.Count()) })));

            inputs.Add(new LinqTestInput(
                "Select(new Where)", b => getQuery(b)
                .Select(f => new { f.FamilyId, ChildrenWithPets = f.Children.Where(c => c.Pets.Count() > 3) })));

            inputs.Add(new LinqTestInput(
                "Select(new Where)", b => getQuery(b)
                .Select(f => new { f.FamilyId, GoodChildren = f.Children.Where(c => c.Grade > 90) })));

            inputs.Add(new LinqTestInput(
                "Select(new Where -> Select)", b => getQuery(b)
                .Select(f => new { f.FamilyId, ChildrenWithPets = f.Children.Where(c => c.Pets.Count() > 3).Select(c => c.GivenName) })));

            inputs.Add(new LinqTestInput(
                "Select(new Where -> Count) -> Where", b => getQuery(b)
                .Select(f => new { Family = f, ChildrenCount = f.Children.Where(c => c.Grade > 0).Count() }).Where(f => f.ChildrenCount > 0)));

            // Array builtin functions

            inputs.Add(new LinqTestInput(
                "Select(Where -> Concat(Where))", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Grade > 90).Concat(f.Children.Where(c => c.Grade < 10)))));

            inputs.Add(new LinqTestInput(
                "Select(Select -> Contains(Sum))", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Grade).Contains(f.Children.Sum(c => c.Pets.Count())))));

            inputs.Add(new LinqTestInput(
                "Select(Select -> Contains(Where -> Count))", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Grade).Contains(f.Children.Where(c => c.Grade > 50).Count()))));

            inputs.Add(new LinqTestInput(
                "Where -> Select(array indexer)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Grade > 20).Count() >= 2)
                .Select(f => f.Children.Where(c => c.Grade > 20).ToArray()[1])));

            inputs.Add(new LinqTestInput(
                "Where -> Select(array indexer)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Grade > 20).Count() >= 2)
                .Select(f => f.Children.Where(c => c.Grade > 20).ToArray()[f.Children.Count() % 2])));

            // Math builtin functions

            inputs.Add(new LinqTestInput(
                "Select(Floor(sum(map), sum(map)))", b => getQuery(b)
                .Select(f => Math.Floor(1.0 * f.Children.Sum(c => c.Grade) / (f.Children.Sum(c => c.Pets.Count()) + 1)))));

            inputs.Add(new LinqTestInput(
                "Select(Pow(Sum(map), Count(Any)))", b => getQuery(b)
                .Select(f => Math.Pow(f.Children.Sum(c => c.Pets.Count()), f.Children.Count(c => c.Pets.Any(p => p.GivenName.Count() == 0 || p.GivenName.Substring(0, 1) == "A"))))));

            inputs.Add(new LinqTestInput(
                "OrderBy(Log(Where -> Count))", b => getQuery(b)
                .OrderBy(f => Math.Log(f.Children.Where(c => c.Pets.Count() > 0).Count()))));

            // ------------------------------------------------------------------
            // Expression with subquery lambdas -> more transformations
            // ------------------------------------------------------------------

            inputs.Add(new LinqTestInput(
                "Select(Select) -> Where", b => getQuery(b).Select(f => f.Children.Select(c => c.Pets.Count())).Where(x => x.Count() > 0)));

            // Customer requested scenario
            inputs.Add(new LinqTestInput(
                "Select(new w/ Where) -> Where -> OrderBy -> Take", b => getQuery(b)
                .Select(f => new { f.FamilyId, ChildrenCount = f.Children.Count(), SmartChildren = f.Children.Where(c => c.Grade > 90) })
                .Where(f => f.FamilyId.CompareTo("ABC") > 0 && f.SmartChildren.Count() > 0)
                .OrderBy(f => f.ChildrenCount)
                .Take(10)));
            // TODO https://github.com/Azure/azure-cosmos-dotnet-v3/issues/375
            //inputs.Add(new LinqTestInput(
            //    "Select(new w/ Where) -> Where -> OrderBy -> Take", b => getQuery(b)
            //    .Select(f => new { f.FamilyId, ChildrenCount = f.Children.Count(), SmartChildren = f.Children.Where(c => c.Grade > 90) })
            //    .Where(f => f.ChildrenCount > 2 && f.SmartChildren.Count() > 1)
            //    .OrderBy(f => f.FamilyId)
            //    .Take(10)));

            inputs.Add(new LinqTestInput(
                "Select(new { Select(Select), conditional Count Take }) -> Where -> Select(Select(Any))", b => getQuery(b)
                .Select(f => new
                {
                    f.FamilyId,
                    ChildrenPetFirstChars = f.Children.Select(c => c.Pets.Select(p => p.GivenName.Substring(0, 1))),
                    FirstChild = f.Children.Count() > 0 ? f.Children.Take(1) : null
                })
                .Where(f => f.FirstChild != null)
                .Select(f => f.ChildrenPetFirstChars.Select(chArray => chArray.Any(a => f.FamilyId.StartsWith(a))))));

            inputs.Add(new LinqTestInput(
                "Select(new (Select(new (Select, Select))))", b => getQuery(b)
                .Select(f => new
                {
                    f.FamilyId,
                    ChildrenProfile = f.Children.Select(c => new
                    {
                        Fullname = c.GivenName + " " + c.FamilyName,
                        PetNames = c.Pets.Select(p => p.GivenName),
                        ParentNames = f.Parents.Select(p => p.GivenName)
                    })
                })));

            // ------------------------------------------------
            // Subquery lambda -> subquery lamda
            // ------------------------------------------------

            inputs.Add(new LinqTestInput(
                "Select(array) -> Where(Sum(map))", b => getQuery(b)
                .Select(f => f.Children).Where(children => children.Sum(c => c.Grade) > 100)));

            inputs.Add(new LinqTestInput(
                "Select(Select) -> Select(Sum())", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Grade)).Select(children => children.Sum())));

            inputs.Add(new LinqTestInput(
                "Select(Select) -> Select(Sum(map))", b => getQuery(b)
                .Select(f => f.Children).Select(children => children.Sum(c => c.Grade))));

            inputs.Add(new LinqTestInput(
                "Where(Any binary) -> Select(Sum(map))", b => getQuery(b)
                .Where(f => f.Children.Any(c => c.Grade > 90) && f.IsRegistered)
                .Select(f => f.Children.Sum(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput(
                "Where(Any binary) -> OrderBy(Count(filter)) -> Select(Sum(map))", b => getQuery(b)
                .Where(f => f.Children.Any(c => c.Grade > 90) && f.IsRegistered)
                .OrderBy(f => f.Children.Count(c => c.Things.Count() > 3))
                .Select(f => f.Children.Sum(c => c.Pets.Count()))));

            // ------------------------------
            // Nested subquery lambdas
            // ------------------------------

            inputs.Add(new LinqTestInput(
                "Select(Select(Select))", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Pets.Select(p => p.GivenName.Count())))));

            inputs.Add(new LinqTestInput(
                "Select(Select(Select))", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Pets.Select(p => p)))));

            inputs.Add(new LinqTestInput(
                "Select(Select(new Count))", b => getQuery(b)
                .Select(f => f.Children
                    .Select(c => new
                    {
                        HasSiblingWithSameStartingChar = f.Children.Count(child => (child.GivenName + " ").Substring(0, 1) == (c.GivenName + " ").Substring(0, 1)) > 1
                    }))));

            inputs.Add(new LinqTestInput(
                "Where -> Select(conditional ? Take : OrderBy -> Array indexer)", b => getQuery(b)
                .Where(f => f.Children.Count() > 0)
                .Select(f => f.Children.Count() == 1 ? f.Children.Take(1).ToArray()[0] : f.Children.OrderBy(c => c.Grade).ToArray()[1])));

            inputs.Add(new LinqTestInput(
                "Select(Where(Where -> Count) -> Select(new Count))", b => getQuery(b)
                .Select(f => f.Children
                    .Where(c => c.Pets
                        .Where(p => p.GivenName.Count() > 10 && p.GivenName.Substring(0, 1) == "A")
                        .Count() > 0)
                    .Select(c => new
                    {
                        HasSiblingWithSameStartingChar = f.Children.Count(child => (child.GivenName + " ").Substring(0, 1) == (c.GivenName + " ").Substring(0, 1)) > 1
                    }))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select(Select))", b => getQuery(b)
                .SelectMany(f => f.Children.Select(c => c.Pets.Select(p => p.GivenName.Count())))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Where(Any))", b => getQuery(b)
                .SelectMany(f => f.Children.Where(c => c.Pets.Any(p => p.GivenName.Count() > 10)))));

            inputs.Add(new LinqTestInput(
                "Where(Where(Count) -> Count)", b => getQuery(b)
                .Where(f => f.Parents.Where(p => p.FamilyName.Count() > 10).Count() > 1)));

            inputs.Add(new LinqTestInput(
                "Where(Where(Where -> Count) -> Count)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Pets.Where(p => p.GivenName.Count() > 15).Count() > 0).Count() > 0)));

            inputs.Add(new LinqTestInput(
                "Where(Select(Select -> Any))", b => getQuery(b)
                .Where(f => f.Children.Select(c => c.Pets.Select(p => p.GivenName)).Any(t => t.Count() > 3))));

            // -------------------------------------
            // Expression -> Subquery lambdas
            // -------------------------------------

            inputs.Add(new LinqTestInput(
                "Select(new) -> Select(Select)", b => getQuery(b)
                .Select(f => new { f.FamilyId, Family = f }).Select(f => f.Family.Children.Select(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput(
                "SelectMany -> Select(Select)", b => getQuery(b)
                .SelectMany(f => f.Children).Select(c => c.Pets.Select(p => p.GivenName.Count()))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Where) -> Where(Any) -> Select(Select)", b => getQuery(b)
                .SelectMany(f => f.Children.Where(c => c.Grade > 80))
                .Where(c => c.Pets.Any(p => p.GivenName.Count() > 20))
                .Select(c => c.Pets.Select(p => p.GivenName.Count()))));

            inputs.Add(new LinqTestInput(
                "Distinct -> Select(new) -> Where(Select(Select -> Any))", b => getQuery(b)
                .Distinct()
                .Select(f => new { f.FamilyId, ChildrenCount = f.Children.Count(), Family = f })
                .Where(f => f.Family.Children.Select(c => c.Pets.Select(p => p.GivenName)).Any(t => t.Count() > 3))));

            inputs.Add(new LinqTestInput(
                "Where -> Select(Select)", b => getQuery(b)
                .Where(f => f.Children.Count() > 0).Select(f => f.Children.Select(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput(
                "Distinct -> Select(Select)", b => getQuery(b)
                .Distinct().Select(f => f.Children.Select(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput(
                "Take -> Select(Select)", b => getQuery(b)
                .Take(10).Select(f => f.Children.Select(c => c.Pets.Count()))));

            // ------------------
            // Any in lambda
            // ------------------

            inputs.Add(new LinqTestInput(
                "Select(Any w const array)", b => getQuery(b)
                .Select(f => new int[] { 1, 2, 3 }.Any())));

            inputs.Add(new LinqTestInput(
                "Select(Any)", b => getQuery(b)
                .Select(f => f.Children.Any())));

            inputs.Add(new LinqTestInput(
                "Select(Any w lambda)", b => getQuery(b)
                .Select(f => f.Children.Any(c => c.Grade > 80))));

            inputs.Add(new LinqTestInput(
                "Select(new Any)", b => getQuery(b)
                .Select(f => new { f.FamilyId, HasGoodChildren = f.Children.Any(c => c.Grade > 80) })));

            inputs.Add(new LinqTestInput(
                "Select(new 2 Any)", b => getQuery(b)
                .Select(f => new { HasChildrenWithPets = f.Children.Any(c => c.Pets.Count() > 0), HasGoodChildren = f.Children.Any(c => c.Grade > 80) })));

            inputs.Add(new LinqTestInput(
                "Select(Nested Any)", b => getQuery(b)
                .Select(f => f.Children.Any(c => c.Pets.Any(p => p.GivenName.Count() > 10)))));

            inputs.Add(new LinqTestInput(
                "Where(Any)", b => getQuery(b)
                .Where(f => f.Children.Any(c => c.Pets.Count() > 0))));

            // Customer requested scenario
            inputs.Add(new LinqTestInput(
                "Where(simple expr && Any)", b => getQuery(b)
                .Where(f => f.FamilyId.Contains("a") && f.Children.Any(c => c.Pets.Count() > 0))));

            inputs.Add(new LinqTestInput(
                "OrderBy(Any)", b => getQuery(b)
                .OrderBy(f => f.Children.Any(c => c.Pets.Count() > 3))));

            // ------------------------------------------------
            // SelectMany with Take and OrderBy in lambda
            // ------------------------------------------------

            inputs.Add(new LinqTestInput(
                "SelectMany(Take)", b => getQuery(b)
                .SelectMany(f => f.Children.Take(2))));

            inputs.Add(new LinqTestInput(
                "SelectMany(OrderBy)", b => getQuery(b)
                .SelectMany(f => f.Children.OrderBy(c => c.Grade))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Where -> Take)", b => getQuery(b)
                .SelectMany(f => f.Children.Where(c => c.FamilyName.Count() > 10).Take(2))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Where -> Take -> Select)", b => getQuery(b)
                .SelectMany(f => f.Children.Where(c => c.FamilyName.Count() > 10).Take(2).Select(c => c.Grade))));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestGroupByTranslation()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("GroupBy Single Value Select Key", b => getQuery(b).GroupBy(k => k /*keySelector*/,
                                                                                (key, values) => key /*return the group by key */)));
            inputs.Add(new LinqTestInput("GroupBy Single Value Select Key", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => key /*return the group by key */)));
            inputs.Add(new LinqTestInput("GroupBy Single Value Select Key Alias", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (stringField, values) => stringField /*return the group by key */)));


            inputs.Add(new LinqTestInput("GroupBy Single Value With Min", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => values.Min(value => value.Int) /*return the Min of each group */)));
            inputs.Add(new LinqTestInput("GroupBy Single Value With Max", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => values.Max(value => value.Int) /*return the Max of each group */)));
            inputs.Add(new LinqTestInput("GroupBy Single Value With Count", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => values.Count() /*return the Count of each group */)));
            inputs.Add(new LinqTestInput("GroupBy Single Value With Average", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => values.Average(value => value.Int) /*return the Count of each group */)));

            // Negative cases

            // The translation is correct (SELECT VALUE MIN(root) FROM root GROUP BY root["Number"]
            // but the behavior between LINQ and SQL is different
            // In Linq, it requires the object to have comparer traits, where as in CosmosDB, we will return null
            inputs.Add(new LinqTestInput("GroupBy Single Value With Min", b => getQuery(b).GroupBy(k => k.Int /*keySelector*/,
                                                                              (key, values) => values.Min() /*return the Min of each group */)));
            inputs.Add(new LinqTestInput("GroupBy Single Value With Max", b => getQuery(b).GroupBy(k => k.Int /*keySelector*/,
                                                                                (key, values) => values.Max() /*return the Max of each group */)));

            // Unsupported node type
            inputs.Add(new LinqTestInput("GroupBy Single Value With Min", b => getQuery(b).GroupBy(k => k.Int /*keySelector*/,
                                                                              (key, values) => "string" /* Unsupported Nodetype*/ )));

            // Incorrect number of arguments
            inputs.Add(new LinqTestInput("GroupBy Single Value With Count", b => getQuery(b).GroupBy(k => k.Id)));
            inputs.Add(new LinqTestInput("GroupBy Single Value With Min", b => getQuery(b).GroupBy(
                                                                              k => k.Int,
                                                                              k2 => k2.Int,
                                                                              (key, values) => "string" /* Unsupported Nodetype*/ )));

            // Non-aggregate method calls
            inputs.Add(new LinqTestInput("GroupBy Single Value With Count", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => values.Select(value => value.Int) /*Not an aggregate*/)));
            inputs.Add(new LinqTestInput("GroupBy Single Value With Count", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => values.OrderBy(f => f.FamilyId) /*Not an aggregate*/)));

            // Currently unsupported case 
            inputs.Add(new LinqTestInput("GroupBy Single Value With Min", b => getQuery(b).GroupBy(k => k.FamilyId /*keySelector*/,
                                                                              (key, values) => new { familyId = key, familyIdCount = values.Count() } /*multi-value select */),
                                                                              ignoreOrder: true));

            // Other methods followed by GroupBy

            inputs.Add(new LinqTestInput("Select + GroupBy", b => getQuery(b)
                .Select(x => x.Id)
                .GroupBy(k => k /*keySelector*/, (key, values) => key /*return the group by key */)));

            inputs.Add(new LinqTestInput("Select + GroupBy 2", b => getQuery(b)
                .Select(x => new { Id1 = x.Id, family1 = x.FamilyId, childrenN1 = x.Children })
                .GroupBy(k => k.family1 /*keySelector*/, (key, values) => key /*return the group by key */)));

            inputs.Add(new LinqTestInput("SelectMany + GroupBy", b => getQuery(b)
                .SelectMany(x => x.Children)
                .GroupBy(k => k.Grade /*keySelector*/, (key, values) => key /*return the group by key */)));

            inputs.Add(new LinqTestInput("SelectMany + GroupBy 2", b => getQuery(b)
                .SelectMany(f => f.Children)
                .Where(c => c.Pets.Count() > 0)
                .SelectMany(c => c.Pets.Select(p => p.GivenName))
                .GroupBy(k => k /*keySelector*/, (key, values) => key /*return the group by key */)));

            inputs.Add(new LinqTestInput("Skip + GroupBy", b => getQuery(b)
                .Skip(10)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)));

            inputs.Add(new LinqTestInput("Take + GroupBy", b => getQuery(b)
                .Take(10)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)));

            inputs.Add(new LinqTestInput("Skip + Take + GroupBy", b => getQuery(b)
                .Skip(10).Take(10)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)));

            inputs.Add(new LinqTestInput("Filter + GroupBy", b => getQuery(b)
                .Where(x => x.Id != "a")
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)));

            // should this become a subquery with order by then group by?
            inputs.Add(new LinqTestInput("OrderBy + GroupBy", b => getQuery(b)
                .OrderBy(x => x.Int)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)));

            inputs.Add(new LinqTestInput("OrderBy Descending + GroupBy", b => getQuery(b)
                .OrderByDescending(x => x.Id)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)));

            inputs.Add(new LinqTestInput("Combination + GroupBy", b => getQuery(b)
                .Where(x => x.Id != "a")
                .OrderBy(x => x.Id)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)));

            // The result for this is not correct yet - the select clause is wrong
            inputs.Add(new LinqTestInput("Combination 2 + GroupBy", b => getQuery(b)
                .Where(x => x.Id != "a")
                .Where(x => x.Children.Min(y => y.Grade) > 10)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)));

            // GroupBy followed by other methods
            inputs.Add(new LinqTestInput("GroupBy + Select", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)
                .Select(x => x)));

            //We should support skip take
            inputs.Add(new LinqTestInput("GroupBy + Skip", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)
                .Skip(10)));

            inputs.Add(new LinqTestInput("GroupBy + Take", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)
                .Take(10)));

            inputs.Add(new LinqTestInput("GroupBy + Skip + Take", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)
                .Skip(10).Take(10)));

            inputs.Add(new LinqTestInput("GroupBy + Filter", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)
                .Where(x => x == "a")));

            inputs.Add(new LinqTestInput("GroupBy + OrderBy", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)
                .OrderBy(x => x)));

            inputs.Add(new LinqTestInput("GroupBy + OrderBy Descending", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)
                .OrderByDescending(x => x)));

            inputs.Add(new LinqTestInput("GroupBy + Combination", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)
                .Where(x => x == "a").Skip(10).Take(10)));

            inputs.Add(new LinqTestInput("GroupBy + GroupBy", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => key /*return the group by key */)
                .GroupBy(k => k /*keySelector*/, (key, values) => key /*return the group by key */)));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestGroupByMultiValueTranslation()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("GroupBy Multi Value Select Constant", b => getQuery(b).GroupBy(k => k /*keySelector*/,
                                                                                (key, values) =>
                                                                                new {
                                                                                    stringField = "abv",
                                                                                    numField = 123}),
                                                                                ignoreOrder: true));

            inputs.Add(new LinqTestInput("GroupBy Multi Value Select Key", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => new {
                                                                                    Key = key,
                                                                                    key}),
                                                                                ignoreOrder: true));

            inputs.Add(new LinqTestInput("GroupBy Multi Value Select Key and Constant", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => new {
                                                                                    KeyAlias = key,
                                                                                    values = 123 /* intentionally have the same spelling as the IGrouping values */}),
                                                                                ignoreOrder: true));

            inputs.Add(new LinqTestInput("GroupBy Multi Value With Aggregate", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => new {
                                                                                    Min = values.Min(value => value.Int),
                                                                                    Max = values.Max(value => value.Int),
                                                                                    Avg = values.Average(value => value.Int),
                                                                                    Count = values.Count()}),
                                                                                ignoreOrder: true));

            inputs.Add(new LinqTestInput("GroupBy Multi Value With Property Ref and Aggregate", b => getQuery(b).GroupBy(k => k.FamilyId /*keySelector*/,
                                                                              (key, values) => new { 
                                                                                  familyId = key, 
                                                                                  familyIdCount = values.Count()}),
                                                                              ignoreOrder: true));

            // Negative cases 

            // The translation is correct (SELECT VALUE MIN(root) FROM root GROUP BY root["Number"]
            // but the behavior between LINQ and SQL is different
            // In Linq, the queries will return an object on root, where as in CosmosDB, we will return null
            inputs.Add(new LinqTestInput("GroupBy Multi Value With Aggregate On Root", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => new {
                                                                                    Min = values.Min(),
                                                                                    Max = values.Max()}), 
                                                                                ignoreOrder: true,
                                                                                skipVerification: true));

            // Non-aggregate method calls
            inputs.Add(new LinqTestInput("GroupBy Multi Value With Non-Aggregate", b => getQuery(b).GroupBy(k => k.Id /*keySelector*/,
                                                                                (key, values) => new {
                                                                                    valueSelect = values.Select(value => value.Int),
                                                                                    valueOrderBy = values.OrderBy(f => f.FamilyId)
                                                                                })));
            // Other methods followed by GroupBy

            inputs.Add(new LinqTestInput("Select + GroupBy", b => getQuery(b)
                .Select(x => x.Id)
                .GroupBy(k => k /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()}), 
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("Select + GroupBy 2", b => getQuery(b)
                .Select(x => new { Id1 = x.Id, family1 = x.FamilyId, childrenN1 = x.Children })
                .GroupBy(k => k.Id1 /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()}), 
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("SelectMany + GroupBy", b => getQuery(b)
                .SelectMany(x => x.Children)
                .GroupBy(k => k /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()}),
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("Skip + GroupBy", b => getQuery(b)
                .Skip(10)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()}), 
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("Take + GroupBy", b => getQuery(b)
                .Take(10)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()}), 
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("Skip + Take + GroupBy", b => getQuery(b)
                .Skip(10).Take(10)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()}),
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("Filter + GroupBy", b => getQuery(b)
                .Where(x => x.Id != "a")
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()}), 
                    ignoreOrder: true));

            // should this become a subquery with order by then group by?
            inputs.Add(new LinqTestInput("OrderBy + GroupBy", b => getQuery(b)
                .OrderBy(x => x.Int)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()
                })));

            inputs.Add(new LinqTestInput("OrderBy Descending + GroupBy", b => getQuery(b)
                .OrderByDescending(x => x.Id)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()
                })));

            inputs.Add(new LinqTestInput("Combination + GroupBy", b => getQuery(b)
                .Where(x => x.Id != "a")
                .OrderBy(x => x.Id)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()
                })));

            // GroupBy followed by other methods
            inputs.Add(new LinqTestInput("GroupBy + Select", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()})
                .Select(x => x), 
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("GroupBy + Select 2", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()})
                .Select(x => x.keyAlias)));

            //We should support skip take
            inputs.Add(new LinqTestInput("GroupBy + Skip", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()})
                .Skip(10),
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("GroupBy + Take", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()})
                .Take(10), 
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("GroupBy + Skip + Take", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()})
                .Skip(10).Take(10), 
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("GroupBy + Filter", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()})
                .Where(x => x.keyAlias == "a"), 
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("GroupBy + OrderBy", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()})
                .OrderBy(x => x.keyAlias)));

            inputs.Add(new LinqTestInput("GroupBy + OrderBy Descending", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()})
                .OrderByDescending(x => x.count)));

            inputs.Add(new LinqTestInput("GroupBy + Combination", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()})
                .Where(x => x.keyAlias == "a").Skip(10).Take(10), 
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("GroupBy + GroupBy", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()})
                .GroupBy(k => k.count /*keySelector*/, (key, values) => key /*return the group by key */), 
                ignoreOrder: true));

            inputs.Add(new LinqTestInput("GroupBy + GroupBy2", b => getQuery(b)
                .GroupBy(k => k.Id /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    count = values.Count()
                })
                .GroupBy(k => k.count /*keySelector*/, (key, values) => new {
                    keyAlias = key,
                    stringField = "abc"
                }), 
                ignoreOrder: true));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestGroupByMultiKeyTranslation()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();
            
            inputs.Add(new LinqTestInput("GroupBy Multi Key Constant", b => getQuery(b).GroupBy(k =>
                                                                                new
                                                                                {
                                                                                    key1 = k.Id,
                                                                                    key2 = k.FamilyId
                                                                                } /*keySelector*/,
                                                                                (key, values) => key.key1)));

            inputs.Add(new LinqTestInput("GroupBy Multi Key Multi Value", b => getQuery(b).GroupBy(k =>
                                                                                new
                                                                                {
                                                                                    key1 = k.Id,
                                                                                    key2 = k.FamilyId
                                                                                } /*keySelector*/,
                                                                                (key, values) => new
                                                                                {
                                                                                    IdField = key.key1,
                                                                                    FamilyField = key.key2
                                                                                })));

            inputs.Add(new LinqTestInput("GroupBy Multi Key Multi Value No Key Alias", b => getQuery(b).GroupBy(k =>
                                                                                new
                                                                                {
                                                                                    k.Id,
                                                                                    k.FamilyId
                                                                                } /*keySelector*/,
                                                                                (key, values) => new
                                                                                {
                                                                                    IdField = key.Id,
                                                                                    FamilyField = key.FamilyId
                                                                                })));


            inputs.Add(new LinqTestInput("GroupBy Multi Key Multi Value No Value Alias", b => getQuery(b).GroupBy(k =>
                                                                                new
                                                                                {
                                                                                    key1 = k.Id,
                                                                                    key2 = k.FamilyId
                                                                                } /*keySelector*/,
                                                                                (key, values) => new
                                                                                {
                                                                                    key.key1,
                                                                                    key.key2
                                                                                })));

            inputs.Add(new LinqTestInput("GroupBy Multi Key Multi Value With Scalar Expressions Key Selector", b => getQuery(b).GroupBy(k =>
                                                                                new
                                                                                {
                                                                                    key1 = k.Id.Trim(),
                                                                                    key2 = k.FamilyId
                                                                                } /*keySelector*/,
                                                                                (key, values) => new
                                                                                {
                                                                                    IdField = key.key1,
                                                                                    FamilyField = key.key2
                                                                                })));

            inputs.Add(new LinqTestInput("GroupBy Multi Key Multi Value With Aggregate", b => getQuery(b).GroupBy(k =>
                                                                                new
                                                                                {
                                                                                    key1 = k.Id,
                                                                                    key2 = k.FamilyId
                                                                                } /*keySelector*/,
                                                                                (key, values) => new
                                                                                {
                                                                                    IdField = key.key1,
                                                                                    FamilyField = values.Count()
                                                                                })));

            //Other methods followed by GroupBy

            //This one the count is wrong for some reason, need to look into the data set

            inputs.Add(new LinqTestInput("Select + GroupBy", b => getQuery(b)
               .Select(x => x.Id)
               .GroupBy(k => new
               {
                   key1 = k,
                   key2 = k.ToLower()
               } /*keySelector*/,
                   (key, values) => new
                   {
                       keyAlias = key.key1,
                       keyAlias2 = key.key2
                   }),
                   ignoreOrder: true));

            inputs.Add(new LinqTestInput("Select + GroupBy 2", b => getQuery(b)
                .Select(x => new { Id1 = x.Id, family1 = x.FamilyId, childrenN1 = x.Children })
                .GroupBy(k => new
                {
                    key1 = k.Id1,
                    key2 = k.family1
                } /*keySelector*/, (key, values) => new
                {
                    keyAlias = key.key1,
                    count = values.Count(x => x.family1 != "a")
                })));

            inputs.Add(new LinqTestInput("SelectMany + GroupBy", b => getQuery(b)
                .SelectMany(x => x.Children)
                .GroupBy(k => new
                {
                    key1 = k.FamilyName,
                    key2 = k.Gender
                } /*keySelector*/, (key, values) => new
                {
                    ValueKey1 = key.key1,
                    ValueKey2 = key.key2,
                })));

            inputs.Add(new LinqTestInput("Skip + GroupBy", b => getQuery(b)
                .Skip(10)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })));

            inputs.Add(new LinqTestInput("Take + GroupBy", b => getQuery(b)
                .Take(10)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })));

            inputs.Add(new LinqTestInput("Skip + Take + GroupBy", b => getQuery(b)
                .Skip(10).Take(10)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })));

            inputs.Add(new LinqTestInput("Filter + GroupBy", b => getQuery(b)
                .Where(x => x.Id != "a")
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })));

            // should this become a subquery with order by then group by?
            inputs.Add(new LinqTestInput("OrderBy + GroupBy", b => getQuery(b)
                .OrderBy(x => x.Int)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })));

            inputs.Add(new LinqTestInput("OrderBy Descending + GroupBy", b => getQuery(b)
                .OrderByDescending(x => x.Id)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })));

            inputs.Add(new LinqTestInput("Combination + GroupBy", b => getQuery(b)
                .Where(x => x.Id != "a")
                .OrderBy(x => x.Id)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })));

            // GroupBy followed by other methods
            inputs.Add(new LinqTestInput("GroupBy + Select", b => getQuery(b)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })
                .Select(x => x)));

            inputs.Add(new LinqTestInput("GroupBy + Select 2", b => getQuery(b)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })
                .Select(x => x.idField)));

            //We should support skip take
            inputs.Add(new LinqTestInput("GroupBy + Skip", b => getQuery(b)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })
                .Skip(10)));

            inputs.Add(new LinqTestInput("GroupBy + Take", b => getQuery(b)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })
                .Take(10)));

            inputs.Add(new LinqTestInput("GroupBy + Skip + Take", b => getQuery(b)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })
                .Skip(10).Take(10)));

            inputs.Add(new LinqTestInput("GroupBy + Filter", b => getQuery(b)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })
                .Where(x => x.idField == "a")));

            inputs.Add(new LinqTestInput("GroupBy + OrderBy", b => getQuery(b)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })
                .OrderBy(x => x.intField)));

            inputs.Add(new LinqTestInput("GroupBy + OrderBy Descending", b => getQuery(b)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })
                .OrderByDescending(x => x.intField)));

            inputs.Add(new LinqTestInput("GroupBy + Combination", b => getQuery(b)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })
                .Where(x => x.idField == "a").Skip(10).Take(10)));

            inputs.Add(new LinqTestInput("GroupBy + GroupBy", b => getQuery(b)
                .GroupBy(k => new
                {
                    key1 = k.Id,
                    key2 = k.Int
                } /*keySelector*/, (key, values) => new
                {
                    idField = key.key1,
                    intField = key.key2,
                })
                .GroupBy(k => k.idField /*keySelector*/, (key, values) => key /*return the group by key */)));

            inputs.Add(new LinqTestInput("GroupBy Multi Key Constant", b => getQuery(b).GroupBy(k =>
                                                                                new
                                                                                {
                                                                                    key1 = 123,
                                                                                    key2 = "abc"
                                                                                } /*keySelector*/,
                                                                                (key, values) => key.key1)));

            inputs.Add(new LinqTestInput("GroupBy Multi Key Multi Value Constant", b => getQuery(b).GroupBy(k =>
                                                                                new
                                                                                {
                                                                                    key1 = 123,
                                                                                    key2 = "abc"
                                                                                } /*keySelector*/,
                                                                                (key, values) => new
                                                                                {
                                                                                    NumValue = key.key1,
                                                                                    StringValue = key.key2
                                                                                })));
            inputs.Add(new LinqTestInput("GroupBy Multi Key Multi Value Constant 2", b => getQuery(b).GroupBy(k =>
                                                                                new {} /*keySelector*/,
                                                                                (key, values) => new
                                                                                {
                                                                                    NumValue = 10,
                                                                                    StringValue = "abc"
                                                                                })));
            inputs.Add(new LinqTestInput("GroupBy Multi Key Multi Value Constant 2", b => getQuery(b).GroupBy(k =>
                                                                                new { k.Id } /*keySelector*/,
                                                                                (key, values) => new
                                                                                {
                                                                                    NumValue = 10,
                                                                                    StringValue = "abc"
                                                                                })));
            inputs.Add(new LinqTestInput("GroupBy Multi Key Multi Value Constant No Member expression", b => getQuery(b).GroupBy(k =>
                                                                                new NoMemberExampleClass(k.Id) /*keySelector*/,
                                                                                (key, values) => new
                                                                                {
                                                                                    NumValue = 10,
                                                                                    StringValue = "abc"
                                                                                })));
            foreach (LinqTestInput input in inputs)
            {
                input.ignoreOrder = true;
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Ignore]
        public void DebuggingTest()
        {

        }

        [TestMethod]
        [Owner("khdang")]
        [Ignore]
        public void TestSkipTake()
        {
            //TODO
            //V2 using V3 pipeline causing issue on accessing Continuation in CosmosQueryResponseMessageHeaders
            //This will be fine once we convert these test cases to use V3 pipeline

            List<LinqTestInput> inputs = new List<LinqTestInput>();

            // --------------------------------
            // Skip - Take combinations
            // --------------------------------

            inputs.Add(new LinqTestInput(
                "Skip", b => getQuery(b)
                .Skip(3)));

            inputs.Add(new LinqTestInput(
                "Skip -> Take", b => getQuery(b)
                .Skip(1).Take(2)));

            inputs.Add(new LinqTestInput(
                "Skip(negative number) -> Take", b => getQuery(b)
                .Skip(-1).Take(1)));

            inputs.Add(new LinqTestInput(
                "Skip -> Take(negative number)", b => getQuery(b)
                .Skip(1).Take(-2)));

            inputs.Add(new LinqTestInput(
                "Skip -> Skip -> Take", b => getQuery(b)
                .Skip(1).Skip(2).Take(3)));

            inputs.Add(new LinqTestInput(
                "Skip -> Take -> Take", b => getQuery(b)
                .Skip(3).Take(5).Take(3)));

            inputs.Add(new LinqTestInput(
                "Skip -> Take -> Skip -> Take", b => getQuery(b)
                .Skip(1).Take(2).Skip(3).Take(4)));

            inputs.Add(new LinqTestInput(
                "Skip -> Take -> Take -> Skip", b => getQuery(b)
                .Skip(1).Take(2).Take(3).Skip(4)));

            inputs.Add(new LinqTestInput(
                "Skip -> Skip -> Take -> Take", b => getQuery(b)
                .Skip(5).Skip(2).Take(10).Take(3)));

            inputs.Add(new LinqTestInput(
                "Take -> Skip", b => getQuery(b)
                .Take(7).Skip(3)));

            inputs.Add(new LinqTestInput(
                "Take -> Take -> Skip", b => getQuery(b)
                .Take(2).Take(3).Skip(5)));

            inputs.Add(new LinqTestInput(
                "Take -> Skip -> Skip", b => getQuery(b)
                .Take(3).Skip(1).Skip(2)));

            inputs.Add(new LinqTestInput(
                "Take -> Take -> Skip -> Skip", b => getQuery(b)
                .Take(5).Take(3).Skip(1).Skip(1)));

            inputs.Add(new LinqTestInput(
                "Take -> Skip -> Take -> Skip", b => getQuery(b)
                .Take(10).Skip(2).Take(5).Skip(1)));

            inputs.Add(new LinqTestInput(
                "Take -> Skip -> Skip -> Take", b => getQuery(b)
                .Take(10).Skip(4).Skip(2).Take(2)));

            // --------------------------
            // Select + Skip & Take
            // --------------------------

            inputs.Add(new LinqTestInput(
                "Select -> Skip -> Take", b => getQuery(b)
                .Select(f => f.FamilyId).Skip(1).Take(2)));

            inputs.Add(new LinqTestInput(
                "Select -> Take -> Skip", b => getQuery(b)
                .Select(f => f.FamilyId).Take(2).Skip(1)));

            inputs.Add(new LinqTestInput(
                "Select -> Skip -> Take -> Select", b => getQuery(b)
                .Select(f => f.FamilyId).Skip(7).Take(13).Select(f => f.Count())));

            inputs.Add(new LinqTestInput(
                "Skip -> Take -> Select -> Take -> Skip", b => getQuery(b)
                .Skip(5).Take(11).Select(f => f.Children.Count()).Take(7).Skip(3)));

            inputs.Add(new LinqTestInput(
                "Skip -> Select -> Take", b => getQuery(b)
                .Skip(10).Select(f => f.FamilyId).Take(7)));

            inputs.Add(new LinqTestInput(
                "Take -> Select -> Skip", b => getQuery(b)
                .Take(7).Select(f => f.FamilyId).Skip(3)));

            // ------------------------------
            // SelectMany + Skip & Take
            // ------------------------------

            inputs.Add(new LinqTestInput(
                "SelectMany -> Skip -> Take", b => getQuery(b)
                .SelectMany(f => f.Children).Skip(7).Take(5)));

            inputs.Add(new LinqTestInput(
                "SelectMany -> Skip -> Take -> SelectMany", b => getQuery(b)
                .SelectMany(f => f.Children).Skip(11).Take(3).SelectMany(c => c.Pets)));

            inputs.Add(new LinqTestInput(
                "Skip -> SelectMany -> Take -> Skip -> SelectMany", b => getQuery(b)
                .Skip(1).SelectMany(f => f.Children).Take(13).Skip(3).SelectMany(c => c.Pets)));

            inputs.Add(new LinqTestInput(
                "SelectMany -> SelectMany -> Skip -> Take", b => getQuery(b)
                .SelectMany(f => f.Children).SelectMany(c => c.Pets).Skip(3).Take(4)));

            inputs.Add(new LinqTestInput(
                "Take -> Skip -> SelectMany -> Skip -> SelectMany", b => getQuery(b)
                .Take(50).Skip(25).SelectMany(f => f.Children).Skip(10).SelectMany(c => c.Pets)));

            // ---------------------------
            // Where + Skip & Take
            // ---------------------------

            inputs.Add(new LinqTestInput(
                "Where -> Skip -> Take", b => getQuery(b)
                .Where(f => f.IsRegistered).Skip(3).Take(5)));

            inputs.Add(new LinqTestInput(
                "Where -> Skip -> Take -> Where -> Skip -> Take", b => getQuery(b)
                .Where(f => f.Children.Count() > 0).Skip(7).Take(11).Where(f => f.Tags.Count() > 2).Skip(1).Take(3)));

            inputs.Add(new LinqTestInput(
                "Skip -> Take -> Where -> Skip", b => getQuery(b)
                .Skip(1).Take(25).Where(f => f.Int > 10).Skip(5)));

            inputs.Add(new LinqTestInput(
                "Take -> Where -> Skip", b => getQuery(b)
                .Take(25).Where(f => f.FamilyId.Contains("A")).Skip(10)));

            inputs.Add(new LinqTestInput(
                "Where -> Take -> Skip -> Where", b => getQuery(b)
                .Where(f => f.Children.Count() > 1).Take(10).Skip(3).Where(f => f.Int > 0)));

            // ---------------------------
            // OrderBy + Skip & Take
            // ---------------------------

            inputs.Add(new LinqTestInput(
                "OrderBy -> Skip -> Take", b => getQuery(b)
                .OrderBy(f => f.Int).Skip(3).Take(7)));

            inputs.Add(new LinqTestInput(
                "OrderBy -> Skip -> Take -> OrderBy -> Take -> Skip", b => getQuery(b)
                .OrderBy(f => f.Int).Skip(3).Take(11).OrderBy(f => f.FamilyId).Take(7).Skip(1)));

            inputs.Add(new LinqTestInput(
                "Skip -> Take -> OrderBy", b => getQuery(b)
                .Skip(3).Take(43).OrderBy(f => f.IsRegistered)));

            inputs.Add(new LinqTestInput(
                "Take -> OrderByDescending -> Skip", b => getQuery(b)
                .Take(50).OrderByDescending(f => f.Int).Skip(13)));

            inputs.Add(new LinqTestInput(
                "Skip -> OrderByDescending -> Take", b => getQuery(b)
                .Skip(7).OrderByDescending(f => f.Int).Take(17)));

            // ---------------------------
            // Distinct + Skip & Take
            // ---------------------------

            inputs.Add(new LinqTestInput(
                "Distinct -> Skip -> Take", b => getQuery(b)
                .Distinct().Skip(3).Take(7)));

            inputs.Add(new LinqTestInput(
                "Skip -> Take -> Distinct", b => getQuery(b)
                .Skip(3).Take(11).Distinct()));

            inputs.Add(new LinqTestInput(
                "Skip -> Take -> Select -> Distinct -> Skip -> Take -> Distinct -> Skip", b => getQuery(b)
                .Skip(10).Take(60).Select(f => f.Int).Distinct().Skip(15).Take(20).Distinct().Skip(7)));

            inputs.Add(new LinqTestInput(
                "Skip -> Distinct -> Skip -> Take", b => getQuery(b)
                .Skip(7).Distinct().Skip(20).Take(10)));

            inputs.Add(new LinqTestInput(
                "Take -> Skip -> Distinct -> Skip", b => getQuery(b)
                .Take(30).Skip(10).Distinct().Skip(7)));

            // ---------------------------------------------------------------------------------
            // Select, SelectMany, Where, OrderBy, Distinct with Skip & Take in between
            // ---------------------------------------------------------------------------------

            // Select, SelectMany

            inputs.Add(new LinqTestInput(
                "Select(new) -> SelectMany -> Skip -> Take", b => getQuery(b)
                .Select(f => new { f.FamilyId, f.Children }).SelectMany(c => c.Children).Skip(3).Take(10)));

            inputs.Add(new LinqTestInput(
                "SelectMany -> Select -> Skip -> Take", b => getQuery(b)
                .SelectMany(f => f.Children).Select(c => c.GivenName).Skip(7).Take(3)));

            inputs.Add(new LinqTestInput(
                "Skip -> Take -> SelectMany -> Skip -> Select -> Take", b => getQuery(b)
                .Skip(10).Take(20).SelectMany(f => f.Children).Skip(3).Select(c => c.GivenName).Take(5)));

            inputs.Add(new LinqTestInput(
                "Skip -> SelectMany -> Take -> Skip -> Select -> Take", b => getQuery(b)
                .Skip(10).SelectMany(f => f.Children).Take(10).Skip(1).Select(c => c.FamilyName).Take(7)));

            inputs.Add(new LinqTestInput(
                "Take -> SelectMany -> Skip -> Select -> Take", b => getQuery(b)
                .Take(30).SelectMany(f => f.Children).Skip(10).Select(c => c.Grade).Take(3)));

            // Select, Where

            inputs.Add(new LinqTestInput(
                "Select -> Where -> Skip -> Take", b => getQuery(b)
                .Select(f => f.Children).Where(c => c.Count() > 0).Skip(3).Take(7)));

            inputs.Add(new LinqTestInput(
                "Where -> Skip -> Take -> Select -> Skip", b => getQuery(b)
                .Where(f => f.IsRegistered).Skip(7).Take(3).Select(f => f.FamilyId).Skip(2)));

            // Select, OrderBy

            inputs.Add(new LinqTestInput(
                "Select -> Skip -> Take -> OrderBy", b => getQuery(b)
                .Select(f => f.FamilyId).Skip(1).Take(2).OrderBy(s => s)));

            inputs.Add(new LinqTestInput(
                "OrderByDescending -> Take -> Skip -> Select", b => getQuery(b)
                .OrderByDescending(f => f.Int).Take(10).Skip(3).Select(f => f.Int + 1)));

            // Select, Distinct

            inputs.Add(new LinqTestInput(
                "Take -> Select -> Distinct -> Skip -> Take", b => getQuery(b)
                .Take(7).Select(f => f.Int).Distinct().Skip(1).Take(5)));

            // SelectMany, Where

            inputs.Add(new LinqTestInput(
                "SelectMany -> Where -> Skip -> Take", b => getQuery(b)
                .SelectMany(f => f.Children).Where(c => c.Grade > 100).Skip(10).Take(20)));

            inputs.Add(new LinqTestInput(
                "Where -> Skip -> SelectMany -> Take", b => getQuery(b)
                .Where(f => f.IsRegistered).Skip(10).SelectMany(f => f.Children).Take(7)));

            // SelectMany, OrderBy

            inputs.Add(new LinqTestInput(
                "OrderBy -> Skip -> SelectMany -> SelectMany", b => getQuery(b)
                .OrderBy(f => f.Int).Skip(10).SelectMany(f => f.Children).SelectMany(c => c.Pets)));

            inputs.Add(new LinqTestInput(
                "SelectMany -> OrderBy -> Skip -> Take", b => getQuery(b)
                .SelectMany(f => f.Children).OrderBy(c => c.Grade).Skip(10).Take(20)));

            // SelectMany, Distinct

            inputs.Add(new LinqTestInput(
                "SelectMany -> Distinct -> Skip -> Take", b => getQuery(b)
                .SelectMany(f => f.Children).Distinct().Skip(7).Take(11)));

            inputs.Add(new LinqTestInput(
                "Distinct -> Skip -> SelectMany", b => getQuery(b)
                .Distinct().Skip(3).SelectMany(f => f.Children)));

            // Where, OrderBy

            inputs.Add(new LinqTestInput(
                "OrderBy -> Where -> Skip -> Take", b => getQuery(b)
                .OrderBy(f => f.Int).Where(f => f.IsRegistered).Skip(11).Take(3)));

            inputs.Add(new LinqTestInput(
                "Skip -> Where -> Take -> OrderBy", b => getQuery(b)
                .Skip(20).Where(f => f.Children.Count() > 0).Take(10).OrderBy(f => f.Int)));

            // Where, Distinct

            inputs.Add(new LinqTestInput(
                "Where -> Skip -> Take -> Distinct", b => getQuery(b)
                .Where(f => f.IsRegistered).Skip(3).Take(17).Distinct()));

            inputs.Add(new LinqTestInput(
                "Skip -> Distinct -> Where -> Take", b => getQuery(b)
                .Skip(22).Distinct().Where(f => f.Parents.Count() > 0).Take(7)));

            // -----------------------------------------
            // All basic operations with Skip & Take
            // -----------------------------------------

            // Start with Select

            inputs.Add(new LinqTestInput(
                "Select -> Where -> OrderBy -> Skip -> Take", b => getQuery(b)
                .Select(f => f.FamilyId).Where(id => id.Count() > 10).OrderBy(id => id).Skip(1).Take(10)));

            inputs.Add(new LinqTestInput(
                "Select -> Where -> OrderBy -> Skip -> Take -> Select -> Where -> Skip -> Take", b => getQuery(b)
                .Select(f => f.FamilyId).Where(id => id.Count() > 10).OrderBy(id => id).Skip(1).Take(10)
                .Select(id => id + "_suffix").Where(id => id.Count() > 12).Skip(2).Take(4)));

            inputs.Add(new LinqTestInput(
                "Select -> Skip -> OrderBy -> Skip -> Distinct -> Skip", b => getQuery(b)
                .Select(f => f.Records).Skip(5).OrderBy(r => r.Transactions.Count()).Skip(1).Distinct().Skip(3)));

            inputs.Add(new LinqTestInput(
                "Select(new(new(new(new(new))))) -> (Skip -> Select) x 6 -> Skip -> Where -> Take", b => getQuery(b)
                .Select(f => new { f.FamilyId, L1 = new { L2 = new { L3 = new { L4 = new { f.Records } } } } })
                .Skip(1).Select(f => f.L1)
                .Skip(2).Select(f => f.L2)
                .Skip(3).Select(f => f.L3)
                .Skip(4).Select(f => f.L4)
                .Skip(5).Select(f => f.Records)
                .Skip(6).Select(f => f.Transactions)
                .Skip(7).Where(t => t.Count() > 100)
                .Take(20)));

            inputs.Add(new LinqTestInput(
                "Select -> (Skip -> Select -> Where -> OrderBy -> Distinct -> Take) x 3 -> Skip -> Take", b => getQuery(b)
                .Select(f => new { L1 = f })
                .Skip(1).Select(f => f.L1).Where(f => f.Int > 10).OrderBy(f => f.FamilyId).Distinct().Take(100)
                .Skip(2).Select(f => new { L2 = f }).Where(f => f.L2.FamilyId.CompareTo("A") > 0).OrderBy(f => f.L2.Int).Distinct().Take(50)
                .Skip(3).Select(f => f.L2).Where(f => f.Children.Count() > 1).OrderBy(f => f.IsRegistered).Distinct().Take(40)
                .Skip(4).Take(25)));

            // Start with Distinct

            inputs.Add(new LinqTestInput(
                "Distinct -> Select -> Where -> Skip -> Take", b => getQuery(b)
                .Distinct().Select(f => f.Records).Where(r => r.Transactions.Count() > 2).Skip(1).Take(10)));

            // Start with Where

            inputs.Add(new LinqTestInput(
                "Where -> OrderBy -> Skip -> Take", b => getQuery(b)
                .Where(f => f.Int > 10).OrderBy(f => f.FamilyId).Skip(1).Take(5)));

            inputs.Add(new LinqTestInput(
                "Where -> OrderBy -> Skip -> Take -> Select -> Where -> Skip -> Take", b => getQuery(b)
                .Where(f => f.Int > 10).OrderBy(f => f.FamilyId).Skip(1).Take(5)
                .Select(f => f.Records).Where(r => r.Transactions != null).Skip(1).Take(3)));

            inputs.Add(new LinqTestInput(
                "Where -> Skip -> Take -> SelectMany -> OrderBy -> Skip -> Take", b => getQuery(b)
                .Where(f => f.Int > 20).Skip(5).Take(20)
                .SelectMany(f => f.Children).OrderBy(c => c.Grade).Skip(5).Take(10)));

            // Start with OrderBy

            inputs.Add(new LinqTestInput(
                "OrderBy -> Skip -> Take -> Select -> Where -> Skip -> Take", b => getQuery(b)
                .OrderBy(f => f.Int).Skip(1).Take(10)
                .Select(f => f.Records).Where(r => r.Transactions.Count() > 10).Skip(2).Take(3)));

            // Start with Skip

            inputs.Add(new LinqTestInput(
                "Skip -> Take -> Select -> Where -> Skip -> Take", b => getQuery(b)
                .Skip(1).Take(10).Select(f => f.Children.Count()).Where(c => c > 1).Skip(1).Take(4)));

            inputs.Add(new LinqTestInput(
                "Skip -> Take -> Where -> Skip -> Take -> OrderBy -> Skip -> Take -> Select -> Skip -> Distinct -> Skip -> Take", b => getQuery(b)
                .Skip(2).Take(10).Where(f => f.Int > 10)
                .Skip(1).Take(9).OrderBy(f => f.FamilyId)
                .Skip(3).Take(5).Select(f => f.Children.Count())
                .Skip(1).Distinct()
                .Skip(1).Take(1)));

            // Start with Take

            inputs.Add(new LinqTestInput(
                "Take -> Skip -> OrderBy -> Skip -> Take", b => getQuery(b)
                .Take(10).Skip(2).Where(f => f.IsRegistered).OrderBy(f => f.Int).Skip(1).Take(9)));

            inputs.Add(new LinqTestInput(
                "Take -> Distinct -> Select -> Where -> Skip -> Take", b => getQuery(b)
                .Take(10).Distinct().Select(f => f.Children).Where(c => c.Count() > 0).Skip(2).Take(5)));

            // -----------------------------------------------------------------------------
            // All basic operations with Skip & Take with SelectMany in the middle
            // -----------------------------------------------------------------------------

            // SelectMany after Take

            inputs.Add(new LinqTestInput(
                "Select -> Skip -> Take -> SelectMany -> Skip -> Take", b => getQuery(b)
                .Select(f => f.Records).Skip(1).Take(10)
                .SelectMany(r => r.Transactions).Skip(1).Take(9)));

            // SelectMany after Where

            inputs.Add(new LinqTestInput(
                "Where -> SelectMany -> OrderBy -> Skip -> Take", b => getQuery(b)
                .Where(f => f.IsRegistered)
                .SelectMany(f => f.Children).OrderBy(c => c.Grade).Skip(10).Take(20)));

            // Start with SelectMany

            inputs.Add(new LinqTestInput(
                "SelectMany -> Select -> Skip -> Take -> Select -> Where -> Skip -> Take", b => getQuery(b)
                .SelectMany(f => f.Children)
                    .Select(c => c.FamilyName).Skip(1).Take(20)
                    .Select(n => n.Count()).Where(l => l > 10).Skip(2).Take(50)));

            // -------------------------------------------------------------
            // Nested Skip & Take in Select, SelectMany, Where, OrderBy
            // -------------------------------------------------------------

            // Nested with Select

            inputs.Add(new LinqTestInput(
                "Select(new(Where -> Skip)) -> Where", b => getQuery(b)
                .Select(f => new {
                    id = f.FamilyId,
                    GoodChildren = f.Children.Where(c => c.Grade > 75).Skip(1)
                })
                .Where(f => f.GoodChildren.Count() > 0)));

            inputs.Add(new LinqTestInput(
                "Select(Skip -> Take) -> Skip -> Take", b => getQuery(b)
                .Select(f => f.Children.Skip(1).Take(1)).Skip(3).Take(10)));

            inputs.Add(new LinqTestInput(
                "Select(new(Skip -> Take, Skip, Take) -> Skip -> Take -> Where", b => getQuery(b)
                .Select(f => new {
                    v0 = f.Children.Skip(1).Take(2),
                    v1 = f.Parents.Skip(1),
                    v2 = f.Records.Transactions.Take(10)
                })
                .Skip(1).Take(20).Where(f => f.v0.Count() > 0).Take(10)));

            inputs.Add(new LinqTestInput(
                "Select(new(SelectMany -> SelectMany -> Distinct, OrderByDescending -> Take -> SelectMany -> Skip, Select -> OrderBy -> Skip -> Take -> Sum) -> Where -> Skip -> Take -> SelectMany -> Skip -> Take", b => getQuery(b)
                .Select(f => new {
                    v0 = f.Children.SelectMany(c => c.Pets.Skip(1).SelectMany(p => p.GivenName).Distinct()),
                    v1 = f.Children.OrderByDescending(c => c.Grade).Take(2).SelectMany(c => c.Pets.Select(p => p.GivenName).Skip(2)),
                    v2 = f.Records.Transactions.Select(t => t.Amount).OrderBy(a => a).Skip(10).Take(20).Sum()
                })
                .Where(f => f.v2 > 100).Skip(5).Take(20)
                .SelectMany(f => f.v1).Distinct().Skip(3).Take(22)));

            inputs.Add(new LinqTestInput(
                "Select -> Skip -> SelectMany(Skip -> OrderBy)", b => getQuery(b)
                .Select(f => f.Records).Skip(1).SelectMany(r => r.Transactions.Skip(10).OrderBy(t => t.Date))));

            // Nested with Where

            inputs.Add(new LinqTestInput(
                "Where -> Where(Skip -> Take -> Count) - > Skip -> Take", b => getQuery(b)
                .Where(f => f.Children.Count() > 2).Where(f => f.Children.Skip(1).Take(10).Count() > 1).Skip(1).Take(10)));

            // Nested with OrderBy

            inputs.Add(new LinqTestInput(
                "OrderBy(Skip -> Take -> Count) -> Skip -> Take", b => getQuery(b)
                .OrderBy(f => f.Children.Skip(1).Take(5).Count())));

            // Nested with SelectMany

            inputs.Add(new LinqTestInput(
                "SelectMany(Skip -> Take) -> Skip -> Take", b => getQuery(b)
                .SelectMany(f => f.Children.Skip(1).Take(2)).Skip(3).Take(20)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Skip -> Take) -> Skip -> Take -> SelectMany(Skip -> Take) -> Skip -> Take", b => getQuery(b)
                .SelectMany(f => f.Children.Skip(1).Take(2)).Skip(3).Take(20)
                .SelectMany(c => c.Pets.Skip(1).Take(2)).Skip(1).Take(10)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Where -> OrderBy -> Skip -> Distinct)", b => getQuery(b)
                .SelectMany(f => f.Children.Where(c => c.Grade > 10).OrderBy(c => c.Grade).Skip(1).Distinct())));

            inputs.Add(new LinqTestInput(
                "SelectMany(Skip -> SelectMany(Skip -> Select) -> Take)", b => getQuery(b)
                .SelectMany(f => f.Children.Skip(1).SelectMany(c => c.Pets.Skip(1).Select(p => p.GivenName)).Take(10))));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Ignore]
        public void TestUnsupportedScenarios()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();

            // --------------------
            // Dictionary type
            // --------------------

            // Iterating through Dictionary type
            inputs.Add(new LinqTestInput("Iterating through Dictionary type", b => getQuery(b).Select(f => f.Children.Select(c => c.Things.Select(t => t.Key.Count() + t.Value.Count())))));

            // Get a count of a Dictionary type
            inputs.Add(new LinqTestInput("Getting Dictionary count", b => getQuery(b).Select(f => f.Children.Select(c => c.Things.Count()))));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestOrderByTranslation()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();

            // Ascending
            inputs.Add(new LinqTestInput("Select -> order by", b => getQuery(b).Select(family => family.FamilyId).OrderBy(id => id)));
            inputs.Add(new LinqTestInput("Select -> order by -> Select", b => getQuery(b).Select(family => family.FamilyId).OrderBy(id => id).Select(x => x.Count())));
            inputs.Add(new LinqTestInput("Where -> OrderBy -> Select query",
                b => from f in getQuery(b)
                     where f.Int == 5 && f.NullableInt != null
                     orderby f.IsRegistered
                     select f.FamilyId));
            inputs.Add(new LinqTestInput("Where -> OrderBy -> Select", b => getQuery(b).Where(f => f.Int == 5 && f.NullableInt != null).OrderBy(f => f.IsRegistered).Select(f => f.FamilyId)));
            inputs.Add(new LinqTestInput("OrderBy query",
                b => from f in getQuery(b)
                     orderby f.FamilyId
                     select f));
            inputs.Add(new LinqTestInput("OrderBy", b => getQuery(b).OrderBy(f => f.FamilyId)));
            inputs.Add(new LinqTestInput("OrderBy -> Select query",
                b => from f in getQuery(b)
                     orderby f.FamilyId
                     select f.FamilyId));
            inputs.Add(new LinqTestInput("OrderBy -> Select", b => getQuery(b).OrderBy(f => f.FamilyId).Select(f => f.FamilyId)));
            inputs.Add(new LinqTestInput("OrderBy -> Select -> Take", b => getQuery(b).OrderBy(f => f.FamilyId).Select(f => f.FamilyId).Take(10)));
            inputs.Add(new LinqTestInput("OrderBy -> Select -> Select", b => getQuery(b).OrderBy(f => f.FamilyId).Select(f => f.FamilyId).Select(x => x)));

            // Descending
            inputs.Add(new LinqTestInput("Select -> order by", b => getQuery(b).Select(family => family.FamilyId).OrderByDescending(id => id)));
            inputs.Add(new LinqTestInput("Select -> order by -> Select", b => getQuery(b).Select(family => family.FamilyId).OrderByDescending(id => id).Select(x => x.Count())));
            inputs.Add(new LinqTestInput("Where -> OrderBy Desc -> Select query",
                b => from f in getQuery(b)
                     where f.Int == 5 && f.NullableInt != null
                     orderby f.IsRegistered descending
                     select f.FamilyId));
            inputs.Add(new LinqTestInput("Where -> OrderBy Desc -> Select", b => getQuery(b).Where(f => f.Int == 5 && f.NullableInt != null).OrderByDescending(f => f.IsRegistered).Select(f => f.FamilyId)));
            inputs.Add(new LinqTestInput("OrderBy Desc query",
                b => from f in getQuery(b)
                     orderby f.FamilyId descending
                     select f));
            inputs.Add(new LinqTestInput("OrderBy Desc", b => getQuery(b).OrderByDescending(f => f.FamilyId)));
            inputs.Add(new LinqTestInput("OrderBy Desc -> Select query",
                b => from f in getQuery(b)
                     orderby f.FamilyId descending
                     select f.FamilyId));
            inputs.Add(new LinqTestInput("OrderBy Desc -> Select", b => getQuery(b).OrderByDescending(f => f.FamilyId).Select(f => f.FamilyId)));
            inputs.Add(new LinqTestInput("OrderBy -> Select -> Take", b => getQuery(b).OrderByDescending(f => f.FamilyId).Select(f => f.FamilyId).Take(10)));
            inputs.Add(new LinqTestInput("OrderBy -> Select -> Select", b => getQuery(b).OrderByDescending(f => f.FamilyId).Select(f => f.FamilyId).Select(x => x)));
            inputs.Add(new LinqTestInput("OrderBy multiple expressions",
                b => from f in getQuery(b)
                     orderby f.FamilyId, f.Int
                     select f.FamilyId));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestLambdaReuse()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();

            System.Linq.Expressions.Expression<Func<Family, bool>> predicate = f => f.Int == 5;
            inputs.Add(new LinqTestInput("Where -> Where with same predicate instance", b => getQuery(b).Where(predicate).Where(predicate)));
            inputs.Add(new LinqTestInput("Where -> Select with same predicate instance", b => getQuery(b).Where(predicate).Select(predicate)));

            System.Linq.Expressions.Expression<Func<object, bool>> predicate2 = f => f.ToString() == "a";
            inputs.Add(new LinqTestInput("Where -> Select -> Where with same predicate instance",
                b => getQuery(b)
                    .Where(predicate2)
                    .Select(c => new { Int = 10, Result = c })
                    .Where(predicate2)));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestThenByTranslation()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();

            // Ascending and descending

            inputs.Add(new LinqTestInput(
                "OrderBy -> ThenBy", b => getQuery(b)
                .OrderBy(f => f.FamilyId)
                .ThenBy(f => f.Int)));

            inputs.Add(new LinqTestInput(
                "OrderBy -> ThenByDescending", b => getQuery(b)
                .OrderBy(f => f.FamilyId)
                .ThenByDescending(f => f.Int)));

            inputs.Add(new LinqTestInput(
                "OrderByDescending -> ThenBy", b => getQuery(b)
                .OrderByDescending(f => f.FamilyId)
                .ThenBy(f => f.Int)));

            inputs.Add(new LinqTestInput(
                "OrderByDescending -> ThenByDescending", b => getQuery(b)
                .OrderByDescending(f => f.FamilyId)
                .ThenByDescending(f => f.Int)));

            // Subquery in OrderBy or in ThenBy

            inputs.Add(new LinqTestInput(
                "OrderBy subquery -> ThenBy", b => getQuery(b)
                .OrderBy(f => f.Children.Where(c => c.Grade > 100).Count())
                .ThenByDescending(f => f.Int)));

            inputs.Add(new LinqTestInput(
                "OrderBy -> ThenBy subquery", b => getQuery(b)
                .OrderBy(f => f.FamilyId)
                .ThenByDescending(f => f.Parents.Where(p => p.GivenName.Length > 10).Count())));

            inputs.Add(new LinqTestInput(
                "OrderBy subquery -> ThenBy subquery", b => getQuery(b)
                .OrderByDescending(f => f.Children.Where(c => c.Grade > 100).Count())
                .ThenBy(f => f.Parents.Where(p => p.GivenName.Length > 10).Count())));

            // OrderBy and ThenBy by the same property (not a realistic scenario)

            inputs.Add(new LinqTestInput(
                "OrderBy -> ThenBy", b => getQuery(b)
                .OrderBy(f => f.FamilyId)
                .ThenBy(f => f.FamilyId)));

            inputs.Add(new LinqTestInput(
                "OrderBy -> ThenByDescending", b => getQuery(b)
                .OrderBy(f => f.FamilyId)
                .ThenByDescending(f => f.FamilyId)));

            inputs.Add(new LinqTestInput(
                "OrderByDescending subquery -> ThenBy subquery", b => getQuery(b)
                .OrderByDescending(f => f.Children.Where(c => c.Grade > 100).Count())
                .ThenBy(f => f.Children.Where(c => c.Grade > 100).Count())));

            // OrderBy and ThenBy with epxressions

            inputs.Add(new LinqTestInput(
                "OrderBy -> ThenBy", b => getQuery(b)
                .OrderBy(f => f.Int * 2)
                .ThenBy(f => f.FamilyId.Substring(2, 3))));

            inputs.Add(new LinqTestInput(
                "OrderBy -> ThenBy", b => getQuery(b)
                .OrderBy(f => !f.NullableInt.IsDefined())
                .ThenByDescending(f => f.Tags.Count() % 2)));

            inputs.Add(new LinqTestInput(
                "OrderBy -> ThenBy", b => getQuery(b)
                .OrderByDescending(f => f.IsRegistered ? f.FamilyId : f.Int.ToString())
                .ThenBy(f => f.Records.Transactions.Max(t => t.Amount) % 1000)));

            // Multiple OrderBy and ThenBy

            inputs.Add(new LinqTestInput(
                "OrderBy -> OrderBy -> ThenBy", b => getQuery(b)
                .OrderByDescending(f => f.FamilyId)
                .OrderBy(f => f.Int)
                .ThenByDescending(f => f.IsRegistered)));

            inputs.Add(new LinqTestInput(
                "OrderBy -> ThenBy -> ThenBy", b => getQuery(b)
                .OrderBy(f => f.FamilyId)
                .ThenBy(f => f.Int)
                .ThenByDescending(f => f.IsRegistered)));

            inputs.Add(new LinqTestInput(
                "OrderBy -> Orderby subquery -> ThenBy subquery", b => getQuery(b)
                .OrderBy(f => f.FamilyId)
                .OrderBy(f => f.Children.Where(c => c.Grade > 100).Count())
                .ThenBy(f => f.Parents.Where(p => p.GivenName.Length > 10).Count())));

            inputs.Add(new LinqTestInput(
                "OrderBy -> ThenBy subquery -> ThenBy subquery", b => getQuery(b)
                .OrderBy(f => f.FamilyId)
                .ThenByDescending(f => f.Parents.Where(p => p.GivenName.Length > 10).Count())
                .ThenBy(f => f.Children.Where(c => c.Grade > 100).Count())));

            // Nested ThenBy

            inputs.Add(new LinqTestInput(
                "OrderBy -> ThenBy(OrderBy -> ThenBy -> Select)", b => getQuery(b)
                .OrderBy(f => f.FamilyId)
                .ThenByDescending(f => f.Parents
                    .OrderBy(p => p.FamilyName)
                    .ThenByDescending(p => p.GivenName)
                    .Select(p => p.FamilyName + p.GivenName))));

            inputs.Add(new LinqTestInput(
                "OrderBy(OrderBy -> ThenBy) -> ThenBy(OrderBy -> ThenBy)", b => getQuery(b)
                .OrderBy(f => f.Children
                    .OrderBy(c => c.Grade)
                    .ThenBy(c => c.Pets.Count))
                .ThenByDescending(f => f.Parents
                    .OrderBy(p => p.FamilyName)
                    .ThenByDescending(p => p.GivenName)
                    .Select(p => p.FamilyName + p.GivenName))));

            // Nested expressions with ThenBy

            inputs.Add(new LinqTestInput(
                "OrderBy -> ThenBy(OrderBy -> ThenBy -> Take -> Select)", b => getQuery(b)
                .OrderBy(f => f.FamilyId)
                .ThenByDescending(f => f.Parents
                    .OrderBy(p => p.FamilyName)
                    .ThenByDescending(p => p.GivenName)
                    .Take(1)
                    .Select(p => p.FamilyName + p.GivenName))));

            inputs.Add(new LinqTestInput(
                "OrderBy(OrderBy -> ThenBy -> Take -> OrderBy -> ThenBy) -> ThenBy(OrderBy -> ThenBy)", b => getQuery(b)
                .OrderBy(f => f.Children
                    .OrderBy(c => c.Grade)
                    .ThenByDescending(c => c.Pets.Count)
                    .Take(10)
                    .OrderByDescending(c => c.GivenName)
                    .ThenBy(c => c.Gender))
                .ThenByDescending(f => f.Parents
                    .OrderBy(p => p.FamilyName)
                    .ThenByDescending(p => p.GivenName)
                    .Select(p => p.FamilyName + p.GivenName))));

            // On a new object

            inputs.Add(new LinqTestInput(
                "Select -> OrderBy -> ThenBy", b => getQuery(b)
                .Select(f => new
                {
                    f.FamilyId,
                    FamilyNumber = f.Int,
                    ChildrenCount = f.Children.Count(),
                    ChildrenPetCount = f.Children.Select(c => c.Pets.Count()).Sum()
                })
                .OrderBy(r => r.FamilyId)
                .ThenBy(r => r.FamilyNumber)));

            inputs.Add(new LinqTestInput(
                "SelectMany -> OrderBy -> ThenBy", b => getQuery(b)
                .SelectMany(f => f.Children.Select(c => new
                {
                    f.FamilyId,
                    FamilyNumber = f.Int,
                    ChildrenCount = f.Children.Count(),
                    Name = c.GivenName,
                    SpecialPetCount = c.Pets.Where(p => p.GivenName.Length > 5).Count()
                })
                .OrderBy(r => r.FamilyId)
                .ThenBy(r => r.FamilyNumber))));

            inputs.Add(new LinqTestInput(
                "SelectMany -> OrderBy -> ThenBy", b => getQuery(b)
                .SelectMany(f => f.Children.Select(c => new
                {
                    f.FamilyId,
                    FamilyNumber = f.Int,
                    ChildrenCount = f.Children.Count(),
                    Name = c.GivenName,
                    SpecialPetCount = c.Pets.Where(p => p.GivenName.Length > 5).Count()
                }))
                .OrderBy(r => r.FamilyId)
                .ThenBy(r => r.FamilyNumber)
                .Select(r => r.FamilyId)));

            inputs.Add(new LinqTestInput(
                "Select(new(Where, Sum) -> OrderBy(Count) -> ThenBy)", b => getQuery(b)
                .Select(f => new {
                    ChildrenWithPets = f.Children.Where(c => c.Pets.Count() > 0),
                    TotalExpenses = f.Records.Transactions.Sum(t => t.Amount)
                })
                .OrderByDescending(r => r.ChildrenWithPets.Count())
                .ThenByDescending(r => r.TotalExpenses)));

            inputs.Add(new LinqTestInput(
                "Select(new(Min, Count, SelectMany->Select->Distinct->Count)) -> OrderByDescending -> ThenBy", b => getQuery(b)
                .Select(f => new {
                    ParentGivenName = f.Parents.Min(p => p.GivenName),
                    ParentCount = f.Parents.Count(),
                    GoodChildrenCount = f.Children.Where(c => c.Grade > 95).Count(),
                    UniquePetsNameCount = f.Children.SelectMany(c => c.Pets).Select(p => p.GivenName).Distinct().Count()
                })
                .OrderByDescending(r => r.GoodChildrenCount)
                .ThenBy(r => r.UniquePetsNameCount)));

            // With other LINQ operators: Where, SelectMany, Distinct, Skip, Take, and aggregates

            inputs.Add(new LinqTestInput(
                "Where -> OrderBy -> ThenBy", b => getQuery(b)
                .Where(f => f.Children.Count() > 0)
                .OrderBy(f => f.IsRegistered)
                .ThenByDescending(f => f.Int)));

            inputs.Add(new LinqTestInput(
                "Where -> SelectMany -> OrderBy -> ThenBy -> Take", b => getQuery(b)
                .Where(f => f.Children.Count() > 0)
                .SelectMany(f => f.Children)
                .OrderBy(c => c.Grade)
                .ThenByDescending(c => c.Pets.Count())
                .Take(3)));

            inputs.Add(new LinqTestInput(
                "Where -> SelectMany -> OrderBy -> ThenBy -> Skip -> Take -> Where -> Select -> Distinct", b => getQuery(b)
                .Where(f => f.Children.Count() > 0)
                .SelectMany(f => f.Children)
                .OrderByDescending(c => c.Grade)
                .ThenBy(c => c.GivenName)
                .Skip(2)
                .Take(20)
                .Where(c => c.Pets.Where(p => p.GivenName.Length > 10).Count() > 0)
                .Select(c => c.GivenName)
                .Distinct()));

            inputs.Add(new LinqTestInput(
                "Where -> SelectMany -> OrderBy -> ThenBy -> Select => Distinct => Take => OrderBy", b => getQuery(b)
                .Where(f => f.Children.Count() > 0)
                .SelectMany(f => f.Children)
                .OrderBy(c => c.Grade)
                .ThenByDescending(c => c.Pets.Count())
                .Select(c => c.GivenName)
                .Distinct()
                .Take(10)
                .Skip(5)
                .OrderBy(n => n.Length)));

            inputs.Add(new LinqTestInput(
                "Where -> SelectMany -> OrderBy -> ThenBy -> Take", b => getQuery(b)
                .Where(f => f.Children.Count() > 0)
                .SelectMany(f => f.Records.Transactions)
                .OrderBy(t => t.Type)
                .ThenBy(t => t.Amount)
                .Take(100)));

            inputs.Add(new LinqTestInput(
                "Take -> OrderBy -> ThenBy", b => getQuery(b)
                .Take(100)
                .OrderBy(f => f.IsRegistered)
                .ThenByDescending(f => f.Int)));

            inputs.Add(new LinqTestInput(
                "Take -> OrderBy -> ThenBy -> Skip", b => getQuery(b)
                .Take(100)
                .OrderBy(f => f.IsRegistered)
                .ThenByDescending(f => f.Int)
                .Skip(5)));

            inputs.Add(new LinqTestInput(
                "Distinct -> OrderBy -> ThenBy", b => getQuery(b)
                .Distinct()
                .OrderBy(f => f.IsRegistered)
                .ThenByDescending(f => f.Int)));

            inputs.Add(new LinqTestInput(
                "Where -> SelectMany(Select(new Where->Count)) -> Distinct -> OrderBy -> ThenBy", b => getQuery(b)
                .Where(f => f.Children.Count() > 0)
                .SelectMany(f => f.Children.Select(c => new {
                    Name = c.GivenName,
                    PetWithLongNames = c.Pets.Where(p => p.GivenName.Length > 8).Count()
                }))
                .Distinct()
                .OrderByDescending(r => r.Name)
                .ThenBy(r => r.PetWithLongNames)));

            inputs.Add(new LinqTestInput(
                "OrderBy(Any) -> ThenBy(Any)", b => getQuery(b)
                .OrderBy(f => f.Children.Any(c => c.Grade > 90))
                .ThenByDescending(f => f.Parents.Any(p => p.GivenName.Length > 10))));

            inputs.Add(new LinqTestInput(
                "OrderBy(Min) -> ThenBy(Max) -> ThenBy(Sum) -> ThenBy(Avg)", b => getQuery(b)
                .OrderBy(f => f.Children.Min(c => c.GivenName))
                .ThenByDescending(f => f.Parents.Max(p => p.GivenName))
                .ThenBy(f => f.Records.Transactions.Sum(t => t.Amount))
                .ThenByDescending(f => f.Records.Transactions.Average(t => t.Amount))));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Ignore]
        public void TestDistinctSelectManyIssues()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();

            // these tests need a fix in the ServiceInterop
            inputs.Add(new LinqTestInput(
                "Distinct -> OrderBy -> Take",
                b => getQuery(b).Select(f => f.Int).Distinct().OrderBy(x => x).Take(10)));

            inputs.Add(new LinqTestInput(
                "OrderBy -> Distinct -> Take",
                b => getQuery(b).Select(f => f.Int).OrderBy(x => x).Distinct().Take(10)));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestDistinctTranslation()
        {
            static LinqTestInput DistinctTestInput(string description, System.Linq.Expressions.Expression<Func<bool, IQueryable>> expr)
            {
                return new LinqTestInput(description, expr, true);
            }

            List<LinqTestInput> inputs = new List<LinqTestInput>();

            // Simple distinct
            // Select -> Distinct for all data types
            inputs.Add(DistinctTestInput(
                "Distinct string",
                b => getQuery(b).Select(f => f.FamilyId).Distinct()));

            inputs.Add(DistinctTestInput(
                "Distinct int",
                b => getQuery(b).Select(f => f.Int).Distinct()));

            inputs.Add(DistinctTestInput(
                "Distinct bool",
                b => getQuery(b).Select(f => f.IsRegistered).Distinct()));

            inputs.Add(DistinctTestInput(
                "Distinct nullable int",
                b => getQuery(b).Where(f => f.NullableInt != null).Select(f => f.NullableInt).Distinct()));

            inputs.Add(DistinctTestInput(
                "Distinct null",
                b => getQuery(b).Where(f => f.NullObject != null).Select(f => f.NullObject).Distinct()));

            inputs.Add(DistinctTestInput(
                "Distinct children",
                b => getQuery(b).SelectMany(f => f.Children).Distinct()));

            inputs.Add(DistinctTestInput(
                "Distinct parent",
                b => getQuery(b).SelectMany(f => f.Parents).Distinct()));

            inputs.Add(DistinctTestInput(
                "Distinct family",
                b => getQuery(b).Distinct()));

            inputs.Add(DistinctTestInput(
                "Multiple distincts",
                b => getQuery(b).Distinct().Distinct()));

            inputs.Add(DistinctTestInput(
                "Distinct new obj",
                b => getQuery(b).Select(f => new { Parents = f.Parents.Count(), Children = f.Children.Count() }).Distinct()));

            inputs.Add(DistinctTestInput(
                "Distinct new obj",
                b => getQuery(b).Select(f => new { Parents = f.Parents.Count(), Children = f.Children.Count() }).Select(f => f.Parents).Distinct()));

            // Distinct + Take
            inputs.Add(DistinctTestInput(
                "Distinct -> Take",
                b => getQuery(b).Select(f => f.Int).Distinct().Take(10)));

            inputs.Add(new LinqTestInput(
                "Take -> Distinct",
                b => getQuery(b).Select(f => f.Int).Take(10).Distinct()));

            // Distinct + Order By
            inputs.Add(new LinqTestInput(
                "Distinct -> OrderBy",
                b => getQuery(b).Select(f => f.Int).Distinct().OrderBy(x => x)));

            inputs.Add(DistinctTestInput(
                "OrderBy -> Distinct",
                b => getQuery(b).OrderBy(f => f.Int).Distinct()));

            // Distinct + Order By + Take
            inputs.Add(new LinqTestInput(
                "Distinct -> Take -> OrderBy",
                b => getQuery(b).Select(f => f.Int).Distinct().Take(10).OrderBy(x => x)));

            inputs.Add(new LinqTestInput(
                "OrderBy -> Take -> Distinct",
                b => getQuery(b).Select(f => f.Int).OrderBy(x => x).Take(10).Distinct()));

            // Distinct + Where
            inputs.Add(DistinctTestInput(
                "Where -> Distinct",
                b => getQuery(b).Select(f => f.Int).Where(x => x > 10).Distinct()));

            inputs.Add(DistinctTestInput(
                "Distinct -> Where",
                b => getQuery(b).Select(f => f.Int).Distinct().Where(x => x > 10)));

            // SelectMany w Distinct
            inputs.Add(DistinctTestInput(
                "SelectMany(Select obj) -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents).Distinct()));

            inputs.Add(DistinctTestInput(
                "SelectMany(SelectMany(Where -> Select)) -> Distinct",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => pet)))
                    .Distinct()));

            inputs.Add(DistinctTestInput(
                "SelectMany(SelectMany(Where -> Select -> Distinct))",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => pet)
                    .Distinct()))));

            inputs.Add(DistinctTestInput(
                "SelectMany(SelectMany(Where -> Select -> Select) -> Distinct)",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => pet.GivenName)
                        .Select(name => name.Count())))
                    .Distinct()));

            inputs.Add(DistinctTestInput(
                "SelectMany(SelectMany(Where -> Select new {} -> Select) -> Distinct)",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => new
                        {
                            family = family.FamilyId,
                            child = child.GivenName,
                            pet = pet.GivenName
                        }).Select(p => p.child))
                    .Distinct())));

            inputs.Add(DistinctTestInput(
                "SelectMany(SelectMany(Where -> Select new {}) -> Distinct)",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => new
                        {
                            family = family.FamilyId,
                            child = child.GivenName,
                            pet = pet.GivenName
                        })))
                    .Distinct()));

            inputs.Add(DistinctTestInput(
                "SelectMany(SelectMany(Where -> Select new {} -> Distinct))",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => new
                        {
                            family = family.FamilyId,
                            child = child.GivenName,
                            pet = pet.GivenName
                        })
                        .Distinct()))));

            // SelectMany(Distinct)
            inputs.Add(DistinctTestInput(
                "SelectMany(Distinct) -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Distinct()));

            inputs.Add(DistinctTestInput(
                "SelectMany(Distinct) -> Where",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)));

            inputs.Add(DistinctTestInput(
                "SelectMany(Distinct) -> Where -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> OrderBy",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> OrderBy -> Where",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f).Where(f => f.FamilyName.Count() < 20)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> OrderBy -> Where -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f).Where(f => f.FamilyName.Count() < 20).Take(5)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> OrderBy -> Where -> Take -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f).Where(f => f.FamilyName.Count() < 20).Take(5).Distinct()));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> OrderBy -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f).Take(5)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> OrderBy -> Take -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f).Take(5).Distinct()));

            inputs.Add(DistinctTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct().Take(5)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> Take -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .Take(5).Distinct()));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> OrderBy",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .OrderBy(f => f)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> OrderBy -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .OrderBy(f => f).Distinct()));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> OrderBy -> Distinct -> OrderBy",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .OrderBy(f => f).Distinct().OrderBy(f => f.GivenName.Length)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> OrderBy -> Distinct -> OrderBy -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .OrderBy(f => f).Distinct().OrderBy(f => f.GivenName.Length).Take(5)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> OrderBy -> Distinct -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .OrderBy(f => f).Take(5)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> OrderBy -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .OrderBy(f => f).Take(5)));

            inputs.Add(DistinctTestInput(
                "SelectMany(Distinct) -> Where -> Where",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .Where(f => f.FamilyName.Count() < 20)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> OrderBy",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).OrderBy(f => f.FamilyName)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> OrderBy -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).OrderBy(f => f.FamilyName).Take(5)));

            inputs.Add(DistinctTestInput(
                "SelectMany(Distinct) -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Take(5)));

            inputs.Add(DistinctTestInput(
                "SelectMany(Distinct) -> Select",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Select(f => f.FamilyName.Count())));

            inputs.Add(DistinctTestInput(
                "SelectMany(Distinct) -> Select -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Select(f => f.FamilyName.Count()).Take(5)));

            // SelectMany(Select -> Distinct)
            inputs.Add(new LinqTestInput(
                "SelectMany(Select -> Distinct) -> OrderBy -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.GivenName).Distinct()).OrderBy(n => n).Take(5)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select -> Distinct) -> Where -> OrderBy -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.GivenName).Distinct()).Where(n => n.Count() > 10).OrderBy(n => n).Take(5)));

            inputs.Add(DistinctTestInput(
                "SelectMany(Select) -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName)).Distinct()));

            inputs.Add(DistinctTestInput(
                "SelectMany(Select -> Distinct)",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName).Distinct())));

            inputs.Add(DistinctTestInput(
                "SelectMany(Select -> Distinct) -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.GivenName).Distinct()).Distinct()));

            inputs.Add(DistinctTestInput(
                "SelectMany(Select -> Distinct) -> Where",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.GivenName).Distinct()).Where(n => n.Count() > 10)));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void ValidateDynamicLinq()
        {
            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Select", b => getQuery(b).Select("FamilyId")));
            inputs.Add(new LinqTestInput("Where", b => getQuery(b).Where("FamilyId = \"some id\"")));
            inputs.Add(new LinqTestInput("Where longer", b => getQuery(b).Where("FamilyId = \"some id\" AND IsRegistered = True OR Int > 101")));
            // with parameters
            inputs.Add(new LinqTestInput("Where w/ parameters", b => getQuery(b).Where("FamilyId = @0 AND IsRegistered = @1 OR Int > @2", "some id", true, 101)));
            inputs.Add(new LinqTestInput("Where -> Select", b => getQuery(b).Where("FamilyId = \"some id\"").Select("Int")));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public async Task ValidateLinqQueries()
        {
            Container container = await testDb.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString("N"), partitionKeyPath: "/id"));

            Parent mother = new Parent { FamilyName = "Wakefield", GivenName = "Robin" };
            Parent father = new Parent { FamilyName = "Miller", GivenName = "Ben" };
            Pet pet = new Pet { GivenName = "Fluffy" };
            Child child = new Child
            {
                FamilyName = "Merriam",
                GivenName = "Jesse",
                Gender = "female",
                Grade = 1,
                Pets = new List<Pet>() { pet, new Pet() { GivenName = "koko" } },
                Things = new Dictionary<string, string>() { { "A", "B" }, { "C", "D" } }
            };

            Address address = new Address { State = "NY", County = "Manhattan", City = "NY" };
            Family family = new Family { FamilyId = "WakefieldFamily", Parents = new Parent[] { mother, father }, Children = new Child[] { child }, IsRegistered = false, Int = 3, NullableInt = 5, Id = "WakefieldFamily" };

            List<Family> fList = new List<Family>();
            fList.Add(family);

            container.CreateItemAsync<Family>(family).Wait();
            IOrderedQueryable<Family> query = container.GetItemLinqQueryable<Family>(allowSynchronousQueryExecution: true);

            IEnumerable<string> q1 = query.Select(f => f.Parents[0].FamilyName);
            Assert.AreEqual(q1.FirstOrDefault(), family.Parents[0].FamilyName);

            IEnumerable<int> q2 = query.Select(f => f.Children[0].Grade + 13);
            Assert.AreEqual(q2.FirstOrDefault(), family.Children[0].Grade + 13);

            IEnumerable<Family> q3 = query.Where(f => f.Children[0].Pets[0].GivenName == "Fluffy");
            Assert.AreEqual(q3.FirstOrDefault().FamilyId, family.FamilyId);

            IEnumerable<Family> q4 = query.Where(f => f.Children[0].Things["A"] == "B");
            Assert.AreEqual(q4.FirstOrDefault().FamilyId, family.FamilyId);

            for (int index = 0; index < 2; index++)
            {
                IEnumerable<Pet> q5 = query.Where(f => f.Children[0].Gender == "female").Select(f => f.Children[0].Pets[index]);
                Assert.AreEqual(q5.FirstOrDefault().GivenName, family.Children[0].Pets[index].GivenName);
            }

            IEnumerable<dynamic> q6 = query.SelectMany(f => f.Children.Select(c => new { Id = f.FamilyId }));
            Assert.AreEqual(q6.FirstOrDefault().Id, family.FamilyId);

            string nullString = null;
            IEnumerable<Family> q7 = query.Where(f => nullString == f.FamilyId);
            Assert.IsNull(q7.FirstOrDefault());

            object nullObject = null;
            q7 = query.Where(f => f.NullObject == nullObject);
            Assert.AreEqual(q7.FirstOrDefault().FamilyId, family.FamilyId);

            q7 = query.Where(f => f.FamilyId == nullString);
            Assert.IsNull(q7.FirstOrDefault());

            IEnumerable<Family> q8 = query.Where(f => null == f.FamilyId);
            Assert.IsNull(q8.FirstOrDefault());

            IEnumerable<Family> q9 = query.Where(f => f.IsRegistered == false);
            Assert.AreEqual(q9.FirstOrDefault().FamilyId, family.FamilyId);

            dynamic q10 = query.Where(f => f.FamilyId.Equals("WakefieldFamily")).AsEnumerable().FirstOrDefault();
            Assert.AreEqual(q10.FamilyId, family.FamilyId);

            GuidClass guidObject = new GuidClass() { Id = new Guid("098aa945-7ed8-4c50-b7b8-bd99eddb54bc") };
            container.CreateItemAsync(guidObject).Wait();
            List<GuidClass> guidData = new List<GuidClass>() { guidObject };

            IOrderedQueryable<GuidClass> guid = container.GetItemLinqQueryable<GuidClass>(allowSynchronousQueryExecution: true);

            IQueryable<GuidClass> q11 = guid.Where(g => g.Id == guidObject.Id);
            Assert.AreEqual(((IEnumerable<GuidClass>)q11).FirstOrDefault().Id, guidObject.Id);

            IQueryable<GuidClass> q12 = guid.Where(g => g.Id.ToString() == guidObject.Id.ToString());
            Assert.AreEqual(((IEnumerable<GuidClass>)q12).FirstOrDefault().Id, guidObject.Id);

            ListArrayClass arrayObject = new ListArrayClass() { Id = "arrayObject", ArrayField = new int[] { 1, 2, 3 } };
            container.CreateItemAsync(arrayObject).Wait();

            IOrderedQueryable<ListArrayClass> listArrayQuery = container.GetItemLinqQueryable<ListArrayClass>(allowSynchronousQueryExecution: true);

            IEnumerable<dynamic> q13 = listArrayQuery.Where(a => a.ArrayField == arrayObject.ArrayField);
            Assert.AreEqual(q13.FirstOrDefault().Id, arrayObject.Id);

            int[] nullArray = null;
            q13 = listArrayQuery.Where(a => a.ArrayField == nullArray);
            Assert.IsNull(q13.FirstOrDefault());

            ListArrayClass listObject = new ListArrayClass() { Id = "listObject", ListField = new List<int> { 1, 2, 3 } };
            container.CreateItemAsync(listObject).Wait();
            List<ListArrayClass> listArrayObjectData = new List<ListArrayClass>() { arrayObject, listObject };

            IEnumerable<dynamic> q14 = listArrayQuery.Where(a => a.ListField == listObject.ListField);
            Assert.AreEqual(q14.FirstOrDefault().Id, listObject.Id);

            IEnumerable<dynamic> q15 = query.Where(f => f.NullableInt == null);
            Assert.AreEqual(q15.ToList().Count, 0);

            int? nullInt = null;
            q15 = query.Where(f => f.NullableInt == nullInt);
            Assert.AreEqual(q15.ToList().Count, 0);

            q15 = query.Where(f => f.NullableInt == 5);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            nullInt = 5;
            q15 = query.Where(f => f.NullableInt == nullInt);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            q15 = query.Where(f => f.NullableInt == nullInt.Value);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            nullInt = 3;
            q15 = query.Where(f => f.Int == nullInt);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            q15 = query.Where(f => f.Int == nullInt.Value);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            nullInt = null;
            q15 = query.Where(f => f.Int == nullInt);
            Assert.AreEqual(q15.ToList().Count, 0);

            List<Family> v = fList.Where(f => f.Int > nullInt).ToList();

            q15 = query.Where(f => f.Int < nullInt);

            string doc1Id = "document1:x:'!@TT){}\"";
            Document doubleQoutesDocument = new Document() { Id = doc1Id };
            container.CreateItemAsync(doubleQoutesDocument).Wait();

            IQueryable<Document> docQuery = from book in container.GetItemLinqQueryable<Document>(allowSynchronousQueryExecution: true)
                                            where book.Id == doc1Id
                                            select book;

            Assert.AreEqual(docQuery.AsEnumerable().Single().Id, doc1Id);

            GreatFamily greatFamily = new GreatFamily() { Family = family };
            GreatGreatFamily greatGreatFamily = new GreatGreatFamily() { GreatFamilyId = Guid.NewGuid().ToString(), GreatFamily = greatFamily };
            container.CreateItemAsync(greatGreatFamily).Wait();
            List<GreatGreatFamily> greatGreatFamilyData = new List<GreatGreatFamily>() { greatGreatFamily };

            IOrderedQueryable<GreatGreatFamily> queryable = container.GetItemLinqQueryable<GreatGreatFamily>(allowSynchronousQueryExecution: true);

            IEnumerable<GreatGreatFamily> q16 = queryable.SelectMany(gf => gf.GreatFamily.Family.Children.Where(c => c.GivenName == "Jesse").Select(c => gf));

            Assert.AreEqual(q16.FirstOrDefault().GreatFamilyId, greatGreatFamily.GreatFamilyId);

            Sport sport = new Sport() { SportName = "Tennis", SportType = "Racquet" };
            container.CreateItemAsync(sport).Wait();
            List<Sport> sportData = new List<Sport>() { sport };

            IOrderedQueryable<Sport> sportQuery = container.GetItemLinqQueryable<Sport>(allowSynchronousQueryExecution: true);

            IEnumerable<Sport> q17 = sportQuery.Where(s => s.SportName == "Tennis");

            Assert.AreEqual(sport.SportName, q17.FirstOrDefault().SportName);

            Sport2 sport2 = new Sport2() { id = "json" };
            container.CreateItemAsync(sport2).Wait();
            List<Sport2> sport2Data = new List<Sport2>() { sport2 };

            IOrderedQueryable<Sport2> sport2Query = container.GetItemLinqQueryable<Sport2>(allowSynchronousQueryExecution: true);

            Func<bool, IQueryable<GuidClass>> getGuidQuery = useQuery => useQuery ? guid : guidData.AsQueryable();
            Func<bool, IQueryable<ListArrayClass>> getListArrayQuery = useQuery => useQuery ? listArrayQuery : listArrayObjectData.AsQueryable();
            Func<bool, IQueryable<GreatGreatFamily>> getGreatFamilyQuery = useQuery => useQuery ? queryable : greatGreatFamilyData.AsQueryable();
            Func<bool, IQueryable<Sport>> getSportQuery = useQuery => useQuery ? sportQuery : sportData.AsQueryable();
            Func<bool, IQueryable<Sport2>> getSport2Query = useQuery => useQuery ? sport2Query : sport2Data.AsQueryable();

            int? nullIntVal = null;
            int? nullableIntVal = 5;

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Select 1st parent family name", b => getQuery(b).Where(f => f.Parents.Count() > 0).Select(f => f.Parents[0].FamilyName)));
            inputs.Add(new LinqTestInput("Select 1st children grade expr", b => getQuery(b).Where(f => f.Children.Count() > 0).Select(f => f.Children[0].Grade + 13)));
            inputs.Add(new LinqTestInput("Filter 1st children's 1st pet name", b => getQuery(b).Where(f => f.Children.Count() > 0 && f.Children[0].Pets.Count() > 0 && f.Children[0].Pets[0].GivenName == "Fluffy")));
            inputs.Add(new LinqTestInput("Filter 1st children's thing A value", b => getQuery(b).Where(f => f.Children.Count() > 0 && f.Children[0].Things["A"] == "B")));
            inputs.Add(new LinqTestInput("Filter 1st children's gender -> Select his 1st pet", b => getQuery(b).Where(f => f.Children.Count() > 0 && f.Children[0].Pets.Count() > 0 && f.Children[0].Gender == "female").Select(f => f.Children[0].Pets[0])));
            inputs.Add(new LinqTestInput("Filter 1st children's gender -> Select his 2nd pet", b => getQuery(b).Where(f => f.Children.Count() > 0 && f.Children[0].Pets.Count() > 1 && f.Children[0].Gender == "female").Select(f => f.Children[0].Pets[1])));
            inputs.Add(new LinqTestInput("Select FamilyId of all children", b => getQuery(b).SelectMany(f => f.Children.Select(c => new { Id = f.FamilyId }))));
            inputs.Add(new LinqTestInput("Filter family with null Id", b => getQuery(b).Where(f => (string)null == f.FamilyId)));
            inputs.Add(new LinqTestInput("Filter family with null Id #2", b => getQuery(b).Where(f => f.FamilyId == (string)null)));
            inputs.Add(new LinqTestInput("Filter family with null object", b => getQuery(b).Where(f => f.NullObject == (object)null)));
            inputs.Add(new LinqTestInput("Filter family with null Id #3", b => getQuery(b).Where(f => null == f.FamilyId)));
            inputs.Add(new LinqTestInput("Filter registered family", b => getQuery(b).Where(f => f.IsRegistered == false)));
            inputs.Add(new LinqTestInput("Filter family by FamilyId", b => getQuery(b).Where(f => f.FamilyId.Equals("WakefieldFamily"))));
            inputs.Add(new LinqTestInput("Filter family nullable int", b => getQuery(b).Where(f => f.NullableInt == null)));
            inputs.Add(new LinqTestInput("Filter family nullable int #2", b => getQuery(b).Where(f => f.NullableInt == nullIntVal)));
            inputs.Add(new LinqTestInput("Filter family nullable int =", b => getQuery(b).Where(f => f.NullableInt == 5)));
            inputs.Add(new LinqTestInput("Filter nullableInt = nullInt", b => getQuery(b).Where(f => f.NullableInt == nullableIntVal)));
            inputs.Add(new LinqTestInput("Filter nullableInt = nullInt value", b => getQuery(b).Where(f => f.NullableInt == nullableIntVal.Value)));
            inputs.Add(new LinqTestInput("Filter int = nullInt", b => getQuery(b).Where(f => f.Int == nullableIntVal)));
            inputs.Add(new LinqTestInput("Filter int = nullInt value", b => getQuery(b).Where(f => f.Int == nullableIntVal.Value)));
            inputs.Add(new LinqTestInput("Filter int = nullInt", b => getQuery(b).Where(f => f.Int == nullIntVal)));
            inputs.Add(new LinqTestInput("Filter int < nullInt", b => getQuery(b).Where(f => f.Int < nullIntVal)));

            inputs.Add(new LinqTestInput("Guid filter by Id", b => getGuidQuery(b).Where(g => g.Id == guidObject.Id)));
            inputs.Add(new LinqTestInput("Guid filter by Id #2", b => getGuidQuery(b).Where(g => g.Id.ToString() == guidObject.Id.ToString())));
            inputs.Add(new LinqTestInput("Array compare", b => getListArrayQuery(b).Where(a => a.ArrayField == arrayObject.ArrayField)));
            inputs.Add(new LinqTestInput("Array compare null", b => getListArrayQuery(b).Where(a => a.ArrayField == nullArray)));
            inputs.Add(new LinqTestInput("List compare", b => getListArrayQuery(b).Where(a => a.ListField == listObject.ListField)));

            inputs.Add(new LinqTestInput("Nested great family query filter children name", b => getGreatFamilyQuery(b).SelectMany(gf => gf.GreatFamily.Family.Children.Where(c => c.GivenName == "Jesse").Select(c => gf))));
            inputs.Add(new LinqTestInput("Sport filter sport name", b => getSportQuery(b).Where(s => s.SportName == "Tennis")));
            inputs.Add(new LinqTestInput("Sport filter sport type", b => getSportQuery(b).Where(s => s.SportType == "Racquet")));
            inputs.Add(new LinqTestInput("Sport2 filter by id", b => getSport2Query(b).Where(s => s.id == "json")));
            this.ExecuteTestSuite(inputs);
        }

        internal static TValue CreateExecuteAndDeleteProcedure<TValue>(DocumentClient client,
            DocumentCollection collection,
            string transientProcedure,
            out StoredProcedureResponse<TValue> response)
        {
            // create
            StoredProcedure storedProcedure = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid().ToString(),
                Body = transientProcedure
            };
            StoredProcedure retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedure).Result;

            // execute
            response = client.ExecuteStoredProcedureAsync<TValue>(retrievedStoredProcedure).Result;

            // delete
            client.Delete<StoredProcedure>(retrievedStoredProcedure.GetIdOrFullName());

            return response.Response;
        }

        [TestMethod]
        public void ValidateBasicQuery()
        {
            this.ValidateBasicQueryAsync().Wait();
        }

        private async Task ValidateBasicQueryAsync()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            Documents.Database database = await client.ReadDatabaseAsync(string.Format("dbs/{0}", testDb.Id));

            string databaseName = database.Id;

            List<Database> queryResults = new List<Database>();
            //Simple Equality
            IQueryable<Documents.Database> dbQuery = from db in client.CreateDatabaseQuery()
                                                     where db.Id == databaseName
                                                     select db;
            IDocumentQuery<Documents.Database> documentQuery = dbQuery.AsDocumentQuery();

            while (documentQuery.HasMoreResults)
            {
                DocumentFeedResponse<Database> pagedResponse = await documentQuery.ExecuteNextAsync<Database>();
                Assert.IsNotNull(pagedResponse.ResponseHeaders, "ResponseHeaders is null");
                Assert.IsNotNull(pagedResponse.ActivityId, "Query ActivityId is null");
                queryResults.AddRange(pagedResponse);
            }

            Assert.AreEqual(1, queryResults.Count);
            Assert.AreEqual(databaseName, queryResults[0].Id);

            //Logical Or 
            dbQuery = from db in client.CreateDatabaseQuery()
                      where db.Id == databaseName || db.ResourceId == database.ResourceId
                      select db;
            documentQuery = dbQuery.AsDocumentQuery();

            while (documentQuery.HasMoreResults)
            {
                queryResults.AddRange(await documentQuery.ExecuteNextAsync<Database>());
            }

            Assert.AreEqual(2, queryResults.Count);
            Assert.AreEqual(databaseName, queryResults[0].Id);

            //Select Property
            IQueryable<string> idQuery = from db in client.CreateDatabaseQuery()
                                         where db.Id == databaseName
                                         select db.ResourceId;
            IDocumentQuery<string> documentIdQuery = idQuery.AsDocumentQuery();

            List<string> idResults = new List<string>();
            while (documentIdQuery.HasMoreResults)
            {
                idResults.AddRange(await documentIdQuery.ExecuteNextAsync<string>());
            }

            Assert.AreEqual(1, idResults.Count);
            Assert.AreEqual(database.ResourceId, idResults[0]);
        }

        [TestMethod]
        public async Task ValidateTransformQuery()
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            DocumentCollection collection = new DocumentCollection
            {
                Id = Guid.NewGuid().ToString("N"),
                PartitionKey = partitionKeyDefinition
            };
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            Database database = await cosmosClient.DocumentClient.ReadDatabaseAsync(string.Format("dbs/{0}", testDb.Id));
            collection = cosmosClient.DocumentClient.Create<DocumentCollection>(database.ResourceId, collection);
            int documentsToCreate = 100;
            for (int i = 0; i < documentsToCreate; i++)
            {
                dynamic myDocument = new Document();
                myDocument.Id = "doc" + i;
                myDocument.Title = "MyBook"; //Simple Property.
                myDocument.Languages = new Language[] { new Language { Name = "English", Copyright = "London Publication" }, new Language { Name = "French", Copyright = "Paris Publication" } }; //Array Property
                myDocument.Author = new Author { Name = "Don", Location = "France" }; //Complex Property
                myDocument.Price = 9.99;
                myDocument = await cosmosClient.DocumentClient.CreateDocumentAsync(collection.DocumentsLink, myDocument);
            }

            //Read response as dynamic.
            IQueryable<dynamic> docQuery = cosmosClient.DocumentClient.CreateDocumentQuery(collection.DocumentsLink, @"select * from root r where r.Title=""MyBook""", new FeedOptions { EnableCrossPartitionQuery = true });

            IDocumentQuery<dynamic> DocumentQuery = docQuery.AsDocumentQuery();
            DocumentFeedResponse<dynamic> queryResponse = await DocumentQuery.ExecuteNextAsync();

            Assert.IsNotNull(queryResponse.ResponseHeaders, "ResponseHeaders is null");
            Assert.IsNotNull(queryResponse.ActivityId, "ActivityId is null");
            Assert.AreEqual(documentsToCreate, queryResponse.Count);

            foreach (dynamic myBook in queryResponse)
            {
                Assert.AreEqual(myBook.Title.ToString(), "MyBook");
            }

            cosmosClient.DocumentClient.DeleteDocumentCollectionAsync(collection.SelfLink).Wait();
        }

        [TestMethod]
        public void ValidateDynamicDocumentQuery() //Ensure query on custom property of document.
        {
            Book myDocument = new Book();
            myDocument.Id = Guid.NewGuid().ToString();
            myDocument.Title = "My Book"; //Simple Property.
            myDocument.Languages = new Language[] { new Language { Name = "English", Copyright = "London Publication" }, new Language { Name = "French", Copyright = "Paris Publication" } }; //Array Property
            myDocument.Author = new Author { Name = "Don", Location = "France" }; //Complex Property
            myDocument.Price = 9.99;
            myDocument.Editions = new List<Edition>() { new Edition() { Name = "First", Year = 2001 }, new Edition() { Name = "Second", Year = 2005 } };

            //Create second document to make sure we have atleast one document which are filtered out of query.
            Book secondDocument = new Book
            {
                Id = Guid.NewGuid().ToString(),
                Title = "My Second Book",
                Languages = new Language[] { new Language { Name = "Spanish", Copyright = "Mexico Publication" } },
                Author = new Author { Name = "Carlos", Location = "Cancun" },
                Price = 25,
                Editions = new List<Edition>() { new Edition() { Name = "First", Year = 1970 } }
            };

            //Unfiltered execution.
            IOrderedQueryable<Book> bookDocQuery = testContainer.GetItemLinqQueryable<Book>(allowSynchronousQueryExecution: true);
            Func<bool, IQueryable<Book>> getBookQuery = useQuery => useQuery ? bookDocQuery : new List<Book>().AsQueryable();

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Simple Equality on custom property",
                b => from book in getBookQuery(b)
                     where book.Title == "My Book"
                     select book));
            inputs.Add(new LinqTestInput("Nested Property access",
                b => from book in getBookQuery(b)
                     where book.Author.Name == "Don"
                     select book));
            inputs.Add(new LinqTestInput("Array references & Project Author out..",
                b => from book in getBookQuery(b)
                     where book.Languages[0].Name == "English"
                     select book.Author));
            inputs.Add(new LinqTestInput("SelectMany",
                b => getBookQuery(b).SelectMany(book => book.Languages).Where(lang => lang.Name == "French").Select(lang => lang.Copyright)));
            inputs.Add(new LinqTestInput("NumericRange query",
                b => from book in getBookQuery(b)
                     where book.Price < 10
                     select book.Author));
            inputs.Add(new LinqTestInput("Or query",
                b => from book in getBookQuery(b)
                     where book.Title == "My Book" || book.Author.Name == "Don"
                     select book));
            inputs.Add(new LinqTestInput("SelectMany query on a List type.",
                b => getBookQuery(b).SelectMany(book => book.Editions).Select(ed => ed.Name)));
            // Below samples are strictly speaking not Any equivalent. But they join and filter "all"
            // subchildren which match predicate. When SQL BE supports ANY, we can replace these with Any Flavor.
            inputs.Add(new LinqTestInput("SelectMany",
                b => getBookQuery(b).SelectMany(book =>
                           book.Languages
                           .Where(lng => lng.Name == "English")
                           .Select(lng => book.Author))));
            inputs.Add(new LinqTestInput("SelectMany",
                b => getBookQuery(b).SelectMany(book =>
                               book.Editions
                               .Where(edition => edition.Year == 2001)
                               .Select(lng => book.Author))));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void ValidateDynamicAttachmentQuery() //Ensure query on custom property of attachment.
        {
            IOrderedQueryable<SpecialAttachment2> attachmentQuery = testContainer.GetItemLinqQueryable<SpecialAttachment2>(allowSynchronousQueryExecution: true);
            Document myDocument = new Document();
            Func<bool, IQueryable<SpecialAttachment2>> getAttachmentQuery = useQuery => useQuery ? attachmentQuery : new List<SpecialAttachment2>().AsQueryable();

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Filter equality on custom property",
                b => from attachment in getAttachmentQuery(b)
                     where attachment.Title == "My Book Title2"
                     select attachment));
            inputs.Add(new LinqTestInput("Filter equality on custom property #2",
                b => from attachment in getAttachmentQuery(b)
                     where attachment.Title == "My Book Title"
                     select attachment));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestLinqTypeSystem()
        {
            Assert.AreEqual(null, TypeSystem.GetElementType(typeof(Book)));
            Assert.AreEqual(null, TypeSystem.GetElementType(typeof(Author)));

            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(Language[])));
            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(List<Language>)));
            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(IList<Language>)));
            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(IEnumerable<Language>)));
            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(ICollection<Language>)));

            Assert.AreEqual(typeof(DerivedFooItem), TypeSystem.GetElementType(typeof(DerivedFooItem[])));
            Assert.AreEqual(typeof(FooItem), TypeSystem.GetElementType(typeof(List<FooItem>)));
            Assert.AreEqual(typeof(string), TypeSystem.GetElementType(typeof(MyList<string>)));
            Assert.AreEqual(typeof(Tuple<string, string>), TypeSystem.GetElementType(typeof(MyTupleList<string>)));

            Assert.AreEqual(typeof(DerivedFooItem), TypeSystem.GetElementType(typeof(DerivedFooCollection)));
            Assert.AreEqual(typeof(string), TypeSystem.GetElementType(typeof(FooStringCollection)));

            Assert.AreEqual(typeof(FooItem), TypeSystem.GetElementType(typeof(FooTCollection<object>)));
            Assert.AreEqual(typeof(FooItem), TypeSystem.GetElementType(typeof(FooTCollection<IFooItem>)));
            Assert.AreEqual(typeof(FooItem), TypeSystem.GetElementType(typeof(FooTCollection<FooItem>)));
            Assert.AreEqual(typeof(DerivedFooItem), TypeSystem.GetElementType(typeof(FooTCollection<DerivedFooItem>)));
        }

        #region DataDocument type tests

        public class BaseDocument
        {
            [JsonProperty(PropertyName = Constants.Properties.Id)]
            public string Id { get; set; }
            public string TypeName { get; set; }
        }

        public class DataDocument : BaseDocument
        {
            public int Number { get; set; }
        }

        private class QueryHelper
        {
            private readonly Container container;

            public QueryHelper(Container container)
            {
                this.container = container;
            }

            public IQueryable<T> Query<T>() where T : BaseDocument
            {
                IQueryable<T> query = this.container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: true)
                                       .Where(d => d.TypeName == "Hello");
                string queryString = query.ToString();
                return query;
            }
        }

        [TestMethod]
        public async Task ValidateLinqOnDataDocumentType()
        {
            Container container = await testDb.CreateContainerAsync(new ContainerProperties(id: nameof(ValidateLinqOnDataDocumentType), partitionKeyPath: "/id"));

            DataDocument doc = new DataDocument() { Id = Guid.NewGuid().ToString("N"), Number = 0, TypeName = "Hello" };
            container.CreateItemAsync(doc).Wait();

            QueryHelper queryHelper = new QueryHelper(container);
            IEnumerable<BaseDocument> result = queryHelper.Query<BaseDocument>();
            Assert.AreEqual(1, result.Count());

            BaseDocument baseDocument = result.FirstOrDefault<BaseDocument>();
            Assert.AreEqual(doc.Id, baseDocument.Id);

            BaseDocument iDocument = doc;
            IOrderedQueryable<DataDocument> q = container.GetItemLinqQueryable<DataDocument>(allowSynchronousQueryExecution: true);

            IEnumerable<DataDocument> iresult = from f in q
            where f.Id == iDocument.Id
                                                select f;
            DataDocument id = iresult.FirstOrDefault<DataDocument>();
            Assert.AreEqual(doc.Id, id.Id);
        }

        #endregion

        #region Book type tests
        public class Author
        {
            [JsonProperty(PropertyName = Constants.Properties.Id)]
            public string Name { get; set; }
            public string Location { get; set; }
        }

        public class Language
        {
            public string Name { get; set; }
            public string Copyright { get; set; }
        }

        public class Edition
        {
            public string Name { get; set; }
            public int Year { get; set; }
        }

        public class Book
        {
            //Verify that we can override the propertyName but still can query them using .NET Property names.
            [JsonProperty(PropertyName = "title")]
            public string Title { get; set; }
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }
            public Language[] Languages { get; set; }
            public Author Author { get; set; }
            public double Price { get; set; }
            [JsonProperty(PropertyName = Constants.Properties.Id)]
            public string Id { get; set; }
            public List<Edition> Editions { get; set; }
        }

        [TestMethod]
        public async Task ValidateServerSideQueryEvalWithPagination()
        {
            await this.ValidateServerSideQueryEvalWithPaginationScenario();
        }

        private async Task ValidateServerSideQueryEvalWithPaginationScenario()
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/title" }), Kind = PartitionKind.Hash };
            ContainerProperties cosmosContainerSettings = new ContainerProperties
            {
                Id = Guid.NewGuid().ToString(),
                PartitionKey = partitionKeyDefinition,
            };
            cosmosContainerSettings.IndexingPolicy.IndexingMode = Microsoft.Azure.Cosmos.IndexingMode.Consistent;

            Container collection = await testDb.CreateContainerAsync(cosmosContainerSettings);

            //Do script post to insert as many document as we could in a tight loop.
            string script = @"function() {
                var output = 0;
                var client = getContext().getCollection();
                function callback(err, docCreated) {
                    if(err) throw 'Error while creating document';
                    output++;
                    getContext().getResponse().setBody(output);
                    if(output < 50) 
                        client.createDocument(client.getSelfLink(), { id: 'testDoc' + output, title : 'My Book'}, {}, callback);                       
                };
                client.createDocument(client.getSelfLink(), { id: 'testDoc' + output, title : 'My Book'}, {}, callback); }";

            StoredProcedureExecuteResponse<int> scriptResponse = null;
            int totalNumberOfDocuments = GatewayTests.CreateExecuteAndDeleteCosmosProcedure(collection, script, out scriptResponse, "My Book");

            IOrderedQueryable<Book> linqQueryable = collection.GetItemLinqQueryable<Book>(allowSynchronousQueryExecution: true);
            int totalHit = linqQueryable.Where(book => book.Title == "My Book").Count();
            Assert.AreEqual(totalHit, totalNumberOfDocuments, "Didnt get all the documents");

        }

        #endregion

        public class SpecialAttachment2 //Non attachemnt derived.
        {
            [JsonProperty(PropertyName = Constants.Properties.Id)]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "contentType")]
            public string ContentType { get; set; }

            [JsonProperty(PropertyName = Constants.Properties.MediaLink)]
            public string Media { get; set; }

            public string Author { get; set; }
            public string Title { get; set; }
        }

        #region TypeSystem test reference classes
        public interface IFooItem { }

        public class FooItem : IFooItem { }

        public class DerivedFooItem : FooItem { }

        public class MyList<T> : List<T> { }

        public class MyTupleList<T> : List<Tuple<T, T>> { }

        public class DerivedFooCollection : IList<IFooItem>, IEnumerable<DerivedFooItem>
        {
            public int IndexOf(IFooItem item)
            {
                throw new NotImplementedException();
            }

            public void Insert(int index, IFooItem item)
            {
                throw new NotImplementedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            public IFooItem this[int index]
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public void Add(IFooItem item)
            {
                throw new NotImplementedException();
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(IFooItem item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(IFooItem[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { throw new NotImplementedException(); }
            }

            public bool IsReadOnly
            {
                get { throw new NotImplementedException(); }
            }

            public bool Remove(IFooItem item)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<IFooItem> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator<DerivedFooItem> IEnumerable<DerivedFooItem>.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }

        public class FooStringCollection : IList<string>, IEnumerable<FooItem>
        {
            public int IndexOf(string item)
            {
                throw new NotImplementedException();
            }

            public void Insert(int index, string item)
            {
                throw new NotImplementedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            public string this[int index]
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public void Add(string item)
            {
                throw new NotImplementedException();
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(string item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(string[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { throw new NotImplementedException(); }
            }

            public bool IsReadOnly
            {
                get { throw new NotImplementedException(); }
            }

            public bool Remove(string item)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<string> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator<FooItem> IEnumerable<FooItem>.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }

        public class FooTCollection<T> : List<FooItem>, IEnumerable<T>
        {
            public new IEnumerator<T> GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }
        #endregion

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input);
        }
    }
}

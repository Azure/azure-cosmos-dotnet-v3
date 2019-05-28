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
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using VisualStudio.TestTools.UnitTesting;
    using BaselineTest;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Class that tests to see that we honor the attributes for members in a class / struct when we create LINQ queries.
    /// </summary>
    [TestClass]
    public class LinqAttributeContractBaselineTests : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static Func<bool, IQueryable<Datum>> getQuery;
        private static DocumentClient client;
        private static Database testDb;
        private static DocumentCollection testCollection;

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            client = TestCommon.CreateClient(true);

            var db = new Database() { Id = nameof(LinqTranslationBaselineTests) };
            try
            {
                var response = client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(db.Id)).Result;
            }
            catch { }
            testDb = client.CreateDatabaseAsync(db).Result;
        }

        [ClassCleanup]
        public static void CleanUp()
        {
            if (testDb != null)
            {
                client.DeleteDatabaseAsync(testDb);
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // The test collection should have range index on string properties
            // for the orderby tests
            var newCol = new DocumentCollection()
            {
                Id = Guid.NewGuid().ToString(),
                IndexingPolicy = new IndexingPolicy()
                {
                    IncludedPaths = new System.Collections.ObjectModel.Collection<IncludedPath>()
                    {
                        new IncludedPath()
                        {
                            Path = "/*",
                            Indexes = new System.Collections.ObjectModel.Collection<Index>()
                            {
                                Index.Range(DataType.Number, -1),
                                Index.Range(DataType.String, -1)
                            }
                        }
                    }
                }
            };
            testCollection = client.CreateDocumentCollectionAsync(testDb, newCol).Result;

            const int Records = 100;
            const int MaxStringLength = 100;
            Func<Random, Datum> createDataFunc = random =>
            {
                var obj = new Datum();
                obj.JsonProperty = random.NextDouble() < 0.3 ? "Hello" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.JsonPropertyAndDataMember = random.NextDouble() < 0.3 ? "Hello" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.DataMember = random.NextDouble() < 0.3 ? "Hello" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.Default = random.NextDouble() < 0.3 ? "Hello" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                return obj;
            };
            getQuery = LinqTestsCommon.GenerateTestData(createDataFunc, Records, client, testCollection);
        }

        [TestCleanup]
        public void TestCleanUp()
        {
            client.DeleteDocumentCollectionAsync(testCollection);
        }

        /// <summary>
        /// Class with attributes on it's members.
        /// </summary>
        [DataContract]
        public class Datum : LinqTestObject
        {
            /// <summary>
            /// Member of the Datum class that has a JsonProperty attribute.
            /// </summary>
            [JsonProperty(PropertyName = "jsonProperty")]
            public string JsonProperty;

            /// <summary>
            /// Member of the Datum class that has a DataMember attribute.
            /// </summary>
            [DataMember(Name = "dataMember")]
            public string DataMember;

            /// <summary>
            /// Member of the Datum class that has no attributes.
            /// </summary>
            [JsonProperty]
            public string Default;

            /// <summary>
            /// Member of the Datum class that has both a JsonProperty and DataMember attribute.
            /// </summary>
            [JsonProperty(PropertyName = "jsonPropertyHasHigherPriority")]
            [DataMember(Name = "thanDataMember")]
            public string JsonPropertyAndDataMember;
        }

        /// <summary>
        /// Class with attributes on it's members and with a constructor.
        /// </summary>
        [DataContract]
        public class Datum2
        {
            [JsonProperty(PropertyName = "jsonProperty2")]
            public string JsonProperty;

            [DataMember(Name = "dataMember2")]
            public string DataMember;

            public string Default;

            [JsonProperty(PropertyName = "jsonPropertyHasHigherPriority2")]
            [DataMember(Name = "thanDataMember2")]
            public string JsonPropertyAndDataMember;

            public Datum2(string jsonProperty, string dataMember, string defaultMember, string jsonPropertyAndDataMember)
            {
                this.JsonProperty = jsonProperty;
                this.DataMember = dataMember;
                this.Default = defaultMember;
                this.JsonPropertyAndDataMember = jsonPropertyAndDataMember;
            }
        }

        /// <summary>
        /// In general the attribute priority is as follows:
        /// 1) JsonProperty
        /// 2) DataMember
        /// 3) Default Member Name
        /// </summary>
        [TestMethod]
        public void TestAttributePriority()
        {
            Assert.AreEqual("jsonProperty", TypeSystem.GetMemberName(typeof(Datum).GetMember("JsonProperty").First()));
            Assert.AreEqual("dataMember", TypeSystem.GetMemberName(typeof(Datum).GetMember("DataMember").First()));
            Assert.AreEqual("Default", TypeSystem.GetMemberName(typeof(Datum).GetMember("Default").First()));
            Assert.AreEqual("jsonPropertyHasHigherPriority", TypeSystem.GetMemberName(typeof(Datum).GetMember("JsonPropertyAndDataMember").First()));
        }

        /// <summary>
        /// Tests to see if we are honoring the attribute contract in a where clause for LINQ Queries.
        /// </summary>
        [TestMethod]
        public void TestWhereAttributeContract()
        {
            var inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Filter by JsonProperty", b => getQuery(b).Where(doc => doc.JsonProperty == "Hello")));
            inputs.Add(new LinqTestInput("Filter by DataMember", b => getQuery(b).Where(doc => doc.DataMember == "Hello")));
            inputs.Add(new LinqTestInput("Filter by Default", b => getQuery(b).Where(doc => doc.Default == "Hello")));
            inputs.Add(new LinqTestInput("Filter by JsonPropertyAndDataMember", b => getQuery(b).Where(doc => doc.JsonPropertyAndDataMember == "Hello")));
            this.ExecuteTestSuite(inputs);
        }

        /// <summary>
        /// Tests to see if we are honoring the attribute contract in a select clause for LINQ Queries.
        /// </summary>
        [TestMethod]
        public void TestSelectAttributeContract()
        {
            var inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Select JsonProperty", b => getQuery(b).Select(doc => doc.JsonProperty)));
            inputs.Add(new LinqTestInput("Select DataMember", b => getQuery(b).Select(doc => doc.DataMember)));
            inputs.Add(new LinqTestInput("Select Default", b => getQuery(b).Select(doc => doc.Default)));
            inputs.Add(new LinqTestInput("Select JsonPropertyAndDataMember", b => getQuery(b).Select(doc => doc.JsonPropertyAndDataMember)));
            this.ExecuteTestSuite(inputs);
        }

        /// <summary>
        /// Tests to see if we are honoring the attribute contract in a orderby clause for LINQ Queries.
        /// </summary>
        [TestMethod]
        public void TestOrderByAttributeContract()
        {
            var inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("OrderBy JsonProperty", b => getQuery(b).OrderBy(doc => doc.JsonProperty)));
            inputs.Add(new LinqTestInput("OrderByDescending JsonProperty", b => getQuery(b).OrderByDescending(doc => doc.JsonProperty)));
            inputs.Add(new LinqTestInput("OrderBy DataMember", b => getQuery(b).OrderBy(doc => doc.DataMember)));
            inputs.Add(new LinqTestInput("OrderByDescending DataMember", b => getQuery(b).OrderByDescending(doc => doc.DataMember)));
            inputs.Add(new LinqTestInput("OrderBy Default", b => getQuery(b).OrderBy(doc => doc.Default)));
            inputs.Add(new LinqTestInput("OrderByDescending Default", b => getQuery(b).OrderByDescending(doc => doc.Default)));
            inputs.Add(new LinqTestInput("OrderBy JsonPropertyAndDataMember", b => getQuery(b).OrderBy(doc => doc.JsonPropertyAndDataMember)));
            inputs.Add(new LinqTestInput("OrderByDescending JsonPropertyAndDataMember", b => getQuery(b).OrderByDescending(doc => doc.JsonPropertyAndDataMember)));
            this.ExecuteTestSuite(inputs);
        }

        /// <summary>
        /// Tests to see if we are honoring the attribute contract in a member assignment.
        /// </summary>
        [TestMethod]
        public void TestMemberAssignmentAttributeContract()
        {
            var inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("MemberAssignment",
                b => getQuery(b).Select(doc => new Datum()
                {
                    DataMember = doc.DataMember,
                    Default = doc.Default,
                    JsonProperty = doc.JsonProperty,
                    JsonPropertyAndDataMember = doc.JsonPropertyAndDataMember
                })));
            this.ExecuteTestSuite(inputs);
        }

        /// <summary>
        /// Tests to see if we are honoring the attribute contract in constructors.
        /// </summary>
        [TestMethod]
        public void TestNewAttributeContract()
        {
            var inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("New", b => getQuery(b).Select(doc => new Datum2(doc.JsonProperty, doc.DataMember, doc.Default, doc.JsonPropertyAndDataMember)), "Constructor invocation is not supported."));
            this.ExecuteTestSuite(inputs);
        }

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input);
        }
    }
}

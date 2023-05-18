﻿//-----------------------------------------------------------------------
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
    using System.Threading.Tasks;

    /// <summary>
    /// Class that tests to see that we honor the attributes for members in a class / struct when we create LINQ queries.
    /// </summary>
    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public class LinqAttributeContractBaselineTests : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static Func<bool, IQueryable<Datum>> getQuery;
        private static CosmosClient client;
        private static Cosmos.Database testDb;
        private static Container testCollection;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            client = TestCommon.CreateCosmosClient(true);

            string dbName = $"{nameof(LinqAttributeContractBaselineTests)}-{Guid.NewGuid().ToString("N")}";
            testDb = await client.CreateDatabaseAsync(dbName);
        }

        [ClassCleanup]
        public static async Task CleanUp()
        {
            if (testDb != null)
            {
                await testDb.DeleteStreamAsync();
            }

            client?.Dispose();
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/Pk" }), Kind = PartitionKind.Hash };
            // The test collection should have range index on string properties
            // for the orderby tests
            var newCol = new ContainerProperties()
            {
                Id = Guid.NewGuid().ToString(),
                PartitionKey = partitionKeyDefinition,
                IndexingPolicy = new Microsoft.Azure.Cosmos.IndexingPolicy()
                {
                    IncludedPaths = new System.Collections.ObjectModel.Collection<Microsoft.Azure.Cosmos.IncludedPath>()
                    {
                        new Microsoft.Azure.Cosmos.IncludedPath()
                        {
                            Path = "/*",
                            Indexes = new System.Collections.ObjectModel.Collection<Microsoft.Azure.Cosmos.Index>()
                            {
                                Microsoft.Azure.Cosmos.Index.Range(Microsoft.Azure.Cosmos.DataType.Number, -1),
                                Microsoft.Azure.Cosmos.Index.Range(Microsoft.Azure.Cosmos.DataType.String, -1)
                            }
                        }
                    }
                }
            };
            testCollection = await testDb.CreateContainerAsync(newCol);

            const int Records = 100;
            const int MaxStringLength = 100;
            Func<Random, Datum> createDataFunc = random =>
            {
                var obj = new Datum();
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                obj.JsonProperty = random.NextDouble() < 0.3 ? "Hello" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.JsonPropertyAndDataMember = random.NextDouble() < 0.3 ? "Hello" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.DataMember = random.NextDouble() < 0.3 ? "Hello" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.Default = random.NextDouble() < 0.3 ? "Hello" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.DataMemberAndJsonPropertyName = random.NextDouble() < 0.3 ? "Hello" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                return obj;
            };
            getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataFunc, Records, testCollection);
        }

        [TestCleanup]
        public void TestCleanUp()
        {
            testCollection.DeleteContainerAsync().Wait();
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

            [JsonProperty]
            public string Pk;

            [JsonProperty(PropertyName = "id")]
            public string Id;

            /// <summary>
            /// Member of the Datum class that has both a JsonProperty and DataMember attribute.
            /// </summary>
            [JsonProperty(PropertyName = "jsonPropertyHasHigherPriority")]
            [DataMember(Name = "thanDataMember")]
            public string JsonPropertyAndDataMember;

            [DataMember(Name = "dataMemberHasHigherPriority")]
            [System.Text.Json.Serialization.JsonPropertyName("thanJsonPropertyName")]
            public string DataMemberAndJsonPropertyName;

            /// <summary>
            /// When generating test data, we can't actually serialize a type with both newtonsoft and System.Text.Json
            /// so we'll just ignore the property since we can't validate it anyway
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("jsonPropertyName")]
            [JsonIgnore]
            public string JsonPropertyName;
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

            [DataMember(Name = "dataMemberHasHigherPriority2")]
            [System.Text.Json.Serialization.JsonPropertyName("thanJsonPropertyName2")]
            public string DataMemberAndJsonPropertyName;

            [System.Text.Json.Serialization.JsonPropertyName("jsonPropertyName2")]
            public string JsonPropertyName;

            public Datum2(string jsonProperty, string dataMember, string defaultMember, string jsonPropertyAndDataMember, string dataMemberAndJsonPropertyName, string jsonPropertyName)
            {
                this.JsonProperty = jsonProperty;
                this.DataMember = dataMember;
                this.Default = defaultMember;
                this.JsonPropertyAndDataMember = jsonPropertyAndDataMember;
                this.DataMemberAndJsonPropertyName = dataMemberAndJsonPropertyName;
                this.JsonPropertyName = jsonPropertyName;
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
            Assert.AreEqual("dataMemberHasHigherPriority", TypeSystem.GetMemberName(typeof(Datum).GetMember("DataMemberAndJsonPropertyName").First()));
            Assert.AreEqual("jsonPropertyName", TypeSystem.GetMemberName(typeof(Datum).GetMember("JsonPropertyName").First()));
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
            inputs.Add(new LinqTestInput("Filter by DataMemberAndJsonPropertyName", b => getQuery(b).Where(doc => doc.DataMemberAndJsonPropertyName == "Hello")));
            // Can't verify this one, since it's serialized with newtonsoft.json and the query uses STJ for this property. End result is that the query returns no results since
            // the name specificed on JsonPropertyName doesn't exist on the serialized document because newtonsoft.json doesn't know about it.
            inputs.Add(new LinqTestInput("Filter by JsonPropertyName", b => getQuery(b).Where(doc => doc.JsonPropertyName == "Hello"), skipVerification: true));
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
            inputs.Add(new LinqTestInput("Select DataMemberAndJsonPropertyName", b => getQuery(b).Select(doc => doc.DataMemberAndJsonPropertyName)));
            // Can't verify this one, since it's serialized with newtonsoft.json and the query uses STJ for this property. End result is that the query returns no results since
            // the name specificed on JsonPropertyName doesn't exist on the serialized document because newtonsoft.json doesn't know about it.
            inputs.Add(new LinqTestInput("Select JsonPropertyName", b => getQuery(b).Select(doc => doc.JsonPropertyName), skipVerification: true));
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
            inputs.Add(new LinqTestInput("OrderBy DataMemberAndJsonPropertyName", b => getQuery(b).OrderBy(doc => doc.DataMemberAndJsonPropertyName)));
            inputs.Add(new LinqTestInput("OrderByDescending DataMemberAndJsonPropertyName", b => getQuery(b).OrderByDescending(doc => doc.DataMemberAndJsonPropertyName)));
            inputs.Add(new LinqTestInput("OrderBy JsonPropertyName", b => getQuery(b).OrderBy(doc => doc.JsonPropertyName)));
            inputs.Add(new LinqTestInput("OrderByDescending JsonPropertyName", b => getQuery(b).OrderByDescending(doc => doc.JsonPropertyName)));
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
                    JsonPropertyAndDataMember = doc.JsonPropertyAndDataMember,
                    DataMemberAndJsonPropertyName = doc.DataMemberAndJsonPropertyName,
                    JsonPropertyName = doc.JsonPropertyName
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
            inputs.Add(new LinqTestInput("New", b => getQuery(b).Select(doc => new Datum2(doc.JsonProperty, doc.DataMember, doc.Default, doc.JsonPropertyAndDataMember, doc.DataMemberAndJsonPropertyName, doc.JsonPropertyName))));
            this.ExecuteTestSuite(inputs);
        }

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input);
        }
    }
}

//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using BaselineTest;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public class LinqSQLTranslationBaselineTest : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static Expression Lambda<T, S>(Expression<Func<T, S>> func)
        {
            return func;
        }

        private static CosmosClient cosmosClient;
        private static Database testDb;
        private static Container testContainer;

        [ClassInitialize]
        public static async Task Initialize(TestContext textContext)
        {
            cosmosClient = TestCommon.CreateCosmosClient((cosmosClientBuilder) =>
            {
                cosmosClientBuilder.WithCustomSerializer(new CustomJsonSerializer(new JsonSerializerSettings()
                {
                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                    // We want to simulate the property not exist so ignoring the null value
                    NullValueHandling = NullValueHandling.Ignore
                })).WithConnectionModeGateway();
            });

            string dbName = $"{nameof(LinqSQLTranslationBaselineTest)}-{Guid.NewGuid().ToString("N")}";
            testDb = await cosmosClient.CreateDatabaseAsync(dbName);
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            testContainer = await testDb.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/pk"));
        }

        [TestCleanup]
        public async Task TestCleanUp()
        {
            await testContainer.DeleteContainerAsync();
        }

        [ClassCleanup]
        public static async Task CleanUp()
        {
            if (testDb != null)
            {
                await testDb.DeleteStreamAsync();
            }
        }

        private struct simple
        {
            public int x;
            public int y;
            public string id;
            public string pk;

            public simple(int x, int y)
            { this.x = x; this.y = y; this.id = Guid.NewGuid().ToString(); this.pk = "Test"; }
        }

        private struct nested
        {
            public int x;
            public simple s;

            public nested(int x, simple s)
            {
                this.x = x;
                this.s = s;
            }
        }

        private struct complex
        {
            private string json;

            public double dbl;
            public string str;
            public bool b;
            public double[] dblArray;
            public simple inside;
            public string id;
            public string pk;

            public complex(double d, string str, bool b, double[] da, simple s)
            {
                this.dbl = d;
                this.str = str;
                this.b = b;
                this.dblArray = da;
                this.inside = s;
                this.json = null;
                this.id = Guid.NewGuid().ToString();
                this.pk = "Test";
            }

            public override string ToString()
            {
                // simple cached serialization
                if (this.json == null)
                {
                    this.json = JsonConvert.SerializeObject(this);
                }
                return this.json;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is complex))
                {
                    return false;
                }

                return this.ToString().Equals(obj.ToString());
            }

            public override int GetHashCode()
            {
                return this.ToString().GetHashCode();
            }
        }

        private static T id<T>(T input)
        {
            return input;
        }

        private struct TestExpression
        {
            public Expression expression;
            public bool success;
            public string result;

            public TestExpression(Expression e, bool s, string r)
            {
                this.expression = e;
                this.success = s;
                this.result = r;
            }
        }

        private struct TestQuery
        {
            public IQueryable query;
            public bool success;
            public SqlQuerySpec result;
            public string errorMessage;

            public TestQuery(IQueryable query, bool success, SqlQuerySpec result, string errorMessage)
            {
                this.query = query;
                this.success = success;
                this.result = result;
                this.errorMessage = errorMessage;
            }

            public static TestQuery CreateSuccess(IQueryable query, string queryText)
            {
                return CreateSuccess(query, new SqlQuerySpec(queryText));
            }

            public static TestQuery CreateSuccess(IQueryable query, SqlQuerySpec result)
            {
                return new TestQuery(query, true, result, null);
            }

            public static TestQuery CreateFailure(IQueryable query, string errorMessage)
            {
                return new TestQuery(query, false, null, errorMessage);
            }
        }

        [TestMethod]
        public void ValidateSQLTranslation()
        {
            int constInt = 2;
            int[] array = { 1, 2, 3 };
            ParameterExpression paramx = Expression.Parameter(typeof(int), "x");
            float floatValue = 5.23f;

            const int Records = 100;
            Func<Random, simple> createDataObj = (random) =>
            {
                simple obj = new simple
                {
                    x = random.Next(),
                    y = random.Next(),
                    id = Guid.NewGuid().ToString(),
                    pk = "Test"
                };
                return obj;
            };
            Func<bool, IQueryable<simple>> dataQuery = LinqTestsCommon.GenerateTestCosmosData<simple>(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Select cast float", b => dataQuery(b).Select(x => (int)floatValue)),
                new LinqTestInput("Select identity", b => dataQuery(b).Select(x => x)),
                new LinqTestInput("Select int expr", b => dataQuery(b).Select(x => (x.x % 10) + 2 + (x.x % 5))),
                new LinqTestInput("Select int expr w const", b => dataQuery(b).Select(x => x.x + constInt)),
                new LinqTestInput("Select w new array", b => dataQuery(b).Select(d => new int[2] { d.x, d.x + 1 })),
                new LinqTestInput("Select new", b => dataQuery(b).Select(d => new { first = d.x, second = d.x })),
                new LinqTestInput("Select nested new", b => dataQuery(b).Select(d => new { first = d.x, second = new { third = d.x } })),
                new LinqTestInput("Filter int >", b => dataQuery(b).Where(x => x.x > 2)),
                new LinqTestInput("Filter method >", b => dataQuery(b).Where(x => x.x > id(3))),
                new LinqTestInput("Filter int > -> Select int expr", b => dataQuery(b).Where(x => x.x > 2).Select(x => x.x + 2)),
                new LinqTestInput("Select int expr -> Filter int >", b => dataQuery(b).Select(x => x.x + 2).Where(x => x > 2)),
                new LinqTestInput("Filter int > -> Filter another field", b => dataQuery(b).Where(x => x.x > 2).Where(y => y.x < 4)),
                new LinqTestInput("Filter x -> Filter y -> Select y expr", b => dataQuery(b).Where(x => x.x > 2).Where(y => y.x < 4).Select(y => y.x + y.x)),
                new LinqTestInput("Select expr w const array", b => dataQuery(b).Select(x => x.x + array[2])),
                new LinqTestInput("Select const array index", b => dataQuery(b)
                    .Where(x => x.x >= 0 && x.x < 3)
                    .Select(x => new int[] { 1, 2, 3 }[x.x])),
                new LinqTestInput("Select new simple", b => dataQuery(b).Select(x => new simple { x = x.x, y = x.x })),
                new LinqTestInput("Select new nested", b => dataQuery(b).Select(x => new nested { s = new simple { x = x.x, y = x.x }, x = 2 })),
                new LinqTestInput("Select new complex", b => dataQuery(b).Select(d => new complex { dbl = 1.0, str = "", b = false, dblArray = new double[] { 1.0, 2.0, }, inside = new simple { x = d.x, y = d.x } })),
                new LinqTestInput("Select cast double x", b => dataQuery(b).Select(x => (double)x.x)),
                new LinqTestInput("Select indexer x", b => dataQuery(b)
                    .Where(x => x.x >= 0 && x.x < array.Length)
                    .Select(x => array[x.x])),
                new LinqTestInput("Select new constructor", b => dataQuery(b).Select(x => new TimeSpan(x.x))),
                new LinqTestInput("Select method id", b => dataQuery(b).Select(x => id(x))),
                new LinqTestInput("Select identity", b => dataQuery(b).Select(x => x)),
                new LinqTestInput("Select simple property", b => dataQuery(b).Select(x => x.x))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void ValidateSQLTranslationComplexData()
        {
            string constString = "s";
            int[] array = { 1, 2, 3 };
            ParameterExpression paramx = Expression.Parameter(typeof(int), "x");

            const int Records = 100;
            const int MaxArraySize = 10;
            const int MaxStringLength = 50;
            Func<Random, complex> createDataObj = (random) =>
            {
                complex obj = new complex
                {
                    b = random.NextDouble() < 0.5,
                    dbl = random.NextDouble(),
                    dblArray = new double[random.Next(MaxArraySize)]
                };
                for (int i = 0; i < obj.dblArray.Length; ++i)
                {
                    obj.dblArray[i] = random.NextDouble() < 0.1 ? 3 : random.NextDouble();
                }
                obj.inside = new simple() { x = random.Next(), y = random.Next() };
                obj.str = random.NextDouble() < 0.1 ? "5" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.id = Guid.NewGuid().ToString();
                obj.pk = "Test";
                return obj;
            };
            Func<bool, IQueryable<complex>> getQuery = LinqTestsCommon.GenerateTestCosmosData<complex>(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Select equality", b => getQuery(b).Select(s => s.str == "5")),
                new LinqTestInput("Select string concat", b => getQuery(b).Select(d => "x" + d.str)),
                new LinqTestInput("Select string concat w const", b => getQuery(b).Select(d => "x" + constString + d.str)),

                new LinqTestInput("SelectMany array", b => getQuery(b).SelectMany(d => d.dblArray)),
                new LinqTestInput("SelectMany array property -> Filter x -> Select x expr", b => getQuery(b).SelectMany(z => z.dblArray).Where(x => x > 2).Select(x => x + 2)),
                new LinqTestInput("SelectMany array property -> Filter x equality -> Select x expr", b => getQuery(b).SelectMany(z => z.dblArray.Where(x => x == 3).Select(x => x + 1))),
                new LinqTestInput("SelectMany array property -> Select identity", b => getQuery(b).SelectMany(d => d.dblArray.Select(x => x))),
                new LinqTestInput("SelectMany array property", b => getQuery(b).SelectMany(d => d.dblArray)),
                new LinqTestInput("SelectMany array property -> Select x expr", b => getQuery(b).SelectMany(z => z.dblArray.Select(x => z.dbl + x))),
                new LinqTestInput("SelectMany array property -> Select new", b => getQuery(b).SelectMany(z => z.dblArray.Select(x => new { z.b, x = Math.Truncate(x * 100) }))),

                new LinqTestInput("SelectMany identity", b => getQuery(b).SelectMany(x => x.dblArray)),
                new LinqTestInput("SelectMany x -> Select y", b => getQuery(b).SelectMany(x => x.dblArray.Select(y => y))),
                new LinqTestInput("SelectMany x -> Select x.y", b => getQuery(b).SelectMany(x => x.dblArray.Select(y => y))),
                new LinqTestInput("SelectMany array", b => getQuery(b).SelectMany(x => x.dblArray))
            };
            this.ExecuteTestSuite(inputs);
        }

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input);
        }
    }
}

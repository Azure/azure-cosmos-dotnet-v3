//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests
{
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json.Converters;
    using BaselineTest;
    using System.Linq.Dynamic;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using System.Threading.Tasks;

    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public class LinqTranslationBaselineTests : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static CosmosClient cosmosClient;
        private static Cosmos.Database testDb;
        private static Container testContainer;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
            Uri uri = new Uri(Utils.ConfigurationManager.AppSettings["GatewayEndpoint"]);
            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Gateway,
                EnableEndpointDiscovery = true,
            };

            cosmosClient = TestCommon.CreateCosmosClient((cosmosClientBuilder) =>
            {
                cosmosClientBuilder.WithCustomSerializer(new CustomJsonSerializer(new JsonSerializerSettings()
                {
                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                    // We want to simulate the property not exist so ignoring the null value
                    NullValueHandling = NullValueHandling.Ignore
                })).WithConnectionModeGateway();
            });

            string dbName = $"{nameof(LinqTranslationBaselineTests)}-{Guid.NewGuid().ToString("N")}";
            testDb = await cosmosClient.CreateDatabaseAsync(dbName);
        }

        [ClassCleanup]
        public async static Task CleanUp()
        {
            if (testDb != null)
            {
                await testDb.DeleteStreamAsync();
            }
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            testContainer = await testDb.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/Pk"));
        }

        [TestCleanup]
        public async Task TestCleanUp()
        {
            await testContainer.DeleteContainerStreamAsync();
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum TestEnum
        {
            Zero,
            One,
            Two
        }

        public enum TestEnum2
        {
            Zero,
            One,
            Two
        }

        public static bool ObjectSequenceEquals<T>(IEnumerable<T> enumA, IEnumerable<T> enumB)
        {
            if (enumA == null || enumB == null) return enumA == enumB;
            return enumA.SequenceEqual(enumB);
        }

        public static bool ObjectEquals(object objA, object objB)
        {
            if (objA == null || objB == null) return objA == objB;
            return objA.Equals(objB);
        }

        internal class DataObject : LinqTestObject
        {
            public double NumericField;
            public decimal DecimalField;
            public double IntField;
            public string StringField;
            public string StringField2;
            public int[] ArrayField;
            public List<int> EnumerableField;
            public Point Point;
            public int? NullableField;

            [JsonConverter(typeof(StringEnumConverter))]
            public TestEnum EnumField1;

            [JsonConverter(typeof(StringEnumConverter))]
            public TestEnum? NullableEnum1;

            // These fields should also serialize as string
            // the attribute is specified on the type level
            public TestEnum EnumField2;
            public TestEnum? NullableEnum2;

            // This field should serialize as number
            // there is no converter applied on the property
            // of the enum definition
            public TestEnum2 EnumNumber;

            [JsonConverter(typeof(UnixDateTimeConverter))]
            public DateTime UnixTime;

            [JsonConverter(typeof(IsoDateTimeConverter))]
            public DateTime IsoTime;

            // This field should serialize as ISO Date
            // as this is the default DateTimeConverter
            // used by Newtonsoft
            public DateTime DefaultTime;

            [JsonProperty(PropertyName = "id")]
            public string Id;

            public string Pk;
        }

        [TestMethod]
        public void TestLiteralSerialization()
        {
            List<DataObject> testData = new List<DataObject>();
            IOrderedQueryable<DataObject> constantQuery = testContainer.GetItemLinqQueryable<DataObject>(allowSynchronousQueryExecution: true);
            Func<bool, IQueryable<DataObject>> getQuery = useQuery => useQuery ? constantQuery : testData.AsQueryable();
            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                // Byte
                new LinqTestInput("Byte 1", b => getQuery(b).Select(doc => new { value = 1 })),
                new LinqTestInput("Byte MinValue", b => getQuery(b).Select(doc => new { value = Byte.MinValue })),
                new LinqTestInput("Byte MaxValue", b => getQuery(b).Select(doc => new { value = Byte.MaxValue })),
                // SByte
                new LinqTestInput("SByte 2", b => getQuery(b).Select(doc => new { value = 2 })),
                new LinqTestInput("SByte MinValue", b => getQuery(b).Select(doc => new { value = SByte.MinValue })),
                new LinqTestInput("SByte MaxValue", b => getQuery(b).Select(doc => new { value = SByte.MaxValue })),
                // UInt16
                new LinqTestInput("UInt16 3", b => getQuery(b).Select(doc => new { value = 3 })),
                new LinqTestInput("UInt16 MinValue", b => getQuery(b).Select(doc => new { value = UInt16.MinValue })),
                new LinqTestInput("UInt16 MaxValue", b => getQuery(b).Select(doc => new { value = UInt16.MaxValue })),
                // UInt32
                new LinqTestInput("UInt32 4", b => getQuery(b).Select(doc => new { value = 4 })),
                new LinqTestInput("UInt32 MinValue", b => getQuery(b).Select(doc => new { value = UInt32.MinValue })),
                new LinqTestInput("UInt32 MaxValue", b => getQuery(b).Select(doc => new { value = UInt32.MaxValue })),
                // UInt64
                new LinqTestInput("UInt64 5", b => getQuery(b).Select(doc => new { value = 5 })),
                new LinqTestInput("UInt64 MinValue", b => getQuery(b).Select(doc => new { value = UInt64.MinValue })),
                new LinqTestInput("UInt64 MaxValue", b => getQuery(b).Select(doc => new { value = UInt64.MaxValue })),
                // Int16
                new LinqTestInput("Int16 6", b => getQuery(b).Select(doc => new { value = 6 })),
                new LinqTestInput("Int16 MinValue", b => getQuery(b).Select(doc => new { value = Int16.MinValue })),
                new LinqTestInput("Int16 MaxValue", b => getQuery(b).Select(doc => new { value = Int16.MaxValue })),
                // Int32
                new LinqTestInput("Int32 7", b => getQuery(b).Select(doc => new { value = 7 })),
                new LinqTestInput("Int32 MinValue", b => getQuery(b).Select(doc => new { value = Int32.MinValue })),
                new LinqTestInput("Int32 MaxValue", b => getQuery(b).Select(doc => new { value = Int32.MaxValue })),
                // Int64
                new LinqTestInput("Int64 8", b => getQuery(b).Select(doc => new { value = 8 })),
                new LinqTestInput("Int64 MinValue", b => getQuery(b).Select(doc => new { value = Int64.MinValue })),
                new LinqTestInput("Int64 MaxValue", b => getQuery(b).Select(doc => new { value = Int64.MaxValue })),
                // Decimal
                new LinqTestInput("Decimal 9", b => getQuery(b).Select(doc => new { value = 9 })),
                new LinqTestInput("Decimal MinValue", b => getQuery(b).Select(doc => new { value = Decimal.MinValue })),
                new LinqTestInput("Decimal MaxValue", b => getQuery(b).Select(doc => new { value = Decimal.MaxValue })),
                // Double
                new LinqTestInput("Double 10", b => getQuery(b).Select(doc => new { value = 10 })),
                new LinqTestInput("Double MinValue", b => getQuery(b).Select(doc => new { value = Double.MinValue })),
                new LinqTestInput("Double MaxValue", b => getQuery(b).Select(doc => new { value = Double.MaxValue })),
                // Single
                new LinqTestInput("Single 11", b => getQuery(b).Select(doc => new { value = 11 })),
                new LinqTestInput("Single MinValue", b => getQuery(b).Select(doc => new { value = Single.MinValue })),
                new LinqTestInput("Single MaxValue", b => getQuery(b).Select(doc => new { value = Single.MaxValue })),
                // Bool
                new LinqTestInput("Bool true", b => getQuery(b).Select(doc => new { value = true })),
                new LinqTestInput("Bool false", b => getQuery(b).Select(doc => new { value = false }))
            };
            // String
            string nullStr = null;
            inputs.Add(new LinqTestInput("String empty", b => getQuery(b).Select(doc => new { value = String.Empty })));
            inputs.Add(new LinqTestInput("String str1", b => getQuery(b).Select(doc => new { value = "str1" })));
            inputs.Add(new LinqTestInput("String special", b => getQuery(b).Select(doc => new { value = "long string with speicial characters (*)(*)__)((*&*(&*&'*(&)()(*_)()(_(_)*!@#$%^ and numbers 132654890" })));
            inputs.Add(new LinqTestInput("String unicode", b => getQuery(b).Select(doc => new { value = "unicode 㐀㐁㨀㨁䶴䶵" })));
            inputs.Add(new LinqTestInput("null object", b => getQuery(b).Select(doc => new { value = nullStr })));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestTypeCheckFunctions()
        {
            // IsDefined, IsNull, and IsPrimitive are not supported on the client side.
            // Partly because IsPrimitive is not trivial to implement.
            // Therefore these methods are verified with baseline only.
            List<DataObject> data = new List<DataObject>();
            IOrderedQueryable<DataObject> query = testContainer.GetItemLinqQueryable<DataObject>(allowSynchronousQueryExecution: true);
            Func<bool, IQueryable<DataObject>> getQuery = useQuery => useQuery ? query : data.AsQueryable();

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("IsDefined array", b => getQuery(b).Select(doc => doc.ArrayField.IsDefined())),
                new LinqTestInput("IsDefined string", b => getQuery(b).Where(doc => doc.StringField.IsDefined())),
                new LinqTestInput("IsNull array", b => getQuery(b).Select(doc => doc.ArrayField.IsNull())),
                new LinqTestInput("IsNull string", b => getQuery(b).Where(doc => doc.StringField.IsNull())),
                new LinqTestInput("IsPrimitive array", b => getQuery(b).Select(doc => doc.ArrayField.IsPrimitive())),
                new LinqTestInput("IsPrimitive string", b => getQuery(b).Where(doc => doc.StringField.IsPrimitive()))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializer()
        {
            const int Records = 100;
            const int NumAbsMax = 500;
            const int MaxStringLength = 100;
            DataObject createDataObj(Random random)
            {
                DataObject obj = new DataObject
                {
                    NumericField = random.Next(NumAbsMax * 2) - NumAbsMax,
                    StringField = LinqTestsCommon.RandomString(random, random.Next(MaxStringLength)),
                    Id = Guid.NewGuid().ToString(),
                    Pk = "Test"
                };
                return obj;
            }
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Select w/ DataObject initializer", b => getQuery(b).Select(doc => new DataObject() { NumericField = doc.NumericField, StringField = doc.StringField })),
                new LinqTestInput("Filter w/ DataObject initializer", b => getQuery(b).Where(doc => doc == new DataObject() { NumericField = doc.NumericField, StringField = doc.StringField }))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestStringEnumJsonConverter()
        {
            const int Records = 100;
            int testEnumCount = Enum.GetNames(typeof(TestEnum)).Length;
            int testEnum2Count = Enum.GetNames(typeof(TestEnum2)).Length;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                DataObject obj = new DataObject
                {
                    EnumField1 = (TestEnum)random.Next(testEnumCount),
                    EnumField2 = (TestEnum)random.Next(testEnumCount)
                };
                if (random.NextDouble() < 0.5)
                {
                    obj.NullableEnum1 = (TestEnum)random.Next(testEnumCount);
                }
                if (random.NextDouble() < 0.5)
                {
                    obj.NullableEnum2 = (TestEnum)random.Next(testEnumCount);
                }
                obj.EnumNumber = (TestEnum2)random.Next(testEnum2Count);
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Filter w/ enum field comparison", b => getQuery(b).Where(doc => doc.EnumField1 == TestEnum.One)),
                new LinqTestInput("Filter w/ enum field comparison #2", b => getQuery(b).Where(doc => TestEnum.One == doc.EnumField1)),
                new LinqTestInput("Filter w/ enum field comparison #3", b => getQuery(b).Where(doc => doc.EnumField2 == TestEnum.Two)),
                new LinqTestInput("Filter w/ nullable enum field comparison", b => getQuery(b).Where(doc => doc.NullableEnum1 == TestEnum.One)),
                new LinqTestInput("Filter w/ nullable enum field comparison #2", b => getQuery(b).Where(doc => doc.NullableEnum2 == TestEnum.Two)),
                new LinqTestInput("Filter w/ enum field comparison #4", b => getQuery(b).Where(doc => doc.EnumNumber == TestEnum2.Zero))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestDateTimeJsonConverter()
        {
            const int Records = 100;
            DateTime midDateTime = new DateTime(2016, 9, 13, 0, 0, 0);
            Func<Random, DataObject> createDataObj = (random) =>
            {
                DataObject obj = new DataObject();
                obj.IsoTime = LinqTestsCommon.RandomDateTime(random, midDateTime);
                obj.UnixTime = LinqTestsCommon.RandomDateTime(random, midDateTime);
                obj.DefaultTime = LinqTestsCommon.RandomDateTime(random, midDateTime);
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("IsoDateTimeConverter = filter", b => getQuery(b).Where(doc => doc.IsoTime == new DateTime(2016, 9, 13, 0, 0, 0))),
                new LinqTestInput("IsoDateTimeConverter > filter", b => getQuery(b).Where(doc => doc.IsoTime > new DateTime(2016, 9, 13, 0, 0, 0))),
                new LinqTestInput("IsoDateTimeConverter < filter", b => getQuery(b).Where(doc => doc.IsoTime < new DateTime(2016, 9, 13, 0, 0, 0))),
                new LinqTestInput("UnixDateTimeConverter = filter", b => getQuery(b).Where(doc => doc.UnixTime == new DateTime(2016, 9, 13, 0, 0, 0))),
                new LinqTestInput("UnixDateTimeConverter > filter", b => getQuery(b).Where(doc => doc.UnixTime > new DateTime(2016, 9, 13, 0, 0, 0))),
                new LinqTestInput("UnixDateTimeConverter < filter", b => getQuery(b).Where(doc => doc.UnixTime < new DateTime(2016, 9, 13, 0, 0, 0))),
                new LinqTestInput("Default (ISO) = filter", b => getQuery(b).Where(doc => doc.DefaultTime == new DateTime(2016, 9, 13, 0, 0, 0))),
                new LinqTestInput("Default (ISO) > filter", b => getQuery(b).Where(doc => doc.DefaultTime > new DateTime(2016, 9, 13, 0, 0, 0))),
                new LinqTestInput("Default (ISO) < filter", b => getQuery(b).Where(doc => doc.DefaultTime < new DateTime(2016, 9, 13, 0, 0, 0)))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestNullableFields()
        {
            const int Records = 5;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                DataObject obj = new DataObject();
                if (random.NextDouble() < 0.5)
                {
                    if (random.NextDouble() < 0.1)
                    {
                        obj.NullableField = 5;
                    }
                    else
                    {
                        obj.NullableField = random.Next();
                    }
                }
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Filter", b => getQuery(b).Where(doc => doc.NullableField == 5)),
                new LinqTestInput("Filter w/ .Value", b => getQuery(b).Where(doc => doc.NullableField.HasValue && doc.NullableField.Value == 5)),
                new LinqTestInput("Filter w/ .HasValue", b => getQuery(b).Where(doc => doc.NullableField.HasValue)),
                new LinqTestInput("Filter w/ .HasValue comparison true", b => getQuery(b).Where(doc => doc.NullableField.HasValue == true)),
                new LinqTestInput("Filter w/ .HasValue comparison false", b => getQuery(b).Where(doc => doc.NullableField.HasValue == false)),
                new LinqTestInput("Filter w/ .HasValue not", b => getQuery(b).Where(doc => !doc.NullableField.HasValue))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Ignore]
        public void TestMathFunctionsIssues()
        {
            // These are issues in scenarios with integers casting
            // the Linq query returns double values which got casted to the integer type
            // the casting is a rounded behavior e.g. 3.567 would become 4
            // whereas the casting behavior for data results is truncate behavior

            const int Records = 20;
            Func<Random, DataObject> createDataObj = (random) => new DataObject()
            {
                NumericField = (1.0 * random.Next()) - (random.NextDouble() / 2)
            };
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Select", b => getQuery(b).Select(doc => (int)doc.NumericField)),
                new LinqTestInput("Abs int", b => getQuery(b)
                    .Where(doc => doc.NumericField >= int.MinValue && doc.NumericField <= int.MaxValue)
                    .Select(doc => Math.Abs((int)doc.NumericField))),
                new LinqTestInput("Abs long", b => getQuery(b)
                    .Where(doc => doc.NumericField >= long.MinValue && doc.NumericField <= long.MaxValue)
                    .Select(doc => Math.Abs((long)doc.NumericField))),
                new LinqTestInput("Abs sbyte", b => getQuery(b)
                    .Where(doc => doc.NumericField >= sbyte.MinValue && doc.NumericField <= sbyte.MaxValue)
                    .Select(doc => Math.Abs((sbyte)doc.NumericField))),
                new LinqTestInput("Abs short", b => getQuery(b)
                    .Where(doc => doc.NumericField >= short.MinValue && doc.NumericField <= short.MaxValue)
                    .Select(doc => Math.Abs((short)doc.NumericField))),
                new LinqTestInput("Sign int", b => getQuery(b).Select(doc => Math.Sign((int)doc.NumericField))),
                new LinqTestInput("Sign long", b => getQuery(b).Select(doc => Math.Sign((long)doc.NumericField))),
                new LinqTestInput("Sign sbyte", b => getQuery(b)
                    .Where(doc => doc.NumericField >= sbyte.MinValue && doc.NumericField <= sbyte.MaxValue)
                    .Select(doc => Math.Sign((sbyte)doc.NumericField))),
                new LinqTestInput("Sign short", b => getQuery(b)
                    .Where(doc => doc.NumericField >= short.MinValue && doc.NumericField <= short.MaxValue)
                    .Select(doc => Math.Sign((short)doc.NumericField))),

                new LinqTestInput("Round decimal", b => getQuery(b).Select(doc => Math.Round((decimal)doc.NumericField))),

                new LinqTestInput("Tan", b => getQuery(b).Select(doc => Math.Tan(doc.NumericField)))
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMathFunctions()
        {
            const int Records = 20;
            // when doing verification between data and query results for integer type (int, long, short, sbyte, etc.)
            // the backend returns double values which got casted to the integer type
            // the casting is a rounded behavior e.g. 3.567 would become 4, whereas the casting behavior for data results is truncate
            // therefore, for test data, we just want to have real number with the decimal part < 0.5.
            DataObject createDataObj(Random random) => new DataObject()
            {
                NumericField = (1.0 * random.Next()) + (random.NextDouble() / 2),
                DecimalField = (decimal)((1.0 * random.Next()) + random.NextDouble()) / 2,
                IntField = 1.0 * random.Next(),
                Id = Guid.NewGuid().ToString(),
                Pk = "Test"
            };
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            // some scenarios below requires input to be within data type range in order to be correct
            // therefore, we filter the right inputs for them accordingly.
            // e.g. float has a precision up to 7 digits so the inputs needs to be within that range before being casted to float.
            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                // Abs
                new LinqTestInput("Abs decimal", b => getQuery(b).Select(doc => Math.Abs(doc.DecimalField))),

                new LinqTestInput("Abs double", b => getQuery(b).Select(doc => Math.Abs((double)doc.NumericField))),
                new LinqTestInput("Abs float", b => getQuery(b)
                    .Where(doc => doc.NumericField > -1000000 && doc.NumericField < 1000000)
                    .Select(doc => Math.Abs((float)doc.NumericField))),
                //inputs.Add(new LinqTestInput("Select", b => getQuery(b)
                //    .Select(doc => (int)doc.NumericField)));
                new LinqTestInput("Abs int", b => getQuery(b)
                    .Where(doc => doc.IntField >= int.MinValue && doc.IntField <= int.MaxValue)
                    .Select(doc => Math.Abs((int)doc.IntField))),
                new LinqTestInput("Abs long", b => getQuery(b)
                    .Where(doc => doc.NumericField >= long.MinValue && doc.NumericField <= long.MaxValue)
                    .Select(doc => Math.Abs((long)doc.NumericField))),
                new LinqTestInput("Abs sbyte", b => getQuery(b)
                    .Where(doc => doc.NumericField >= sbyte.MinValue && doc.NumericField <= sbyte.MaxValue)
                    .Select(doc => Math.Abs((sbyte)doc.NumericField))),
                new LinqTestInput("Abs short", b => getQuery(b)
                    .Where(doc => doc.NumericField >= short.MinValue && doc.NumericField <= short.MaxValue)
                    .Select(doc => Math.Abs((short)doc.NumericField))),
                // Acos
                new LinqTestInput("Acos", b => getQuery(b)
                    .Where(doc => doc.NumericField >= -1 && doc.NumericField <= 1)
                    .Select(doc => Math.Acos(doc.NumericField))),
                // Asin
                new LinqTestInput("Asin", b => getQuery(b)
                    .Where(doc => doc.NumericField >= -1 && doc.NumericField <= 1)
                    .Select(doc => Math.Asin(doc.NumericField))),
                // Atan
                new LinqTestInput("Atan", b => getQuery(b).Select(doc => Math.Atan2(doc.NumericField, 1))),
                // Ceiling
                new LinqTestInput("Ceiling decimal", b => getQuery(b).Select(doc => Math.Ceiling((decimal)doc.NumericField))),
                new LinqTestInput("Ceiling double", b => getQuery(b).Select(doc => Math.Ceiling((double)doc.NumericField))),
                new LinqTestInput("Ceiling float", b => getQuery(b)
                    .Where(doc => doc.NumericField > -1000000 && doc.NumericField < 1000000)
                    .Select(doc => Math.Ceiling((float)doc.NumericField))),
                // Cos
                new LinqTestInput("Cos", b => getQuery(b).Select(doc => Math.Cos(doc.NumericField))),
                // Exp
                new LinqTestInput("Exp", b => getQuery(b)
                    .Where(doc => doc.NumericField >= -3 && doc.NumericField <= 3)
                    .Select(doc => Math.Exp(doc.NumericField))),
                // Floor
                new LinqTestInput("Floor decimal", b => getQuery(b).Select(doc => Math.Floor((decimal)doc.NumericField))),
                new LinqTestInput("Floor double", b => getQuery(b).Select(doc => Math.Floor((double)doc.NumericField))),
                new LinqTestInput("Floor float", b => getQuery(b)
                    .Where(doc => doc.NumericField > -1000000 && doc.NumericField < 1000000)
                    .Select(doc => Math.Floor((float)doc.NumericField))),
                // Log
                new LinqTestInput("Log", b => getQuery(b)
                    .Where(doc => doc.NumericField != 0)
                    .Select(doc => Math.Log(doc.NumericField))),
                new LinqTestInput("Log 1", b => getQuery(b)
                    .Where(doc => doc.NumericField != 0)
                    .Select(doc => Math.Log(doc.NumericField, 2))),
                new LinqTestInput("Log10", b => getQuery(b)
                    .Where(doc => doc.NumericField != 0)
                    .Select(doc => Math.Log10(doc.NumericField))),
                // Pow
                new LinqTestInput("Pow", b => getQuery(b).Select(doc => Math.Pow(doc.NumericField, 1))),
                // Round
                new LinqTestInput("Round double", b => getQuery(b).Select(doc => Math.Round((double)doc.NumericField))),
                // Sign
                new LinqTestInput("Sign decimal", b => getQuery(b).Select(doc => Math.Sign((decimal)doc.NumericField))),
                new LinqTestInput("Sign double", b => getQuery(b).Select(doc => Math.Sign((double)doc.NumericField))),
                new LinqTestInput("Sign float", b => getQuery(b)
                    .Where(doc => doc.NumericField > -1000000 && doc.NumericField < 1000000)
                    .Select(doc => Math.Sign((float)doc.NumericField))),
                new LinqTestInput("Sign int", b => getQuery(b).Select(doc => Math.Sign((int)doc.NumericField))),
                new LinqTestInput("Sign long", b => getQuery(b).Select(doc => Math.Sign((long)doc.NumericField))),
                new LinqTestInput("Sign sbyte", b => getQuery(b)
                    .Where(doc => doc.NumericField >= sbyte.MinValue && doc.NumericField <= sbyte.MaxValue)
                    .Select(doc => Math.Sign((sbyte)doc.NumericField))),
                new LinqTestInput("Sign short", b => getQuery(b)
                    .Where(doc => doc.NumericField >= short.MinValue && doc.NumericField <= short.MaxValue)
                    .Select(doc => Math.Sign((short)doc.NumericField))),
                // Sin
                new LinqTestInput("Sin", b => getQuery(b).Select(doc => Math.Sin(doc.NumericField))),
                // Sqrt
                new LinqTestInput("Sqrt", b => getQuery(b).Select(doc => Math.Sqrt(doc.NumericField))),
                // Truncate
                new LinqTestInput("Truncate decimal", b => getQuery(b).Select(doc => Math.Truncate((decimal)doc.NumericField))),
                new LinqTestInput("Truncate double", b => getQuery(b).Select(doc => Math.Truncate((double)doc.NumericField)))
            };
            this.ExecuteTestSuite(inputs);
        }

        private Func<bool, IQueryable<DataObject>> CreateDataTestStringFunctions()
        {
            const int Records = 100;
            const int MaxStrLength = 100;
            const int MinStrLength = 5;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                StringBuilder sb = new StringBuilder(LinqTestsCommon.RandomString(random, random.Next(MaxStrLength - MinStrLength) + MinStrLength));
                if (random.NextDouble() < 0.5)
                {
                    // make a "str" substring for StartsWith, EndsWith, and IndexOf
                    int p = random.Next(sb.Length - 3);
                    sb[p] = 's';
                    sb[p] = 't';
                    sb[p] = 'r';
                }
                return new DataObject()
                {
                    StringField = sb.ToString(),
                    Id = Guid.NewGuid().ToString(),
                    Pk = "Test"
                };
            };
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);
            return getQuery;
        }

        [TestMethod]
        [Ignore]
        public void TestStringFunctionsIssues()
        {
            // issue when doing string.Reverse()
            List<LinqTestInput> inputs = new List<LinqTestInput>();
            Func<bool, IQueryable<DataObject>> getQuery = this.CreateDataTestStringFunctions();
            inputs.Add(new LinqTestInput("Reverse", b => getQuery(b).Select(doc => doc.StringField.Reverse())));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestStringFunctions()
        {
            List<string> emptyList = new List<string>();
            List<string> constantList = new List<string>() { "one", "two", "three" };
            string[] constantArray = new string[] { "one", "two", "three" };

            Func<bool, IQueryable<DataObject>> getQuery = this.CreateDataTestStringFunctions();

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                // Concat
                new LinqTestInput("Concat 2", b => getQuery(b).Select(doc => string.Concat(doc.StringField, "str"))),
                new LinqTestInput("Concat 3", b => getQuery(b).Select(doc => string.Concat(doc.StringField, "str1", "str2"))),
                new LinqTestInput("Concat 4", b => getQuery(b).Select(doc => string.Concat(doc.StringField, "str1", "str2", "str3"))),
                new LinqTestInput("Concat 5", b => getQuery(b).Select(doc => string.Concat(doc.StringField, "str1", "str2", "str3", "str4"))),
                new LinqTestInput("Concat array", b => getQuery(b).Select(doc => string.Concat(new string[] { doc.StringField, "str1", "str2", "str3", "str4" }))),
                // Contains
                new LinqTestInput("Contains w/ string", b => getQuery(b).Select(doc => doc.StringField.Contains("str"))),
                new LinqTestInput("Contains w/ char", b => getQuery(b).Select(doc => doc.StringField.Contains('c'))),
                new LinqTestInput("Contains in string constant", b => getQuery(b).Select(doc => "str".Contains(doc.StringField))),
                // Contains (case-sensitive)
                new LinqTestInput("Contains w/ string (case-sensitive)", b => getQuery(b).Select(doc => doc.StringField.Contains("Str", StringComparison.Ordinal))),
                new LinqTestInput("Contains in string constant (case-sensitive)", b => getQuery(b).Select(doc => "sTr".Contains(doc.StringField, StringComparison.Ordinal))),
                // Contains (case-insensitive)
                new LinqTestInput("Contains w/ string (case-insensitive)", b => getQuery(b).Select(doc => doc.StringField.Contains("Str", StringComparison.OrdinalIgnoreCase))),
                new LinqTestInput("Contains in string constant (case-insensitive)", b => getQuery(b).Select(doc => "sTr".Contains(doc.StringField, StringComparison.OrdinalIgnoreCase))),
                // Contains with constants should be IN
                new LinqTestInput("Contains in constant list", b => getQuery(b).Select(doc => constantList.Contains(doc.StringField))),
                new LinqTestInput("Contains in constant array", b => getQuery(b).Select(doc => constantArray.Contains(doc.StringField))),
                new LinqTestInput("Contains in constant list in filter", b => getQuery(b).Select(doc => doc.StringField).Where(str => constantList.Contains(str))),
                new LinqTestInput("Contains in constant array in filter", b => getQuery(b).Select(doc => doc.StringField).Where(str => constantArray.Contains(str))),
                // NOT IN
                new LinqTestInput("Not in constant list", b => getQuery(b).Select(doc => !constantList.Contains(doc.StringField))),
                new LinqTestInput("Not in constant array", b => getQuery(b).Select(doc => !constantArray.Contains(doc.StringField))),
                new LinqTestInput("Filter not in constant list", b => getQuery(b).Select(doc => doc.StringField).Where(str => !constantList.Contains(str))),
                new LinqTestInput("Filter not in constant array", b => getQuery(b).Select(doc => doc.StringField).Where(str => !constantArray.Contains(str))),
                // Empty list
                new LinqTestInput("Empty list contains", b => getQuery(b).Select(doc => emptyList.Contains(doc.StringField))),
                new LinqTestInput("Empty list not contains", b => getQuery(b).Select(doc => !emptyList.Contains(doc.StringField))),
                // EndsWith
                new LinqTestInput("EndsWith", b => getQuery(b).Select(doc => doc.StringField.EndsWith("str"))),
                new LinqTestInput("Constant string EndsWith", b => getQuery(b).Select(doc => "str".EndsWith(doc.StringField))),
                // EndsWith (case-sensitive)
                new LinqTestInput("EndsWith (case-sensitive)", b => getQuery(b).Select(doc => doc.StringField.EndsWith("stR", StringComparison.Ordinal))),
                new LinqTestInput("Constant string EndsWith (case-sensitive)", b => getQuery(b).Select(doc => "STR".EndsWith(doc.StringField, StringComparison.Ordinal))),
                // EndsWith (case-insensitive)
                new LinqTestInput("EndsWith (case-insensitive)", b => getQuery(b).Select(doc => doc.StringField.EndsWith("stR", StringComparison.OrdinalIgnoreCase))),
                new LinqTestInput("Constant string EndsWith (case-insensitive)", b => getQuery(b).Select(doc => "STR".EndsWith(doc.StringField, StringComparison.OrdinalIgnoreCase))),
                // IndexOf
                new LinqTestInput("IndexOf char", b => getQuery(b).Select(doc => doc.StringField.IndexOf('c'))),
                new LinqTestInput("IndexOf string", b => getQuery(b).Select(doc => doc.StringField.IndexOf("str"))),
                new LinqTestInput("IndexOf char w/ startIndex", b => getQuery(b).Select(doc => doc.StringField.IndexOf('c', 0))),
                new LinqTestInput("IndexOf string w/ startIndex", b => getQuery(b).Select(doc => doc.StringField.IndexOf("str", 0))),
                // Count
                new LinqTestInput("Count", b => getQuery(b).Select(doc => doc.StringField.Count())),
                // ToLower
                new LinqTestInput("ToLower", b => getQuery(b).Select(doc => doc.StringField.ToLower())),
                // TrimStart
                new LinqTestInput("TrimStart", b => getQuery(b).Select(doc => doc.StringField.TrimStart())),
                // Replace
                new LinqTestInput("Replace char", b => getQuery(b).Select(doc => doc.StringField.Replace('c', 'a'))),
                new LinqTestInput("Replace string", b => getQuery(b).Select(doc => doc.StringField.Replace("str", "str2"))),
                // TrimEnd
                new LinqTestInput("TrimEnd", b => getQuery(b).Select(doc => doc.StringField.TrimEnd())),
                //StartsWith
                new LinqTestInput("StartsWith", b => getQuery(b).Select(doc => doc.StringField.StartsWith("str"))),
                new LinqTestInput("String constant StartsWith", b => getQuery(b).Select(doc => "str".StartsWith(doc.StringField))),
                //StartsWith (case-sensitive)
                new LinqTestInput("StartsWith (case-sensitive)", b => getQuery(b).Select(doc => doc.StringField.StartsWith("Str", StringComparison.Ordinal))),
                new LinqTestInput("String constant StartsWith (case-sensitive)", b => getQuery(b).Select(doc => "sTr".StartsWith(doc.StringField, StringComparison.Ordinal))),
                //StartsWith (case-insensitive)
                new LinqTestInput("StartsWith (case-insensitive)", b => getQuery(b).Select(doc => doc.StringField.StartsWith("Str", StringComparison.OrdinalIgnoreCase))),
                new LinqTestInput("String constant StartsWith (case-insensitive)", b => getQuery(b).Select(doc => "sTr".StartsWith(doc.StringField, StringComparison.OrdinalIgnoreCase))),
                // Substring
                new LinqTestInput("Substring", b => getQuery(b).Select(doc => doc.StringField.Substring(0, 1))),
                // ToUpper
                new LinqTestInput("ToUpper", b => getQuery(b).Select(doc => doc.StringField.ToUpper()))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestArrayFunctions()
        {
            const int Records = 100;
            const int MaxAbsValue = 10;
            const int MaxArraySize = 50;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                DataObject obj = new DataObject();
                obj.ArrayField = new int[random.Next(MaxArraySize)];
                for (int i = 0; i < obj.ArrayField.Length; ++i)
                {
                    obj.ArrayField[i] = random.Next(MaxAbsValue * 2) - MaxAbsValue;
                }
                obj.EnumerableField = new List<int>();
                for (int i = 0; i < random.Next(MaxArraySize); ++i)
                {
                    obj.EnumerableField.Add(random.Next(MaxAbsValue * 2) - MaxAbsValue);
                }
                obj.NumericField = random.Next(MaxAbsValue * 2) - MaxAbsValue;
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<int> emptyList = new List<int>();
            List<int> constantList = new List<int>() { 1, 2, 3 };
            int[] constantArray = new int[] { 1, 2, 3 };

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                // Concat
                new LinqTestInput("Concat another array", b => getQuery(b).Select(doc => doc.ArrayField.Concat(new int[] { 1, 2, 3 }))),
                new LinqTestInput("Concat constant list", b => getQuery(b).Select(doc => doc.ArrayField.Concat(constantList))),
                new LinqTestInput("Concat w/ ArrayField itself", b => getQuery(b).Select(doc => doc.ArrayField.Concat(doc.ArrayField))),
                new LinqTestInput("Concat enumerable w/ constant list", b => getQuery(b).Select(doc => doc.EnumerableField.Concat(constantList))),
                // Contains
                new LinqTestInput("ArrayField contains", b => getQuery(b).Select(doc => doc.ArrayField.Contains(1))),
                new LinqTestInput("EnumerableField contains", b => getQuery(b).Select(doc => doc.EnumerableField.Contains(1))),
                new LinqTestInput("EnumerableField not contains", b => getQuery(b).Select(doc => !doc.EnumerableField.Contains(1))),
                // Contains with constants should be IN
                new LinqTestInput("Constant list contains numeric field", b => getQuery(b).Select(doc => constantList.Contains((int)doc.NumericField))),
                new LinqTestInput("Constant array contains numeric field", b => getQuery(b).Select(doc => constantArray.Contains((int)doc.NumericField))),
                new LinqTestInput("Constant list contains numeric field in filter", b => getQuery(b).Select(doc => doc.NumericField).Where(number => constantList.Contains((int)number))),
                new LinqTestInput("Constant array contains numeric field in filter", b => getQuery(b).Select(doc => doc.NumericField).Where(number => constantArray.Contains((int)number))),
                // NOT IN
                new LinqTestInput("Constant list not contains", b => getQuery(b).Select(doc => !constantList.Contains((int)doc.NumericField))),
                new LinqTestInput("Constant array not contains", b => getQuery(b).Select(doc => !constantArray.Contains((int)doc.NumericField))),
                new LinqTestInput("Filter constant list not contains", b => getQuery(b).Select(doc => doc.NumericField).Where(number => !constantList.Contains((int)number))),
                new LinqTestInput("Filter constant array not contains", b => getQuery(b).Select(doc => doc.NumericField).Where(number => !constantArray.Contains((int)number))),
                // Empty list
                new LinqTestInput("Empty list contains", b => getQuery(b).Select(doc => emptyList.Contains((int)doc.NumericField))),
                new LinqTestInput("Empty list not contains", b => getQuery(b).Select(doc => !emptyList.Contains((int)doc.NumericField))),
                // Count
                new LinqTestInput("Count ArrayField", b => getQuery(b).Select(doc => doc.ArrayField.Count())),
                new LinqTestInput("Count EnumerableField", b => getQuery(b).Select(doc => doc.EnumerableField.Count()))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestSpatialFunctions()
        {
            // The spatial functions are not supported on the client side.
            // Therefore these methods are verified with baselines only.
            List<DataObject> data = new List<DataObject>();
            IOrderedQueryable<DataObject> query = testContainer.GetItemLinqQueryable<DataObject>(allowSynchronousQueryExecution: true);
            Func<bool, IQueryable<DataObject>> getQuery = useQuery => useQuery ? query : data.AsQueryable();

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                // Distance
                new LinqTestInput("Point distance", b => getQuery(b).Select(doc => doc.Point.Distance(new Point(20.1, 20))))
            };
            // Within
            Polygon polygon = new Polygon(
                new[]
                    {
                        new Position(10, 10),
                        new Position(30, 10),
                        new Position(30, 30),
                        new Position(10, 30),
                        new Position(10, 10),
                    });
            inputs.Add(new LinqTestInput("Point within polygon", b => getQuery(b).Select(doc => doc.Point.Within(polygon))));
            // Intersects
            inputs.Add(new LinqTestInput("Point intersects with polygon", b => getQuery(b).Where(doc => doc.Point.Intersects(polygon))));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestSpecialMethods()
        {
            const int Records = 100;
            const int MaxStringLength = 20;
            const int MaxArrayLength = 10;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                DataObject obj = new DataObject();
                obj.StringField = random.NextDouble() < 0.1 ? "str" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.EnumerableField = new List<int>();
                for (int i = 0; i < random.Next(MaxArrayLength - 1) + 1; ++i)
                {
                    obj.EnumerableField.Add(random.Next());
                }
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                // Equals
                new LinqTestInput("Equals", b => getQuery(b).Select(doc => doc.StringField.Equals("str"))),
                // Equals (case-sensitive)
                new LinqTestInput("Equals (case-sensitive)", b => getQuery(b).Select(doc => doc.StringField.Equals("STR", StringComparison.Ordinal))),
                // Equals (case-insensitive)
                new LinqTestInput("Equals (case-insensitive)", b => getQuery(b).Select(doc => doc.StringField.Equals("STR", StringComparison.OrdinalIgnoreCase))),
                // ToString
                new LinqTestInput("ToString", b => getQuery(b).Select(doc => doc.StringField.ToString())),
                // get_item
                new LinqTestInput("get_item", b => getQuery(b).Select(doc => doc.EnumerableField[0]))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestConditional()
        {
            const int Records = 100;
            const int MaxStringLength = 20;
            const int MaxArrayLength = 10;
            const int MaxAbsValue = 10;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                DataObject obj = new DataObject();
                obj.StringField = random.NextDouble() < 0.1 ? "str" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.NumericField = random.Next(MaxAbsValue * 2) - MaxAbsValue;
                obj.ArrayField = new int[random.Next(MaxArrayLength)];
                for (int i = 0; i < obj.ArrayField.Length; ++i)
                {
                    obj.ArrayField[i] = random.Next(MaxAbsValue * 2) - MaxAbsValue;
                }
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Filter w/ ternary conditional ?", b => getQuery(b).Where(doc => doc.NumericField < 3 ? true : false).Select(doc => doc.StringField)),
                new LinqTestInput("Filter w/ ternary conditional ? and contains", b => getQuery(b).Where(doc => doc.NumericField == (doc.ArrayField.Contains(1) ? 1 : 5)).Select(doc => doc.StringField)),
                new LinqTestInput("Filter w/ ternary conditional ? and contains #2", b => getQuery(b).Where(doc => doc.NumericField == (doc.StringField == "str" ? 1 : doc.ArrayField.Contains(1) ? 3 : 4)).Select(doc => doc.StringField))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestCoalesce()
        {
            const int Records = 100;
            const int MaxStringLength = 20;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                DataObject obj = new DataObject();
                obj.StringField = random.NextDouble() < 0.1 ? "str" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.StringField2 = random.NextDouble() < 0.1 ? "str" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.NumericField = random.Next();
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Coalesce", b => getQuery(b).Select(doc => doc.StringField ?? "str")),
                new LinqTestInput("Filter with coalesce comparison", b => getQuery(b).Where(doc => doc.StringField == (doc.StringField2 ?? "str")).Select(doc => doc.NumericField)),
                new LinqTestInput("Filter with coalesce comparison #2", b => getQuery(b).Select(doc => doc.StringField).Where(str => (str ?? "str") == "str2"))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [TestCategory("Ignore")]
        public void TestStringCompareTo()
        {
            IOrderedQueryable<DataObject> testQuery = testContainer.GetItemLinqQueryable<DataObject>(allowSynchronousQueryExecution: true);

            const int Records = 100;
            const int MaxStringLength = 20;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                DataObject obj = new DataObject();
                obj.StringField = LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.StringField2 = random.NextDouble() < 0.5 ? obj.StringField : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                // projected compare
                new LinqTestInput("Projected CompareTo ==", b => getQuery(b).Select(doc => doc.StringField.CompareTo(doc.StringField2) == 0)),
                new LinqTestInput("Projected CompareTo >", b => getQuery(b).Select(doc => doc.StringField.CompareTo(doc.StringField2) > 0)),
                new LinqTestInput("Projected CompareTo >=", b => getQuery(b).Select(doc => doc.StringField.CompareTo(doc.StringField2) >= 0)),
                new LinqTestInput("Projected CompareTo <", b => getQuery(b).Select(doc => doc.StringField.CompareTo(doc.StringField2) < 0)),
                new LinqTestInput("Projected CompareTo <=", b => getQuery(b).Select(doc => doc.StringField.CompareTo(doc.StringField2) <= 0)),
                // static strings
                new LinqTestInput("CompareTo static string ==", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") == 0)),
                new LinqTestInput("CompareTo static string >", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") > 0)),
                new LinqTestInput("CompareTo static string >=", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") >= 0)),
                new LinqTestInput("CompareTo static string <", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") < 0)),
                new LinqTestInput("CompareTo static string <=", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") <= 0)),
                // reverse operands
                new LinqTestInput("Projected CompareTo == reverse operands", b => getQuery(b).Select(doc => 0 == doc.StringField.CompareTo(doc.StringField2))),
                new LinqTestInput("Projected CompareTo < reverse operands", b => getQuery(b).Select(doc => 0 < doc.StringField.CompareTo(doc.StringField2))),
                new LinqTestInput("Projected CompareTo <= reverse operands", b => getQuery(b).Select(doc => 0 <= doc.StringField.CompareTo(doc.StringField2))),
                new LinqTestInput("Projected CompareTo > reverse operands", b => getQuery(b).Select(doc => 0 > doc.StringField.CompareTo(doc.StringField2))),
                new LinqTestInput("Projected CompareTo >= reverse operands", b => getQuery(b).Select(doc => 0 >= doc.StringField.CompareTo(doc.StringField2))),
                // errors Invalid compare value
                new LinqTestInput("CompareTo > 1", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") > 1)),
                new LinqTestInput("CompareTo == 1", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") == 1)),
                new LinqTestInput("CompareTo == -1", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") == -1)),
                // errors Invalid operator
                new LinqTestInput("CompareTo | 0", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") | 0)),
                new LinqTestInput("CompareTo & 0", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") & 0)),
                new LinqTestInput("CompareTo ^ 0", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") ^ 0))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestUDFs()
        {
            // The UDFs invokation are not supported on the client side.
            // Therefore these methods are verified with baselines only.
            List<DataObject> data = new List<DataObject>();
            IOrderedQueryable<DataObject> query = testContainer.GetItemLinqQueryable<DataObject>(allowSynchronousQueryExecution: true);
            Func<bool, IQueryable<DataObject>> getQuery = useQuery => useQuery ? query : data.AsQueryable();

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("No param", b => getQuery(b).Select(f => CosmosLinq.InvokeUserDefinedFunction("NoParameterUDF"))),
                new LinqTestInput("Single param", b => getQuery(b).Select(doc => CosmosLinq.InvokeUserDefinedFunction("SingleParameterUDF", doc.NumericField))),
                new LinqTestInput("Single param w/ array", b => getQuery(b).Select(doc => CosmosLinq.InvokeUserDefinedFunction("SingleParameterUDFWithArray", doc.ArrayField))),
                new LinqTestInput("Multi param", b => getQuery(b).Select(doc => CosmosLinq.InvokeUserDefinedFunction("MultiParamterUDF", doc.NumericField, doc.StringField, doc.Point))),
                new LinqTestInput("Multi param w/ array", b => getQuery(b).Select(doc => CosmosLinq.InvokeUserDefinedFunction("MultiParamterUDWithArrayF", doc.ArrayField, doc.NumericField, doc.Point))),
                new LinqTestInput("ArrayCount", b => getQuery(b).Where(doc => (int)CosmosLinq.InvokeUserDefinedFunction("ArrayCount", doc.ArrayField) > 2)),
                new LinqTestInput("ArrayCount && SomeBooleanUDF", b => getQuery(b).Where(doc => (int)CosmosLinq.InvokeUserDefinedFunction("ArrayCount", doc.ArrayField) > 2 && (bool)CosmosLinq.InvokeUserDefinedFunction("SomeBooleanUDF"))),
                new LinqTestInput("expression", b => getQuery(b).Where(doc => (int)CosmosLinq.InvokeUserDefinedFunction("SingleParameterUDF", doc.NumericField) + 2 == 4)),
                // UDF with constant parameters
                new LinqTestInput("Single constant param", b => getQuery(b).Select(doc => CosmosLinq.InvokeUserDefinedFunction("SingleParameterUDF", 1))),
                new LinqTestInput("Single constant int array param", b => getQuery(b).Select(doc => CosmosLinq.InvokeUserDefinedFunction("SingleParameterUDFWithArray", new int[] { 1, 2, 3 }))),
                new LinqTestInput("Single constant string array param", b => getQuery(b).Select(doc => CosmosLinq.InvokeUserDefinedFunction("SingleParameterUDFWithArray", new string[] { "1", "2" }))),
                new LinqTestInput("Multi constant params", b => getQuery(b).Select(doc => CosmosLinq.InvokeUserDefinedFunction("MultiParamterUDF", 1, "str", true))),
                new LinqTestInput("Multi constant array params", b => getQuery(b).Select(doc => CosmosLinq.InvokeUserDefinedFunction("MultiParamterUDWithArrayF", new int[] { 1, 2, 3 }, 1, "str"))),
                new LinqTestInput("ArrayCount with constant param", b => getQuery(b).Where(doc => (int)CosmosLinq.InvokeUserDefinedFunction("ArrayCount", new int[] { 1, 2, 3 }) > 2)),
                // regression (different type parameters including objects)
                new LinqTestInput("different type parameters including objects", b => getQuery(b).Where(doc => (bool)CosmosLinq.InvokeUserDefinedFunction("MultiParamterUDF2", doc.Point, "str", 1))),
                // errors
                new LinqTestInput("Null udf name", b => getQuery(b).Select(doc => CosmosLinq.InvokeUserDefinedFunction(null))),
                new LinqTestInput("Empty udf name", b => getQuery(b).Select(doc => CosmosLinq.InvokeUserDefinedFunction("")))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestClausesOrderVariations()
        {
            const int Records = 100;
            const int MaxStringLength = 20;
            const int MaxAbsValue = 5;
            const int MaxCoordinateValue = 200;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                DataObject obj = new DataObject();
                obj.StringField = random.NextDouble() < 0.5 ? "str" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.NumericField = random.Next(MaxAbsValue * 2) - MaxAbsValue;
                List<double> coordinates = new List<double>
                {
                    random.NextDouble() < 0.5 ? 10 : random.Next(MaxCoordinateValue),
                    random.NextDouble() < 0.5 ? 5 : random.Next(MaxCoordinateValue),
                    random.NextDouble() < 0.5 ? 20 : random.Next(MaxCoordinateValue)
                };
                obj.Point = new Point(new Position(coordinates));
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Where -> Select",
                b => getQuery(b).Where(doc => doc.StringField == "str")
                .Select(doc => doc.NumericField)),
                new LinqTestInput("Select -> Where",
                b => getQuery(b).Select(doc => doc.NumericField)
                .Where(number => number == 0)),
                new LinqTestInput("Select -> Multiple Where",
                b => getQuery(b).Select(doc => doc.Point)
                .Where(point => point.Position.Latitude == 100)
                .Where(point => point.Position.Longitude == 50)
                .Where(point => point.Position.Altitude == 20)
                .Where(point => point.Position.Coordinates[0] == 100)
                .Where(point => point.Position.Coordinates[1] == 50)),
                new LinqTestInput("Multiple Where -> Select",
                b => getQuery(b).Where(doc => doc.Point.Position.Latitude == 100)
                .Where(doc => doc.Point.Position.Longitude == 50)
                .Where(doc => doc.Point.Position.Altitude == 20)
                .Where(doc => doc.Point.Position.Coordinates[0] == 100)
                .Where(doc => doc.Point.Position.Coordinates[1] == 50)
                .Select(doc => doc.Point))
            };
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestSelectTop()
        {
            Tuple<int, List<DataObject>> generatedData = this.CreateDataTestSelectTop();
            int seed = generatedData.Item1;
            List<DataObject> data = generatedData.Item2;

            IOrderedQueryable<DataObject> query = testContainer.GetItemLinqQueryable<DataObject>(allowSynchronousQueryExecution: true);
            Func<bool, IQueryable<DataObject>> getQuery = useQuery => useQuery ? query : data.AsQueryable();

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                // Take
                new LinqTestInput("Take 0", b => getQuery(b).Take(0)),
                new LinqTestInput("Select -> Take", b => getQuery(b).Select(doc => doc.NumericField).Take(1)),
                new LinqTestInput("Filter -> Take", b => getQuery(b).Where(doc => doc.NumericField > 100).Take(2)),
                new LinqTestInput("Select -> Filter -> Take", b => getQuery(b).Select(doc => doc.NumericField).Where(number => number > 100).Take(7)),
                new LinqTestInput("Filter -> Select -> Take", b => getQuery(b).Where(doc => doc.NumericField > 100).Select(doc => doc.NumericField).Take(8)),
                new LinqTestInput("Fitler -> OrderBy -> Select -> Take", b => getQuery(b).Where(doc => doc.NumericField > 100).OrderBy(doc => doc.NumericField).Select(doc => doc.NumericField).Take(9)),
                new LinqTestInput("Take -> Filter", b => getQuery(b).Take(3).Where(doc => doc.NumericField > 100)),
                new LinqTestInput("Take -> Filter -> Select", b => getQuery(b).Take(4).Where(doc => doc.NumericField > 100).Select(doc => doc.NumericField)),
                new LinqTestInput("Take -> Select -> Filter", b => getQuery(b).Take(5).Select(doc => doc.NumericField).Where(number => number > 100)),
                new LinqTestInput("Select -> Take -> Filter", b => getQuery(b).Select(doc => doc.NumericField).Take(6).Where(number => number > 100)),
                new LinqTestInput("Take -> Filter -> OrderBy -> Select", b => getQuery(b).Take(10).Where(doc => doc.NumericField > 100).OrderByDescending(doc => doc.NumericField).Select(doc => doc.NumericField)),
                // multiple takes
                new LinqTestInput("Take 10 -> Take 5", b => getQuery(b).Take(10).Take(5)),
                new LinqTestInput("Take 5 -> Take 10", b => getQuery(b).Take(5).Take(10)),
                new LinqTestInput("Take 10 -> Select -> Take 1", b => getQuery(b).Take(10).Select(doc => doc.NumericField).Take(1)),
                new LinqTestInput("Take 10 -> Filter -> Take 2", b => getQuery(b).Take(10).Where(doc => doc.NumericField > 100).Take(2)),
                // negative value
                new LinqTestInput("Take -1 -> Take 5", b => getQuery(b).Take(-1).Take(5)),
                new LinqTestInput("Take -2 -> Select", b => getQuery(b).Take(-2).Select(doc => doc.NumericField)),
                new LinqTestInput("Filter -> Take -3", b => getQuery(b).Where(doc => doc.NumericField > 100).Take(-3))
            };
            this.ExecuteTestSuite(inputs);
        }

        private Tuple<int, List<DataObject>> CreateDataTestSelectTop()
        {
            const int Records = 100;
            const int NumAbsMax = 500;
            List<DataObject> data = new List<DataObject>();
            int seed = DateTime.UtcNow.Millisecond;
            Random random = new Random(seed);
            for (int i = 0; i < Records; ++i)
            {
                DataObject obj = new DataObject();
                obj.NumericField = random.Next(NumAbsMax * 2) - NumAbsMax;
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                data.Add(obj);
            }

            foreach (DataObject obj in data)
            {
                testContainer.CreateItemAsync(obj).Wait();
            }

            return Tuple.Create(seed, data);
        }

        private Func<bool, IQueryable<DataObject>> CreateDataTestSelectManyWithFilters()
        {
            const int Records = 10;
            const int ListSizeMax = 20;
            const int NumAbsMax = 10000;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                DataObject obj = new DataObject();
                obj.EnumerableField = new List<int>();
                int listSize = random.Next(ListSizeMax);
                for (int j = 0; j < listSize; ++j)
                {
                    obj.EnumerableField.Add(random.Next(NumAbsMax * 2) - NumAbsMax);
                }
                obj.NumericField = random.Next(NumAbsMax * 2) - NumAbsMax;
                obj.StringField = LinqTestsCommon.RandomString(random, random.Next(listSize));
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };

            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);
            return getQuery;
        }

        [TestMethod]
        public void TestSelectManyWithFilters()
        {
            Func<bool, IQueryable<DataObject>> getQuery = this.CreateDataTestSelectManyWithFilters();

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                // Filter outer query
                new LinqTestInput("SelectMany(Where -> Select)",
                b => getQuery(b).SelectMany(doc => doc.EnumerableField
                    .Where(number => doc.NumericField > 0)
                    .Select(number => number))),
                new LinqTestInput("Where -> SelectMany(Select)",
                b => getQuery(b).Where(doc => doc.NumericField > 0)
                .SelectMany(doc => doc.EnumerableField
                    .Select(number => number))),
                // Filter inner query
                new LinqTestInput("SelectMany(Where -> Select)",
                b => getQuery(b).SelectMany(doc => doc.EnumerableField
                    .Where(number => number > 0)
                    .Select(number => number))),
                new LinqTestInput("SelectMany(Select -> Where)",
                b => getQuery(b).SelectMany(doc => doc.EnumerableField
                    .Select(number => number)
                    .Where(number => number > 0))),
                // Filter both
                new LinqTestInput("SelectMany(Where1 -> Where2 -> Select)",
                b => getQuery(b).SelectMany(doc => doc.EnumerableField
                    .Where(number => doc.NumericField > 0)
                    .Where(number => number > 10)
                    .Select(number => number))),
                new LinqTestInput("SelectMany(Where2 -> Where1 -> Select)",
                b => getQuery(b).SelectMany(doc => doc.EnumerableField
                    .Where(number => number > 10)
                    .Where(number => doc.NumericField > 0)
                    .Select(number => number))),
                new LinqTestInput("Where -> SelectMany(Where -> Select)",
                b => getQuery(b).Where(doc => doc.NumericField > 0)
                .SelectMany(doc => doc.EnumerableField
                    .Where(number => number > 10)
                    .Select(number => number))),
                // OrderBy + Take
                new LinqTestInput("Where -> OrderBy -> Take -> SelectMany(Where -> Select)",
                b => getQuery(b).Where(doc => doc.NumericField > 0)
                .OrderBy(doc => doc.StringField)
                .Take(10)
                .SelectMany(doc => doc.EnumerableField
                    .Where(number => number > 10)
                    .Select(number => number)))
            };
            this.ExecuteTestSuite(inputs);
        }

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input);
        }
    }
}
//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Xml;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.BaselineTest;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class LinqTestsCommon
    {
        /// <summary>
        /// Compare two list of anonymous objects
        /// </summary>
        /// <param name="queryResults"></param>
        /// <param name="dataResults"></param>
        /// <returns></returns>
        private static bool CompareListOfAnonymousType(List<object> queryResults, List<dynamic> dataResults, bool ignoreOrder)
        {
            if (!ignoreOrder)
            {
                return queryResults.SequenceEqual(dataResults);
            }

            if (queryResults.Count != dataResults.Count)
            {
                return false;
            }

            bool resultMatched = true;
            foreach (object obj in queryResults)
            {
                if (!dataResults.Any(a => a.Equals(obj)))
                {
                    resultMatched = false;
                    return false;
                }
            }

            foreach (dynamic obj in dataResults)
            {
                if (!queryResults.Any(a => a.Equals(obj)))
                {
                    resultMatched = false;
                    break;
                }
            }

            return resultMatched;
        }

        /// <summary>
        /// Compare 2 IEnumerable which may contain IEnumerable themselves.
        /// </summary>
        /// <param name="queryResults">The query results from Cosmos DB</param>
        /// <param name="dataResults">The query results from actual data</param>
        /// <returns>True if the two IEbumerable equal</returns>
        private static bool NestedListsSequenceEqual(IEnumerable queryResults, IEnumerable dataResults)
        {
            IEnumerator queryIter, dataIter;
            for (queryIter = queryResults.GetEnumerator(), dataIter = dataResults.GetEnumerator();
                queryIter.MoveNext() && dataIter.MoveNext();)
            {
                IEnumerable queryEnumerable = queryIter.Current as IEnumerable;
                IEnumerable dataEnumerable = dataIter.Current as IEnumerable;
                if (queryEnumerable == null && dataEnumerable == null)
                {
                    if (!queryIter.Current.Equals(dataIter.Current)) return false;

                }

                else if (queryEnumerable == null || dataEnumerable == null)
                {
                    return false;
                }

                else
                {
                    if (!LinqTestsCommon.NestedListsSequenceEqual(queryEnumerable, dataEnumerable)) return false;
                }
            }

            return !(queryIter.MoveNext() || dataIter.MoveNext());
        }

        /// <summary>
        /// Compare the list of results from CosmosDB query and the list of results from LinQ query on the original data
        /// Similar to Collections.SequenceEqual with the assumption that these lists are non-empty
        /// </summary>
        /// <param name="queryResults">A list representing the query restuls from CosmosDB</param>
        /// <param name="dataResults">A list representing the linQ query results from the original data</param>
        /// <returns>true if the two </returns>
        private static bool CompareListOfArrays(List<object> queryResults, List<dynamic> dataResults)
        {
            if (NestedListsSequenceEqual(queryResults, dataResults)) return true;

            bool resultMatched = true;

            // dataResults contains type ConcatIterator whereas queryResults may contain IEnumerable
            // therefore it's simpler to just cast them into List<INumerable<object>> manually for simplify the verification
            List<List<object>> l1 = new List<List<object>>();
            foreach (IEnumerable list in dataResults)
            {
                List<object> l = new List<object>();
                IEnumerator iterator = list.GetEnumerator();
                while (iterator.MoveNext())
                {
                    l.Add(iterator.Current);
                }

                l1.Add(l);
            }

            List<List<object>> l2 = new List<List<object>>();
            foreach (IEnumerable list in queryResults)
            {
                List<object> l = new List<object>();
                IEnumerator iterator = list.GetEnumerator();
                while (iterator.MoveNext())
                {
                    l.Add(iterator.Current);
                }

                l2.Add(l);
            }

            foreach (IEnumerable<object> list in l1)
            {
                if (!l2.Any(a => a.SequenceEqual(list)))
                {
                    resultMatched = false;
                    return false;
                }
            }

            foreach (IEnumerable<object> list in l2)
            {
                if (!l1.Any(a => a.SequenceEqual(list)))
                {
                    resultMatched = false;
                    break;
                }
            }

            return resultMatched;
        }

        private static bool IsNumber(dynamic value)
        {
            return value is sbyte
                || value is byte
                || value is short
                || value is ushort
                || value is int
                || value is uint
                || value is long
                || value is ulong
                || value is float
                || value is double
                || value is decimal;
        }

        public static Boolean IsAnonymousType(Type type)
        {
            Boolean hasCompilerGeneratedAttribute = type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Count() > 0;
            Boolean nameContainsAnonymousType = type.FullName.Contains("AnonymousType");
            Boolean isAnonymousType = hasCompilerGeneratedAttribute && nameContainsAnonymousType;

            return isAnonymousType;
        }

        /// <summary>
        /// Gets the results of CosmosDB query and the results of LINQ query on the original data
        /// </summary>
        /// <param name="queryResults"></param>
        /// <param name="dataResults"></param>
        public static (List<object> queryResults, List<dynamic> dataResults) GetResults(IQueryable queryResults, IQueryable dataResults)
        {
            // execution validation
            IEnumerator queryEnumerator = queryResults.GetEnumerator();
            List<object> queryResultsList = new List<object>();
            while (queryEnumerator.MoveNext())
            {
                queryResultsList.Add(queryEnumerator.Current);
            }

            List<dynamic> dataResultsList = dataResults?.Cast<dynamic>()?.ToList();

            return (queryResultsList, dataResultsList);
        }

        /// <summary>
        /// Validates the results of CosmosDB query and the results of LINQ query on the original data
        /// Using Assert, will fail the unit test if the two results list are not SequenceEqual
        /// </summary>
        /// <param name="queryResultsList"></param>
        /// <param name="dataResultsList"></param>
        private static void ValidateResults(List<object> queryResultsList, List<dynamic> dataResultsList, bool ignoreOrder)
        {
            bool resultMatched = true;
            string actualStr = null;
            string expectedStr = null;
            if (dataResultsList.Count == 0 || queryResultsList.Count == 0)
            {
                resultMatched &= dataResultsList.Count == queryResultsList.Count;
            }
            else
            {
                dynamic firstElem = dataResultsList.FirstOrDefault();
                if (firstElem is IEnumerable)
                {
                    resultMatched &= CompareListOfArrays(queryResultsList, dataResultsList);
                }
                else if (LinqTestsCommon.IsAnonymousType(firstElem.GetType()))
                {
                    resultMatched &= CompareListOfAnonymousType(queryResultsList, dataResultsList, ignoreOrder);
                }
                else if (LinqTestsCommon.IsNumber(firstElem))
                {
                    const double Epsilon = 1E-6;
                    Type dataType = firstElem.GetType();
                    List<dynamic> dataSortedList = dataResultsList.OrderBy(x => x).ToList();
                    List<object> querySortedList = queryResultsList.OrderBy(x => x).ToList();
                    if (dataSortedList.Count != querySortedList.Count)
                    {
                        resultMatched = false;
                    }
                    else
                    {
                        for (int i = 0; i < dataSortedList.Count; ++i)
                        {
                            if (Math.Abs(dataSortedList[i] - (dynamic)querySortedList[i]) > (dynamic)Convert.ChangeType(Epsilon, dataType))
                            {
                                resultMatched = false;
                                break;
                            }
                        }
                    }

                    if (!resultMatched)
                    {
                        actualStr = JsonConvert.SerializeObject(querySortedList);
                        expectedStr = JsonConvert.SerializeObject(dataSortedList);
                    }
                }
                else
                {
                    List<dynamic> dataNotQuery = dataResultsList.Except(queryResultsList).ToList();
                    List<dynamic> queryNotData = queryResultsList.Except(dataResultsList).ToList();
                    resultMatched &= !dataNotQuery.Any() && !queryNotData.Any();
                }
            }

            string assertMsg = string.Empty;
            if (!resultMatched)
            {
                actualStr ??= JsonConvert.SerializeObject(queryResultsList);
                expectedStr ??= JsonConvert.SerializeObject(dataResultsList);

                resultMatched |= actualStr.Equals(expectedStr);
                if (!resultMatched)
                {
                    assertMsg = $"Expected: {expectedStr}, Actual: {actualStr}, RandomSeed: {LinqTestInput.RandomSeed}";
                }
            }

            Assert.IsTrue(resultMatched, assertMsg);
        }

        /// <summary>
        /// Generate a random string containing alphabetical characters
        /// </summary>
        /// <param name="random"></param>
        /// <param name="length"></param>
        /// <returns>a random string</returns>
        public static string RandomString(Random random, int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz ";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// Generate a random DateTime object from a DateTime,
        /// with the variance of the time span between the provided DateTime to the current time
        /// </summary>
        /// <param name="random"></param>
        /// <param name="midDateTime"></param>
        /// <returns></returns>
        public static DateTime RandomDateTime(Random random, DateTime midDateTime)
        {
            TimeSpan timeSpan = DateTime.Now - midDateTime;
            TimeSpan newSpan = new TimeSpan(0, random.Next(0, (int)timeSpan.TotalMinutes * 2) - (int)timeSpan.TotalMinutes, 0);
            DateTime newDate = midDateTime + newSpan;
            return newDate;
        }

        /// <summary>
        /// Generate test data for most LINQ tests
        /// </summary>
        /// <typeparam name="T">the object type</typeparam>
        /// <param name="func">the lamda to create an instance of test data</param>
        /// <param name="count">number of test data to be created</param>
        /// <param name="container">the target container</param>
        /// <returns>a lambda that takes a boolean which indicate where the query should run against CosmosDB or against original data, and return a query results as IQueryable</returns>
        public static Func<bool, IQueryable<T>> GenerateTestCosmosData<T>(Func<Random, T> func, int count, Container container)
        {
            List<T> data = new List<T>();
            int seed = DateTime.Now.Millisecond;
            Random random = new Random(seed);
            Debug.WriteLine("Random seed: {0}", seed);
            LinqTestInput.RandomSeed = seed;
            for (int i = 0; i < count; ++i)
            {
                data.Add(func(random));
            }

            foreach (T obj in data)
            {
                ItemResponse<T> response = container.CreateItemAsync(obj, new Cosmos.PartitionKey("Test")).Result;
            }

            FeedOptions feedOptions = new FeedOptions() { EnableScanInQuery = true, EnableCrossPartitionQuery = true };
            QueryRequestOptions requestOptions = new QueryRequestOptions() { EnableOptimisticDirectExecution = false };

            IOrderedQueryable<T> query = container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: true, requestOptions: requestOptions);

            // To cover both query against backend and queries on the original data using LINQ nicely, 
            // the LINQ expression should be written once and they should be compiled and executed against the two sources.
            // That is done by using Func that take a boolean Func. The parameter of the Func indicate whether the Cosmos DB query 
            // or the data list should be used. When a test is executed, the compiled LINQ expression would pass different values
            // to this getQuery method.
            IQueryable<T> getQuery(bool useQuery) => useQuery ? query : data.AsQueryable();

            return getQuery;
        }

        /// <summary>
        /// Generate a non-random payload for serializer LINQ tests. 
        /// </summary>
        /// <typeparam name="T">the object type</typeparam>
        /// <param name="func">the lamda to create an instance of test data</param>
        /// <param name="count">number of test data to be created</param>
        /// <param name="container">the target container</param>
        /// <param name="camelCaseSerialization">if theCosmosLinqSerializerOption of camelCaseSerialization should be applied</param>
        /// <returns>a lambda that takes a boolean which indicate where the query should run against CosmosDB or against original data, and return a query results as IQueryable.</returns>
        public static Func<bool, IQueryable<T>> GenerateSerializationTestCosmosData<T>(Func<int, bool, T> func, int count, Container container, CosmosLinqSerializerOptions linqSerializerOptions)
        {
            List<T> data = new List<T>();
            for (int i = 0; i < count; i++)
            {
                data.Add(func(i, linqSerializerOptions.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase));
            }

            foreach (T obj in data)
            {
                ItemResponse<T> response = container.CreateItemAsync(obj, new Cosmos.PartitionKey("Test")).Result;
            }

            FeedOptions feedOptions = new FeedOptions() { EnableScanInQuery = true, EnableCrossPartitionQuery = true };
            QueryRequestOptions requestOptions = new QueryRequestOptions() { EnableOptimisticDirectExecution = false };

            IOrderedQueryable<T> query = container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: true, requestOptions: requestOptions, linqSerializerOptions: linqSerializerOptions);

            IQueryable<T> getQuery(bool useQuery) => useQuery ? query : data.AsQueryable();

            return getQuery;
        }

        public static Func<bool, IQueryable<Family>> GenerateFamilyCosmosData(
            Cosmos.Database cosmosDatabase, out Container container)
        {
            // The test collection should have range index on string properties
            // for the orderby tests
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/Pk" }), Kind = PartitionKind.Hash };
            ContainerProperties newCol = new ContainerProperties()
            {
                Id = Guid.NewGuid().ToString(),
                PartitionKey = partitionKeyDefinition,
                IndexingPolicy = new Microsoft.Azure.Cosmos.IndexingPolicy()
                {
                    IncludedPaths = new Collection<Cosmos.IncludedPath>()
                    {
                        new Cosmos.IncludedPath()
                        {
                            Path = "/*",
                            Indexes = new System.Collections.ObjectModel.Collection<Microsoft.Azure.Cosmos.Index>()
                            {
                                Microsoft.Azure.Cosmos.Index.Range(Microsoft.Azure.Cosmos.DataType.Number, -1),
                                Microsoft.Azure.Cosmos.Index.Range(Microsoft.Azure.Cosmos.DataType.String, -1)
                            }
                        }
                    },
                    CompositeIndexes = new Collection<Collection<Cosmos.CompositePath>>()
                    {
                        new Collection<Cosmos.CompositePath>()
                        {
                            new Cosmos.CompositePath() { Path = "/FamilyId", Order = Cosmos.CompositePathSortOrder.Ascending },
                            new Cosmos.CompositePath() { Path = "/Int", Order = Cosmos.CompositePathSortOrder.Ascending }
                        },
                        new Collection<Cosmos.CompositePath>()
                        {
                            new Cosmos.CompositePath() { Path = "/FamilyId", Order = Cosmos.CompositePathSortOrder.Ascending },
                            new Cosmos.CompositePath() { Path = "/Int", Order = Cosmos.CompositePathSortOrder.Descending }
                        },
                        new Collection<Cosmos.CompositePath>()
                        {
                            new Cosmos.CompositePath() { Path = "/FamilyId", Order = Cosmos.CompositePathSortOrder.Ascending },
                            new Cosmos.CompositePath() { Path = "/Int", Order = Cosmos.CompositePathSortOrder.Ascending },
                            new Cosmos.CompositePath() { Path = "/IsRegistered", Order = Cosmos.CompositePathSortOrder.Descending }
                        },
                        new Collection<Cosmos.CompositePath>()
                        {
                            new Cosmos.CompositePath() { Path = "/Int", Order = Cosmos.CompositePathSortOrder.Ascending },
                            new Cosmos.CompositePath() { Path = "/IsRegistered", Order = Cosmos.CompositePathSortOrder.Descending }
                        },
                        new Collection<Cosmos.CompositePath>()
                        {
                            new Cosmos.CompositePath() { Path = "/IsRegistered", Order = Cosmos.CompositePathSortOrder.Ascending },
                            new Cosmos.CompositePath() { Path = "/Int", Order = Cosmos.CompositePathSortOrder.Descending }
                        }
                    }
                }
            };
            container = cosmosDatabase.CreateContainerAsync(newCol).Result;
            const int Records = 100;
            const int MaxNameLength = 100;
            const int MaxThingStringLength = 50;
            const int MaxChild = 5;
            const int MaxPets = MaxChild;
            const int MaxThings = MaxChild;
            const int MaxGrade = 101;
            const int MaxTransaction = 20;
            const int MaxTransactionMinuteRange = 200;
            int MaxTransactionType = Enum.GetValues(typeof(TransactionType)).Length;
            Family createDataObj(Random random)
            {
                Family obj = new Family
                {
                    FamilyId = random.NextDouble() < 0.05 ? "some id" : Guid.NewGuid().ToString(),
                    IsRegistered = random.NextDouble() < 0.5,
                    NullableInt = random.NextDouble() < 0.5 ? (int?)random.Next() : null,
                    Int = random.NextDouble() < 0.5 ? 5 : random.Next(),
                    Id = Guid.NewGuid().ToString(),
                    Pk = "Test",
                    Parents = new Parent[random.Next(2) + 1]
                };
                for (int i = 0; i < obj.Parents.Length; ++i)
                {
                    obj.Parents[i] = new Parent()
                    {
                        FamilyName = LinqTestsCommon.RandomString(random, random.Next(MaxNameLength)),
                        GivenName = LinqTestsCommon.RandomString(random, random.Next(MaxNameLength))
                    };
                }

                obj.Tags = new string[random.Next(MaxChild)];
                for (int i = 0; i < obj.Tags.Length; ++i)
                {
                    obj.Tags[i] = (i + random.Next(30, 36)).ToString();
                }

                obj.Children = new Child[random.Next(MaxChild)];
                for (int i = 0; i < obj.Children.Length; ++i)
                {
                    obj.Children[i] = new Child()
                    {
                        Gender = random.NextDouble() < 0.5 ? "male" : "female",
                        FamilyName = obj.Parents[random.Next(obj.Parents.Length)].FamilyName,
                        GivenName = LinqTestsCommon.RandomString(random, random.Next(MaxNameLength)),
                        Grade = random.Next(MaxGrade)
                    };

                    obj.Children[i].Pets = new List<Pet>();
                    for (int j = 0; j < random.Next(MaxPets); ++j)
                    {
                        obj.Children[i].Pets.Add(new Pet()
                        {
                            GivenName = random.NextDouble() < 0.5 ?
                                LinqTestsCommon.RandomString(random, random.Next(MaxNameLength)) :
                                "Fluffy"
                        });
                    }

                    obj.Children[i].Things = new Dictionary<string, string>();
                    for (int j = 0; j < random.Next(MaxThings) + 1; ++j)
                    {
                        obj.Children[i].Things.Add(
                            j == 0 ? "A" : $"{j}-{random.Next()}",
                            LinqTestsCommon.RandomString(random, random.Next(MaxThingStringLength)));
                    }
                }

                obj.Records = new Logs
                {
                    LogId = LinqTestsCommon.RandomString(random, random.Next(MaxNameLength)),
                    Transactions = new Transaction[random.Next(MaxTransaction)]
                };
                for (int i = 0; i < obj.Records.Transactions.Length; ++i)
                {
                    Transaction transaction = new Transaction()
                    {
                        Amount = random.Next(),
                        Date = DateTime.Now.AddMinutes(random.Next(MaxTransactionMinuteRange)),
                        Type = (TransactionType)random.Next(MaxTransactionType)
                    };
                    obj.Records.Transactions[i] = transaction;
                }

                return obj;
            }

            Func<bool, IQueryable<Family>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, container);
            return getQuery;
        }

        public static Func<bool, IQueryable<Data>> GenerateSimpleCosmosData(Cosmos.Database cosmosDatabase, bool useRandomData = true)
        {
            const int DocumentCount = 10;
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/Pk" }), Kind = PartitionKind.Hash };
            Container container = cosmosDatabase.CreateContainerAsync(new ContainerProperties { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition }).Result;

            ILinqTestDataGenerator dataGenerator = useRandomData ? new LinqTestRandomDataGenerator(DocumentCount) : new LinqTestDataGenerator(DocumentCount);
            List<Data> testData = new List<Data>(dataGenerator.GenerateData());
            foreach (Data dataEntry in testData)
            {
                Data response = container.CreateItemAsync<Data>(dataEntry, new Cosmos.PartitionKey(dataEntry.Pk)).Result;
            }

            FeedOptions feedOptions = new FeedOptions() { EnableScanInQuery = true, EnableCrossPartitionQuery = true };
            QueryRequestOptions requestOptions = new QueryRequestOptions() { EnableOptimisticDirectExecution = false };

            IOrderedQueryable<Data> query = container.GetItemLinqQueryable<Data>(allowSynchronousQueryExecution: true, requestOptions: requestOptions);

            // To cover both query against backend and queries on the original data using LINQ nicely, 
            // the LINQ expression should be written once and they should be compiled and executed against the two sources.
            // That is done by using Func that take a boolean Func. The parameter of the Func indicate whether the Cosmos DB query 
            // or the data list should be used. When a test is executed, the compiled LINQ expression would pass different values
            // to this getQuery method.
            IQueryable<Data> getQuery(bool useQuery) => useQuery ? query : testData.AsQueryable();
            return getQuery;
        }

        public static LinqTestOutput ExecuteTest(LinqTestInput input, bool serializeResultsInBaseline = false)
        {
            string querySqlStr = string.Empty;
            try
            {
                Func<bool, IQueryable> compiledQuery = input.Expression.Compile();

                IQueryable query = compiledQuery(true);
                querySqlStr = JObject.Parse(query.ToString()).GetValue("query", StringComparison.Ordinal).ToString();

                IQueryable dataQuery = input.skipVerification ? null : compiledQuery(false);

                (List<object> queryResults, List<dynamic> dataResults) = GetResults(query, dataQuery);

                // we skip unordered query because the LINQ results vs actual query results are non-deterministic
                if (!input.skipVerification)
                {
                    LinqTestsCommon.ValidateResults(queryResults, dataResults, input.ignoreOrder);
                }

                string serializedResults = serializeResultsInBaseline ?
                    JsonConvert.SerializeObject(queryResults.Select(item => item is LinqTestObject ? item.ToString() : item), new JsonSerializerSettings { Formatting = Newtonsoft.Json.Formatting.Indented}) :
                    null;

                return new LinqTestOutput(querySqlStr, serializedResults, errorMsg: null, input.inputData);
            }
            catch (Exception e)
            {
                return new LinqTestOutput(querySqlStr, serializedResults: null, errorMsg: LinqTestsCommon.BuildExceptionMessageForTest(e), inputData: input.inputData);
            }
        }

        public static string BuildExceptionMessageForTest(Exception ex)
        {
            StringBuilder message = new StringBuilder();
            do
            {
                if (ex is CosmosException cosmosException)
                {
                    message.Append($"Status Code: {cosmosException.StatusCode}");
                }
                else if (ex is DocumentClientException documentClientException)
                {
                    message.Append(documentClientException.RawErrorMessage);
                }
                else
                {
                    message.Append(ex.Message);
                }

                ex = ex.InnerException;
                if (ex != null)
                {
                    message.Append(",");
                }
            }
            while (ex != null);

            return message.ToString();
        }
    }

    /// <summary>
    /// A base class that determines equality based on its json representation
    /// </summary>
    public class LinqTestObject
    {
        private string json;

        protected virtual string SerializeForTestBaseline()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override string ToString()
        {
            // simple cached serialization
            this.json ??= this.SerializeForTestBaseline();
            return this.json;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is LinqTestObject &&
                obj.GetType().IsAssignableFrom(this.GetType()) &&
                this.GetType().IsAssignableFrom(obj.GetType()))) return false;
            if (obj == null) return false;

            return this.ToString().Equals(obj.ToString());
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }
    }

    public class LinqTestInput : BaselineTestInput
    {
        internal static Regex classNameRegex = new Regex("(value\\(.+?\\+)?\\<\\>.+?__([A-Za-z]+)((\\d+_\\d+(`\\d+\\[.+?\\])?\\)(\\.value)?)|\\d+`\\d+)");
        internal static Regex invokeCompileRegex = new Regex("(Convert\\()?Invoke\\([^.]+\\.[^.,]+(\\.Compile\\(\\))?, b\\)(\\.Cast\\(\\))?(\\))?");

        // As the tests are executed sequentially
        // We can store the random seed in a static variable for diagnostics
        internal static int RandomSeed = -1;

        internal int randomSeed = -1;
        internal Expression<Func<bool, IQueryable>> Expression { get; }
        internal string expressionStr;
        internal string inputData;

        // We skip the verification between Cosmos DB and actual query restuls in the following cases
        //     - unordered query since the results are not deterministics for LinQ results and actual query results
        //     - scenarios not supported in LINQ, e.g. sequence doesn't contain element.
        internal bool skipVerification;

        // Ignore Ordering for AnonymousType object
        internal bool ignoreOrder;

        internal LinqTestInput(
            string description, 
            Expression<Func<bool, IQueryable>> expr, 
            bool skipVerification = false, 
            bool ignoreOrder = false,
            string expressionStr = null, 
            string inputData = null)
            : base(description)
        {
            this.Expression = expr ?? throw new ArgumentNullException($"{nameof(expr)} must not be null.");
            this.skipVerification = skipVerification;
            this.ignoreOrder = ignoreOrder;
            this.expressionStr = expressionStr;
            this.inputData = inputData;
        }

        public static string FilterInputExpression(string input)
        {
            StringBuilder expressionSb = new StringBuilder(input);
            // simplify full qualified class name
            // e.g. before: value(Microsoft.Azure.Documents.Services.Management.Tests.LinqSQLTranslationTest+<>c__DisplayClass7_0), after: DisplayClass
            // before: <>f__AnonymousType14`2(, after: AnonymousType(
            // value(Microsoft.Azure.Documents.Services.Management.Tests.LinqProviderTests.LinqTranslationBaselineTests +<> c__DisplayClass24_0`1[System.String]).value
            Match match = classNameRegex.Match(expressionSb.ToString());
            while (match.Success)
            {
                expressionSb = expressionSb.Replace(match.Groups[0].Value, match.Groups[2].Value);
                match = match.NextMatch();
            }

            // remove the Invoke().Compile() string from the Linq scanning tests
            match = invokeCompileRegex.Match(expressionSb.ToString());
            while (match.Success)
            {
                expressionSb = expressionSb.Replace(match.Groups[0].Value, string.Empty);
                match = match.NextMatch();
            }

            expressionSb.Insert(0, "query");

            return expressionSb.ToString();
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            if (xmlWriter == null)
            {
                throw new ArgumentNullException($"{nameof(xmlWriter)} cannot be null.");
            }

            this.expressionStr ??= LinqTestInput.FilterInputExpression(this.Expression.Body.ToString());

            xmlWriter.WriteStartElement("Description");
            xmlWriter.WriteCData(this.Description);
            xmlWriter.WriteEndElement();
            xmlWriter.WriteStartElement("Expression");
            xmlWriter.WriteCData(this.expressionStr);
            xmlWriter.WriteEndElement();
        }
    }

    public class LinqTestOutput : BaselineTestOutput
    {
        internal static Regex sdkVersion = new Regex("(,\\W*)?documentdb-dotnet-sdk[^]]+");
        internal static Regex activityId = new Regex("(,\\W*)?ActivityId:.+", RegexOptions.Multiline);
        internal static Regex newLine = new Regex("(\r\n|\r|\n)");

        internal string SqlQuery { get; }
        internal string ErrorMessage { get; }
        internal string Results { get; }
        internal string InputData { get; }

        private static readonly Dictionary<string, string> newlineKeywords = new Dictionary<string, string>() {
            { "SELECT", "\nSELECT" },
            { "FROM", "\nFROM" },
            { "WHERE", "\nWHERE" },
            { "JOIN", "\nJOIN" },
            { "ORDER BY", "\nORDER BY" },
            { "OFFSET", "\nOFFSET" },
            { "GROUP BY", "\nGROUP BY" },
            { " )", "\n)" }
        };

        public static string FormatErrorMessage(string msg)
        {
            msg = newLine.Replace(msg, string.Empty);

            // remove sdk version in the error message which can change in the future. 
            // e.g. <![CDATA[Method 'id' is not supported., documentdb-dotnet-sdk/2.0.0 Host/64-bit MicrosoftWindowsNT/6.3.9600.0]]>
            msg = sdkVersion.Replace(msg, string.Empty);

            // remove activity Id
            msg = activityId.Replace(msg, string.Empty);

            return msg;
        }

        internal LinqTestOutput(string sqlQuery, string serializedResults, string errorMsg, string inputData)
        {
            this.SqlQuery = FormatSql(sqlQuery);
            this.Results = serializedResults;
            this.ErrorMessage = errorMsg;
            this.InputData = inputData;
        }

        public static String FormatSql(string sqlQuery)
        {
            const string subqueryCue = "(SELECT";
            bool hasSubquery = sqlQuery.IndexOf(subqueryCue, StringComparison.OrdinalIgnoreCase) > 0;

            StringBuilder sb = new StringBuilder(sqlQuery);
            foreach (KeyValuePair<string, string> kv in newlineKeywords)
            {
                sb.Replace(kv.Key, kv.Value);
            }

            if (!hasSubquery) return sb.ToString();

            const string oneTab = "    ";
            const string startCue = "SELECT";
            const string endCue = ")";
            string[] tokens = sb.ToString().Split('\n');
            bool firstSelect = true;
            sb.Length = 0;
            StringBuilder indentSb = new StringBuilder();
            for (int i = 0; i < tokens.Length; ++i)
            {
                if (tokens[i].StartsWith(startCue, StringComparison.OrdinalIgnoreCase))
                {
                    if (!firstSelect) indentSb.Append(oneTab); else firstSelect = false;

                }
                else if (tokens[i].StartsWith(endCue, StringComparison.OrdinalIgnoreCase))
                {
                    indentSb.Length -= oneTab.Length;
                }

                sb.Append(indentSb).Append(tokens[i]).Append("\n");
            }

            return sb.ToString();
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(this.SqlQuery));
            xmlWriter.WriteCData(this.SqlQuery);
            xmlWriter.WriteEndElement();
            if (this.InputData != null)
            {
                xmlWriter.WriteStartElement("InputData");
                xmlWriter.WriteCData(this.InputData);
                xmlWriter.WriteEndElement();
            }
            if (this.Results != null)
            {
                xmlWriter.WriteStartElement("Results");
                xmlWriter.WriteCData(this.Results);
                xmlWriter.WriteEndElement();
            }
            if (this.ErrorMessage != null)
            {
                xmlWriter.WriteStartElement("ErrorMessage");
                xmlWriter.WriteCData(LinqTestOutput.FormatErrorMessage(this.ErrorMessage));
                xmlWriter.WriteEndElement();
            }
        }
    }

    class SystemTextJsonLinqSerializer : CosmosLinqSerializer
    {
        private readonly JsonObjectSerializer systemTextJsonSerializer;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        public SystemTextJsonLinqSerializer(JsonSerializerOptions jsonSerializerOptions)
        {
            this.systemTextJsonSerializer = new JsonObjectSerializer(jsonSerializerOptions);
            this.jsonSerializerOptions = jsonSerializerOptions;
        }

        public override T FromStream<T>(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            using (stream)
            {
                if (stream.CanSeek && stream.Length == 0)
                {
                    return default;
                }

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    return (T)(object)stream;
                }

                return (T)this.systemTextJsonSerializer.Deserialize(stream, typeof(T), default);
            }
        }

        public override Stream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new MemoryStream();
            this.systemTextJsonSerializer.Serialize(streamPayload, input, input.GetType(), default);
            streamPayload.Position = 0;
            return streamPayload;
        }

        public override string SerializeMemberName(MemberInfo memberInfo)
        {
            System.Text.Json.Serialization.JsonExtensionDataAttribute jsonExtensionDataAttribute =
                memberInfo.GetCustomAttribute<System.Text.Json.Serialization.JsonExtensionDataAttribute>(true);
            if (jsonExtensionDataAttribute != null)
            {
                return null;
            }

            JsonPropertyNameAttribute jsonPropertyNameAttribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>(true);
            if (!string.IsNullOrEmpty(jsonPropertyNameAttribute?.Name))
            {
                return jsonPropertyNameAttribute.Name;
            }

            if (this.jsonSerializerOptions.PropertyNamingPolicy != null)
            {
                return this.jsonSerializerOptions.PropertyNamingPolicy.ConvertName(memberInfo.Name);
            }

            // Do any additional handling of JsonSerializerOptions here.

            return memberInfo.Name;
        }
    }

    class SystemTextJsonSerializer : CosmosSerializer
    {
        private readonly JsonObjectSerializer systemTextJsonSerializer;

        public SystemTextJsonSerializer(JsonSerializerOptions jsonSerializerOptions)
        {
            this.systemTextJsonSerializer = new JsonObjectSerializer(jsonSerializerOptions);
        }

        public override T FromStream<T>(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            using (stream)
            {
                if (stream.CanSeek && stream.Length == 0)
                {
                    return default;
                }

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    return (T)(object)stream;
                }

                return (T)this.systemTextJsonSerializer.Deserialize(stream, typeof(T), default);
            }
        }

        public override Stream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new MemoryStream();
            this.systemTextJsonSerializer.Serialize(streamPayload, input, input.GetType(), default);
            streamPayload.Position = 0;
            return streamPayload;
        }
    }
}

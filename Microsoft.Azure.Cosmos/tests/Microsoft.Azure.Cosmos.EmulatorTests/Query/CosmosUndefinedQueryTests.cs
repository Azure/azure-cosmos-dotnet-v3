namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Azure;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class CosmosUndefinedQueryTests : QueryTestsBase
    {
        private const int DocumentCount = 350;

        private const int MixedTypeCount = 7;

        private const int DocumentsPerTypeCount = DocumentCount / MixedTypeCount;

        private const int IntegerValue = 42;

        private const string StringValue = "string";

        private const string ArrayValue = "[10, 20]";

        private const string ObjectValue = "{\"type\":\"object\"}";

        private static readonly int[] PageSizes = new[] { 5, 10, -1 };

        private static readonly IndexingPolicy CompositeIndexPolicy = CreateIndexingPolicy();

        private static readonly List<MixedTypeDocument> MixedTypeDocuments = CreateDocuments(DocumentCount);

        private static readonly List<string> Documents = MixedTypeDocuments
            .Select(x => x.ToString())
            .ToList();

        [TestMethod]
        public async Task AllTests()
        {
            // Removing the await causes the test framework to not run this test
            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct | ConnectionModes.Gateway,
                collectionTypes: CollectionTypes.MultiPartition | CollectionTypes.SinglePartition,
                documents: Documents,
                query: RunTests,
                indexingPolicy: CompositeIndexPolicy);
        }

        private static async Task RunTests(Container container, IReadOnlyList<CosmosObject> _)
        {
            await OrderByTests(container);
            await GroupByTests(container);
            await UntypedTests(container);
        }

        private static async Task UntypedTests(Container container)
        {
            UndefinedProjectionTestCase[] undefinedProjectionTestCases = new[]
            {
                MakeUndefinedProjectionTest(
                    query: "SELECT VALUE c.AlwaysUndefinedField FROM c",
                    expectedCount: 0),
                MakeUndefinedProjectionTest(
                    query: "SELECT VALUE c.AlwaysUndefinedField FROM c ORDER BY c.AlwaysUndefinedField",
                    expectedCount: 0),
                MakeUndefinedProjectionTest(
                    query: "SELECT c.AlwaysUndefinedField FROM c ORDER BY c.AlwaysUndefinedField",
                    expectedCount: DocumentCount),
                MakeUndefinedProjectionTest(
                    query: "SELECT VALUE c.AlwaysUndefinedField FROM c GROUP BY c.AlwaysUndefinedField",
                    expectedCount: 0),
                MakeUndefinedProjectionTest(
                    query: $"SELECT VALUE SUM(c.{nameof(MixedTypeDocument.MixedTypeField)}) FROM c",
                    expectedCount: 0),
                MakeUndefinedProjectionTest(
                    query: $"SELECT VALUE AVG(c.{nameof(MixedTypeDocument.MixedTypeField)}) FROM c",
                    expectedCount: 0),
                MakeUndefinedProjectionTest(
                    query: $"SELECT DISTINCT VALUE SUM(c.{nameof(MixedTypeDocument.MixedTypeField)}) FROM c",
                    expectedCount: 0)
            };

            foreach (UndefinedProjectionTestCase testCase in undefinedProjectionTestCases)
            {
                foreach (int pageSize in PageSizes)
                {
                    IAsyncEnumerable<ResponseMessage> results = RunSimpleQueryAsync(
                        container,
                        testCase.Query,
                        new QueryRequestOptions { MaxItemCount = pageSize });

                    long actualCount = 0;
                    await foreach (ResponseMessage responseMessage in results)
                    {
                        Assert.IsTrue(responseMessage.IsSuccessStatusCode);

                        string content = responseMessage.Content.ReadAsString();
                        IJsonNavigator navigator = JsonNavigator.Create(System.Text.Encoding.UTF8.GetBytes(content));
                        IJsonNavigatorNode rootNode = navigator.GetRootNode();
                        Assert.IsTrue(navigator.TryGetObjectProperty(rootNode, "_count", out ObjectProperty countProperty));

                        long count = Number64.ToLong(navigator.GetNumberValue(countProperty.ValueNode));
                        actualCount += count;

                        Assert.IsTrue(navigator.TryGetObjectProperty(rootNode, "Documents", out ObjectProperty documentsProperty));
                        int documentCount = navigator.GetArrayItemCount(documentsProperty.ValueNode);
                        Assert.AreEqual(count, documentCount);

                        for (int index= 0; index < documentCount; ++index)
                        {
                            IJsonNavigatorNode documentNode = navigator.GetArrayItemAt(documentsProperty.ValueNode, index);
                            int propertyCount = navigator.GetObjectPropertyCount(documentNode);
                            Assert.AreEqual(0, propertyCount);
                        }
                    }

                    Assert.AreEqual(testCase.ExpectedResultCount, actualCount);
                }
            }
        }

        private static async Task OrderByTests(Container container)
        {
            UndefinedProjectionTestCase[] undefinedProjectionTestCases = new[]
            {
                MakeUndefinedProjectionTest(
                    query: "SELECT c.AlwaysUndefinedField FROM c ORDER BY c.AlwaysUndefinedField",
                    expectedCount: DocumentCount),
                MakeUndefinedProjectionTest(
                    query: "SELECT VALUE c.AlwaysUndefinedField FROM c ORDER BY c.AlwaysUndefinedField",
                    expectedCount: 0)
            };

            foreach (UndefinedProjectionTestCase testCase in undefinedProjectionTestCases)
            {
                foreach (int pageSize in PageSizes)
                {
                    List<UndefinedProjection> results = await RunQueryAsync<UndefinedProjection>(
                        container,
                        testCase.Query,
                        new QueryRequestOptions { MaxItemCount = pageSize });

                    Assert.AreEqual(testCase.ExpectedResultCount, results.Count);
                    Assert.IsTrue(results.All(x => x is UndefinedProjection));
                }
            }

            OrderByTestCase[] orderByTestCases = new[]
            {
                MakeOrderByTest(
                    query:  $"SELECT VALUE c.{nameof(MixedTypeDocument.Index)} " +
                                "FROM c " +
                                $"ORDER BY c.{nameof(MixedTypeDocument.MixedTypeField)}",
                    expectation: (actual) => Expectations.ElementsAreInTypeOrder(actual, isReverse: false)),
                MakeOrderByTest(
                    query:  $"SELECT VALUE c.{nameof(MixedTypeDocument.Index)} " +
                                "FROM c " +
                                $"ORDER BY c.{nameof(MixedTypeDocument.MixedTypeField)} DESC",
                    expectation: (actual) => Expectations.ElementsAreInTypeOrder(actual, isReverse: true)),
                MakeOrderByTest(
                    query:  $"SELECT VALUE c.{nameof(MixedTypeDocument.Index)} " +
                                "FROM c " +
                                $"ORDER BY c.{nameof(MixedTypeDocument.MixedTypeField)}, c.{nameof(MixedTypeDocument.Index)}",
                    expectation: (actual) => Expectations.ElementsAreInTypeThenIndexOrder(actual, isReverse: false)),
                MakeOrderByTest(
                    query:  $"SELECT VALUE c.{nameof(MixedTypeDocument.Index)} " +
                                "FROM c " +
                                $"ORDER BY c.{nameof(MixedTypeDocument.MixedTypeField)} DESC, c.{nameof(MixedTypeDocument.Index)} DESC",
                    expectation: (actual) => Expectations.ElementsAreInTypeThenIndexOrder(actual, isReverse: true)),
                MakeOrderByTest(
                    query:  $"SELECT VALUE c.{nameof(MixedTypeDocument.Index)} " +
                                "FROM c " +
                                $"ORDER BY c.{nameof(MixedTypeDocument.Index)}, c.{nameof(MixedTypeDocument.MixedTypeField)}",
                    expectation: (actual) => Expectations.ElementsAreInIndexOrder(actual, isReverse: false)),
                MakeOrderByTest(
                    query:  $"SELECT VALUE c.{nameof(MixedTypeDocument.Index)} " +
                                "FROM c " +
                                $"ORDER BY c.{nameof(MixedTypeDocument.Index)} DESC, c.{nameof(MixedTypeDocument.MixedTypeField)} DESC",
                    expectation: (actual) => Expectations.ElementsAreInIndexOrder(actual, isReverse: true)),
            };

            foreach (OrderByTestCase testCase in orderByTestCases)
            {
                foreach (int pageSize in PageSizes)
                {
                    List<int> result = await RunQueryAsync<int>(
                        container,
                        testCase.Query,
                        new QueryRequestOptions { MaxItemCount = pageSize });

                    testCase.ValidateResult(result);
                }
            }
        }

        private static async Task GroupByTests(Container container)
        {
            GroupByUndefinedTestCase[] mixedTypeTestCases = new[]
            {
                MakeGroupByTest(
                    query:  $"SELECT c.AlwaysUndefinedField as {nameof(GroupByProjection.MixedTypeField)}, "+
                                $"COUNT(1) as {nameof(GroupByProjection.ExpectedCount)} " +
                            $"FROM c " +
                            $"GROUP BY c.AlwaysUndefinedField",
                    groups: new List<GroupByProjection>()
                    {
                        MakeGrouping(
                            key: null,
                            value: DocumentCount)
                    }),
                MakeGroupByTest(
                    query:  $"SELECT c.{nameof(MixedTypeDocument.MixedTypeField)} as {nameof(GroupByProjection.MixedTypeField)}, " +
                                $"COUNT(1) as {nameof(GroupByProjection.ExpectedCount)} " +
                            $"FROM c " +
                            $"GROUP BY c.{nameof(MixedTypeDocument.MixedTypeField)}",
                    groups: new List<GroupByProjection>()
                    {
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosNull.Create(),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosBoolean.Create(true),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosNumber64.Create(IntegerValue),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosString.Create(StringValue),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosArray.Parse(ArrayValue),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosObject.Parse(ObjectValue),
                            value: DocumentsPerTypeCount),
                    }),
                MakeGroupByTest(
                    query:  $"SELECT SUM(c.{nameof(MixedTypeDocument.MixedTypeField)}) as {nameof(GroupByProjection.MixedTypeField)}, " +
                                $"COUNT(1) as {nameof(GroupByProjection.ExpectedCount)} " +
                            $"FROM c " +
                            $"GROUP BY c.{nameof(MixedTypeDocument.MixedTypeField)}",
                    groups: new List<GroupByProjection>()
                    {
                        MakeGrouping(
                            key: CosmosNumber64.Create(0),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosNumber64.Create(IntegerValue * DocumentsPerTypeCount),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                    }),
                MakeGroupByTest(
                    query:  $"SELECT AVG(c.{nameof(MixedTypeDocument.MixedTypeField)}) as {nameof(GroupByProjection.MixedTypeField)}, " +
                                $"COUNT(1) as {nameof(GroupByProjection.ExpectedCount)} " +
                            $"FROM c " +
                            $"GROUP BY c.{nameof(MixedTypeDocument.MixedTypeField)}",
                    groups: new List<GroupByProjection>()
                    {
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosNumber64.Create(IntegerValue),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                    }),
                MakeGroupByTest(
                    query:  $"SELECT MIN(c.{nameof(MixedTypeDocument.MixedTypeField)}) as {nameof(GroupByProjection.MixedTypeField)}, " +
                                $"COUNT(1) as {nameof(GroupByProjection.ExpectedCount)} " +
                            $"FROM c " +
                            $"GROUP BY c.{nameof(MixedTypeDocument.MixedTypeField)}",
                    groups: new List<GroupByProjection>()
                    {
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosNull.Create(),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosBoolean.Create(true),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosNumber64.Create(IntegerValue),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosString.Create(StringValue),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                    }),
                MakeGroupByTest(
                    query:  $"SELECT MAX(c.{nameof(MixedTypeDocument.MixedTypeField)}) as {nameof(GroupByProjection.MixedTypeField)}, " +
                                $"COUNT(1) as {nameof(GroupByProjection.ExpectedCount)} " +
                            $"FROM c " +
                            $"GROUP BY c.{nameof(MixedTypeDocument.MixedTypeField)}",
                    groups: new List<GroupByProjection>()
                    {
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosNull.Create(),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosBoolean.Create(true),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosNumber64.Create(IntegerValue),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: CosmosString.Create(StringValue),
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                        MakeGrouping(
                            key: null,
                            value: DocumentsPerTypeCount),
                    }),
            };

            foreach (GroupByUndefinedTestCase testCase in mixedTypeTestCases)
            {
                foreach (int pageSize in PageSizes)
                {
                    List<GroupByProjection> actual = await QueryWithoutContinuationTokensAsync<GroupByProjection>(
                        container,
                        testCase.Query,
                        new QueryRequestOptions { MaxItemCount = pageSize });

                    CollectionAssert.AreEquivalent(testCase.ExpectedGroups, actual);
                }
            }

            UndefinedProjectionTestCase[] undefinedProjectionTestCases = new[]
            {
                MakeUndefinedProjectionTest(
                    query: "SELECT VALUE c.AlwaysUndefinedField FROM c GROUP BY c.AlwaysUndefinedField",
                    expectedCount: 0),
                MakeUndefinedProjectionTest(
                    query: "SELECT c.AlwaysUndefinedField FROM c GROUP BY c.AlwaysUndefinedField",
                    expectedCount: 1),
                MakeUndefinedProjectionTest(
                    query: $"SELECT VALUE SUM(c.{nameof(MixedTypeDocument.MixedTypeField)}) FROM c",
                    expectedCount: 0),
                MakeUndefinedProjectionTest(
                    query: $"SELECT SUM(c.{nameof(MixedTypeDocument.MixedTypeField)}) FROM c",
                    expectedCount: 1),
                MakeUndefinedProjectionTest(
                    query: $"SELECT VALUE AVG(c.{nameof(MixedTypeDocument.MixedTypeField)}) FROM c",
                    expectedCount: 0),
                MakeUndefinedProjectionTest(
                    query: $"SELECT AVG(c.{nameof(MixedTypeDocument.MixedTypeField)}) FROM c",
                    expectedCount: 1),
            };

            foreach (UndefinedProjectionTestCase testCase in undefinedProjectionTestCases)
            {
                foreach (int pageSize in PageSizes)
                {
                    List<UndefinedProjection> results = await QueryWithoutContinuationTokensAsync<UndefinedProjection>(
                        container,
                        testCase.Query,
                        new QueryRequestOptions { MaxItemCount = pageSize });

                    Assert.AreEqual(testCase.ExpectedResultCount, results.Count);
                    Assert.IsTrue(results.All(x => x is UndefinedProjection));
                }
            }
        }

        private static IndexingPolicy CreateIndexingPolicy()
        {
            IndexingPolicy policy = new IndexingPolicy();

            policy.IncludedPaths.Add(new IncludedPath { Path = IndexingPolicy.DefaultPath });
            policy.CompositeIndexes.Add(new Collection<CompositePath>
            {
                new CompositePath { Path = $"/{nameof(MixedTypeDocument.Index)}" },
                new CompositePath { Path = $"/{nameof(MixedTypeDocument.MixedTypeField)}" },
            });
            policy.CompositeIndexes.Add(new Collection<CompositePath>
            {
                new CompositePath { Path = $"/{nameof(MixedTypeDocument.MixedTypeField)}" },
                new CompositePath { Path = $"/{nameof(MixedTypeDocument.Index)}" },
            });

            return policy;
        }

        private static List<MixedTypeDocument> CreateDocuments(int count)
        {
            List<MixedTypeDocument> documents = new List<MixedTypeDocument>();

            for (int index = 0; index < count; ++index)
            {
                CosmosElement mixedTypeElement;
                switch (index % MixedTypeCount)
                {
                    case 0:
                        mixedTypeElement = CosmosUndefined.Create();
                        break;

                    case 1:
                        mixedTypeElement = CosmosNull.Create();
                        break;

                    case 2:
                        mixedTypeElement = CosmosBoolean.Create(true);
                        break;

                    case 3:
                        mixedTypeElement = CosmosNumber64.Create(IntegerValue);
                        break;

                    case 4:
                        mixedTypeElement = CosmosString.Create(StringValue);
                        break;

                    case 5:
                        mixedTypeElement = CosmosArray.Parse(ArrayValue);
                        break;

                    case 6:
                        mixedTypeElement = CosmosObject.Parse(ObjectValue);
                        break;

                    default:
                        mixedTypeElement = null;
                        Assert.Fail("Illegal value found for mixed type");
                        break;
                }

                MixedTypeDocument document = new MixedTypeDocument(index, mixedTypeElement);
                documents.Add(document);
            }

            return documents;
        }

        private readonly struct UndefinedProjectionTestCase
        {
            public UndefinedProjectionTestCase(string query, int expectedResultCount)
            {
                this.Query = query;
                this.ExpectedResultCount = expectedResultCount;
            }

            public string Query { get; }

            public int ExpectedResultCount { get; }
        }

        private static UndefinedProjectionTestCase MakeUndefinedProjectionTest(string query, int expectedCount)
        {
            return new UndefinedProjectionTestCase(query, expectedCount);
        }

        private readonly struct OrderByTestCase
        {
            public OrderByTestCase(string query, Action<List<int>> validateResult)
            {
                this.Query = query;
                this.ValidateResult = validateResult;
            }

            public string Query { get; }

            public Action<List<int>> ValidateResult { get; }
        }

        private static OrderByTestCase MakeOrderByTest(string query, Action<List<int>> expectation)
        {
            return new OrderByTestCase(query, expectation);
        }

        private static class Expectations
        {
            private static readonly List<int> DocumentIndices = Enumerable.Range(0, DocumentCount).ToList();

            private static readonly List<int> DocumentIndicesReversed = Enumerable
                .Range(0, DocumentCount)
                .Reverse()
                .ToList();

            private static readonly List<int> TypeIndices = Enumerable
                .Range(0, MixedTypeCount)
                .SelectMany(x => Enumerable.Repeat(x, DocumentsPerTypeCount))
                .ToList();

            private static readonly List<int> TypeIndicesReversed = Enumerable
                .Range(0, MixedTypeCount)
                .SelectMany(x => Enumerable.Repeat(x, DocumentsPerTypeCount))
                .Reverse()
                .ToList();

            private static readonly List<int> DocumentIndicesInTypeOrder = Enumerable
                .Range(0, DocumentCount)
                .Select(x => Tuple.Create(x, x % MixedTypeCount))
                .OrderBy(tuple => tuple.Item2)
                .ThenBy(tuple => tuple.Item1)
                .Select(tuple => tuple.Item1)
                .ToList();

            private static readonly List<int> DocumentIndicesInTypeOrderReversed = Enumerable
                .Range(0, DocumentCount)
                .Select(x => Tuple.Create(x, x % MixedTypeCount))
                .OrderBy(tuple => tuple.Item2)
                .ThenBy(tuple => tuple.Item1)
                .Select(tuple => tuple.Item1)
                .Reverse()
                .ToList();

            public static void ElementsAreInIndexOrder(List<int> actual, bool isReverse)
            {
                List<int> expected = isReverse ? DocumentIndicesReversed : DocumentIndices;
                CollectionAssert.AreEqual(expected, actual);
            }

            public static void ElementsAreInTypeOrder(List<int> actual, bool isReverse)
            {
                CollectionAssert.AreEquivalent(DocumentIndices, actual);

                List<int> actualTypes = actual.Select(x => x % MixedTypeCount).ToList();
                List<int> expectedTypes = isReverse ? TypeIndicesReversed : TypeIndices;
                CollectionAssert.AreEqual(expectedTypes, actualTypes);
            }

            public static void ElementsAreInTypeThenIndexOrder(List<int> actual, bool isReverse)
            {
                List<int> expected = isReverse ? DocumentIndicesInTypeOrderReversed : DocumentIndicesInTypeOrder;
                CollectionAssert.AreEqual(expected, actual);
            }
        }

        private readonly struct GroupByUndefinedTestCase
        {
            public GroupByUndefinedTestCase(string query, List<GroupByProjection> groups)
            {
                this.Query = query;
                this.ExpectedGroups = groups;
            }

            public string Query { get; }

            public List<GroupByProjection> ExpectedGroups { get; }
        }

        private static GroupByUndefinedTestCase MakeGroupByTest(string query, List<GroupByProjection> groups)
        {
            return new GroupByUndefinedTestCase(query, groups);
        }

        private class GroupByProjection : IEquatable<GroupByProjection>
        {
            public CosmosElement MixedTypeField { get; set; }

            public int ExpectedCount { get; set; }

            public static bool operator ==(GroupByProjection left, GroupByProjection right)
            {
                bool result;
                if(left is null && right is null)
                {
                    result = true;
                }
                else if(left is null || right is null)
                {
                    result = false;
                }
                else
                {
                    result = left.Equals(right);
                }

                return result;
            }

            public static bool operator !=(GroupByProjection left, GroupByProjection right)
            {
                return !(left == right);
            }

            public bool Equals(GroupByProjection other)
            {
                bool result;
                if(other is null)
                {
                    result = false;
                }
                else
                {
                    result = this.ExpectedCount == other.ExpectedCount;
                    if (this.MixedTypeField == null && other.MixedTypeField == null)
                    {
                    }
                    else if (this.MixedTypeField == null || other.MixedTypeField == null)
                    {
                        result = false;
                    }
                    else
                    {
                        result = result && this.MixedTypeField.Equals(other.MixedTypeField);
                    }
                }

                return result;
            }

            public override bool Equals(object other)
            {
                return (other is not null) && (other is GroupByProjection projection)
                    && this.Equals(projection);
            }

            public override int GetHashCode()
            {
                HashCode hash = new HashCode();
                hash.Add(this.ExpectedCount);
                hash.Add(this.MixedTypeField);
                return hash.ToHashCode();
            }
        }

        private static GroupByProjection MakeGrouping(CosmosElement key, int value)
        {
            return new GroupByProjection() { MixedTypeField = key, ExpectedCount = value };
        }

        private class UndefinedProjection : IEquatable<UndefinedProjection>
        {
            public bool Equals(UndefinedProjection other)
            {
                return true;
            }
        }

        private class MixedTypeDocument
        {
            public int Index { get; set; }

            public CosmosElement MixedTypeField { get; set; }

            public string AlwaysUndefinedField { get; set; }

            public MixedTypeDocument()
            {
            }

            public MixedTypeDocument(int index, CosmosElement mixedTypeField)
            {
                this.Index = index;
                this.MixedTypeField = mixedTypeField;
            }

            public override string ToString()
            {
                IJsonWriter writer = JsonWriter.Create(JsonSerializationFormat.Text);
                writer.WriteObjectStart();

                writer.WriteFieldName(nameof(this.Index));
                writer.WriteNumberValue(this.Index);

                if (this.MixedTypeField is not CosmosUndefined)
                {
                    writer.WriteFieldName(nameof(this.MixedTypeField));
                    this.MixedTypeField.WriteTo(writer);
                }

                writer.WriteObjectEnd();

                return Utf8StringHelpers.ToString(writer.GetResult());
            }
        }
    }
}
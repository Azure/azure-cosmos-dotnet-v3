namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class CosmosUndefinedQueryTests : QueryTestsBase
    {
        // Kinds of tests that we need to run:
        // 1. typed response
        // 2. typed response with custom serializer
        // 3. untyped response
        private const int DocumentCount = 400;

        private const int MixedTypeCount = 5;

        private static readonly IEnumerable<string> Documents = CreateDocuments(DocumentCount);

        [TestMethod]
        public async Task OrderByUndefinedProjectUndefined()
        {
            UndefinedProjectionTestCase[] testCases = new[]
            {
                MakeUndefinedProjectionTest(
                    query: "SELECT c.AlwaysUndefinedField FROM c ORDER BY c.AlwaysUndefinedField",
                    expectedCount: DocumentCount),
                MakeUndefinedProjectionTest(
                    query: "SELECT VALUE c.AlwaysUndefinedField FROM c ORDER BY c.AlwaysUndefinedField",
                    expectedCount: 0)
            };

            static async Task TypedImplementation(Container container, IReadOnlyList<CosmosObject> _, UndefinedProjectionTestCase[] testCases)
            {
                foreach (UndefinedProjectionTestCase testCase in testCases)
                {
                    foreach (int pageSize in new[] { 5, 10, -1 })
                    {
                        List<UndefinedProjection> results = await RunQueryAsync<UndefinedProjection>(
                            container,
                            testCase.Query,
                            new QueryRequestOptions { MaxItemCount = pageSize });
    
                        Assert.AreEqual(testCase.ExpectedResultCount, results.Count);
                        Assert.IsTrue(results.All(x => x is UndefinedProjection));
                    }
                }
            }

            await this.CreateIngestQueryDeleteAsync<UndefinedProjectionTestCase[]>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.MultiPartition | CollectionTypes.SinglePartition | CollectionTypes.NonPartitioned,
                Documents,
                TypedImplementation,
                testCases);
        }

        [TestMethod]
        public async Task GroupByUndefined()
        {
            GroupByUndefinedTestCase[] testCases = new[]
            {
                MakeGroupByTest(
                    query: "SELECT c.AlwaysUndefinedField as MixedTypeField, COUNT(1) as ExpectedCount FROM c GROUP BY c.AlwaysUndefinedField",
                    groups: new List<GroupByProjection>()
                    {
                        MakeGrouping(
                            key: null,
                            value: DocumentCount)
                    })
            };

            static async Task TypedImplementation(Container container, IReadOnlyList<CosmosObject> _, GroupByUndefinedTestCase[] testCases)
            {
                foreach (GroupByUndefinedTestCase testCase in testCases)
                {
                    foreach (int pageSize in new[] { 5, 10, -1 })
                    {
                        List<GroupByProjection> actual = await QueryWithoutContinuationTokensAsync<GroupByProjection>(
                            container,
                            testCase.Query,
                            new QueryRequestOptions { MaxItemCount = pageSize, MaxConcurrency = 0 });

                        CollectionAssert.AreEquivalent(testCase.ExpectedGroups, actual);
                    }
                }
            }

            await this.CreateIngestQueryDeleteAsync<GroupByUndefinedTestCase[]>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.MultiPartition | CollectionTypes.SinglePartition | CollectionTypes.NonPartitioned,
                Documents,
                TypedImplementation,
                testCases);
        }

        private static IEnumerable<string> CreateDocuments(int count)
        {
            List<string> documents = new List<string>();

            int booleanCount = 0;
            int integerCount = 0;
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
                        mixedTypeElement = CosmosBoolean.Create(++booleanCount % 2 == 0);
                        break;

                    case 3:
                        mixedTypeElement = CosmosNumber64.Create(++integerCount);
                        break;

                    case 4:
                        mixedTypeElement = CosmosString.Create((++integerCount).ToString());
                        break;

                    default:
                        mixedTypeElement = null;
                        Assert.Fail("Illegal value found for mixed type");
                        break;
                }

                MixedTypeDocument document = new MixedTypeDocument(index, mixedTypeElement);
                documents.Add(document.ToString());
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
            public int IntegerField { get; set; }

            public CosmosElement MixedTypeField { get; set; }

            public string AlwaysUndefinedField { get; set; }

            public MixedTypeDocument()
            {
            }

            public MixedTypeDocument(int integerField, CosmosElement mixedTypeField)
            {
                this.IntegerField = integerField;
                this.MixedTypeField = mixedTypeField;
            }

            public override string ToString()
            {
                IJsonWriter writer = JsonWriter.Create(JsonSerializationFormat.Text);
                writer.WriteObjectStart();

                writer.WriteFieldName(nameof(this.IntegerField));
                writer.WriteNumber64Value(this.IntegerField);

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
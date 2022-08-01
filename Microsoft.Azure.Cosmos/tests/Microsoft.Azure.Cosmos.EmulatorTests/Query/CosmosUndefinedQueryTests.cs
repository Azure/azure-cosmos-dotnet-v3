namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
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

        [TestMethod]
        public async Task OrderByUndefinedProjectUndefined()
        {
            IEnumerable<string> documents = CreateDocuments();

            static async Task TypedImplementation(Container container, IReadOnlyList<CosmosObject> _)
            {
                string query = "SELECT c.AlwaysUndefinedField FROM c ORDER BY c.AlwaysUndefinedField";

                List<UndefinedProjection> results = await QueryWithContinuationTokensAsync<UndefinedProjection>(container, query);
                Assert.AreEqual(DocumentCount, results.Count);
                Assert.IsTrue(results.All(x => x is UndefinedProjection));
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                documents,
                TypedImplementation);
        }


        private static IEnumerable<string> CreateDocuments()
        {
            List<MixedTypeDocument> documents = new List<MixedTypeDocument>();

            int booleanCount = 0;
            int integerCount = 0;
            for (int index = 0; index < DocumentCount; ++index)
            {
                CosmosElement mixedTypeElement;
                switch (index % MixedTypeCount)
                {
                    case 0:
                        mixedTypeElement = CosmosUndefined.Instance;
                        break;

                    case 1:
                        mixedTypeElement = CosmosNull.Create();
                        break;

                    case 2:
                        mixedTypeElement = CosmosBoolean.Create(++booleanCount % 2 == 0);
                        break;

                    case 3:
                        mixedTypeElement = CosmosNumber.Parse((++integerCount).ToString());
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
                documents.Add(document);
            }

            return documents.Select(x => x.ToString());
        }

        private readonly struct UntypedTestCase
        {
            public string Query { get; }

            public int PageSize { get; }
        }

        private class UndefinedProjection
        {
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
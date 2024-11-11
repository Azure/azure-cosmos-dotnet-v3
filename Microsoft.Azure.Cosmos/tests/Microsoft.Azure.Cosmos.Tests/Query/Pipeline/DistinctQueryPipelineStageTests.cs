namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class DistinctQueryPipelineStageTests
    {
        private const int InputElementCount = 400;

        // This list SHOULD NOT contain duplicates
        private static readonly IReadOnlyList<CosmosElement> Literals = new List<CosmosElement>
        {
            CosmosUndefined.Create(),

            CosmosNull.Create(),

            CosmosBoolean.Create(true),
            CosmosBoolean.Create(false),

            CosmosNumber64.Create(0),
            CosmosNumber64.Create(42),
            CosmosNumber64.Create(-1),
            CosmosNumber64.Create(100010),
            CosmosNumber64.Create(3.141619),
            CosmosNumber64.Create(Math.PI),

            CosmosString.Create(string.Empty),
            CosmosString.Create("Hello World"),
            CosmosString.Create(string.Join(',', Enumerable.Repeat("Hello World", 75))),
            CosmosString.Create(string.Join(',', Enumerable.Repeat("Hello World", 100))),
            CosmosString.Create("敏捷的棕色狐狸跳过了懒狗"),
            CosmosString.Create(string.Join(',', Enumerable.Repeat("敏捷的棕色狐狸跳过了懒狗", 50))),
            CosmosString.Create(string.Join(',', Enumerable.Repeat("敏捷的棕色狐狸跳过了懒狗", 60))),

            CosmosArray.Create(),
            CosmosArray.Create(new CosmosElement[]
            {
                CosmosUndefined.Create(),
                CosmosNull.Create(),
                CosmosBoolean.Create(true),
                CosmosBoolean.Create(false),
                CosmosNumber64.Create(0),
            }),
            CosmosArray.Create(new CosmosElement[]
            {
                CosmosUndefined.Create(),
                CosmosNull.Create(),
                CosmosBoolean.Create(true),
                CosmosBoolean.Create(false)
            }),
            CosmosArray.Create(new CosmosElement[]
            {
                CosmosUndefined.Create(),
                CosmosNull.Create(),
                CosmosBoolean.Create(true),
                CosmosBoolean.Create(false),
                CosmosNumber64.Create(0),
                CosmosNumber64.Create(42),
                CosmosNumber64.Create(-1),
                CosmosNumber64.Create(100010),
                CosmosNumber64.Create(3.141619),
            }),

            CosmosObject.Create(new Dictionary<string, CosmosElement>()),
            CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["敏"] = CosmosUndefined.Create(),
                ["b"] = CosmosNull.Create(),
                ["c"] = CosmosBoolean.Create(true),
                ["d"] = CosmosBoolean.Create(false),
                ["懒"] = CosmosNumber64.Create(0),
            }),
            CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["敏"] = CosmosUndefined.Create(),
                ["b"] = CosmosNull.Create(),
                ["c"] = CosmosBoolean.Create(true),
                ["d"] = CosmosBoolean.Create(false),
                ["懒"] = CosmosNumber64.Create(0),
                ["e"] = CosmosNumber64.Create(42),
                ["f"] = CosmosNumber64.Create(-1),
                ["فوق"] = CosmosNumber64.Create(100010),
                ["g"] = CosmosNumber64.Create(3.141619),
            }),

            CosmosGuid.Create(Guid.Parse("D29F71E3-2D43-4573-A0E6-16D7E03FDEB5")),
            CosmosGuid.Create(Guid.Parse("D29F71E3-2D43-4573-B0E6-16D7E03FDEB5")),
            CosmosGuid.Create(Guid.Parse("D29F71E3-2D43-4573-C0E6-16D7E03FDEB5")),
            CosmosGuid.Create(Guid.Parse("D29F71E3-2D43-4573-D0E6-16D7E03FDEB5")),
            CosmosGuid.Create(Guid.Parse("D29F71E3-2D43-4573-E0E6-16D7E03FDEB5")),

            CosmosBinary.Create(Guid.Parse("D29F71E3-2D43-4573-A0E6-16D7E03FDEB5").ToByteArray()),
            CosmosBinary.Create(Guid.Parse("D29F71E3-2D43-4573-B0E6-16D7E03FDEB5").ToByteArray()),
            CosmosBinary.Create(Guid.Parse("D29F71E3-2D43-4573-C0E6-16D7E03FDEB5").ToByteArray()),
            CosmosBinary.Create(Guid.Parse("D29F71E3-2D43-4573-D0E6-16D7E03FDEB5").ToByteArray()),
            CosmosBinary.Create(Guid.Parse("D29F71E3-2D43-4573-E0E6-16D7E03FDEB5").ToByteArray()),
            CosmosBinary.Create(Encoding.UTF8.GetBytes("Hello World")),
            CosmosBinary.Create(Encoding.UTF8.GetBytes(string.Join(',', Enumerable.Repeat("Hello World", 75)))),
            CosmosBinary.Create(Encoding.UTF8.GetBytes(string.Join(',', Enumerable.Repeat("Hello World", 100)))),
            CosmosBinary.Create(Encoding.UTF8.GetBytes("敏捷的棕色狐狸跳过了懒狗")),
            CosmosBinary.Create(Encoding.UTF8.GetBytes(string.Join(',', Enumerable.Repeat("敏捷的棕色狐狸跳过了懒狗", 50)))),
            CosmosBinary.Create(Encoding.UTF8.GetBytes(string.Join(',', Enumerable.Repeat("敏捷的棕色狐狸跳过了懒狗", 60)))),
        };

        [TestMethod]
        public async Task SanityTests()
        {
            long[] values = { 42, 1337, 1337, 42 };

            IEnumerable<CosmosElement> input = values
                .Select(value => CosmosObject.Create(new Dictionary<string, CosmosElement>
                {
                    ["item"] = CosmosNumber64.Create(value)
                }));

            IEnumerable<CosmosElement> expected = values
                .Distinct()
                .Select(value => CosmosObject.Create(new Dictionary<string, CosmosElement>
                {
                    ["item"] = CosmosNumber64.Create(value)
                }));

            DistinctQueryPipelineStageTestCase MakeTest(int pageSize)
            {
                return new DistinctQueryPipelineStageTestCase(input: input, pageSize: pageSize, expected: expected);
            }

            IEnumerable<DistinctQueryPipelineStageTestCase> testCases = new List<DistinctQueryPipelineStageTestCase>
            {
                MakeTest(pageSize: 1),
                MakeTest(pageSize: 3),
                MakeTest(pageSize : 10),
            };

            await RunTests(testCases);
        }

        [TestMethod]
        public async Task MixedTypeTests()
        {
            IEnumerable<CosmosElement> mixedTypeValues = Enumerable
                .Range(0, InputElementCount)
                .Select(index => Literals[index % Literals.Count]);

            DistinctQueryPipelineStageTestCase MakeTest(int pageSize)
            {
                return new DistinctQueryPipelineStageTestCase(input: mixedTypeValues, pageSize: pageSize, expected: Literals);
            }

            int[] pageSizes = { 400, 100, 10, 1 };
            IEnumerable<DistinctQueryPipelineStageTestCase> testCases = pageSizes
                .Select(x => MakeTest(pageSize: x))
                .ToList();

            await RunTests(testCases);
        }

        private static async Task RunTests(IEnumerable<DistinctQueryPipelineStageTestCase> testCases)
        {
            foreach (DistinctQueryPipelineStageTestCase testCase in testCases)
            {
                IEnumerator<CosmosElement> enumerator = testCase.Input.GetEnumerator();
                int pageSize = 0;
                List<List<CosmosElement>> pages = new List<List<CosmosElement>>() { new List<CosmosElement>() };
                while (enumerator.MoveNext())
                {
                    if (pageSize > testCase.PageSize)
                    {
                        pageSize = 0;
                        pages.Add(new List<CosmosElement>());
                    }

                    pages[^1].Add(enumerator.Current);
                    ++pageSize;
                }

                IEnumerable<CosmosElement> elements = await DistinctQueryPipelineStageTests.CreateAndDrainAsync(
                    pages: pages,
                    continuationToken: null,
                    distinctQueryType: DistinctQueryType.Unordered);

                List<string> actual = elements
                    .Select(value => value.ToString())
                    .ToList();

                List<string> expected = testCase.Expected
                    .Select(value => value.ToString())
                    .ToList();

                CollectionAssert.AreEquivalent(expected, actual);
            }
        }

        private static async Task<IEnumerable<CosmosElement>> CreateAndDrainAsync(
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages,
            CosmosElement continuationToken,
            DistinctQueryType distinctQueryType)
        {
            IQueryPipelineStage source = new MockQueryPipelineStage(pages);

            TryCatch<IQueryPipelineStage> tryCreateDistinctQueryPipelineStage = DistinctQueryPipelineStage.MonadicCreate(
                requestContinuation: continuationToken,
                distinctQueryType: distinctQueryType,
                monadicCreatePipelineStage: (CosmosElement continuationToken) => TryCatch<IQueryPipelineStage>.FromResult(source));
            Assert.IsTrue(tryCreateDistinctQueryPipelineStage.Succeeded);

            IQueryPipelineStage distinctQueryPipelineStage = tryCreateDistinctQueryPipelineStage.Result;

            IEnumerable<CosmosElement> elements = Enumerable.Empty<CosmosElement>();
            await foreach (TryCatch<QueryPage> page in new EnumerableStage(distinctQueryPipelineStage, NoOpTrace.Singleton))
            {
                page.ThrowIfFailed();

                elements = elements.Concat(page.Result.Documents);
            }

            return elements;
        }

        private struct DistinctQueryPipelineStageTestCase
        {
            public IEnumerable<CosmosElement> Input { get; }

            public int PageSize { get; }

            public IEnumerable<CosmosElement> Expected { get; }

            public DistinctQueryPipelineStageTestCase(IEnumerable<CosmosElement> input, int pageSize, IEnumerable<CosmosElement> expected)
            {
                this.Input = input ?? throw new ArgumentNullException(nameof(input));
                this.PageSize = pageSize;
                this.Expected = expected ?? throw new ArgumentNullException(nameof(expected));
            }
        }
    }
}
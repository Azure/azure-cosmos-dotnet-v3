namespace Microsoft.Azure.Cosmos.Performance.Tests.Query
{
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;

    [MemoryDiagnoser]
    public class DistinctHashBenchmark
    {
        private static readonly CosmosNull cosmosNull = CosmosNull.Create();
        private static readonly CosmosBoolean cosmosFalse = CosmosBoolean.Create(false);
        private static readonly CosmosBoolean cosmosTrue = CosmosBoolean.Create(true);
        private static readonly CosmosNumber cosmosNumber = CosmosNumber64.Create(1234);
        private static readonly CosmosString cosmosString = CosmosString.Create("asdfasdfasdfasdfasdfasdf");
        private static readonly CosmosArray cosmosEmptyArray = CosmosArray.Create(
            new List<CosmosElement>()
            {
            });
        private static readonly CosmosArray cosmosArrayWithItems = CosmosArray.Create(
            new List<CosmosElement>()
            {
                cosmosNull,
                cosmosFalse,
                cosmosTrue,
                cosmosNumber,
                cosmosString
            });
        private static readonly CosmosObject cosmosEmptyObject = CosmosObject.Create(new Dictionary<string, CosmosElement>());
        private static readonly CosmosObject cosmosObjectWithItems = CosmosObject.Create(new Dictionary<string, CosmosElement>()
        {
            { "null", cosmosNull },
            { "false", cosmosFalse },
            { "true", cosmosTrue },
            { "cosmosNumber", cosmosNumber },
            { "cosmosString", cosmosString },
        });

        [Benchmark]
        public void Null()
        {
            DistinctHashBenchmark.ExecuteBenchmark(cosmosNull);
        }

        [Benchmark]
        public void False()
        {
            DistinctHashBenchmark.ExecuteBenchmark(cosmosFalse);
        }

        [Benchmark]
        public void True()
        {
            DistinctHashBenchmark.ExecuteBenchmark(cosmosTrue);
        }

        [Benchmark]
        public void Number()
        {
            DistinctHashBenchmark.ExecuteBenchmark(cosmosNumber);
        }

        [Benchmark]
        public void String()
        {
            DistinctHashBenchmark.ExecuteBenchmark(cosmosString);
        }

        [Benchmark]
        public void EmptyArray()
        {
            DistinctHashBenchmark.ExecuteBenchmark(cosmosEmptyArray);
        }

        [Benchmark]
        public void ArrayWithItems()
        {
            DistinctHashBenchmark.ExecuteBenchmark(cosmosArrayWithItems);
        }

        [Benchmark]
        public void EmptyObject()
        {
            DistinctHashBenchmark.ExecuteBenchmark(cosmosEmptyObject);
        }

        [Benchmark]
        public void ObjectWithItems()
        {
            DistinctHashBenchmark.ExecuteBenchmark(cosmosObjectWithItems);
        }

        private static void ExecuteBenchmark(CosmosElement cosmosElement)
        {
            for (int i = 0; i < 100000; i++)
            {
                DistinctHash.GetHash(cosmosElement);
            }
        }
    }
}


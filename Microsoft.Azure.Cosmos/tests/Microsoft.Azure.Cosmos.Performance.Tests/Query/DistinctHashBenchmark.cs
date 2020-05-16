namespace Microsoft.Azure.Cosmos.Performance.Tests.Query
{
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct;

    [MemoryDiagnoser]
    public class DistinctHashBenchmark
    {
        
        private static readonly CosmosNull cosmosNull = CosmosNull.Create();
        private static readonly CosmosBoolean cosmosFalse = CosmosBoolean.Create(false);
        private static readonly CosmosBoolean cosmosTrue = CosmosBoolean.Create(true);
        private static readonly CosmosNumber cosmosNumber = CosmosNumber64.Create(1234);
        private static readonly CosmosString cosmosString = CosmosString.Create("asdfasdfasdfasdfasdfasdf");
        private static readonly CosmosArray cosmosArray = CosmosArray.Create(new List<CosmosElement>());
        private static readonly CosmosObject cosmosObject = CosmosObject.Create(new Dictionary<string, CosmosElement>());

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
        public void Array()
        {
            DistinctHashBenchmark.ExecuteBenchmark(cosmosArray);
        }

        [Benchmark]
        public void Object()
        {
            DistinctHashBenchmark.ExecuteBenchmark(cosmosObject);
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


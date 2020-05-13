namespace Microsoft.Azure.Cosmos.Performance.Tests.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Tests.Poco;
    using Newtonsoft.Json;

    [MemoryDiagnoser]
    public class DeserializerBenchmark
    {
        private readonly string json;
        private readonly ReadOnlyMemory<byte> textBuffer;
        private readonly ReadOnlyMemory<byte> binaryBuffer;

        public DeserializerBenchmark()
        {
            List<Person> people = new List<Person>();
            for (int i = 0; i < 1000; i++)
            {
                people.Add(Person.GetRandomPerson());
            }

            this.json = JsonConvert.SerializeObject(people);

            IJsonWriter jsonTextWriter = Microsoft.Azure.Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Text);
            CosmosElement.Parse(this.json).WriteTo(jsonTextWriter);
            this.textBuffer = jsonTextWriter.GetResult();

            IJsonWriter jsonBinaryWriter = Microsoft.Azure.Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Binary);
            CosmosElement.Parse(this.json).WriteTo(jsonBinaryWriter);
            this.binaryBuffer = jsonBinaryWriter.GetResult();
        }

        [Benchmark]
        public void CosmosElementDeserializer_TryDeserialize_Text()
        {
            _ = Cosmos.Json.JsonSerializer.Deserialize<IReadOnlyList<Person>>(this.textBuffer);
        }

        [Benchmark]
        public void CosmosElementDeserializer_TryDeserialize_Binary()
        {
            _ = Cosmos.Json.JsonSerializer.Deserialize<IReadOnlyList<Person>>(this.binaryBuffer);
        }

        [Benchmark]
        public void JsonConvert_DeserializeObject_Text()
        {
            _ = JsonConvert.DeserializeObject<IReadOnlyList<Person>>(this.json);
        }

        [Benchmark]
        public void JsonConvert_DeserializeObject_Binary()
        {
            Newtonsoft.Json.JsonSerializer jsonSerializer = Newtonsoft.Json.JsonSerializer.CreateDefault();
            CosmosDBToNewtonsoftReader cosmosDBToNewtonsoftReader = new CosmosDBToNewtonsoftReader(this.binaryBuffer);
            _ = jsonSerializer.Deserialize<IReadOnlyList<Person>>(cosmosDBToNewtonsoftReader);
        }
    }
}

namespace Microsoft.Azure.Cosmos.Performance.Tests.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Performance.Tests.Models;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Newtonsoft.Json;

    [MemoryDiagnoser]
    public class DeserializerBenchmark
    {
        private readonly string json;
        private readonly ReadOnlyMemory<byte> textBuffer;

        public DeserializerBenchmark()
        {
            List<Person> people = new List<Person>();
            for (int i = 0; i < 1000; i++)
            {
                people.Add(Person.GetRandomPerson());
            }

            this.json = JsonConvert.SerializeObject(people);
            IJsonWriter jsonTextWriter = Json.JsonWriter.Create(JsonSerializationFormat.Text);
            CosmosElement.Parse(this.json).WriteTo(jsonTextWriter);
            this.textBuffer = jsonTextWriter.GetResult();
        }

        [Benchmark]
        public void CosmosElementDeserializer_TryDeserialize()
        {
            TryCatch<IReadOnlyList<Person>> tryCatch = CosmosElementDeserializer.TryDeserialize<IReadOnlyList<Person>>(this.textBuffer);
        }

        [Benchmark]
        public void JsonConvert_DeserializeObject()
        {
            IReadOnlyList<Person> deserializedPeople = JsonConvert.DeserializeObject<IReadOnlyList<Person>>(this.json);
        }
    }
}

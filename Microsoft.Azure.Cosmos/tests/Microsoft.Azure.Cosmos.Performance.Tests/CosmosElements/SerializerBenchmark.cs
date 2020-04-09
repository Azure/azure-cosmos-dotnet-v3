namespace Microsoft.Azure.Cosmos.Performance.Tests.CosmosElements
{
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Performance.Tests.Models;
    using Newtonsoft.Json;

    [MemoryDiagnoser]
    public class SerializerBenchmark
    {
        private readonly IReadOnlyList<Person> people;

        public SerializerBenchmark()
        {
            List<Person> people = new List<Person>();
            for (int i = 0; i < 1000; i++)
            {
                people.Add(Person.GetRandomPerson());
            }

            this.people = people;
        }

        [Benchmark]
        public void CosmosElement_Serializer_Text()
        {
            Serializer.Serialize(this.people, JsonSerializationFormat.Text);
        }

        [Benchmark]
        public void CosmosElement_Serializer_Binary()
        {
            Serializer.Serialize(this.people, JsonSerializationFormat.Text);
        }

        [Benchmark]
        public void Newtonsoft_Serializer_Text()
        {
            JsonConvert.SerializeObject(this.people);
        }

        [Benchmark]
        public void Newtonsoft_Serializer_Binary()
        {
            JsonSerializer jsonSerializer = JsonSerializer.CreateDefault();
            CosmosDBToNewtonsoftWriter writer = new CosmosDBToNewtonsoftWriter(JsonSerializationFormat.Binary);
            jsonSerializer.Serialize(writer, this.people);
        }
    }
}

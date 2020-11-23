namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Tests.Json;
    using Microsoft.Azure.Cosmos.Tests.Poco;
    using Newtonsoft.Json;

    [MemoryDiagnoser]
    public class PocoDeserializationBenchmark
    {
        private readonly Payload peoplePayload;

        public PocoDeserializationBenchmark()
        {
            List<Person> people = new List<Person>();
            for (int i = 0; i < 100; i++)
            {
                people.Add(Person.GetRandomPerson());
            }

            string peopleToString = JsonConvert.SerializeObject(people);
            this.peoplePayload = Payload.Create(peopleToString);
        }

        [Benchmark]
        [ArgumentsSource(nameof(Data))]
        public void RunBenchmark(PocoSerializationFormat serializationFormat)
        {
            Newtonsoft.Json.JsonReader reader;
            switch (serializationFormat)
            {
                case PocoSerializationFormat.Text:
                    reader = new CosmosDBToNewtonsoftReader(
                        Cosmos.Json.JsonReader.Create(
                            this.peoplePayload.Text));
                    break;

                case PocoSerializationFormat.NewtonsoftText:
                    if (!MemoryMarshal.TryGetArray(this.peoplePayload.Text, out ArraySegment<byte> segment))
                    {
                        throw new InvalidOperationException("Failed to get segment");
                    }

                    reader = new Newtonsoft.Json.JsonTextReader(
                        new StreamReader(
                            new MemoryStream(segment.Array, index: segment.Offset, count: segment.Count)));
                    break;

                case PocoSerializationFormat.Binary:
                    reader = new CosmosDBToNewtonsoftReader(
                        Cosmos.Json.JsonReader.Create(
                            this.peoplePayload.Binary));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(serializationFormat.ToString());
            }

            Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
            _ = serializer.Deserialize<List<Person>>(reader);
        }

        public IEnumerable<object> Data()
        {
            foreach (PocoSerializationFormat serializationFormat in Enum.GetValues(typeof(PocoSerializationFormat)))
            {
                yield return serializationFormat;
            }
        }

        public enum PocoSerializationFormat
        {
            Text,
            NewtonsoftText,
            Binary,
        }

        private readonly struct Payload
        {
            private Payload(
                ReadOnlyMemory<byte> text,
                ReadOnlyMemory<byte> binary)
            {
                this.Text = text;
                this.Binary = binary;
            }

            public ReadOnlyMemory<byte> Text { get; }
            public ReadOnlyMemory<byte> Binary { get; }

            public static Payload Create(string json)
            {
                ReadOnlyMemory<byte> text = Encoding.UTF8.GetBytes(json);
                ReadOnlyMemory<byte> binary = JsonTestUtils.ConvertTextToBinary(json);

                return new Payload(
                    text,
                    binary);
            }
        }
    }
}

namespace Microsoft.Azure.Cosmos.Performance.Tests.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Serializer;
    using Newtonsoft.Json;

    [MemoryDiagnoser]
    [SimpleJob(invocationCount: 25, targetCount: 25)]
    public class WireFormatBenchmark
    {
        private readonly CosmosSerializerCore serializerCore = new CosmosSerializerCore();
        private readonly byte[] payloadBytes;
        private readonly byte[] binaryPayload;

        public class ToDoActivity
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            public string status { get; set; }
            public string CamelCase { get; set; }

            public bool valid { get; set; }

            public ToDoActivity[] children { get; set; }

            public override bool Equals(object obj)
            {
                ToDoActivity input = obj as ToDoActivity;
                if (input == null)
                {
                    return false;
                }

                return string.Equals(this.id, input.id)
                    && this.taskNum == input.taskNum
                    && this.cost == input.cost
                    && string.Equals(this.description, input.description)
                    && string.Equals(this.status, input.status);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public static ToDoActivity CreateRandomToDoActivity(string pk = null, string id = null)
            {
                if (string.IsNullOrEmpty(pk))
                {
                    pk = "TBD" + Guid.NewGuid().ToString();
                }
                if (id == null)
                {
                    id = Guid.NewGuid().ToString();
                }
                return new ToDoActivity()
                {
                    id = id,
                    description = "CreateRandomToDoActivity",
                    status = pk,
                    taskNum = 42,
                    cost = double.MaxValue,
                    CamelCase = "camelCase",
                    children = new ToDoActivity[]
                    { new ToDoActivity { id = "child1", taskNum = 30 },
                  new ToDoActivity { id = "child2", taskNum = 40}
                    },
                    valid = true
                };
            }
        }

        public class ServiceResponse
        {
            public List<ToDoActivity> Documents { get; set; } = new List<ToDoActivity>();
        }

        public WireFormatBenchmark()
        {
            ServiceResponse serviceResponse = new ServiceResponse();
            for (int i = 0; i < 1000; i++)
            {
                serviceResponse.Documents.Add(ToDoActivity.CreateRandomToDoActivity(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()));
            }

            MemoryStream streamPayload = new MemoryStream();
            using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true),
                bufferSize: 1024,
                leaveOpen: true))
            {
                using (JsonTextWriter writer = new JsonTextWriter(streamWriter))
                {
                    writer.Formatting = Newtonsoft.Json.Formatting.None;
                    Newtonsoft.Json.JsonSerializer jsonSerializer = new Newtonsoft.Json.JsonSerializer();
                    jsonSerializer.Serialize(writer, serviceResponse);
                    writer.Flush();
                    streamWriter.Flush();
                }
            }
            streamPayload.Position = 0;
            this.payloadBytes = streamPayload.ToArray();

            string jsonString = JsonConvert.SerializeObject(serviceResponse);
            CosmosObject cosmosObj = CosmosObject.Parse(jsonString);
            IJsonWriter jsonWriter = Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Binary);
            cosmosObj.WriteTo(jsonWriter);
            ReadOnlyMemory<byte> result = jsonWriter.GetResult();
            this.binaryPayload = result.ToArray();
            Console.WriteLine($"Text: {this.payloadBytes.Length}; Binary: {this.binaryPayload.Length}");
        }

        private MemoryStream ms;

        [IterationSetup(Target = nameof(Version3_8_0TextOverWire))]
        public void IterationVersion3_8_0TextOverWire()
        {
            this.ms = new MemoryStream(this.payloadBytes, 0, this.payloadBytes.Length, false, true);
        }

        [Benchmark]
        public void Version3_8_0TextOverWire()
        {
            using (this.ms) { }
        }

        [IterationSetup(Target = nameof(LastestChangeTextOverWire))]
        public void IterationLastestChangeTextOverWire()
        {
            this.ms = new MemoryStream(this.payloadBytes, 0, this.payloadBytes.Length, false, true);
        }

        [Benchmark]
        public async Task LastestChangeTextOverWire()
        {
            using (await this.JsonConversion()) { }
        }

        [IterationSetup(Target = nameof(LastestChangeBinaryOverWire))]
        public void IterationLastestChangeBinaryOverWire()
        {
            this.ms = new MemoryStream(this.binaryPayload, 0, this.binaryPayload.Length, false, true);
        }

        [Benchmark]
        public async Task LastestChangeBinaryOverWire()
        {
            using (await this.JsonConversion()) { }
        }

        private async Task<MemoryStream> JsonConversion()
        {
            // This is a copy from FeedRangeIteratorCore logic
            using (this.ms)
            {
                // Rewrite the payload to be in the specified format.
                // If it's already in the correct format, then the following will be a memcpy.
                MemoryStream memoryStream;
                if (this.ms is MemoryStream responseContentAsMemoryStream)
                {
                    memoryStream = responseContentAsMemoryStream;
                }
                else
                {
                    memoryStream = new MemoryStream();
                    await this.ms.CopyToAsync(memoryStream);
                }

                ReadOnlyMemory<byte> buffer;
                if (memoryStream.TryGetBuffer(out ArraySegment<byte> segment))
                {
                    buffer = segment.Array.AsMemory().Slice(start: segment.Offset, length: segment.Count);
                }
                else
                {
                    throw new Exception("TryGetBuffer");
                }

                IJsonReader jsonReader = Cosmos.Json.JsonReader.Create(buffer);
                IJsonWriter jsonWriter = NewtonsoftToCosmosDBWriter.CreateTextWriter();


                jsonWriter.WriteAll(jsonReader);

                ReadOnlyMemory<byte> result = jsonWriter.GetResult();
                MemoryStream rewrittenMemoryStream;
                if (MemoryMarshal.TryGetArray(result, out ArraySegment<byte> rewrittenSegment))
                {
                    rewrittenMemoryStream = new MemoryStream(rewrittenSegment.Array, index: rewrittenSegment.Offset, count: rewrittenSegment.Count, writable: false, publiclyVisible: true);
                }
                else
                {
                    throw new Exception("TryGetArray");
                }

                return rewrittenMemoryStream;
            }
        }
    }
}
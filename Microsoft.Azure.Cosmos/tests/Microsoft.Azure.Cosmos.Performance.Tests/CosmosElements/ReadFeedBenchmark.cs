namespace Microsoft.Azure.Cosmos.Performance.Tests.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Serializer;
    using Newtonsoft.Json;

    [MemoryDiagnoser]
    public class ReadFeedBenchmark
    {
        private readonly CosmosSerializerCore serializerCore = new CosmosSerializerCore();
        private readonly byte[] payloadBytes;

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

        public ReadFeedBenchmark()
        {
            ServiceResponse serviceResponse = new ServiceResponse();
            for (int i = 0; i < 1000; i++)
            {
                serviceResponse.Documents.Add(ToDoActivity.CreateRandomToDoActivity(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()));
            }

            MemoryStream streamPayload = new MemoryStream();
            using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true), bufferSize: 1024, leaveOpen: true))
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
        }

        [Benchmark]
        public void ByteParsingToFindJsonArray()
        {
            using (MemoryStream ms = new MemoryStream(this.payloadBytes))
            {
                long length = ms.Length;
                using (MemoryStream memoryStream = CosmosFeedResponseSerializer.GetStreamWithoutServiceEnvelope(ms))
                {
                    if (length == memoryStream.Length)
                    {
                        throw new Exception();
                    }
                }
            }
        }

        [Benchmark]
        public void ByteParsingToFindJsonArrayWithSeriliazation()
        {
            using (MemoryStream ms = new MemoryStream(this.payloadBytes))
            {
                IReadOnlyList<ToDoActivity> results = CosmosFeedResponseSerializer.FromFeedResponseStream<ToDoActivity>(
                    this.serializerCore,
                    ms);

                if (results.Count != 1000)
                {
                    throw new Exception();
                }
            }
        }

        [Benchmark]
        public void CosmosElementsToFindArray()
        {
            using (MemoryStream ms = new MemoryStream(this.payloadBytes))
            {
                CosmosArray array = CosmosElementSerializer.ToCosmosElements(
                    ms,
                    Documents.ResourceType.Document,
                    null);

                using (MemoryStream memoryStream = CosmosElementSerializer.ElementToMemoryStream(
                    array,
                    null))
                {
                    if (ms.Length == memoryStream.Length)
                    {
                        throw new Exception();
                    }
                }
            }
        }

        [Benchmark]
        public void CosmosElementsToFindArrayWithSerialization()
        {
            using (MemoryStream ms = new MemoryStream(this.payloadBytes))
            {
                CosmosArray array = CosmosElementSerializer.ToCosmosElements(
                    ms,
                    Documents.ResourceType.Document,
                    null);

                IReadOnlyList<ToDoActivity> results = CosmosElementSerializer.GetResources<ToDoActivity>(
                    array,
                    this.serializerCore);

                if (results.Count != 1000)
                {
                    throw new Exception();
                }
            }
        }
    }
}
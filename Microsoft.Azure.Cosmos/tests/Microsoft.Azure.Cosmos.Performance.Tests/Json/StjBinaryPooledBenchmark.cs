// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Jobs;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tests.Json;

    /// <summary>
    /// Before/after micro-benchmark for both read paths of
    /// <c>CosmosSystemTextJsonSerializer.FromStream</c>. MediumRun +
    /// MemoryDiagnoser, with an Op/s column (throughput; higher = less CPU
    /// per operation).
    ///
    /// Each method mirrors the SDK branch exactly:
    ///   Binary_Old = JsonSerializer.Deserialize&lt;T&gt;(cosmosObject.ToString())                       // UTF-16 string
    ///   Binary_New = WriteTo(JsonWriter Text, pooled) + Deserialize&lt;T&gt;(ReadOnlySpan&lt;byte&gt;) + Dispose // span + ArrayPool
    ///   Text_Old   = new StreamReader(stream).ReadToEnd() + Deserialize&lt;T&gt;(string)                  // UTF-16 string
    ///   Text_New   = Deserialize&lt;T&gt;(stream)                                                          // streamed, no buffer
    ///
    /// Note: the text path has no transcode buffer, so there is nothing to pool;
    /// its "New" is the stream optimization only.
    /// </summary>
    [Config(typeof(MediumRunConfig))]
    [MemoryDiagnoser]
    public class StjBinaryPooledBenchmark
    {
        private class MediumRunConfig : ManualConfig
        {
            public MediumRunConfig()
            {
                this.AddJob(Job.MediumRun);
                this.AddColumn(StatisticColumn.OperationsPerSecond);
            }
        }

        private static readonly JsonSerializerOptions Options = new ()
        {
            PropertyNameCaseInsensitive = true,
        };

        [Params(1, 100, 1000)]
        public int DocumentCount;

        private CosmosObject cosmosObject;
        private byte[] textUtf8;

        [GlobalSetup]
        public void Setup()
        {
            string unit = File.ReadAllText("samplepayload.json");

            StringBuilder sb = new ();
            sb.Append("{\"id\":\"root\",\"items\":[");
            for (int i = 0; i < this.DocumentCount; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(unit);
            }

            sb.Append("]}");
            string json = sb.ToString();

            this.textUtf8 = Encoding.UTF8.GetBytes(json);

            byte[] binaryBuffer = JsonTestUtils.ConvertTextToBinary(json);
            this.cosmosObject = CosmosObject.CreateFromBuffer(binaryBuffer);
        }

        [Benchmark(Description = "Binary Before")]
        public object Binary_Old()
        {
            string text = this.cosmosObject.ToString();
            return System.Text.Json.JsonSerializer.Deserialize<FamilyRoot>(text, Options);
        }

        [Benchmark(Description = "Binary After")]
        public object Binary_Pooled()
        {
            using JsonWriter jsonWriter = (JsonWriter)JsonWriter.Create(JsonSerializationFormat.Text, pooled: true);
            this.cosmosObject.WriteTo(jsonWriter);
            return System.Text.Json.JsonSerializer.Deserialize<FamilyRoot>(jsonWriter.GetResult().Span, Options);
        }

        [Benchmark(Description = "Text Before")]
        public object Text_Old()
        {
            using MemoryStream stream = new (this.textUtf8, writable: false);
            using StreamReader reader = new (stream);
            return System.Text.Json.JsonSerializer.Deserialize<FamilyRoot>(reader.ReadToEnd(), Options);
        }

        [Benchmark(Description = "Text After")]
        public object Text_New()
        {
            using MemoryStream stream = new (this.textUtf8, writable: false);
            return System.Text.Json.JsonSerializer.Deserialize<FamilyRoot>(stream, Options);
        }

        private class FamilyRoot
        {
            public string Id { get; set; }

            public Family[] Items { get; set; }
        }

        private class Family
        {
            public string Id { get; set; }

            public string LastName { get; set; }

            public Parent[] Parents { get; set; }

            public Child[] Children { get; set; }

            public Location Location { get; set; }

            public bool IsRegistered { get; set; }
        }

        private class Parent
        {
            public string FirstName { get; set; }

            public string Relationship { get; set; }
        }

        private class Child
        {
            public string FirstName { get; set; }

            public string Gender { get; set; }

            public int Grade { get; set; }

            public Pet[] Pets { get; set; }
        }

        private class Pet
        {
            public string GivenName { get; set; }

            public string Type { get; set; }
        }

        private class Location
        {
            public string State { get; set; }

            public string County { get; set; }

            public string City { get; set; }
        }
    }
}

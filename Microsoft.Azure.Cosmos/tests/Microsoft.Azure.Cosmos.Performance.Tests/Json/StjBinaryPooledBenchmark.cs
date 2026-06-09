// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tests.Json;

    /// <summary>
    /// A/B/C micro-benchmark for the binary read path of
    /// <c>CosmosSystemTextJsonSerializer.FromStream</c>, isolating the
    /// transcode-buffer allocation. Uses the team's <see cref="SdkBenchmarkConfiguration"/>
    /// (Op/s throughput, percentiles, MemoryDiagnoser + ThreadingDiagnoser).
    ///
    /// Each method mirrors the SDK binary branch exactly
    /// (ReadAll -&gt; TryCreateFromBuffer -&gt; transcode -&gt; deserialize):
    ///   Old    = JsonSerializer.Deserialize&lt;T&gt;(cosmosObject.ToString())          // UTF-16 string
    ///   New    = WriteTo(JsonWriter Text) + Deserialize&lt;T&gt;(ReadOnlySpan&lt;byte&gt;)   // PR #5908
    ///   Pooled = WriteTo(JsonWriter Text, pooled) + Deserialize + Dispose         // this prototype
    /// </summary>
    [Config(typeof(SdkBenchmarkConfiguration))]
    public class StjBinaryPooledBenchmark
    {
        private static readonly JsonSerializerOptions Options = new ()
        {
            PropertyNameCaseInsensitive = true,
        };

        [Params(1, 100, 1000)]
        public int DocumentCount;

        private CosmosObject cosmosObject;

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

            byte[] binaryBuffer = JsonTestUtils.ConvertTextToBinary(sb.ToString());
            this.cosmosObject = CosmosObject.CreateFromBuffer(binaryBuffer);
        }

        [Benchmark(Description = "Binary_Old (ToString -> Deserialize<string>)", Baseline = true)]
        public object Binary_Old()
        {
            string text = this.cosmosObject.ToString();
            return System.Text.Json.JsonSerializer.Deserialize<FamilyRoot>(text, Options);
        }

        [Benchmark(Description = "Binary_New (WriteTo -> Deserialize<ReadOnlySpan<byte>>)")]
        public object Binary_New()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            this.cosmosObject.WriteTo(jsonWriter);
            return System.Text.Json.JsonSerializer.Deserialize<FamilyRoot>(jsonWriter.GetResult().Span, Options);
        }

        [Benchmark(Description = "Binary_Pooled (WriteTo pooled -> Deserialize -> Dispose)")]
        public object Binary_Pooled()
        {
            using JsonWriter jsonWriter = (JsonWriter)JsonWriter.Create(JsonSerializationFormat.Text, pooled: true);
            this.cosmosObject.WriteTo(jsonWriter);
            return System.Text.Json.JsonSerializer.Deserialize<FamilyRoot>(jsonWriter.GetResult().Span, Options);
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

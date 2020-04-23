//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: JsonMicroBenchmarks.tt: 99

namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;

    [MemoryDiagnoser]
    public class JsonWriteMicroBenchmarks
    {
        [Benchmark]
        public void Null_Text()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNullValue(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Null_Binary()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNullValue(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Null_Newtonsoft()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNullValue(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void True_Text()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteBoolValue(true); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void True_Binary()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteBoolValue(true); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void True_Newtonsoft()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteBoolValue(true); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void False_Text()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteBoolValue(false); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void False_Binary()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteBoolValue(false); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void False_Newtonsoft()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteBoolValue(false); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Number64Integer_Text()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNumber64Value(123); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Number64Integer_Binary()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNumber64Value(123); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Number64Integer_Newtonsoft()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNumber64Value(123); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Number64Double_Text()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNumber64Value(6.0221409e+23); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Number64Double_Binary()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNumber64Value(6.0221409e+23); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Number64Double_Newtonsoft()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNumber64Value(6.0221409e+23); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void String_Text()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteStringValue("Hello World"); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void String_Binary()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteStringValue("Hello World"); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void String_Newtonsoft()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteStringValue("Hello World"); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Array_Text()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteArrayStart(); jsonWriter.WriteArrayEnd(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Array_Binary()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteArrayStart(); jsonWriter.WriteArrayEnd(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Array_Newtonsoft()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteArrayStart(); jsonWriter.WriteArrayEnd(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Object_Text()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteObjectStart(); jsonWriter.WriteObjectEnd(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Object_Binary()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteObjectStart(); jsonWriter.WriteObjectEnd(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Object_Newtonsoft()
        {
            JsonWriteMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteObjectStart(); jsonWriter.WriteObjectEnd(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }


        private static void ExecuteWriteMicroBenchmark(
            Action<IJsonWriter> writeTokens,
            BenchmarkSerializationFormat benchmarkSerializationFormat)
        {
            if (writeTokens == null)
            {
                throw new ArgumentNullException(nameof(writeTokens));
            }

            IJsonWriter jsonWriter;
            switch (benchmarkSerializationFormat)
            {
                case BenchmarkSerializationFormat.Text:
                    jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                    break;

                case BenchmarkSerializationFormat.Binary:
                    jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
                    break;

                case BenchmarkSerializationFormat.Newtonsoft:
                    jsonWriter = NewtonsoftToCosmosDBWriter.CreateTextWriter();
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(BenchmarkSerializationFormat)}: '{benchmarkSerializationFormat}'.");
            }

            jsonWriter.WriteArrayStart();

            for (int i = 0; i < 2000000; i++)
            {
                writeTokens(jsonWriter);
            }

            jsonWriter.WriteArrayEnd();
        }
    }
}

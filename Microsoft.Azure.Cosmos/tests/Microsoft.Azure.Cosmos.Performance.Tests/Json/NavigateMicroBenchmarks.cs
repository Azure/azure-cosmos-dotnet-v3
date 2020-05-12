//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: JsonMicroBenchmarks.tt: 168

namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Json;

    [MemoryDiagnoser]
    public class JsonNavigateMicroBenchmarks
    {
        private static readonly BenchmarkPayload NullPayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteNullValue(); });
        private static readonly BenchmarkPayload TruePayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteBoolValue(true); });
        private static readonly BenchmarkPayload FalsePayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteBoolValue(false); });
        private static readonly BenchmarkPayload Number64IntegerPayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteNumber64Value(123); });
        private static readonly BenchmarkPayload Number64DoublePayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteNumber64Value(6.0221409e+23); });
        private static readonly BenchmarkPayload StringPayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteStringValue("Hello World"); });
        private static readonly BenchmarkPayload ArrayPayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteArrayStart(); jsonWriter.WriteArrayEnd(); });
        private static readonly BenchmarkPayload ObjectPayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteObjectStart(); jsonWriter.WriteObjectEnd(); });

        [Benchmark]
        public void Navigate_Null_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Navigate_Null_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Navigate_Null_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Navigate_True_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Navigate_True_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Navigate_True_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Navigate_False_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Navigate_False_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Navigate_False_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Navigate_Number64Double_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Navigate_Number64Double_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Navigate_Number64Double_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Navigate_String_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Navigate_String_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Navigate_String_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Navigate_Array_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Navigate_Array_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Navigate_Array_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Navigate_Object_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Navigate_Object_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Navigate_Object_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }


        private static void ExecuteNavigateMicroBenchmark(
            BenchmarkPayload payload,
            BenchmarkSerializationFormat benchmarkSerializationFormat)
        {
            IJsonNavigator jsonNavigator = benchmarkSerializationFormat switch
            {
                BenchmarkSerializationFormat.Text => JsonNavigator.Create(payload.Text),
                BenchmarkSerializationFormat.Binary => JsonNavigator.Create(payload.Binary),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(BenchmarkSerializationFormat)}: '{benchmarkSerializationFormat}'."),
            };

            Utils.DrainNavigator(jsonNavigator);
        }
    }
}

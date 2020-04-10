//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: JsonMicroBenchmarks.tt: 29

namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;

    [MemoryDiagnoser]
    public class JsonMicroBenchmarks
    {
        private static readonly Payload NullPayload = new Payload((jsonWriter) => { jsonWriter.WriteNullValue(); });
        private static readonly Payload TruePayload = new Payload((jsonWriter) => { jsonWriter.WriteBoolValue(true); });
        private static readonly Payload FalsePayload = new Payload((jsonWriter) => { jsonWriter.WriteBoolValue(false); });
        private static readonly Payload Number64IntegerPayload = new Payload((jsonWriter) => { jsonWriter.WriteNumberValue(123); });
        private static readonly Payload Number64DoublePayload = new Payload((jsonWriter) => { jsonWriter.WriteNumberValue(6.0221409e+23); });
        private static readonly Payload StringPayload = new Payload((jsonWriter) => { jsonWriter.WriteStringValue("Hello World"); });
        private static readonly Payload ArrayPayload = new Payload((jsonWriter) => { jsonWriter.WriteArrayStart(); jsonWriter.WriteArrayEnd(); });
        private static readonly Payload ObjectPayload = new Payload((jsonWriter) => { jsonWriter.WriteObjectStart(); jsonWriter.WriteObjectEnd(); });

        public JsonMicroBenchmarks()
        {
        }

        [Benchmark]
        public void Read_Null_Text()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Null_Binary()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Null_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_True_Text()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_True_Binary()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_True_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_False_Text()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_False_Binary()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_False_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Number64Integer_Text_Materialize()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: true);
        }

        [Benchmark]
        public void Read_Number64Integer_Text()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Number64Integer_Binary_Materialize()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: true);
        }

        [Benchmark]
        public void Read_Number64Integer_Binary()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Number64Integer_Newtonsoft_Materialize()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: true);
        }

        [Benchmark]
        public void Read_Number64Integer_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Number64Double_Text_Materialize()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: true);
        }

        [Benchmark]
        public void Read_Number64Double_Text()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Number64Double_Binary_Materialize()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: true);
        }

        [Benchmark]
        public void Read_Number64Double_Binary()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Number64Double_Newtonsoft_Materialize()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: true);
        }

        [Benchmark]
        public void Read_Number64Double_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_String_Text_Materialize()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: true);
        }

        [Benchmark]
        public void Read_String_Text()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_String_Binary_Materialize()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: true);
        }

        [Benchmark]
        public void Read_String_Binary()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_String_Newtonsoft_Materialize()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: true);
        }

        [Benchmark]
        public void Read_String_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Array_Text()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Array_Binary()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Array_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Object_Text()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Object_Binary()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Read_Object_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }


        [Benchmark]
        public void Write_Null_Text()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNullValue(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Write_Null_Binary()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNullValue(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Write_Null_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNullValue(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Write_True_Text()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteBoolValue(true); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Write_True_Binary()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteBoolValue(true); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Write_True_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteBoolValue(true); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Write_False_Text()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteBoolValue(false); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Write_False_Binary()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteBoolValue(false); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Write_False_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteBoolValue(false); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Write_Number64Integer_Text()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNumberValue(123); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Write_Number64Integer_Binary()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNumberValue(123); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Write_Number64Integer_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNumberValue(123); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Write_Number64Double_Text()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNumberValue(6.0221409e+23); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Write_Number64Double_Binary()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNumberValue(6.0221409e+23); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Write_Number64Double_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteNumberValue(6.0221409e+23); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Write_String_Text()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteStringValue("Hello World"); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Write_String_Binary()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteStringValue("Hello World"); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Write_String_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteStringValue("Hello World"); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Write_Array_Text()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteArrayStart(); jsonWriter.WriteArrayEnd(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Write_Array_Binary()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteArrayStart(); jsonWriter.WriteArrayEnd(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Write_Array_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteArrayStart(); jsonWriter.WriteArrayEnd(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }

        [Benchmark]
        public void Write_Object_Text()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteObjectStart(); jsonWriter.WriteObjectEnd(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text);
        }

        [Benchmark]
        public void Write_Object_Binary()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteObjectStart(); jsonWriter.WriteObjectEnd(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary);
        }

        [Benchmark]
        public void Write_Object_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteWriteMicroBenchmark(
                writeTokens: (jsonWriter) => { jsonWriter.WriteObjectStart(); jsonWriter.WriteObjectEnd(); },
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft);
        }


        [Benchmark]
        public void Navigate_Null_Text_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Null_Text()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Null_Binary_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Null_Binary()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Null_Newtonsoft_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Null_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_True_Text_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_True_Text()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_True_Binary_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_True_Binary()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_True_Newtonsoft_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_True_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_False_Text_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_False_Text()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_False_Binary_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_False_Binary()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_False_Newtonsoft_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_False_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Text_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Text()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Binary_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Binary()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Newtonsoft_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Number64Double_Text_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Number64Double_Text()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Number64Double_Binary_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Number64Double_Binary()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Number64Double_Newtonsoft_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Number64Double_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_String_Text_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_String_Text()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_String_Binary_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_String_Binary()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_String_Newtonsoft_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_String_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Array_Text_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Array_Text()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Array_Binary_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Array_Binary()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Array_Newtonsoft_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Array_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Object_Text_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Object_Text()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Object_Binary_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Object_Binary()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Object_Newtonsoft_Materialize()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Object_Newtonsoft()
        {
            JsonMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        private static void ExecuteReadMicroBenchmark(
            Payload payload,
            BenchmarkSerializationFormat benchmarkSerializationFormat,
            bool materializeValue)
        {
            IJsonReader jsonReader = benchmarkSerializationFormat switch
            {
                BenchmarkSerializationFormat.Text => JsonReader.Create(payload.Text),
                BenchmarkSerializationFormat.Binary => JsonReader.Create(payload.Binary),
                BenchmarkSerializationFormat.Newtonsoft => NewtonsoftToCosmosDBReader.CreateFromBuffer(payload.Newtonsoft),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(BenchmarkSerializationFormat)}: '{benchmarkSerializationFormat}'."),
            };

            while (jsonReader.Read())
            {
                // Materialize the value
                switch (jsonReader.CurrentTokenType)
                {
                    case JsonTokenType.BeginArray:
                    case JsonTokenType.EndArray:
                    case JsonTokenType.BeginObject:
                    case JsonTokenType.EndObject:
                    case JsonTokenType.Null:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                        // Single byte tokens
                        break;

                    case JsonTokenType.String:
                    case JsonTokenType.FieldName:
                        if (materializeValue)
                        {
                            string stringValue = jsonReader.GetStringValue();
                        }
                        break;

                    case JsonTokenType.Number:
                        if (materializeValue)
                        {
                            Number64 number64Value = jsonReader.GetNumberValue();
                        }
                        break;

                    case JsonTokenType.Int8:
                        if (materializeValue)
                        {
                            sbyte int8Value = jsonReader.GetInt8Value();
                        }
                        break;

                    case JsonTokenType.Int16:
                        if (materializeValue)
                        {
                            short int16Value = jsonReader.GetInt16Value();
                        }
                        break;

                    case JsonTokenType.Int32:
                        if (materializeValue)
                        {
                            int int32Value = jsonReader.GetInt32Value();
                        }
                        break;

                    case JsonTokenType.Int64:
                        if (materializeValue)
                        {
                            long int64Value = jsonReader.GetInt64Value();
                        }
                        break;

                    case JsonTokenType.UInt32:
                        if (materializeValue)
                        {
                            uint uInt32Value = jsonReader.GetUInt32Value();
                        }
                        break;

                    case JsonTokenType.Float32:
                        if (materializeValue)
                        {
                            float float32Value = jsonReader.GetFloat32Value();
                        }
                        break;

                    case JsonTokenType.Float64:
                        if (materializeValue)
                        {
                            double doubleValue = jsonReader.GetFloat64Value();
                        }
                        break;

                    case JsonTokenType.Guid:
                        if (materializeValue)
                        {
                            Guid guidValue = jsonReader.GetGuidValue();
                        }
                        break;

                    case JsonTokenType.Binary:
                        if (materializeValue)
                        {
                            ReadOnlyMemory<byte> binaryValue = jsonReader.GetBinaryValue();
                        }
                        break;

                    default:
                        throw new ArgumentException("$Unknown token type.");
                }
            }
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

        private static void ExecuteNavigateMicroBenchmark(
            Payload payload,
            BenchmarkSerializationFormat benchmarkSerializationFormat,
            bool materialize)
        {
            ReadOnlyMemory<byte> buffer = benchmarkSerializationFormat switch
            {
                BenchmarkSerializationFormat.Text => payload.Text,
                BenchmarkSerializationFormat.Binary => payload.Binary,
                BenchmarkSerializationFormat.Newtonsoft => payload.Newtonsoft,
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(BenchmarkSerializationFormat)}: '{benchmarkSerializationFormat}'."),
            };

            IJsonNavigator jsonNavigator = JsonNavigator.Create(buffer);

            void Navigate(IJsonNavigator navigator, IJsonNavigatorNode node, bool materialize)
            {
                switch (navigator.GetNodeType(node))
                {
                    case JsonNodeType.Null:
                    case JsonNodeType.False:
                    case JsonNodeType.True:
                        // no value to materialize.
                        break;

                    case JsonNodeType.Number:
                        if (materialize)
                        {
                            Number64 value = navigator.GetNumberValue(node);
                        }
                        break;

                    case JsonNodeType.String:
                        if (materialize)
                        {
                            string value = navigator.GetStringValue(node);
                        }
                        break;

                    case JsonNodeType.Array:
                        foreach (IJsonNavigatorNode arrayItem in navigator.GetArrayItems(node))
                        {
                            Navigate(navigator, arrayItem, materialize);
                        }
                        break;

                    case JsonNodeType.Object:
                        foreach (ObjectProperty objectProperty in navigator.GetObjectProperties(node))
                        {
                            IJsonNavigatorNode nameNode = objectProperty.NameNode;
                            IJsonNavigatorNode valueNode = objectProperty.ValueNode;
                            if (materialize)
                            {
                                string name = jsonNavigator.GetStringValue(nameNode);
                            }

                            Navigate(navigator, valueNode, materialize);
                        }
                        break;

                    case JsonNodeType.Int8:
                        if (materialize)
                        {
                            sbyte value = navigator.GetInt8Value(node);
                        }
                        break;

                    case JsonNodeType.Int16:
                        if (materialize)
                        {
                            short value = navigator.GetInt16Value(node);
                        }
                        break;

                    case JsonNodeType.Int32:
                        if (materialize)
                        {
                            short value = navigator.GetInt16Value(node);
                        }
                        break;

                    case JsonNodeType.Int64:
                        if (materialize)
                        {
                            long value = navigator.GetInt64Value(node);
                        }
                        break;

                    case JsonNodeType.UInt32:
                        if (materialize)
                        {
                            uint value = navigator.GetUInt32Value(node);
                        }
                        break;

                    case JsonNodeType.Float32:
                        if (materialize)
                        {
                            float value = navigator.GetFloat32Value(node);
                        }
                        break;

                    case JsonNodeType.Float64:
                        if (materialize)
                        {
                            double value = navigator.GetFloat64Value(node);
                        }
                        break;

                    case JsonNodeType.Binary:
                        if (materialize)
                        {
                            ReadOnlyMemory<byte> value = navigator.GetBinaryValue(node);
                        }
                        break;

                    case JsonNodeType.Guid:
                        if (materialize)
                        {
                            Guid value = navigator.GetGuidValue(node);
                        }
                        break;

                    default:
                        throw new ArgumentOutOfRangeException($"Unknown {nameof(JsonNodeType)}: '{navigator.GetNodeType(node)}.'");
                }
            }

            Navigate(jsonNavigator, jsonNavigator.GetRootNode(), materialize);
        }

        private enum BenchmarkSerializationFormat
        {
            Text,
            Binary,
            Newtonsoft,
        }

        private readonly struct Payload
        {
            public Payload(Action<IJsonWriter> writeToken)
            {
                if (writeToken == null)
                {
                    throw new ArgumentNullException(nameof(writeToken));
                }

                IJsonWriter jsonTextWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                IJsonWriter jsonBinaryWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
                IJsonWriter jsonNewtonsoftWriter = NewtonsoftToCosmosDBWriter.CreateTextWriter();

                jsonTextWriter.WriteArrayStart();
                jsonBinaryWriter.WriteArrayStart();
                jsonNewtonsoftWriter.WriteArrayStart();

                for (int i = 0; i < 1000000; i++)
                {
                    writeToken(jsonTextWriter);
                    writeToken(jsonBinaryWriter);
                    writeToken(jsonNewtonsoftWriter);
                }

                jsonTextWriter.WriteArrayEnd();
                jsonBinaryWriter.WriteArrayEnd();
                jsonNewtonsoftWriter.WriteArrayEnd();

                this.Text = jsonTextWriter.GetResult();
                this.Binary = jsonTextWriter.GetResult();
                this.Newtonsoft = jsonNewtonsoftWriter.GetResult();
            }

            public ReadOnlyMemory<byte> Text { get; }
            public ReadOnlyMemory<byte> Binary { get; }
            public ReadOnlyMemory<byte> Newtonsoft { get; }
        }
    }
}

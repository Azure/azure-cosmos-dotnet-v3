//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: JsonMicroBenchmarks.tt: 264

namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;

    [MemoryDiagnoser]
    public class JsonNavigateMicroBenchmarks
    {
        private static readonly BenchmarkPayload NullPayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteNullValue(); });
        private static readonly BenchmarkPayload TruePayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteBoolValue(true); });
        private static readonly BenchmarkPayload FalsePayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteBoolValue(false); });
        private static readonly BenchmarkPayload Number64IntegerPayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteNumberValue(123); });
        private static readonly BenchmarkPayload Number64DoublePayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteNumberValue(6.0221409e+23); });
        private static readonly BenchmarkPayload StringPayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteStringValue("Hello World"); });
        private static readonly BenchmarkPayload ArrayPayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteArrayStart(); jsonWriter.WriteArrayEnd(); });
        private static readonly BenchmarkPayload ObjectPayload = new BenchmarkPayload((jsonWriter) => { jsonWriter.WriteObjectStart(); jsonWriter.WriteObjectEnd(); });
        [Benchmark]
        public void Navigate_Null_Text_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Null_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Null_Binary_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Null_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Null_Newtonsoft_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Null_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_True_Text_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_True_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_True_Binary_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_True_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_True_Newtonsoft_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_True_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_False_Text_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_False_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_False_Binary_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_False_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_False_Newtonsoft_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_False_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Text_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Binary_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Newtonsoft_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Number64Integer_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Number64Double_Text_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Number64Double_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Number64Double_Binary_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Number64Double_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Number64Double_Newtonsoft_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Number64Double_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_String_Text_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_String_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_String_Binary_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_String_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_String_Newtonsoft_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_String_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Array_Text_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Array_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Array_Binary_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Array_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Array_Newtonsoft_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Array_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Object_Text_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Object_Text()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Object_Binary_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Object_Binary()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materialize: false);
        }

        [Benchmark]
        public void Navigate_Object_Newtonsoft_Materialize()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: true);
        }

        [Benchmark]
        public void Navigate_Object_Newtonsoft()
        {
            JsonNavigateMicroBenchmarks.ExecuteNavigateMicroBenchmark(
                payload: JsonNavigateMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materialize: false);
        }


        private static void ExecuteNavigateMicroBenchmark(
            BenchmarkPayload payload,
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
    }
}

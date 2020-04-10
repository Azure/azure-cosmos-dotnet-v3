//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: JsonMicroBenchmarks.tt: 45

namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;

    [MemoryDiagnoser]
    public class JsonReadMicroBenchmarks
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
        public void Null_Text()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Null_Binary()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Null_Newtonsoft()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.NullPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void True_Text()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void True_Binary()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void True_Newtonsoft()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.TruePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void False_Text()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void False_Binary()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void False_Newtonsoft()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.FalsePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void Number64Integer_Text_Materialize()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: true);
        }

        [Benchmark]
        public void Number64Integer_Text()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Number64Integer_Binary_Materialize()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: true);
        }

        [Benchmark]
        public void Number64Integer_Binary()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Number64Integer_Newtonsoft_Materialize()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: true);
        }

        [Benchmark]
        public void Number64Integer_Newtonsoft()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.Number64IntegerPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void Number64Double_Text_Materialize()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: true);
        }

        [Benchmark]
        public void Number64Double_Text()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Number64Double_Binary_Materialize()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: true);
        }

        [Benchmark]
        public void Number64Double_Binary()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Number64Double_Newtonsoft_Materialize()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: true);
        }

        [Benchmark]
        public void Number64Double_Newtonsoft()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.Number64DoublePayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void String_Text_Materialize()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: true);
        }

        [Benchmark]
        public void String_Text()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void String_Binary_Materialize()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: true);
        }

        [Benchmark]
        public void String_Binary()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void String_Newtonsoft_Materialize()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: true);
        }

        [Benchmark]
        public void String_Newtonsoft()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.StringPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void Array_Text()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Array_Binary()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Array_Newtonsoft()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.ArrayPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }

        [Benchmark]
        public void Object_Text()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Text,
                materializeValue: false);
        }

        [Benchmark]
        public void Object_Binary()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Binary,
                materializeValue: false);
        }

        [Benchmark]
        public void Object_Newtonsoft()
        {
            JsonReadMicroBenchmarks.ExecuteReadMicroBenchmark(
                payload: JsonReadMicroBenchmarks.ObjectPayload,
                benchmarkSerializationFormat: BenchmarkSerializationFormat.Newtonsoft,
                materializeValue: false);
        }


        private static void ExecuteReadMicroBenchmark(
            BenchmarkPayload payload,
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
    }
}

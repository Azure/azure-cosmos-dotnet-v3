//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: JsonRoundtripBenchmark.tt: 36

/// <summary>
/// We don't have access to Microsoft.Azure.Cosmos.Core which is needed for JsonStringDictionary / dictionary encoded strings.
/// </summary>
#define NeedCore

namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System;
    using System.Text;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tests.Json;

    [MemoryDiagnoser]
    public class JsonRoundtripBenchmark
    {
        private static readonly CurratedDocsPayload NutritionDataData = CurratedDocsPayload.CreateFromCurratedDocs("NutritionData");

        [Benchmark]
        public void NutritionData_Text_To_Text()
        {
            JsonRoundtripBenchmark.ExecuteBenchmark(
                payload: JsonRoundtripBenchmark.NutritionDataData,
                sourceFormat: SerializationFormat.Text,
                destinationFormat: SerializationFormat.Text);
        }

        [Benchmark]
        public void NutritionData_Text_To_Binary()
        {
            JsonRoundtripBenchmark.ExecuteBenchmark(
                payload: JsonRoundtripBenchmark.NutritionDataData,
                sourceFormat: SerializationFormat.Text,
                destinationFormat: SerializationFormat.Binary);
        }

        [Benchmark]
        public void NutritionData_Text_To_NewtonsoftText()
        {
            JsonRoundtripBenchmark.ExecuteBenchmark(
                payload: JsonRoundtripBenchmark.NutritionDataData,
                sourceFormat: SerializationFormat.Text,
                destinationFormat: SerializationFormat.NewtonsoftText);
        }

        [Benchmark]
        public void NutritionData_Binary_To_Text()
        {
            JsonRoundtripBenchmark.ExecuteBenchmark(
                payload: JsonRoundtripBenchmark.NutritionDataData,
                sourceFormat: SerializationFormat.Binary,
                destinationFormat: SerializationFormat.Text);
        }

        [Benchmark]
        public void NutritionData_Binary_To_Binary()
        {
            JsonRoundtripBenchmark.ExecuteBenchmark(
                payload: JsonRoundtripBenchmark.NutritionDataData,
                sourceFormat: SerializationFormat.Binary,
                destinationFormat: SerializationFormat.Binary);
        }

        [Benchmark]
        public void NutritionData_Binary_To_NewtonsoftText()
        {
            JsonRoundtripBenchmark.ExecuteBenchmark(
                payload: JsonRoundtripBenchmark.NutritionDataData,
                sourceFormat: SerializationFormat.Binary,
                destinationFormat: SerializationFormat.NewtonsoftText);
        }

        [Benchmark]
        public void NutritionData_NewtonsoftText_To_Text()
        {
            JsonRoundtripBenchmark.ExecuteBenchmark(
                payload: JsonRoundtripBenchmark.NutritionDataData,
                sourceFormat: SerializationFormat.NewtonsoftText,
                destinationFormat: SerializationFormat.Text);
        }

        [Benchmark]
        public void NutritionData_NewtonsoftText_To_Binary()
        {
            JsonRoundtripBenchmark.ExecuteBenchmark(
                payload: JsonRoundtripBenchmark.NutritionDataData,
                sourceFormat: SerializationFormat.NewtonsoftText,
                destinationFormat: SerializationFormat.Binary);
        }

        [Benchmark]
        public void NutritionData_NewtonsoftText_To_NewtonsoftText()
        {
            JsonRoundtripBenchmark.ExecuteBenchmark(
                payload: JsonRoundtripBenchmark.NutritionDataData,
                sourceFormat: SerializationFormat.NewtonsoftText,
                destinationFormat: SerializationFormat.NewtonsoftText);
        }


        private static void ExecuteBenchmark(
            CurratedDocsPayload payload,
            SerializationFormat sourceFormat,
            SerializationFormat destinationFormat)
        {
            IJsonReader reader = sourceFormat switch
            {
                SerializationFormat.Text => JsonReader.Create(payload.Text),
                SerializationFormat.Binary => JsonReader.Create(payload.Binary),
                SerializationFormat.NewtonsoftText => NewtonsoftToCosmosDBReader.CreateFromBuffer(payload.Text),
                _ => throw new ArgumentException($"Unexpected {nameof(sourceFormat)} of type: '{sourceFormat}'"),
            };

            IJsonWriter writer = destinationFormat switch
            {
                SerializationFormat.Text => JsonWriter.Create(JsonSerializationFormat.Text),
                SerializationFormat.Binary => JsonWriter.Create(JsonSerializationFormat.Binary),
                SerializationFormat.NewtonsoftText => NewtonsoftToCosmosDBWriter.CreateTextWriter(),
                _ => throw new ArgumentException($"Unexpected {nameof(destinationFormat)} of type: {destinationFormat}"),
            };

            writer.WriteAll(reader);
        }

        private enum SerializationFormat
        {
            Text,
            Binary,
            NewtonsoftText,
        }

        private readonly struct CurratedDocsPayload
        {
            private CurratedDocsPayload(
                ReadOnlyMemory<byte> text,
                ReadOnlyMemory<byte> binary
#if !NeedCore
                ,
                (ReadOnlyMemory<byte> binary, JsonStringDictionary dictionary) binaryWithDictionaryEncoding
#endif
                )
            {
                this.Text = text;
                this.Binary = binary;
#if !NeedCore
                this.BinaryWithDictionaryEncoding = binaryWithDictionaryEncoding;
#endif
            }

            public ReadOnlyMemory<byte> Text { get; }
            public ReadOnlyMemory<byte> Binary { get; }
#if !NeedCore
            public (ReadOnlyMemory<byte> binary, JsonStringDictionary dictionary) BinaryWithDictionaryEncoding { get; }
#endif
            public static CurratedDocsPayload CreateFromCurratedDocs(string name)
            {
                if (name == null)
                {
                    throw new ArgumentNullException(nameof(name));
                }

                string path = $"TestJsons/{name}.json";
                string json = TextFileConcatenation.ReadMultipartFile(path);

                ReadOnlyMemory<byte> text = Encoding.UTF8.GetBytes(json);
                ReadOnlyMemory<byte> binary = JsonTestUtils.ConvertTextToBinary(json);
#if !NeedCore
                JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 128);
                ReadOnlyMemory<byte> dictionaryEncodedBinary = JsonTestUtils.ConvertTextToBinary(json, jsonStringDictionary);
#endif

                return new CurratedDocsPayload(
                    text: text,
                    binary: binary
#if !NeedCore
                    ,
                    binaryWithDictionaryEncoding: (dictionaryEncodedBinary, jsonStringDictionary)
#endif
                    );
            }
        }
    }
}

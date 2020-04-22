//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: CuratedDocsBenchmark.tt: 36

namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tests.Json;

    [MemoryDiagnoser]
    public class CuratedDocsBenchmark
    {
        private static readonly CurratedDocsPayload NutritionDataPayload = CurratedDocsPayload.CreateFromCurratedDocs("NutritionData");

        [Benchmark]
        public void ReadNutritionData_Text()
        {
            CuratedDocsBenchmark.ExecuteReadBenchmark(
                payload: CuratedDocsBenchmark.NutritionDataPayload,
                serializationFormat: SerializationFormat.Text);
        }

        [Benchmark]
        public void ReadNutritionData_Binary()
        {
            CuratedDocsBenchmark.ExecuteReadBenchmark(
                payload: CuratedDocsBenchmark.NutritionDataPayload,
                serializationFormat: SerializationFormat.Binary);
        }

        [Benchmark]
        public void ReadNutritionData_Newtonsoft()
        {
            CuratedDocsBenchmark.ExecuteReadBenchmark(
                payload: CuratedDocsBenchmark.NutritionDataPayload,
                serializationFormat: SerializationFormat.Newtonsoft);
        }


        [Benchmark]
        public void WriteNutritionData_Text()
        {
            CuratedDocsBenchmark.ExecuteWriteBenchmark(
                payload: CuratedDocsBenchmark.NutritionDataPayload,
                serializationFormat: SerializationFormat.Text);
        }

        [Benchmark]
        public void WriteNutritionData_Binary()
        {
            CuratedDocsBenchmark.ExecuteWriteBenchmark(
                payload: CuratedDocsBenchmark.NutritionDataPayload,
                serializationFormat: SerializationFormat.Binary);
        }

        [Benchmark]
        public void WriteNutritionData_Newtonsoft()
        {
            CuratedDocsBenchmark.ExecuteWriteBenchmark(
                payload: CuratedDocsBenchmark.NutritionDataPayload,
                serializationFormat: SerializationFormat.Newtonsoft);
        }


        private static void ExecuteReadBenchmark(
            CurratedDocsPayload payload,
            SerializationFormat serializationFormat)
        {
            IJsonReader jsonReader = serializationFormat switch
            {
                SerializationFormat.Text => JsonReader.Create(payload.Text),
                SerializationFormat.Binary => JsonReader.Create(payload.Binary),
                SerializationFormat.Newtonsoft => NewtonsoftToCosmosDBReader.CreateFromBuffer(payload.Text),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(SerializationFormat)}: '{serializationFormat}'."),
            };

            Utils.DrainReader(jsonReader, materializeValue: true);
        }

        private static void ExecuteWriteBenchmark(
            CurratedDocsPayload payload,
            SerializationFormat serializationFormat)
        {
            IJsonWriter jsonWriter = serializationFormat switch
            {
                SerializationFormat.Text => JsonWriter.Create(JsonSerializationFormat.Text),
                SerializationFormat.Binary => JsonWriter.Create(JsonSerializationFormat.Binary),
                SerializationFormat.Newtonsoft => NewtonsoftToCosmosDBWriter.CreateTextWriter(),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(SerializationFormat)}: '{serializationFormat}'."),
            };

            Utils.FlushToWriter(jsonWriter, payload.TokensToWrite);
        }

        private enum SerializationFormat
        {
            Text,
            Binary,
            Newtonsoft,
        }

        private readonly struct CurratedDocsPayload
        {
            private CurratedDocsPayload(
                ReadOnlyMemory<byte> text,
                ReadOnlyMemory<byte> binary,
                IReadOnlyList<JsonToken> tokensToWrite)
            {
                if (tokensToWrite == null)
                {
                    throw new ArgumentNullException(nameof(tokensToWrite));
                }

                this.Text = text;
                this.Binary = binary;
                this.TokensToWrite = new List<JsonToken>(tokensToWrite);
            }

            public ReadOnlyMemory<byte> Text { get; }

            public ReadOnlyMemory<byte> Binary { get; }

            public IReadOnlyList<JsonToken> TokensToWrite { get; }

            public static CurratedDocsPayload CreateFromCurratedDocs(string name)
            {
                if (name == null)
                {
                    throw new ArgumentNullException(nameof(name));
                }

                string path = $"TestJsons/{name}.json";
                string json = TextFileConcatenation.ReadMultipartFile(path);
                json = JsonTestUtils.RandomSampleJson(json, seed: 42, maxNumberOfItems: 50);

                ReadOnlyMemory<byte> text = Encoding.UTF8.GetBytes(json);
                ReadOnlyMemory<byte> binary = JsonTestUtils.ConvertTextToBinary(json);
                IReadOnlyList<JsonToken> tokensToWrite = Utils.Tokenize(text);

                return new CurratedDocsPayload(
                    text: text,
                    binary: binary,
                    tokensToWrite: tokensToWrite);
            }
        }
    }
}


namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tests.Json;

    [MemoryDiagnoser]
    public class JsonRoundTripBenchmarks
    {
        private static class Payloads
        {
            //public static readonly CurratedDocsPayload CombinedScriptsData = CurratedDocsPayload.CreateFromCurratedDocs("CombinedScriptsData");
            //public static readonly CurratedDocsPayload Countries = CurratedDocsPayload.CreateFromCurratedDocs("countries");
            //public static readonly CurratedDocsPayload Devtestcoll = CurratedDocsPayload.CreateFromCurratedDocs("devtestcoll");
            //public static readonly CurratedDocsPayload Lastfm = CurratedDocsPayload.CreateFromCurratedDocs("lastfm");
            //public static readonly CurratedDocsPayload LogData = CurratedDocsPayload.CreateFromCurratedDocs("LogData");
            //public static readonly CurratedDocsPayload MillionSong1KDocuments = CurratedDocsPayload.CreateFromCurratedDocs("MillionSong1KDocuments");
            //public static readonly CurratedDocsPayload MsnCollection = CurratedDocsPayload.CreateFromCurratedDocs("MsnCollection");
            //public static readonly CurratedDocsPayload NutritionData = CurratedDocsPayload.CreateFromCurratedDocs("NutritionData");
            //public static readonly CurratedDocsPayload RunsCollection = CurratedDocsPayload.CreateFromCurratedDocs("runsCollection");
            //public static readonly CurratedDocsPayload StatesCommittees = CurratedDocsPayload.CreateFromCurratedDocs("states_committees");
            //public static readonly CurratedDocsPayload StatesLegislators = CurratedDocsPayload.CreateFromCurratedDocs("states_legislators");
            //public static readonly CurratedDocsPayload Store01C = CurratedDocsPayload.CreateFromCurratedDocs("store01C");
            //public static readonly CurratedDocsPayload TicinoErrorBuckets = CurratedDocsPayload.CreateFromCurratedDocs("TicinoErrorBuckets");
            //public static readonly CurratedDocsPayload TwitterData = CurratedDocsPayload.CreateFromCurratedDocs("twitter_data");
            //public static readonly CurratedDocsPayload Ups1 = CurratedDocsPayload.CreateFromCurratedDocs("ups1");
            //public static readonly CurratedDocsPayload XpertEvents = CurratedDocsPayload.CreateFromCurratedDocs("XpertEvents");
        }

#if false
        [Benchmark]
        [ArgumentsSource(nameof(Arguments))]
        public void ReaderToWriter(
            CurratedDocsPayload payload,
            SerializationFormat sourceFormat,
            SerializationFormat destinationFormat)
        {
            IJsonReader reader = sourceFormat switch
            {
                SerializationFormat.Text => JsonReader.Create(payload.Text),
                SerializationFormat.Binary => JsonReader.Create(payload.Binary),
                SerializationFormat.BinaryWithDictionaryEncoding => JsonReader.Create(
                    payload.BinaryWithDictionaryEncoding.binary,
                    payload.BinaryWithDictionaryEncoding.dictionary),
                SerializationFormat.NewtonsoftText => NewtonsoftToCosmosDBReader.CreateFromBuffer(payload.Text),
                _ => throw new ArgumentException($"Unexpected {nameof(sourceFormat)} of type: '{sourceFormat}'"),
            };

            IJsonWriter writer = destinationFormat switch
            {
                SerializationFormat.Text => JsonWriter.Create(JsonSerializationFormat.Text),
                SerializationFormat.Binary => JsonWriter.Create(JsonSerializationFormat.Binary),
                SerializationFormat.BinaryWithDictionaryEncoding => JsonWriter.Create(
                    JsonSerializationFormat.Binary,
                    new JsonStringDictionary(capacity: 128)),
                SerializationFormat.NewtonsoftText => NewtonsoftToCosmosDBWriter.CreateTextWriter(),
                _ => throw new ArgumentException($"Unexpected {nameof(destinationFormat)} of type: {destinationFormat}"),
            };

            writer.WriteAll(reader);
        }
#endif

        [Benchmark]
        [ArgumentsSource(nameof(NavigatorToWriterArguments))]
        public void NavigatorToWriter(
            CurratedDocsPayload payload,
            SerializationFormat sourceFormat,
            SerializationFormat destinationFormat)
        {
            IJsonNavigator navigator = sourceFormat switch
            {
                SerializationFormat.Text => JsonNavigator.Create(payload.Text),
                SerializationFormat.BinaryWithDictionaryEncoding => JsonNavigator.Create(
                    payload.BinaryWithDictionaryEncoding.binary,
                    payload.BinaryWithDictionaryEncoding.dictionary),
                SerializationFormat.Binary => JsonNavigator.Create(payload.Binary),
                _ => throw new ArgumentException($"Unexpected {nameof(sourceFormat)} of type: '{sourceFormat}'"),
            };

            IJsonWriter writer = destinationFormat switch
            {
                SerializationFormat.Text => JsonWriter.Create(JsonSerializationFormat.Text),
                SerializationFormat.Binary => JsonWriter.Create(JsonSerializationFormat.Binary),
                SerializationFormat.BinaryWithDictionaryEncoding => JsonWriter.Create(
                    JsonSerializationFormat.Binary,
                    new JsonStringDictionary(capacity: 128)),
                SerializationFormat.NewtonsoftText => NewtonsoftToCosmosDBWriter.CreateTextWriter(),
                _ => throw new ArgumentException($"Unexpected {nameof(destinationFormat)} of type: {destinationFormat}"),
            };

            navigator.WriteTo(navigator.GetRootNode(), writer);
        }

        public enum SerializationFormat
        {
            Text,
            Binary,
            BinaryWithDictionaryEncoding,
            NewtonsoftText,
        }

        public readonly struct CurratedDocsPayload
        {
            private CurratedDocsPayload(
                string name,
                ReadOnlyMemory<byte> text,
                ReadOnlyMemory<byte> binary,
                (ReadOnlyMemory<byte> binary, JsonStringDictionary dictionary) binaryWithDictionaryEncoding)
            {
                this.Name = name;
                this.Text = text;
                this.Binary = binary;
                this.BinaryWithDictionaryEncoding = binaryWithDictionaryEncoding;
            }

            public string Name { get; }
            public ReadOnlyMemory<byte> Text { get; }
            public ReadOnlyMemory<byte> Binary { get; }
            internal (ReadOnlyMemory<byte> binary, JsonStringDictionary dictionary) BinaryWithDictionaryEncoding { get; }

            public static CurratedDocsPayload CreateFromCurratedDocs(string name)
            {
                if (name == null)
                {
                    throw new ArgumentNullException(nameof(name));
                }

                string path = $"TestJsons/{name}.json";
                string json = TextFileConcatenation.ReadMultipartFile(path);
                json = JsonTestUtils.RandomSampleJson(json, seed: 42, maxNumberOfItems: 100);

                ReadOnlyMemory<byte> text = Encoding.UTF8.GetBytes(json);
                ReadOnlyMemory<byte> binary = JsonTestUtils.ConvertTextToBinary(json);
                JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 1024);
                ReadOnlyMemory<byte> dictionaryEncodedBinary = JsonTestUtils.ConvertTextToBinary(json, jsonStringDictionary);

                return new CurratedDocsPayload(
                    name: name,
                    text: text,
                    binary: binary,
                    binaryWithDictionaryEncoding: (dictionaryEncodedBinary, jsonStringDictionary));
            }

            public override string ToString()
            {
                return this.Name;
            }
        }

        public IEnumerable<object[]> NavigatorToWriterArguments()
        {
            foreach (FieldInfo fieldInfo in typeof(Payloads).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                CurratedDocsPayload payload = (CurratedDocsPayload)fieldInfo.GetValue(null);
                foreach (SerializationFormat sourceFormat in Enum.GetValues(typeof(SerializationFormat)))
                {
                    if (sourceFormat == SerializationFormat.NewtonsoftText)
                    {
                        continue;
                    }

                    foreach (SerializationFormat destinationFormat in Enum.GetValues(typeof(SerializationFormat)))
                    {
                        yield return new object[] { payload, sourceFormat, destinationFormat };
                    }
                }
            }
        }
    }
}

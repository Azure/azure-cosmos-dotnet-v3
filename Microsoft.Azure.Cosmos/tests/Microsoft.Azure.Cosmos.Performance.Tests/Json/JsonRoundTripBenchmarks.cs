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
            //public static readonly CuratedDocsPayload CombinedScriptsData = CuratedDocsPayload.CreateFromCuratedDocs("CombinedScriptsData");
            //public static readonly CuratedDocsPayload Countries = CuratedDocsPayload.CreateFromCuratedDocs("countries");
            //public static readonly CuratedDocsPayload Devtestcoll = CuratedDocsPayload.CreateFromCuratedDocs("devtestcoll");
            //public static readonly CuratedDocsPayload Lastfm = CuratedDocsPayload.CreateFromCuratedDocs("lastfm");
            //public static readonly CuratedDocsPayload LogData = CuratedDocsPayload.CreateFromCuratedDocs("LogData");
            //public static readonly CuratedDocsPayload MillionSong1KDocuments = CuratedDocsPayload.CreateFromCuratedDocs("MillionSong1KDocuments");
            //public static readonly CuratedDocsPayload MsnCollection = CuratedDocsPayload.CreateFromCuratedDocs("MsnCollection");
            public static readonly CuratedDocsPayload NutritionData = CuratedDocsPayload.CreateFromCuratedDocs("NutritionData");
            //public static readonly CuratedDocsPayload RunsCollection = CuratedDocsPayload.CreateFromCuratedDocs("runsCollection");
            //public static readonly CuratedDocsPayload StatesCommittees = CuratedDocsPayload.CreateFromCuratedDocs("states_committees");
            //public static readonly CuratedDocsPayload StatesLegislators = CuratedDocsPayload.CreateFromCuratedDocs("states_legislators");
            //public static readonly CuratedDocsPayload Store01C = CuratedDocsPayload.CreateFromCuratedDocs("store01C");
            //public static readonly CuratedDocsPayload TicinoErrorBuckets = CuratedDocsPayload.CreateFromCuratedDocs("TicinoErrorBuckets");
            //public static readonly CuratedDocsPayload TwitterData = CuratedDocsPayload.CreateFromCuratedDocs("twitter_data");
            //public static readonly CuratedDocsPayload Ups1 = CuratedDocsPayload.CreateFromCuratedDocs("ups1");
            //public static readonly CuratedDocsPayload XpertEvents = CuratedDocsPayload.CreateFromCuratedDocs("XpertEvents");
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
            CuratedDocsPayload payload,
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
                    jsonStringDictionary: sourceFormat == SerializationFormat.BinaryWithDictionaryEncoding ? payload.BinaryWithDictionaryEncoding.dictionary : new JsonStringDictionary()),
                SerializationFormat.NewtonsoftText => NewtonsoftToCosmosDBWriter.CreateTextWriter(),
                _ => throw new ArgumentException($"Unexpected {nameof(destinationFormat)} of type: {destinationFormat}"),
            };

            navigator.WriteNode(navigator.GetRootNode(), writer);
        }

        public enum SerializationFormat
        {
            Text,
            Binary,
            NewtonsoftText,
            BinaryWithDictionaryEncoding
        }

        public readonly struct CuratedDocsPayload
        {
            private CuratedDocsPayload(
                string name,
                ReadOnlyMemory<byte> text,
                ReadOnlyMemory<byte> binary,
                (ReadOnlyMemory<byte> binary, IJsonStringDictionary dictionary) binaryWithDictionaryEncoding)
            {
                this.Name = name;
                this.Text = text;
                this.Binary = binary;
                this.BinaryWithDictionaryEncoding = binaryWithDictionaryEncoding;
            }

            public string Name { get; }
            public ReadOnlyMemory<byte> Text { get; }
            public ReadOnlyMemory<byte> Binary { get; }
            internal (ReadOnlyMemory<byte> binary, IJsonStringDictionary dictionary) BinaryWithDictionaryEncoding { get; }

            public static CuratedDocsPayload CreateFromCuratedDocs(string name)
            {
                if (name == null)
                {
                    throw new ArgumentNullException(nameof(name));
                }

                try
                {
                    string path = $"TestJsons/{name}.json";
                    string json = TextFileConcatenation.ReadMultipartFile(path);
                    json = JsonTestUtils.RandomSampleJson(json, seed: 42, maxNumberOfItems: 100);

                    ReadOnlyMemory<byte> text = Encoding.UTF8.GetBytes(json);
                    ReadOnlyMemory<byte> binary = JsonTestUtils.ConvertTextToBinary(json);
                    ReadOnlyMemory<byte> dictionaryEncodedBinary = JsonTestUtils.ConvertTextToBinary(json, out IJsonStringDictionary jsonStringDictionary);

                    return new CuratedDocsPayload(
                        name: name,
                        text: text,
                        binary: binary,
                        binaryWithDictionaryEncoding: (dictionaryEncodedBinary, jsonStringDictionary));
                }
                catch (Exception)
                {
                    // initializer can not throw exception:
                    return default;
                }
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
                CuratedDocsPayload payload = (CuratedDocsPayload)fieldInfo.GetValue(null);
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

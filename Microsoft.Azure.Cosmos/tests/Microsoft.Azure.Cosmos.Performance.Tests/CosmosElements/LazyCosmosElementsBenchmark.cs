namespace Microsoft.Azure.Cosmos.Performance.Tests.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;

    [MemoryDiagnoser]
    public class LazyCosmosElementsBenchmarks
    {
        public enum AccessPattern
        {
            Never,
            Once,
            Many
        }

        public enum SerializationFormat
        {
            Text,
            Binary
        }

        private static class Payloads
        {
            //public static readonly Payload String = Payload.Create(name: "string", "\"Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.\"");
            //public static readonly Payload Double = Payload.Create(name: "double", "1234.5678");
            //public static readonly Payload Array = Payload.Create(name: "array", "[1, 2, 3, 4, 5, 6, 7, 8, 9, 10]");
            public static readonly Payload Object = Payload.Create(name: "object", @"{
                ""id"": ""7029d079-4016-4436-b7da-36c0bae54ff6"",
                ""double"": 0.18963001816981939,
                ""int"": -1330192615,
                ""string"": ""XCPCFXPHHF"",
                ""boolean"": true,
                ""null"": null,
                ""datetime"": ""2526-07-11T18:18:16.4520716"",
                ""spatialPoint"": {
                    ""type"": ""Point"",
                    ""coordinates"": [
                        118.9897,
                        -46.6781
                    ]
                },
                ""text"": ""tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby""
            }");
        }

        [Benchmark]
        [ArgumentsSource(nameof(CreateAndNavigateCosmosElementArguments))]
        public void CreateAndNavigateCosmosElement(
            Payload payload,
            SerializationFormat jsonSerializationFormat,
            AccessPattern accessMode)
        {
            ReadOnlyMemory<byte> bytes = jsonSerializationFormat switch
            {
                SerializationFormat.Text => payload.Text,
                SerializationFormat.Binary => payload.Binary,
                _ => throw new ArgumentOutOfRangeException(nameof(jsonSerializationFormat)),
            };

            for (int i = 0; i < 1; i++)
            {
                CosmosElement element = CosmosElement.CreateFromBuffer(bytes);
                int numAccessIterations = accessMode switch
                {
                    AccessPattern.Never => 0,
                    AccessPattern.Once => 1,
                    AccessPattern.Many => 10,
                    _ => throw new ArgumentOutOfRangeException(nameof(accessMode)),
                };

                for (int accessIteration = 0; accessIteration < numAccessIterations; accessIteration++)
                {
                    Access(element);
                }
            }
        }

        private static void Access(CosmosElement element)
        {
            switch (element)
            {
                case CosmosBoolean _:
                case CosmosNull _:
                    break;

                case CosmosString cosmosString:
                    _ = cosmosString.Value;
                    break;

                case CosmosNumber cosmosNumber:
                    _ = cosmosNumber.Value;
                    break;

                case CosmosArray cosmosArray:
                    // Navigate using enumerator 
                    foreach (CosmosElement arrayItem in cosmosArray)
                    {
                        Access(arrayItem);
                    }

                    // Navigate using indexer
                    for (int i = 0; i < cosmosArray.Count; i++)
                    {
                        Access(cosmosArray[i]);
                    }

                    // Count should also be cached
                    for (int i = 0; i < 10; i++)
                    {
                        _ = cosmosArray.Count;
                    }

                    break;

                case CosmosObject cosmosObject:
                    // Navigate using enumerator
                    foreach (KeyValuePair<string, CosmosElement> kvp in cosmosObject)
                    {
                        Access(kvp.Value);
                    }

                    // Navigate using indexer
                    foreach (string key in cosmosObject.Keys)
                    {
                        Access(cosmosObject[key]);

                        // Contains key should be cached
                        _ = cosmosObject.ContainsKey(key);
                    }

                    // Navigates using values
                    foreach (CosmosElement value in cosmosObject.Values)
                    {
                        Access(value);
                    }

                    for (int i = 0; i < 10; i++)
                    {
                        // Keys should be cached
                        _ = cosmosObject.Keys;

                        // Values should be cached
                        _ = cosmosObject.Values;

                        // Count should also be cached
                        _ = cosmosObject.Count;
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(element));
            }
        }

        public IEnumerable<object[]> CreateAndNavigateCosmosElementArguments()
        {
            foreach (FieldInfo fieldInfo in typeof(Payloads).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Payload payload = (Payload)fieldInfo.GetValue(null);
                //foreach (SerializationFormat serializationFormat in Enum.GetValues(typeof(SerializationFormat)))
                //{
                //    foreach (AccessPattern accessPattern in Enum.GetValues(typeof(AccessPattern)))
                //    {
                //        yield return new object[] { payload, serializationFormat, accessPattern };
                //    }
                //}

                foreach (AccessPattern accessPattern in Enum.GetValues(typeof(AccessPattern)))
                {
                    yield return new object[] { payload, SerializationFormat.Binary, accessPattern };
                }
            }
        }

        public readonly struct Payload
        {
            private Payload(
                string name,
                ReadOnlyMemory<byte> text,
                ReadOnlyMemory<byte> binary)
            {
                this.Name = name;
                this.Text = text;
                this.Binary = binary;
            }

            public string Name { get; }
            public ReadOnlyMemory<byte> Text { get; }
            public ReadOnlyMemory<byte> Binary { get; }

            public static Payload Create(string name, string jsonText)
            {
                if (jsonText == null)
                {
                    throw new ArgumentNullException(nameof(jsonText));
                }

                CosmosElement element = CosmosElement.Parse(jsonText);
                IJsonWriter jsonBinaryWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
                IJsonWriter jsonTextWriter = JsonWriter.Create(JsonSerializationFormat.Text);

                element.WriteTo(jsonTextWriter);
                element.WriteTo(jsonBinaryWriter);

                ReadOnlyMemory<byte> text = jsonTextWriter.GetResult();
                ReadOnlyMemory<byte> binary = jsonBinaryWriter.GetResult();

                return new Payload(name, text, binary);
            }

            public override string ToString()
            {
                return this.Name;
            }
        }
    }
}

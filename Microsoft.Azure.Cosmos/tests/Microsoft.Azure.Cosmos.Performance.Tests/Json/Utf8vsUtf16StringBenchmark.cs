//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: Utf8vsUtf16StringBenchmarkGenerator.tt: 16

namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System;
    using System.Text;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Json;

    [MemoryDiagnoser]
    public class Utf8vsUtf16StringBenchmark
    {
        private static readonly Payload StringLength8Payload =  Payload.CreateStringLength(length: 8);
        private static readonly Payload StringLength32Payload =  Payload.CreateStringLength(length: 32);
        private static readonly Payload StringLength256Payload =  Payload.CreateStringLength(length: 256);
        private static readonly Payload StringLength1024Payload =  Payload.CreateStringLength(length: 1024);
        private static readonly Payload StringLength4096Payload =  Payload.CreateStringLength(length: 4096);

        [Benchmark]
        public void ReadUtf16StringLength8()
        {
            Utf8vsUtf16StringBenchmark.RunReadBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength8Payload,
                useUtf8: false);
        }

        [Benchmark]
        public void ReadUtf8StringLength8()
        {
            Utf8vsUtf16StringBenchmark.RunReadBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength8Payload,
                useUtf8: true);
        }

        [Benchmark]
        public void ReadUtf16StringLength32()
        {
            Utf8vsUtf16StringBenchmark.RunReadBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength32Payload,
                useUtf8: false);
        }

        [Benchmark]
        public void ReadUtf8StringLength32()
        {
            Utf8vsUtf16StringBenchmark.RunReadBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength32Payload,
                useUtf8: true);
        }

        [Benchmark]
        public void ReadUtf16StringLength256()
        {
            Utf8vsUtf16StringBenchmark.RunReadBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength256Payload,
                useUtf8: false);
        }

        [Benchmark]
        public void ReadUtf8StringLength256()
        {
            Utf8vsUtf16StringBenchmark.RunReadBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength256Payload,
                useUtf8: true);
        }

        [Benchmark]
        public void ReadUtf16StringLength1024()
        {
            Utf8vsUtf16StringBenchmark.RunReadBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength1024Payload,
                useUtf8: false);
        }

        [Benchmark]
        public void ReadUtf8StringLength1024()
        {
            Utf8vsUtf16StringBenchmark.RunReadBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength1024Payload,
                useUtf8: true);
        }

        [Benchmark]
        public void ReadUtf16StringLength4096()
        {
            Utf8vsUtf16StringBenchmark.RunReadBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength4096Payload,
                useUtf8: false);
        }

        [Benchmark]
        public void ReadUtf8StringLength4096()
        {
            Utf8vsUtf16StringBenchmark.RunReadBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength4096Payload,
                useUtf8: true);
        }


        [Benchmark]
        public void WriteUtf16StringLength8()
        {
            Utf8vsUtf16StringBenchmark.RunWriteBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength8Payload,
                useUtf8: false);
        }

        [Benchmark]
        public void WriteUtf8StringLength8()
        {
            Utf8vsUtf16StringBenchmark.RunWriteBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength8Payload,
                useUtf8: true);
        }

        [Benchmark]
        public void WriteUtf16StringLength32()
        {
            Utf8vsUtf16StringBenchmark.RunWriteBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength32Payload,
                useUtf8: false);
        }

        [Benchmark]
        public void WriteUtf8StringLength32()
        {
            Utf8vsUtf16StringBenchmark.RunWriteBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength32Payload,
                useUtf8: true);
        }

        [Benchmark]
        public void WriteUtf16StringLength256()
        {
            Utf8vsUtf16StringBenchmark.RunWriteBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength256Payload,
                useUtf8: false);
        }

        [Benchmark]
        public void WriteUtf8StringLength256()
        {
            Utf8vsUtf16StringBenchmark.RunWriteBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength256Payload,
                useUtf8: true);
        }

        [Benchmark]
        public void WriteUtf16StringLength1024()
        {
            Utf8vsUtf16StringBenchmark.RunWriteBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength1024Payload,
                useUtf8: false);
        }

        [Benchmark]
        public void WriteUtf8StringLength1024()
        {
            Utf8vsUtf16StringBenchmark.RunWriteBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength1024Payload,
                useUtf8: true);
        }

        [Benchmark]
        public void WriteUtf16StringLength4096()
        {
            Utf8vsUtf16StringBenchmark.RunWriteBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength4096Payload,
                useUtf8: false);
        }

        [Benchmark]
        public void WriteUtf8StringLength4096()
        {
            Utf8vsUtf16StringBenchmark.RunWriteBenchmark(
                payload: Utf8vsUtf16StringBenchmark.StringLength4096Payload,
                useUtf8: true);
        }


        private static void RunReadBenchmark(
            Payload payload,
            bool useUtf8)
        {
            // Don't really need to test both serialization formats, since they are similiar.
            IJsonReader jsonReader = JsonReader.Create(payload.Binary);

            while (jsonReader.Read())
            {
                // Materialize the value
                switch (jsonReader.CurrentTokenType)
                {
                    case JsonTokenType.BeginArray:
                    case JsonTokenType.EndArray:
                        // Single byte tokens
                        break;

                    case JsonTokenType.String:
                        if (useUtf8)
                        {
                            if (!jsonReader.TryGetBufferedUtf8StringValue(out ReadOnlyMemory<byte> bufferedUtf8StringValue))
                            {
                                throw new InvalidOperationException("Failed to get utf8 string.");
                            }
                        }
                        else
                        {
                            string value = jsonReader.GetStringValue();
                        }
                        break;

                    default:
                        throw new ArgumentException("$Unknown token type.");
                }
            }
        }

        private static void RunWriteBenchmark(
            Payload payload,
            bool useUtf8)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);

            jsonWriter.WriteArrayStart();

            for (int i = 0; i < 100000; i++)
            {
                if (useUtf8)
                {
                    jsonWriter.WriteStringValue(payload.Utf8StringToken.Span);
                }
                else
                {
                    jsonWriter.WriteStringValue(payload.Utf16StringToken);
                }
            }

            jsonWriter.WriteArrayEnd();
        }

        private readonly struct Payload
        {
            private Payload(
                ReadOnlyMemory<byte> text,
                ReadOnlyMemory<byte> binary,
                ReadOnlyMemory<byte> utf8StringToken,
                string utf16StringToken)
            {
                this.Text = text;
                this.Binary = binary;
                this.Utf8StringToken = utf8StringToken;
                this.Utf16StringToken = utf16StringToken;
            }

            public ReadOnlyMemory<byte> Text { get; }

            public ReadOnlyMemory<byte> Binary { get; }

            public ReadOnlyMemory<byte> Utf8StringToken { get; }

            public string Utf16StringToken { get; }

            public static Payload CreateStringLength(int length)
            {
                string stringValue = new string('a', length);
                IJsonWriter jsonTextWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                IJsonWriter jsonBinaryWriter = JsonWriter.Create(JsonSerializationFormat.Binary);

                jsonTextWriter.WriteArrayStart();
                jsonBinaryWriter.WriteArrayStart();

                for (int i = 0; i < 100000; i++)
                {
                    jsonTextWriter.WriteStringValue(stringValue);
                }

                jsonTextWriter.WriteArrayEnd();
                jsonBinaryWriter.WriteArrayEnd();

                ReadOnlyMemory<byte> text = jsonTextWriter.GetResult();
                ReadOnlyMemory<byte> binary = jsonTextWriter.GetResult();

                return new Payload(
                    text: text, 
                    binary: binary,
                    utf8StringToken: Encoding.UTF8.GetBytes(stringValue),
                    utf16StringToken: stringValue);
            }
        }
    }
}

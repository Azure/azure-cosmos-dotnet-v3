//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json.Interop;

    public abstract class JsonMicroBenchmarksBase
    {
        protected static class WriteDelegates
        {
            internal static readonly Action<IJsonWriter> Null = (jsonWriter) => { jsonWriter.WriteNullValue(); };
            internal static readonly Action<IJsonWriter> True = (jsonWriter) => { jsonWriter.WriteBoolValue(true); };
            internal static readonly Action<IJsonWriter> False = (jsonWriter) => { jsonWriter.WriteBoolValue(false); };
            internal static readonly Action<IJsonWriter> Number64Integer = (jsonWriter) => { jsonWriter.WriteNumberValue(123); };
            internal static readonly Action<IJsonWriter> Number64Double = (jsonWriter) => { jsonWriter.WriteNumberValue(6.0221409e+23); };
            internal static readonly Action<IJsonWriter> String = (jsonWriter) => { jsonWriter.WriteStringValue("Hello World"); };
            internal static readonly Action<IJsonWriter> Array = (jsonWriter) => { jsonWriter.WriteArrayStart(); jsonWriter.WriteArrayEnd(); };
            internal static readonly Action<IJsonWriter> Object = (jsonWriter) => { jsonWriter.WriteObjectStart(); jsonWriter.WriteObjectEnd(); };
        }

        protected static class NamedWriteDelegates
        {
            internal static readonly NamedWriteDelegate Null = new NamedWriteDelegate("null", WriteDelegates.Null);
            internal static readonly NamedWriteDelegate True = new NamedWriteDelegate("true", WriteDelegates.True);
            internal static readonly NamedWriteDelegate False = new NamedWriteDelegate("false", WriteDelegates.False);
            internal static readonly NamedWriteDelegate Number64Integer = new NamedWriteDelegate("integer", WriteDelegates.Number64Integer);
            internal static readonly NamedWriteDelegate Number64Double = new NamedWriteDelegate("double", WriteDelegates.Number64Double);
            internal static readonly NamedWriteDelegate String = new NamedWriteDelegate("string", WriteDelegates.String);
            internal static readonly NamedWriteDelegate Array = new NamedWriteDelegate("array", WriteDelegates.Array);
            internal static readonly NamedWriteDelegate Object = new NamedWriteDelegate("object", WriteDelegates.Object);
        }

        protected static class Payloads
        {
            public static readonly BenchmarkPayload Null = new BenchmarkPayload("null", WriteDelegates.Null);
            public static readonly BenchmarkPayload True = new BenchmarkPayload("true", WriteDelegates.True);
            public static readonly BenchmarkPayload False = new BenchmarkPayload("false", WriteDelegates.False);
            public static readonly BenchmarkPayload Number64Integer = new BenchmarkPayload("integer", WriteDelegates.Number64Integer);
            public static readonly BenchmarkPayload Number64Double = new BenchmarkPayload("double", WriteDelegates.Number64Double);
            public static readonly BenchmarkPayload String = new BenchmarkPayload("string", WriteDelegates.String);
            public static readonly BenchmarkPayload Array = new BenchmarkPayload("array", WriteDelegates.Array);
            public static readonly BenchmarkPayload Object = new BenchmarkPayload("object", WriteDelegates.Object);
        }

        public enum BenchmarkSerializationFormat
        {
            Text,
            Binary,
            Newtonsoft,
        }

        public readonly struct BenchmarkPayload
        {
            private readonly string description;

            internal BenchmarkPayload(string description, Action<IJsonWriter> writeToken)
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
                this.description = description;
            }

            public ReadOnlyMemory<byte> Text { get; }
            public ReadOnlyMemory<byte> Binary { get; }
            public ReadOnlyMemory<byte> Newtonsoft { get; }

            public override string ToString()
            {
                return this.description;
            }
        }

        public readonly struct NamedWriteDelegate
        {
            internal NamedWriteDelegate(string name, Action<IJsonWriter> writeDelegate)
            {
                this.Name = name ?? throw new ArgumentNullException(nameof(name));
                this.WriteDelegate = writeDelegate ?? throw new ArgumentNullException(nameof(writeDelegate));
            }

            internal string Name { get; }
            internal Action<IJsonWriter> WriteDelegate { get; }

            public override string ToString()
            {
                return this.Name;
            }
        }
    }
}

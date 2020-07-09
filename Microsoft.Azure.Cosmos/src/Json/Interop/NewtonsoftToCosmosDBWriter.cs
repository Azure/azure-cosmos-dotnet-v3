// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json.Interop
{
    using System;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Newtonsoft.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class NewtonsoftToCosmosDBWriter : Microsoft.Azure.Cosmos.Json.JsonWriter
    {
        private readonly Newtonsoft.Json.JsonWriter writer;
        private readonly Func<byte[]> getResultCallback;

        private NewtonsoftToCosmosDBWriter(
            Newtonsoft.Json.JsonWriter writer,
            Func<byte[]> getResultCallback)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.getResultCallback = getResultCallback ?? throw new ArgumentNullException(nameof(getResultCallback));
        }

        public override long CurrentLength
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Text;

        public override ReadOnlyMemory<byte> GetResult()
        {
            return this.getResultCallback();
        }

        public override void WriteArrayEnd()
        {
            this.writer.WriteEndArray();
        }

        public override void WriteArrayStart()
        {
            this.writer.WriteStartArray();
        }

        public override void WriteBinaryValue(ReadOnlySpan<byte> value)
        {
            throw new NotImplementedException();
        }

        public override void WriteBoolValue(bool value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteFieldName(string fieldName)
        {
            this.writer.WritePropertyName(fieldName);
        }

        public override void WriteFloat32Value(float value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteFloat64Value(double value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteGuidValue(Guid value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteInt16Value(short value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteInt32Value(int value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteInt64Value(long value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteInt8Value(sbyte value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteNullValue()
        {
            this.writer.WriteNull();
        }

        public override void WriteNumber64Value(Number64 value)
        {
            if (value.IsInteger)
            {
                this.writer.WriteValue(Number64.ToLong(value));
            }
            else
            {
                this.writer.WriteValue(Number64.ToDouble(value));
            }
        }

        public override void WriteObjectEnd()
        {
            this.writer.WriteEndObject();
        }

        public override void WriteObjectStart()
        {
            this.writer.WriteStartObject();
        }

        public override void WriteStringValue(string value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteUInt32Value(uint value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteRawJsonToken(
            JsonTokenType jsonTokenType,
            ReadOnlySpan<byte> rawJsonToken)
        {
            string rawJson = Encoding.UTF8.GetString(rawJsonToken);
            Newtonsoft.Json.JsonTextReader jsonTextReader = new Newtonsoft.Json.JsonTextReader(new StringReader(rawJson))
            {
                DateParseHandling = DateParseHandling.None,
            };

            while (jsonTextReader.Read())
            {
                if (jsonTokenType == JsonTokenType.FieldName)
                {
                    this.writer.WritePropertyName(jsonTextReader.Value as string);
                }
                else
                {
                    switch (jsonTextReader.TokenType)
                    {
                        case Newtonsoft.Json.JsonToken.StartObject:
                            this.writer.WriteStartObject();
                            break;

                        case Newtonsoft.Json.JsonToken.StartArray:
                            this.writer.WriteStartArray();
                            break;

                        case Newtonsoft.Json.JsonToken.PropertyName:
                            this.writer.WritePropertyName(jsonTextReader.Value as string);
                            break;

                        case Newtonsoft.Json.JsonToken.Integer:
                        case Newtonsoft.Json.JsonToken.Float:
                        case Newtonsoft.Json.JsonToken.String:
                        case Newtonsoft.Json.JsonToken.Boolean:
                            this.writer.WriteValue(jsonTextReader.Value);
                            break;

                        case Newtonsoft.Json.JsonToken.Null:
                            this.writer.WriteNull();
                            break;

                        case Newtonsoft.Json.JsonToken.EndObject:
                            this.writer.WriteEndObject();
                            break;

                        case Newtonsoft.Json.JsonToken.EndArray:
                            this.writer.WriteEndArray();
                            break;

                        default:
                            throw new ArgumentOutOfRangeException($"Unknown {nameof(JsonToken)}: {jsonTextReader.TokenType}.");
                    }
                }
                
            }
        }

        public static NewtonsoftToCosmosDBWriter CreateTextWriter()
        {
            StringWriter stringWriter = new StringWriter();
            Newtonsoft.Json.JsonTextWriter newtonsoftJsonWriter = new Newtonsoft.Json.JsonTextWriter(stringWriter);
            NewtonsoftToCosmosDBWriter newtonsoftToCosmosDBWriter = new NewtonsoftToCosmosDBWriter(
                newtonsoftJsonWriter,
                () => Encoding.UTF8.GetBytes(stringWriter.ToString()));
            return newtonsoftToCosmosDBWriter;
        }

        public static NewtonsoftToCosmosDBWriter CreateFromWriter(Newtonsoft.Json.JsonWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            return new NewtonsoftToCosmosDBWriter(writer, () => throw new NotSupportedException());
        }

        public override void WriteFieldName(Utf8Span utf8FieldName)
        {
            this.WriteFieldName(Encoding.UTF8.GetString(utf8FieldName.Span));
        }

        public override void WriteStringValue(Utf8Span utf8StringValue)
        {
            this.WriteStringValue(Encoding.UTF8.GetString(utf8StringValue.Span));
        }
    }
}

// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json.Interop
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.Json;

    /// <summary>
    /// IJsonReader adapter for a Newtonsoft reader meaning we get a newtonsoft reader for testing purposes.
    /// </summary>
    internal sealed class NewtonsoftToCosmosDBReader : Microsoft.Azure.Cosmos.Json.JsonReader
    {
        private readonly Newtonsoft.Json.JsonReader reader;

        private NewtonsoftToCosmosDBReader(Newtonsoft.Json.JsonReader reader)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Text;

        public override ReadOnlyMemory<byte> GetBinaryValue()
        {
            throw new NotImplementedException();
        }

        public override float GetFloat32Value()
        {
            return (float)this.reader.Value;
        }

        public override double GetFloat64Value()
        {
            return (double)this.reader.Value;
        }

        public override Guid GetGuidValue()
        {
            return (Guid)this.reader.Value;
        }

        public override short GetInt16Value()
        {
            return (short)this.reader.Value;
        }

        public override int GetInt32Value()
        {
            return (int)this.reader.Value;
        }

        public override long GetInt64Value()
        {
            return (long)this.reader.Value;
        }

        public override sbyte GetInt8Value()
        {
            return (sbyte)this.reader.Value;
        }

        public override Number64 GetNumberValue()
        {
            string numberString;
            object value = this.reader.Value;
            if (value is double doubleValue)
            {
                numberString = ((double)value).ToString("R");
            }
            else
            {
                numberString = value.ToString();
            }

            return double.Parse(numberString);
        }

        public override UtfAnyString GetStringValue()
        {
            return this.reader.Value.ToString();
        }

        public override uint GetUInt32Value()
        {
            return (uint)this.reader.Value;
        }

        public override bool Read()
        {
            bool succesfullyRead = this.reader.Read();
            if (succesfullyRead)
            {
                switch (this.reader.TokenType)
                {
                    case Newtonsoft.Json.JsonToken.StartObject:
                        this.JsonObjectState.RegisterToken(JsonTokenType.BeginObject);
                        break;

                    case Newtonsoft.Json.JsonToken.StartArray:
                        this.JsonObjectState.RegisterToken(JsonTokenType.BeginArray);
                        break;

                    case Newtonsoft.Json.JsonToken.PropertyName:
                        this.JsonObjectState.RegisterToken(JsonTokenType.FieldName);
                        break;

                    case Newtonsoft.Json.JsonToken.String:
                        this.JsonObjectState.RegisterToken(JsonTokenType.String);
                        break;

                    case Newtonsoft.Json.JsonToken.Integer:
                    case Newtonsoft.Json.JsonToken.Float:
                        this.JsonObjectState.RegisterToken(JsonTokenType.Number);
                        break;

                    case Newtonsoft.Json.JsonToken.Boolean:
                        this.JsonObjectState.RegisterToken(this.reader.Value is true ? JsonTokenType.True : JsonTokenType.False);
                        break;

                    case Newtonsoft.Json.JsonToken.Null:
                        this.JsonObjectState.RegisterToken(JsonTokenType.Null);
                        break;

                    case Newtonsoft.Json.JsonToken.EndObject:
                        this.JsonObjectState.RegisterToken(JsonTokenType.EndObject);
                        break;

                    case Newtonsoft.Json.JsonToken.EndArray:
                        this.JsonObjectState.RegisterToken(JsonTokenType.EndArray);
                        break;

                    default:
                        throw new ArgumentException("Got an invalid newtonsoft type");
                }
            }

            return succesfullyRead;
        }

        public static NewtonsoftToCosmosDBReader CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            MemoryStream stream;
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
            {
                stream = new MemoryStream(segment.Array, segment.Offset, segment.Count);
            }
            else
            {
                stream = new MemoryStream(buffer.ToArray());
            }

            StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
            Newtonsoft.Json.JsonTextReader newtonsoftReader = new Newtonsoft.Json.JsonTextReader(streamReader)
            {
                DateParseHandling = Newtonsoft.Json.DateParseHandling.None
            };

            return NewtonsoftToCosmosDBReader.CreateFromReader(newtonsoftReader);
        }

        public static NewtonsoftToCosmosDBReader CreateFromString(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            StringReader stringReader = new StringReader(json);
            Newtonsoft.Json.JsonTextReader newtonsoftReader = new Newtonsoft.Json.JsonTextReader(stringReader)
            {
                DateParseHandling = Newtonsoft.Json.DateParseHandling.None
            };

            return NewtonsoftToCosmosDBReader.CreateFromReader(newtonsoftReader);
        }

        public static NewtonsoftToCosmosDBReader CreateFromReader(Newtonsoft.Json.JsonReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            return new NewtonsoftToCosmosDBReader(reader);
        }

        public override bool TryGetBufferedStringValue(out Utf8Memory bufferedUtf8StringValue)
        {
            bufferedUtf8StringValue = default;
            return false;
        }

        /// <inheritdoc />
        protected override bool TryGetUInt64NumberValue(out ulong value)
        {
            value = 0;
            return false;
        }
    }
}

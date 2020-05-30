//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.Query.Core;

    /// <summary>
    /// Partial JsonReader with a private JsonBinaryReader implementation
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract partial class JsonReader : IJsonReader
    {
        /// <summary>
        /// JsonReader that can read from a json serialized in binary <see cref="JsonBinaryEncoding"/>.
        /// </summary>
        private sealed class JsonBinaryReader : JsonReader
        {
            /// <summary>
            /// Buffer to read from.
            /// </summary>
            private readonly JsonBinaryMemoryReader jsonBinaryBuffer;

            /// <summary>
            /// Dictionary used for user string encoding.
            /// </summary>
            private readonly JsonStringDictionary jsonStringDictionary;

            /// <summary>
            /// For binary there is no end of token marker in the actual binary, but the JsonReader interface still needs to surface ObjectEndToken and ArrayEndToken.
            /// To accommodate for this we have a progress stack to let us know how many bytes there are left to read for all levels of nesting. 
            /// With this information we know that we are at the end of a context and can now surface an end object / array token.
            /// </summary>
            private readonly Stack<int> arrayAndObjectEndStack;

            private int currentTokenPosition;

            public JsonBinaryReader(
                ReadOnlyMemory<byte> buffer,
                JsonStringDictionary jsonStringDictionary = null)
            {
                if (buffer.Length < 2)
                {
                    throw new ArgumentException($"{nameof(buffer)} must have at least two byte.");
                }

                if (buffer.Span[0] != (byte)JsonSerializationFormat.Binary)
                {
                    throw new ArgumentNullException("buffer must be binary encoded.");
                }

                // offset for the 0x80 (128) binary serialization type marker.
                buffer = buffer.Slice(1);

                // Only navigate the outer most json value and trim off trailing bytes
                int jsonValueLength = JsonBinaryEncoding.GetValueLength(buffer.Span);
                if (buffer.Length < jsonValueLength)
                {
                    throw new ArgumentException("buffer is shorter than the length prefix.");
                }

                buffer = buffer.Slice(0, jsonValueLength);

                this.jsonBinaryBuffer = new JsonBinaryMemoryReader(buffer);
                this.arrayAndObjectEndStack = new Stack<int>();
                this.jsonStringDictionary = jsonStringDictionary;
            }

            /// <inheritdoc />
            public override JsonSerializationFormat SerializationFormat
            {
                get
                {
                    return JsonSerializationFormat.Binary;
                }
            }

            /// <inheritdoc />
            public override bool Read()
            {
                JsonTokenType jsonTokenType;
                int nextTokenOffset;
                // First check if we just finished an array or object context
                if ((this.arrayAndObjectEndStack.Count != 0) && (this.arrayAndObjectEndStack.Peek() == this.jsonBinaryBuffer.Position))
                {
                    if (this.JsonObjectState.InArrayContext)
                    {
                        jsonTokenType = JsonTokenType.EndArray;
                    }
                    else if (this.JsonObjectState.InObjectContext)
                    {
                        jsonTokenType = JsonTokenType.EndObject;
                    }
                    else
                    {
                        throw new JsonInvalidTokenException();
                    }

                    nextTokenOffset = 0;
                    this.arrayAndObjectEndStack.Pop();
                }
                else
                {
                    // We are not at the end of a context.
                    if (this.jsonBinaryBuffer.IsEof)
                    {
                        // Need to check if we are still inside of an object or array
                        if (this.JsonObjectState.CurrentDepth != 0)
                        {
                            if (this.JsonObjectState.InObjectContext)
                            {
                                throw new JsonMissingEndObjectException();
                            }
                            else if (this.JsonObjectState.InArrayContext)
                            {
                                throw new JsonMissingEndArrayException();
                            }
                            else
                            {
                                throw new InvalidOperationException("Expected to be in either array or object context");
                            }
                        }

                        return false;
                    }

                    if ((this.JsonObjectState.CurrentDepth == 0) && (this.CurrentTokenType != JsonTokenType.NotStarted))
                    {
                        // There are trailing characters outside of the outter most object or array
                        throw new JsonUnexpectedTokenException();
                    }

                    jsonTokenType = JsonBinaryReader.GetJsonTokenType(this.jsonBinaryBuffer.Peek());
                    if ((jsonTokenType == JsonTokenType.String) && this.JsonObjectState.IsPropertyExpected)
                    {
                        jsonTokenType = JsonTokenType.FieldName;
                    }

                    // If this is begin array/object token then we need to identify where array/object end token is.
                    // Also the next token offset is just the array type marker + length prefix + count prefix
                    if ((jsonTokenType == JsonTokenType.BeginArray) || (jsonTokenType == JsonTokenType.BeginObject))
                    {
                        if (!JsonBinaryEncoding.TryGetValueLength(
                            this.jsonBinaryBuffer.GetBufferedRawJsonToken().Span,
                            out int arrayOrObjectLength))
                        {
                            throw new JsonUnexpectedTokenException();
                        }

                        this.arrayAndObjectEndStack.Push(this.jsonBinaryBuffer.Position + arrayOrObjectLength);
                        nextTokenOffset = JsonBinaryReader.GetArrayOrObjectPrefixLength(this.jsonBinaryBuffer.Peek());
                    }
                    else
                    {
                        if (!JsonBinaryEncoding.TryGetValueLength(
                            this.jsonBinaryBuffer.GetBufferedRawJsonToken().Span,
                            out nextTokenOffset))
                        {
                            throw new JsonUnexpectedTokenException();
                        }
                    }
                }

                this.JsonObjectState.RegisterToken(jsonTokenType);
                this.currentTokenPosition = this.jsonBinaryBuffer.Position;
                this.jsonBinaryBuffer.SkipBytes(nextTokenOffset);
                return true;
            }

            /// <inheritdoc />
            public override Number64 GetNumberValue()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Number)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetNumberValue(
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition).Span);
            }

            /// <inheritdoc />
            public override string GetStringValue()
            {
                if (!(
                    (this.JsonObjectState.CurrentTokenType == JsonTokenType.String) ||
                    (this.JsonObjectState.CurrentTokenType == JsonTokenType.FieldName)))
                {
                    throw new JsonInvalidTokenException();
                }

                return JsonBinaryEncoding.GetStringValue(
                    Utf8Memory.UnsafeCreateNoValidation(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition)),
                    this.jsonStringDictionary);
            }

            /// <inheritdoc />
            public override bool TryGetBufferedStringValue(out Utf8Memory bufferedUtf8StringValue)
            {
                if (!(
                    (this.JsonObjectState.CurrentTokenType == JsonTokenType.String) ||
                    (this.JsonObjectState.CurrentTokenType == JsonTokenType.FieldName)))
                {
                    throw new JsonInvalidTokenException();
                }

                return JsonBinaryEncoding.TryGetBufferedStringValue(
                    Utf8Memory.UnsafeCreateNoValidation(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition)),
                    this.jsonStringDictionary,
                    out bufferedUtf8StringValue);
            }

            /// <inheritdoc />
            public override bool TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken)
            {
                if (!JsonBinaryEncoding.TryGetValueLength(
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition).Span,
                    out int length))
                {
                    throw new InvalidOperationException();
                }

                ReadOnlyMemory<byte> candidate = this.jsonBinaryBuffer.GetBufferedRawJsonToken(
                    this.currentTokenPosition,
                    this.currentTokenPosition + length);

                if ((this.jsonStringDictionary != null) && JsonBinaryReader.IsStringOrNested(this.CurrentTokenType))
                {
                    // If there is dictionary encoding, then we need to force a rewrite.
                    bufferedRawJsonToken = default;
                    return false;
                }

                bufferedRawJsonToken = candidate;
                return true;
            }

            private static bool IsStringOrNested(JsonTokenType type)
            {
                switch (type)
                {
                    case JsonTokenType.BeginArray:
                    case JsonTokenType.BeginObject:
                    case JsonTokenType.String:
                    case JsonTokenType.FieldName:
                        return true;
                    default:
                        return false;
                }
            }

            /// <inheritdoc />
            public override sbyte GetInt8Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Int8)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetInt8Value(
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition).Span);
            }

            /// <inheritdoc />
            public override short GetInt16Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Int16)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetInt16Value(
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition).Span);
            }

            /// <inheritdoc />
            public override int GetInt32Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Int32)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetInt32Value(
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition).Span);
            }

            /// <inheritdoc />
            public override long GetInt64Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Int64)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetInt64Value(
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition).Span);
            }

            /// <inheritdoc />
            public override uint GetUInt32Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.UInt32)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetUInt32Value(
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition).Span);
            }

            /// <inheritdoc />
            public override float GetFloat32Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Float32)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetFloat32Value(
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition).Span);
            }

            /// <inheritdoc />
            public override double GetFloat64Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Float64)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetFloat64Value(
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition).Span);
            }

            /// <inheritdoc />
            public override Guid GetGuidValue()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Guid)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetGuidValue(
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition).Span);
            }

            /// <inheritdoc />
            public override ReadOnlyMemory<byte> GetBinaryValue()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Binary)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetBinaryValue(this.jsonBinaryBuffer.GetRawMemoryJsonToken(this.currentTokenPosition));
            }

            private static JsonTokenType GetJsonTokenType(byte typeMarker)
            {
                JsonTokenType jsonTokenType;
                if (JsonBinaryEncoding.TypeMarker.IsEncodedNumberLiteral(typeMarker))
                {
                    jsonTokenType = JsonTokenType.Number;
                }
                else if (JsonBinaryEncoding.TypeMarker.IsOneByteEncodedString(typeMarker))
                {
                    jsonTokenType = JsonTokenType.String;
                }
                else if (JsonBinaryEncoding.TypeMarker.IsTwoByteEncodedString(typeMarker))
                {
                    jsonTokenType = JsonTokenType.String;
                }
                else if (JsonBinaryEncoding.TypeMarker.IsEncodedLengthString(typeMarker))
                {
                    jsonTokenType = JsonTokenType.String;
                }
                else
                {
                    switch (typeMarker)
                    {
                        // Single-byte values
                        case JsonBinaryEncoding.TypeMarker.Null:
                            jsonTokenType = JsonTokenType.Null;
                            break;

                        case JsonBinaryEncoding.TypeMarker.False:
                            jsonTokenType = JsonTokenType.False;
                            break;

                        case JsonBinaryEncoding.TypeMarker.True:
                            jsonTokenType = JsonTokenType.True;
                            break;

                        // Number values
                        case JsonBinaryEncoding.TypeMarker.NumberUInt8:
                        case JsonBinaryEncoding.TypeMarker.NumberInt16:
                        case JsonBinaryEncoding.TypeMarker.NumberInt32:
                        case JsonBinaryEncoding.TypeMarker.NumberInt64:
                        case JsonBinaryEncoding.TypeMarker.NumberDouble:
                            jsonTokenType = JsonTokenType.Number;
                            break;

                        // Extended Type System
                        case JsonBinaryEncoding.TypeMarker.Int8:
                            jsonTokenType = JsonTokenType.Int8;
                            break;

                        case JsonBinaryEncoding.TypeMarker.Int16:
                            jsonTokenType = JsonTokenType.Int16;
                            break;

                        case JsonBinaryEncoding.TypeMarker.Int32:
                            jsonTokenType = JsonTokenType.Int32;
                            break;

                        case JsonBinaryEncoding.TypeMarker.Int64:
                            jsonTokenType = JsonTokenType.Int64;
                            break;

                        case JsonBinaryEncoding.TypeMarker.UInt32:
                            jsonTokenType = JsonTokenType.UInt32;
                            break;

                        case JsonBinaryEncoding.TypeMarker.Float32:
                            jsonTokenType = JsonTokenType.Float32;
                            break;

                        case JsonBinaryEncoding.TypeMarker.Float64:
                            jsonTokenType = JsonTokenType.Float64;
                            break;

                        case JsonBinaryEncoding.TypeMarker.Guid:
                            jsonTokenType = JsonTokenType.Guid;
                            break;

                        case JsonBinaryEncoding.TypeMarker.Binary1ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Binary2ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Binary4ByteLength:
                            jsonTokenType = JsonTokenType.Binary;
                            break;

                        // Variable Length String Values
                        case JsonBinaryEncoding.TypeMarker.String1ByteLength:
                        case JsonBinaryEncoding.TypeMarker.String2ByteLength:
                        case JsonBinaryEncoding.TypeMarker.String4ByteLength:
                            jsonTokenType = JsonTokenType.String;
                            break;

                        // Array Values
                        case JsonBinaryEncoding.TypeMarker.EmptyArray:
                        case JsonBinaryEncoding.TypeMarker.SingleItemArray:
                        case JsonBinaryEncoding.TypeMarker.Array1ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Array2ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Array4ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Array1ByteLengthAndCount:
                        case JsonBinaryEncoding.TypeMarker.Array2ByteLengthAndCount:
                        case JsonBinaryEncoding.TypeMarker.Array4ByteLengthAndCount:
                            jsonTokenType = JsonTokenType.BeginArray;
                            break;

                        // Object Values
                        case JsonBinaryEncoding.TypeMarker.EmptyObject:
                        case JsonBinaryEncoding.TypeMarker.SinglePropertyObject:
                        case JsonBinaryEncoding.TypeMarker.Object1ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Object2ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Object4ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Object1ByteLengthAndCount:
                        case JsonBinaryEncoding.TypeMarker.Object2ByteLengthAndCount:
                        case JsonBinaryEncoding.TypeMarker.Object4ByteLengthAndCount:
                            jsonTokenType = JsonTokenType.BeginObject;
                            break;

                        default:
                            throw new JsonInvalidTokenException();
                    }
                }

                return jsonTokenType;
            }

            private static int GetArrayOrObjectPrefixLength(byte typeMarker)
            {
                int prefixLength;
                switch (typeMarker)
                {
                    // Array Values
                    case JsonBinaryEncoding.TypeMarker.EmptyArray:
                    case JsonBinaryEncoding.TypeMarker.SingleItemArray:
                        prefixLength = 1;
                        break;

                    case JsonBinaryEncoding.TypeMarker.Array1ByteLength:
                        prefixLength = 1 + 1;
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array2ByteLength:
                        prefixLength = 1 + 2;
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array4ByteLength:
                        prefixLength = 1 + 4;
                        break;

                    case JsonBinaryEncoding.TypeMarker.Array1ByteLengthAndCount:
                        prefixLength = 1 + 1 + 1;
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array2ByteLengthAndCount:
                        prefixLength = 1 + 2 + 2;
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array4ByteLengthAndCount:
                        prefixLength = 1 + 4 + 4;
                        break;

                    // Object Values
                    case JsonBinaryEncoding.TypeMarker.EmptyObject:
                    case JsonBinaryEncoding.TypeMarker.SinglePropertyObject:
                        prefixLength = 1;
                        break;

                    case JsonBinaryEncoding.TypeMarker.Object1ByteLength:
                        prefixLength = 1 + 1;
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object2ByteLength:
                        prefixLength = 1 + 2;
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object4ByteLength:
                        prefixLength = 1 + 4;
                        break;

                    case JsonBinaryEncoding.TypeMarker.Object1ByteLengthAndCount:
                        prefixLength = 1 + 1 + 1;
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object2ByteLengthAndCount:
                        prefixLength = 1 + 2 + 2;
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object4ByteLengthAndCount:
                        prefixLength = 1 + 4 + 4;
                        break;

                    default:
                        throw new ArgumentException($"Unknown typemarker: {typeMarker}");
                }

                return prefixLength;
            }

            private sealed class JsonBinaryMemoryReader : JsonMemoryReader
            {
                public JsonBinaryMemoryReader(ReadOnlyMemory<byte> buffer)
                    : base(buffer)
                {
                }

                public void SkipBytes(int offset)
                {
                    this.position += offset;
                }

                public ReadOnlyMemory<byte> GetRawMemoryJsonToken(int startPosition)
                {
                    return this.buffer.Slice(startPosition);
                }
            }
        }
    }
}
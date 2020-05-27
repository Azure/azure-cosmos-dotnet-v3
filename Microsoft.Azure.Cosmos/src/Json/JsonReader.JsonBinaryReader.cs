//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;

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
        private static readonly JsonTokenType[] TypeMarkerToTokenType = new JsonTokenType[256]
        {
            // Encoded literal integer value (32 values)
            JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number,
            JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number,
            JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number,
            JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number,

            // Encoded 1-byte system string (32 values)
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,

            // Encoded 1-byte user string (32 values)
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
    
            // Encoded 2-byte user string (32 values)
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,

            // TypeMarker-encoded string length (64 values)
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,

            // Variable Length String Values / Binary Values
            JsonTokenType.String,       // StrL1 (1-byte length)
            JsonTokenType.String,       // StrL2 (2-byte length)
            JsonTokenType.String,       // StrL4 (4-byte length)
            JsonTokenType.Binary,       // BinL1 (1-byte length)
            JsonTokenType.Binary,       // BinL2 (2-byte length)
            JsonTokenType.Binary,       // BinL4 (4-byte length)
            JsonTokenType.NotStarted,   // <empty> 0xC6
            JsonTokenType.NotStarted,   // <empty> 0xC7

            // Number Values
            JsonTokenType.Number,       // NumUI8
            JsonTokenType.Number,       // NumI16,
            JsonTokenType.Number,       // NumI32,
            JsonTokenType.Number,       // NumI64,
            JsonTokenType.Number,       // NumDbl,
            JsonTokenType.Float32,      // Float32
            JsonTokenType.Float64,      // Float64
            JsonTokenType.NotStarted,   // <empty> 0xCF

            // Other Value Types
            JsonTokenType.Null,         // Null
            JsonTokenType.False,        // False
            JsonTokenType.True,         // True
            JsonTokenType.Guid,         // Guid
            JsonTokenType.NotStarted,   // <empty> 0xD4
            JsonTokenType.NotStarted,   // <empty> 0xD5
            JsonTokenType.NotStarted,   // <empty> 0xD6
            JsonTokenType.NotStarted,   // <empty> 0xD7

            JsonTokenType.Int8,         // Int8
            JsonTokenType.Int16,        // Int16
            JsonTokenType.Int32,        // Int32
            JsonTokenType.Int64,        // Int64
            JsonTokenType.UInt32,       // UInt32
            JsonTokenType.NotStarted,   // <empty> 0xDD
            JsonTokenType.NotStarted,   // <empty> 0xDE
            JsonTokenType.NotStarted,   // <empty> 0xDF

            // Array Type Markers
            JsonTokenType.BeginArray,   // Arr0
            JsonTokenType.BeginArray,   // Arr1
            JsonTokenType.BeginArray,   // ArrL1 (1-byte length)
            JsonTokenType.BeginArray,   // ArrL2 (2-byte length)
            JsonTokenType.BeginArray,   // ArrL4 (4-byte length)
            JsonTokenType.BeginArray,   // ArrLC1 (1-byte length and count)
            JsonTokenType.BeginArray,   // ArrLC2 (2-byte length and count)
            JsonTokenType.BeginArray,   // ArrLC4 (4-byte length and count)

            // Object Type Markers
            JsonTokenType.BeginObject,  // Obj0
            JsonTokenType.BeginObject,  // Obj1
            JsonTokenType.BeginObject,  // ObjL1 (1-byte length)
            JsonTokenType.BeginObject,  // ObjL2 (2-byte length)
            JsonTokenType.BeginObject,  // ObjL4 (4-byte length)
            JsonTokenType.BeginObject,  // ObjLC1 (1-byte length and count)
            JsonTokenType.BeginObject,  // ObjLC2 (2-byte length and count)
            JsonTokenType.BeginObject,  // ObjLC4 (4-byte length and count)

            // Empty Range
            JsonTokenType.NotStarted,   // <empty> 0xF0
            JsonTokenType.NotStarted,   // <empty> 0xF1
            JsonTokenType.NotStarted,   // <empty> 0xF2
            JsonTokenType.NotStarted,   // <empty> 0xF3
            JsonTokenType.NotStarted,   // <empty> 0xF4
            JsonTokenType.NotStarted,   // <empty> 0xF5
            JsonTokenType.NotStarted,   // <empty> 0xF7
            JsonTokenType.NotStarted,   // <empty> 0xF8

            // Special Values
            JsonTokenType.NotStarted,   // <special value reserved> 0xF8
            JsonTokenType.NotStarted,   // <special value reserved> 0xF9
            JsonTokenType.NotStarted,   // <special value reserved> 0xFA
            JsonTokenType.NotStarted,   // <special value reserved> 0xFB
            JsonTokenType.NotStarted,   // <special value reserved> 0xFC
            JsonTokenType.NotStarted,   // <special value reserved> 0xFD
            JsonTokenType.NotStarted,   // <special value reserved> 0xFE
            JsonTokenType.NotStarted,   // Invalid
        };

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
                if (!this.arrayAndObjectEndStack.Empty() && (this.arrayAndObjectEndStack.Peek() == this.jsonBinaryBuffer.Position))
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

                    byte firstByte = this.jsonBinaryBuffer.Peek();
                    jsonTokenType = JsonBinaryReader.GetJsonTokenType(firstByte);
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
                        nextTokenOffset = JsonBinaryReader.GetArrayOrObjectPrefixLength(firstByte);
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
                JsonTokenType jsonTokenType = JsonBinaryReader.TypeMarkerToTokenType[typeMarker];
                if (jsonTokenType == JsonTokenType.NotStarted)
                {
                    throw new JsonInvalidTokenException();
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
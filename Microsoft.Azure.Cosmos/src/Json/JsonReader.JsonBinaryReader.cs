//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;

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
        private static readonly ImmutableArray<JsonTokenType> TypeMarkerToTokenType = new JsonTokenType[256]
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
    
            // Encoded 2-byte user string (8 values)
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,

            // String Values [0x68, 0x70)
            JsonTokenType.String,  // <empty> 0x68
            JsonTokenType.String,  // <empty> 0x69
            JsonTokenType.String,  // <empty> 0x6A
            JsonTokenType.String,  // <empty> 0x6B
            JsonTokenType.String,  // <empty> 0x6C
            JsonTokenType.String,  // <empty> 0x6D
            JsonTokenType.String,  // <empty> 0x6E
            JsonTokenType.String,  // <empty> 0x6F

            // String Values [0x70, 0x78)
            JsonTokenType.String,  // <empty> 0x70
            JsonTokenType.String,  // <empty> 0x71
            JsonTokenType.String,  // <empty> 0x72
            JsonTokenType.String,  // <empty> 0x73
            JsonTokenType.String,  // <empty> 0x74
            JsonTokenType.String,  // StrGL (Lowercase GUID string)
            JsonTokenType.String,  // StrGU (Uppercase GUID string)
            JsonTokenType.String,  // StrGQ (Double-quoted lowercase GUID string)

            // Compressed strings [JsonTokenType.Stringx78, JsonTokenType.Stringx8JsonTokenType.String)
            JsonTokenType.String,  // String 1-byte length - Lowercase hexadecimal digits encoded as 4-bit characters
            JsonTokenType.String,  // String 1-byte length - Uppercase hexadecimal digits encoded as 4-bit characters
            JsonTokenType.String,  // String 1-byte length - Date-time character set encoded as 4-bit characters
            JsonTokenType.String,  // String 1-byte Length - 4-bit packed characters relative to a base value
            JsonTokenType.String,  // String 1-byte Length - 5-bit packed characters relative to a base value
            JsonTokenType.String,  // String 1-byte Length - 6-bit packed characters relative to a base value
            JsonTokenType.String,  // String 1-byte Length - 7-bit packed characters
            JsonTokenType.String,  // String 2-byte Length - 7-bit packed characters

            // TypeMarker-encoded string length (64 values)
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,
            JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String, JsonTokenType.String,

            // Variable Length String Values
            JsonTokenType.String,       // StrL1 (1-byte length)
            JsonTokenType.String,       // StrL2 (2-byte length)
            JsonTokenType.String,       // StrL4 (4-byte length)
            JsonTokenType.String,       // StrR1 (Reference string of 1-byte offset)
            JsonTokenType.String,       // StrR2 (Reference string of 2-byte offset)
            JsonTokenType.String,       // StrR3 (Reference string of 3-byte offset)
            JsonTokenType.String,       // StrR4 (Reference string of 4-byte offset)
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
            JsonTokenType.Guid,         // GUID
            JsonTokenType.NotStarted,   // <empty> 0xD4
            JsonTokenType.NotStarted,   // <empty> 0xD5
            JsonTokenType.NotStarted,   // <empty> 0xD6
            JsonTokenType.NotStarted,   // <empty> 0xD7

            JsonTokenType.Int8,         // Int8
            JsonTokenType.Int16,        // Int16
            JsonTokenType.Int32,        // Int32
            JsonTokenType.Int64,        // Int64
            JsonTokenType.UInt32,       // UInt32
            JsonTokenType.Binary,       // BinL1 (1-byte length)
            JsonTokenType.Binary,       // BinL2 (2-byte length)
            JsonTokenType.Binary,       // BinL4 (4-byte length)

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
        }.ToImmutableArray();

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
            private readonly IReadOnlyJsonStringDictionary jsonStringDictionary;

            /// <summary>
            /// For binary there is no end of token marker in the actual binary, but the JsonReader interface still needs to surface ObjectEndToken and ArrayEndToken.
            /// To accommodate for this we have a progress stack to let us know how many bytes there are left to read for all levels of nesting. 
            /// With this information we know that we are at the end of a context and can now surface an end object / array token.
            /// </summary>
            private readonly Stack<int> arrayAndObjectEndStack;

            private readonly ReadOnlyMemory<byte> rootBuffer;

            private int currentTokenPosition;

            public JsonBinaryReader(
                ReadOnlyMemory<byte> buffer,
                IReadOnlyJsonStringDictionary jsonStringDictionary = null)
                : this(buffer, indexToStartFrom: null, jsonStringDictionary: jsonStringDictionary)
            {
            }

            internal JsonBinaryReader(
                ReadOnlyMemory<byte> rootBuffer,
                int? indexToStartFrom = null,
                IReadOnlyJsonStringDictionary jsonStringDictionary = null)
            {
                if (rootBuffer.IsEmpty)
                {
                    throw new ArgumentException($"{nameof(rootBuffer)} must not be empty.");
                }

                this.rootBuffer = rootBuffer;

                ReadOnlyMemory<byte> readerBuffer = this.rootBuffer;

                if (indexToStartFrom.HasValue)
                {
                    readerBuffer = readerBuffer.Slice(start: indexToStartFrom.Value);
                }
                else
                {
                    // Skip the 0x80
                    readerBuffer = readerBuffer.Slice(start: 1);
                }

                // Only navigate the outer most json value and trim off trailing bytes
                int jsonValueLength = JsonBinaryEncoding.GetValueLength(readerBuffer.Span);
                if (readerBuffer.Length < jsonValueLength)
                {
                    throw new ArgumentException("buffer is shorter than the length prefix.");
                }

                readerBuffer = readerBuffer.Slice(0, jsonValueLength);

                // offset for the 0x80 binary type marker
                this.jsonBinaryBuffer = new JsonBinaryMemoryReader(readerBuffer);
                this.arrayAndObjectEndStack = new Stack<int>();
                this.jsonStringDictionary = jsonStringDictionary;
            }

            /// <inheritdoc />
            public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Binary;

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
                    this.rootBuffer,
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition),
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
                    this.rootBuffer,
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition),
                    this.jsonStringDictionary,
                    out bufferedUtf8StringValue);
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

                return JsonBinaryEncoding.GetBinaryValue(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition));
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
            }
        }
    }
}
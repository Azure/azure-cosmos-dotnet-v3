//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Microsoft.Azure.Cosmos.Core.Utf8;

    /// <summary>
    /// Partial class for the JsonWriter that has a private JsonTextWriter below.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract partial class JsonWriter : IJsonWriter
    {
        internal static bool EnableEncodedStrings = true;

        /// <summary>
        /// Concrete implementation of <see cref="JsonWriter"/> that knows how to serialize to binary encoding.
        /// </summary>
        private sealed class JsonBinaryWriter : JsonWriter, IJsonBinaryWriterExtensions
        {
            private enum RawValueType : byte
            {
                Token,
                StrUsr,
                StrEncLen,
                StrL1,
                StrL2,
                StrL4,
                StrR1,
                StrR2,
                StrR3,
                StrR4,
                Arr1,
                Obj1,
                Arr,
                Obj,
            }

            private const int MaxStackAllocSize = 4 * 1024;
            private const int MinReferenceStringLength = 2;
            private const int MaxReferenceStringLength = 88;

            private static readonly ImmutableArray<byte> RawValueTypes = new RawValueType[256]
            {
                // Encoded literal integer value (32 values)
                RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token,
                RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token,
                RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token,
                RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token,

                // Encoded 1-byte system string (32 values)
                RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token,
                RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token,
                RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token,
                RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token, RawValueType.Token,

                // Encoded 1-byte user string (32 values)
                RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr,
                RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr,
                RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr,
                RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr,

                // Encoded 2-byte user string (8 values)
                RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr, RawValueType.StrUsr,

                // Empty Range
                RawValueType.Token,      // <empty> 0x68
                RawValueType.Token,      // <empty> 0x69
                RawValueType.Token,      // <empty> 0x6A
                RawValueType.Token,      // <empty> 0x6B
                RawValueType.Token,      // <empty> 0x6C
                RawValueType.Token,      // <empty> 0x6D
                RawValueType.Token,      // <empty> 0x6E
                RawValueType.Token,      // <empty> 0x6F

                // Empty Range
                RawValueType.Token,      // <empty> 0x70
                RawValueType.Token,      // <empty> 0x71
                RawValueType.Token,      // <empty> 0x72
                RawValueType.Token,      // <empty> 0x73
                RawValueType.Token,      // <empty> 0x74
                RawValueType.Token,      // RawValueType.StrGL (Lowercase GUID string)
                RawValueType.Token,      // RawValueType.StrGU (Uppercase GUID string)
                RawValueType.Token,      // RawValueType.StrGQ (Double-quoted lowercase GUID string)

                // Compressed strings [0x78, 0x80)
                RawValueType.Token,      // RawValueType.String 1-byte length - Lowercase hexadecimal digits encoded as 4-bit characters
                RawValueType.Token,      // RawValueType.String 1-byte length - Uppercase hexadecimal digits encoded as 4-bit characters
                RawValueType.Token,      // RawValueType.String 1-byte length - Date-time character set encoded as 4-bit characters
                RawValueType.Token,      // RawValueType.String 1-byte Length - 4-bit packed characters relative to a base value
                RawValueType.Token,      // RawValueType.String 1-byte Length - 5-bit packed characters relative to a base value
                RawValueType.Token,      // RawValueType.String 1-byte Length - 6-bit packed characters relative to a base value
                RawValueType.Token,      // RawValueType.String 1-byte Length - 7-bit packed characters
                RawValueType.Token,      // RawValueType.String 2-byte Length - 7-bit packed characters

                // TypeMarker-encoded string length (64 values)
                RawValueType.Token, RawValueType.Token, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen,
                RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen,
                RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen,
                RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen,
                RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen,
                RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen,
                RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen,
                RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen, RawValueType.StrEncLen,

                // Variable Length RawValueType.String Values
                RawValueType.StrL1,      // RawValueType.StrL1 (1-byte length)
                RawValueType.StrL2,      // RawValueType.StrL2 (2-byte length)
                RawValueType.StrL4,      // RawValueType.StrL4 (4-byte length)
                RawValueType.StrR1,      // RawValueType.StrR1 (Reference string of 1-byte offset)
                RawValueType.StrR2,      // RawValueType.StrR2 (Reference string of 2-byte offset)
                RawValueType.StrR3,      // RawValueType.StrR3 (Reference string of 3-byte offset)
                RawValueType.StrR4,      // RawValueType.StrR4 (Reference string of 4-byte offset)
                RawValueType.Token,      // <empty> 0xC7

                // Number Values
                RawValueType.Token,      // NumUI8
                RawValueType.Token,      // NumI16,
                RawValueType.Token,      // NumI32,
                RawValueType.Token,      // NumI64,
                RawValueType.Token,      // NumDbl,
                RawValueType.Token,      // Float32
                RawValueType.Token,      // Float64
                RawValueType.Token,      // <empty> 0xCF

                // Other Value Types
                RawValueType.Token,      // Null
                RawValueType.Token,      // False
                RawValueType.Token,      // True
                RawValueType.Token,      // GUID
                RawValueType.Token,      // <empty> 0xD4
                RawValueType.Token,      // <empty> 0xD5
                RawValueType.Token,      // <empty> 0xD6
                RawValueType.Token,      // <empty> 0xD7

                RawValueType.Token,      // Int8
                RawValueType.Token,      // Int16
                RawValueType.Token,      // Int32
                RawValueType.Token,      // Int64
                RawValueType.Token,      // UInt32
                RawValueType.Token,      // BinL1 (1-byte length)
                RawValueType.Token,      // BinL2 (2-byte length)
                RawValueType.Token,      // BinL4 (4-byte length)

                // RawValueType.Array Type Markers
                RawValueType.Token,      // RawValueType.Arr0
                RawValueType.Arr1,       // RawValueType.Arr1 <unknown>
                RawValueType.Arr,        // RawValueType.ArrL1 (1-byte length)
                RawValueType.Arr,        // RawValueType.ArrL2 (2-byte length)
                RawValueType.Arr,        // RawValueType.ArrL4 (4-byte length)
                RawValueType.Arr,        // RawValueType.ArrLC1 (1-byte length and count)
                RawValueType.Arr,        // RawValueType.ArrLC2 (2-byte length and count)
                RawValueType.Arr,        // RawValueType.ArrLC4 (4-byte length and count)

                // RawValueType.Object Type Markers
                RawValueType.Token,      // RawValueType.Obj0
                RawValueType.Obj1,       // RawValueType.Obj1 <unknown>
                RawValueType.Obj,        // RawValueType.ObjL1 (1-byte length)
                RawValueType.Obj,        // RawValueType.ObjL2 (2-byte length)
                RawValueType.Obj,        // RawValueType.ObjL4 (4-byte length)
                RawValueType.Obj,        // RawValueType.ObjLC1 (1-byte length and count)
                RawValueType.Obj,        // RawValueType.ObjLC2 (2-byte length and count)
                RawValueType.Obj,        // RawValueType.ObjLC4 (4-byte length and count)

                // Empty Range
                RawValueType.Token,      // <empty> 0xF0
                RawValueType.Token,      // <empty> 0xF1
                RawValueType.Token,      // <empty> 0xF2
                RawValueType.Token,      // <empty> 0xF3
                RawValueType.Token,      // <empty> 0xF4
                RawValueType.Token,      // <empty> 0xF5
                RawValueType.Token,      // <empty> 0xF6
                RawValueType.Token,      // <empty> 0xF7

                // Special Values
                RawValueType.Token,      // <special value reserved> 0xF8
                RawValueType.Token,      // <special value reserved> 0xF9
                RawValueType.Token,      // <special value reserved> 0xFA
                RawValueType.Token,      // <special value reserved> 0xFB
                RawValueType.Token,      // <special value reserved> 0xFC
                RawValueType.Token,      // <special value reserved> 0xFD
                RawValueType.Token,      // <special value reserved> 0xFE
                RawValueType.Token,      // Invalid
            }.Select(x => (byte)x).ToImmutableArray();

            /// <summary>
            /// Writer used to write fully materialized context to the internal stream.
            /// </summary>
            private readonly JsonBinaryMemoryWriter binaryWriter;

            /// <summary>
            /// With binary encoding all the json elements are length prefixed,
            /// unfortunately the caller of this class only provides what tokens to write.
            /// This means that whenever a user call WriteObject/ArrayStart we don't know the length of said object or array
            /// until WriteObject/ArrayEnd is invoked.
            /// To get around this we reserve some space for the length and write to it when the user supplies the end token.
            /// This stack remembers for each nesting level where it begins and how many items it has.
            /// </summary>
            private readonly Stack<ArrayAndObjectInfo> bufferedContexts;

            /// <summary>
            /// With binary encoding json elements like arrays and object are prefixed with a length in bytes and optionally a count.
            /// This flag just determines whether you want to serialize the count, since it's optional and up to the user to make the
            /// tradeoff between O(1) .Count() operation as the cost of additional storage.
            /// </summary>
            private readonly bool serializeCount;

            /// <summary>
            /// When a user writes an open array or object we reserve this much space for the type marker + length + count
            /// And correct it later when they write a close array or object.
            /// </summary>
            private readonly int reservationSize;

            private readonly List<SharedStringValue> sharedStrings;

            private readonly ReferenceStringDictionary sharedStringIndexes;

            /// <summary>
            /// Initializes a new instance of the JsonBinaryWriter class.
            /// </summary>
            /// <param name="initialCapacity">The initial capacity to avoid intermediary allocations.</param>
            /// <param name="serializeCount">Whether to serialize the count for object and array typemarkers.</param>
            public JsonBinaryWriter(
                int initialCapacity = 256,
                bool serializeCount = false)
            {
                this.binaryWriter = new JsonBinaryMemoryWriter(initialCapacity);
                this.bufferedContexts = new Stack<ArrayAndObjectInfo>();
                this.serializeCount = serializeCount;
                this.reservationSize = JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.OneByteLength + (this.serializeCount ? JsonBinaryEncoding.OneByteCount : 0);
                this.sharedStrings = new List<SharedStringValue>();
                this.sharedStringIndexes = new ReferenceStringDictionary();

                // Write the serialization format as the very first byte
                byte binaryTypeMarker = (byte)JsonSerializationFormat.Binary;
                this.binaryWriter.Write(binaryTypeMarker);

                // Push on the outermost context
                this.bufferedContexts.Push(new ArrayAndObjectInfo(this.CurrentLength));
            }

            /// <inheritdoc />
            public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Binary;

            /// <inheritdoc />
            public override long CurrentLength => this.binaryWriter.Position;

            /// <inheritdoc />
            public override void WriteObjectStart() => this.WriterArrayOrObjectStart(isArray: false);

            /// <inheritdoc />
            public override void WriteObjectEnd() => this.WriteArrayOrObjectEnd(isArray: false);

            /// <inheritdoc />
            public override void WriteArrayStart() => this.WriterArrayOrObjectStart(isArray: true);

            /// <inheritdoc />
            public override void WriteArrayEnd() => this.WriteArrayOrObjectEnd(isArray: true);

            /// <inheritdoc />
            public override void WriteFieldName(Utf8Span fieldName) => this.WriteFieldNameOrString(isFieldName: true, fieldName);

            /// <inheritdoc />
            public override void WriteStringValue(Utf8Span value) => this.WriteFieldNameOrString(isFieldName: false, value);

            /// <inheritdoc />
            public override void WriteNumber64Value(Number64 value)
            {
                if (value.IsInteger)
                {
                    this.WriteIntegerInternal(Number64.ToLong(value));
                }
                else
                {
                    this.WriteDoubleInternal(Number64.ToDouble(value));
                }

                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteBoolValue(bool value)
            {
                this.JsonObjectState.RegisterToken(value ? JsonTokenType.True : JsonTokenType.False);
                this.binaryWriter.Write(value ? JsonBinaryEncoding.TypeMarker.True : JsonBinaryEncoding.TypeMarker.False);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteNullValue()
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Null);
                this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Null);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteInt8Value(sbyte value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Int8);
                this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Int8);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteInt16Value(short value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Int16);
                this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Int16);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteInt32Value(int value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Int32);
                this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Int32);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteInt64Value(long value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Int64);
                this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Int64);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteFloat32Value(float value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Float32);
                this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Float32);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteFloat64Value(double value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Float64);
                this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Float64);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteUInt32Value(uint value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.UInt32);
                this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.UInt32);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteGuidValue(Guid value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Guid);
                this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Guid);
                this.binaryWriter.Write(value.ToByteArray());
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteBinaryValue(ReadOnlySpan<byte> value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Binary);

                long length = value.Length;
                if ((length & ~0xFF) == 0)
                {
                    this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Binary1ByteLength);
                    this.binaryWriter.Write((byte)length);
                }
                else if ((length & ~0xFFFF) == 0)
                {
                    this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Binary2ByteLength);
                    this.binaryWriter.Write((ushort)length);
                }
                else if ((length & ~0xFFFFFFFFL) == 0)
                {
                    this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Binary4ByteLength);
                    this.binaryWriter.Write((ulong)length);
                }
                else
                {
                    throw new ArgumentOutOfRangeException("Binary length was too large.");
                }

                this.binaryWriter.Write(value);

                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override ReadOnlyMemory<byte> GetResult()
            {
                if (this.bufferedContexts.Count > 1)
                {
                    throw new JsonNotCompleteException();
                }

                if (this.binaryWriter.Position == 1)
                {
                    // We haven't written anything but the type marker, so just return an empty buffer.
                    return ReadOnlyMemory<byte>.Empty;
                }

                return this.binaryWriter.BufferAsMemory.Slice(
                    0,
                    this.binaryWriter.Position);
            }

            private void WriterArrayOrObjectStart(bool isArray)
            {
                this.JsonObjectState.RegisterToken(isArray ? JsonTokenType.BeginArray : JsonTokenType.BeginObject);

                // Save the start index
                ArrayAndObjectInfo info = new ArrayAndObjectInfo(this.CurrentLength)
                {
                    StringStartIndex = this.sharedStrings.Count
                };
                this.bufferedContexts.Push(info);

                // Assume 1-byte value length; as such, we need to reserve up 3 bytes (1 byte type marker, 1 byte length, 1 byte count).
                // We'll adjust this as needed when writing the end of the array/object.
                this.binaryWriter.Write((byte)0);
                this.binaryWriter.Write((byte)0);
                if (this.serializeCount)
                {
                    this.binaryWriter.Write((byte)0);
                }
            }

            private void WriteArrayOrObjectEnd(bool isArray)
            {
                this.JsonObjectState.RegisterToken(isArray ? JsonTokenType.EndArray : JsonTokenType.EndObject);
                ArrayAndObjectInfo nestedContext = this.bufferedContexts.Pop();

                // Do some math
                int typeMarkerIndex = (int)nestedContext.Offset;
                int payloadIndex = typeMarkerIndex + this.reservationSize;
                int originalCursor = (int)this.CurrentLength;
                int payloadLength = originalCursor - payloadIndex;
                int count = (int)nestedContext.Count;
                int stringStartIndex = (int)nestedContext.StringStartIndex;

                // Figure out what the typemarker and length should be and do any corrections needed
                if (count == 0)
                {
                    // Empty object

                    // Move the cursor back
                    this.binaryWriter.Position = typeMarkerIndex;

                    // Write the type marker
                    this.binaryWriter.Write(isArray ? JsonBinaryEncoding.TypeMarker.EmptyArray : JsonBinaryEncoding.TypeMarker.EmptyObject);
                }
                else if (count == 1)
                {
                    // Single-property object

                    // Move the buffer back but leave one byte for the typemarker
                    Span<byte> buffer = this.binaryWriter.BufferAsSpan;
                    int bytesToWrite = JsonBinaryEncoding.TypeMarkerLength;
                    this.MoveBuffer(buffer, payloadIndex, payloadLength, typeMarkerIndex, bytesToWrite, stringStartIndex);

                    // Move the cursor back
                    this.binaryWriter.Position = typeMarkerIndex;

                    // Write the type marker
                    this.binaryWriter.Write(isArray ? JsonBinaryEncoding.TypeMarker.SingleItemArray : JsonBinaryEncoding.TypeMarker.SinglePropertyObject);

                    // Move the cursor forward
                    this.binaryWriter.Position = typeMarkerIndex + JsonBinaryEncoding.TypeMarkerLength + payloadLength;
                }
                else
                {
                    // Need to figure out how many bytes to encode the length and the count
                    if (payloadLength <= byte.MaxValue)
                    {
                        // 1 byte length - don't need to move the buffer
                        int bytesToWrite = JsonBinaryEncoding.TypeMarkerLength
                            + JsonBinaryEncoding.OneByteLength
                            + (this.serializeCount ? JsonBinaryEncoding.OneByteCount : 0);

                        // Move the cursor back
                        this.binaryWriter.Position = typeMarkerIndex;

                        // Write the type marker
                        if (this.serializeCount)
                        {
                            this.binaryWriter.Write(isArray ? JsonBinaryEncoding.TypeMarker.Array1ByteLengthAndCount : JsonBinaryEncoding.TypeMarker.Object1ByteLengthAndCount);
                            this.binaryWriter.Write((byte)payloadLength);
                            this.binaryWriter.Write((byte)count);
                        }
                        else
                        {
                            this.binaryWriter.Write(isArray ? JsonBinaryEncoding.TypeMarker.Array1ByteLength : JsonBinaryEncoding.TypeMarker.Object1ByteLength);
                            this.binaryWriter.Write((byte)payloadLength);
                        }

                        // Move the cursor forward
                        this.binaryWriter.Position = typeMarkerIndex + bytesToWrite + payloadLength;
                    }
                    else if (payloadLength <= ushort.MaxValue)
                    {
                        // 2 byte length - make space for the extra byte length (and extra byte count)
                        this.binaryWriter.Write((byte)0);
                        if (this.serializeCount)
                        {
                            this.binaryWriter.Write((byte)0);
                        }

                        // Move the buffer forward
                        Span<byte> buffer = this.binaryWriter.BufferAsSpan;
                        int bytesToWrite = JsonBinaryEncoding.TypeMarkerLength
                            + JsonBinaryEncoding.TwoByteLength
                            + (this.serializeCount ? JsonBinaryEncoding.TwoByteCount : 0);
                        this.MoveBuffer(buffer, payloadIndex, payloadLength, typeMarkerIndex, bytesToWrite, stringStartIndex);

                        // Move the cursor back
                        this.binaryWriter.Position = typeMarkerIndex;

                        // Write the type marker
                        if (this.serializeCount)
                        {
                            this.binaryWriter.Write(isArray ? JsonBinaryEncoding.TypeMarker.Array2ByteLengthAndCount : JsonBinaryEncoding.TypeMarker.Object2ByteLengthAndCount);
                            this.binaryWriter.Write((ushort)payloadLength);
                            this.binaryWriter.Write((ushort)count);
                        }
                        else
                        {
                            this.binaryWriter.Write(isArray ? JsonBinaryEncoding.TypeMarker.Array2ByteLength : JsonBinaryEncoding.TypeMarker.Object2ByteLength);
                            this.binaryWriter.Write((ushort)payloadLength);
                        }

                        // Move the cursor forward
                        this.binaryWriter.Position = typeMarkerIndex + bytesToWrite + payloadLength;
                    }
                    else
                    {
                        // (payloadLength <= uint.MaxValue)

                        // 4 byte length - make space for an extra 3 byte length (and 3 byte count)
                        this.binaryWriter.Write((byte)0);
                        this.binaryWriter.Write((ushort)0);
                        if (this.serializeCount)
                        {
                            this.binaryWriter.Write((byte)0);
                            this.binaryWriter.Write((ushort)0);
                        }

                        // Move the buffer forward
                        Span<byte> buffer = this.binaryWriter.BufferAsSpan;
                        int bytesToWrite = JsonBinaryEncoding.TypeMarkerLength
                            + JsonBinaryEncoding.FourByteLength
                            + (this.serializeCount ? JsonBinaryEncoding.FourByteCount : 0);
                        this.MoveBuffer(buffer, payloadIndex, payloadLength, typeMarkerIndex, bytesToWrite, stringStartIndex);

                        // Move the cursor back
                        this.binaryWriter.Position = typeMarkerIndex;

                        // Write the type marker
                        if (this.serializeCount)
                        {
                            this.binaryWriter.Write(isArray ? JsonBinaryEncoding.TypeMarker.Array4ByteLengthAndCount : JsonBinaryEncoding.TypeMarker.Object4ByteLengthAndCount);
                            this.binaryWriter.Write((uint)payloadLength);
                            this.binaryWriter.Write((uint)count);
                        }
                        else
                        {
                            this.binaryWriter.Write(isArray ? JsonBinaryEncoding.TypeMarker.Array4ByteLength : JsonBinaryEncoding.TypeMarker.Object4ByteLength);
                            this.binaryWriter.Write((uint)payloadLength);
                        }

                        // Move the cursor forward
                        this.binaryWriter.Position = typeMarkerIndex + bytesToWrite + payloadLength;
                    }
                }

                this.bufferedContexts.Peek().Count++;

                // If we are closing the outermost array / object, we need to fix up reference string offsets
                if (typeMarkerIndex == 1)
                {
                    this.FixReferenceStringOffsets(this.binaryWriter.RawBuffer.Slice(start: 1));
                }
            }

            private void MoveBuffer(Span<byte> buffer, int payloadIndex, int payloadLength, int typeMarkerIndex, int bytesToWrite, int stringStartIndex)
            {
                Span<byte> payload = buffer.Slice(payloadIndex, payloadLength);
                int newPayloadIndex = typeMarkerIndex + bytesToWrite;
                Span<byte> newPayload = buffer.Slice(newPayloadIndex);
                payload.CopyTo(newPayload);

                int delta = newPayloadIndex - payloadIndex;
                for (int index = stringStartIndex; index < this.sharedStrings.Count; index++)
                {
                    SharedStringValue sharedStringValue = this.sharedStrings[index];
                    this.sharedStrings[index] = new SharedStringValue(offset: sharedStringValue.Offset + delta, maxOffset: sharedStringValue.MaxOffset);
                }
            }

            private void FixReferenceStringOffsets(Memory<byte> buffer)
            {
                if (this.sharedStrings.Count == 0)
                {
                    return;
                }

                byte typeMarker = buffer.Span[0];

                JsonNodeType nodeType = JsonBinaryEncoding.NodeTypes.Lookup[typeMarker];
                switch (nodeType)
                {
                    case JsonNodeType.Null:
                    case JsonNodeType.False:
                    case JsonNodeType.True:
                    case JsonNodeType.Number64:
                    case JsonNodeType.Int8:
                    case JsonNodeType.Int16:
                    case JsonNodeType.Int32:
                    case JsonNodeType.Int64:
                    case JsonNodeType.UInt32:
                    case JsonNodeType.Float32:
                    case JsonNodeType.Float64:
                    case JsonNodeType.Binary:
                    case JsonNodeType.Guid:
                        // Do Nothing
                        break;

                    case JsonNodeType.String:
                    case JsonNodeType.FieldName:
                        {
                            Memory<byte> offsetBuffer = buffer.Slice(start: 1);
                            switch (typeMarker)
                            {
                                case JsonBinaryEncoding.TypeMarker.ReferenceString1ByteOffset:
                                    {
                                        byte stringIndex = JsonBinaryEncoding.GetFixedSizedValue<byte>(offsetBuffer.Span);
                                        SharedStringValue sharedStringValue = this.sharedStrings[stringIndex];
                                        JsonBinaryEncoding.SetFixedSizedValue<byte>(offsetBuffer.Span, (byte)sharedStringValue.Offset);
                                    }
                                    break;

                                case JsonBinaryEncoding.TypeMarker.ReferenceString2ByteOffset:
                                    {
                                        ushort stringIndex = JsonBinaryEncoding.GetFixedSizedValue<ushort>(offsetBuffer.Span);
                                        SharedStringValue sharedStringValue = this.sharedStrings[stringIndex];
                                        JsonBinaryEncoding.SetFixedSizedValue<ushort>(offsetBuffer.Span, (ushort)sharedStringValue.Offset);
                                    }
                                    break;

                                case JsonBinaryEncoding.TypeMarker.ReferenceString3ByteOffset:
                                    {
                                        JsonBinaryEncoding.UInt24 stringIndex = JsonBinaryEncoding.GetFixedSizedValue<JsonBinaryEncoding.UInt24>(offsetBuffer.Span);
                                        SharedStringValue sharedStringValue = this.sharedStrings[stringIndex];
                                        JsonBinaryEncoding.SetFixedSizedValue<JsonBinaryEncoding.UInt24>(offsetBuffer.Span, (JsonBinaryEncoding.UInt24)sharedStringValue.Offset);
                                    }
                                    break;

                                case JsonBinaryEncoding.TypeMarker.ReferenceString4ByteOffset:
                                    {
                                        int stringIndex = JsonBinaryEncoding.GetFixedSizedValue<int>(offsetBuffer.Span);
                                        SharedStringValue sharedStringValue = this.sharedStrings[stringIndex];
                                        JsonBinaryEncoding.SetFixedSizedValue<int>(offsetBuffer.Span, (int)sharedStringValue.Offset);
                                    }
                                    break;

                                default:
                                    // Do Nothing
                                    break;
                            }
                        }
                        break;

                    case JsonNodeType.Array:
                        foreach (Memory<byte> arrayItem in JsonBinaryEncoding.Enumerator.GetMutableArrayItems(buffer))
                        {
                            this.FixReferenceStringOffsets(arrayItem);
                        }

                        break;

                    case JsonNodeType.Object:
                        foreach (JsonBinaryEncoding.Enumerator.MutableObjectProperty mutableObjectProperty in JsonBinaryEncoding.Enumerator.GetMutableObjectProperties(buffer))
                        {
                            this.FixReferenceStringOffsets(mutableObjectProperty.Name);
                            this.FixReferenceStringOffsets(mutableObjectProperty.Value);
                        }

                        break;

                    case JsonNodeType.Unknown:
                    default:
                        throw new InvalidOperationException($"Unknown {nameof(nodeType)}: {nodeType}.");
                }
            }

            private void WriteFieldNameOrString(bool isFieldName, Utf8Span utf8Span)
            {
                this.binaryWriter.EnsureRemainingBufferSpace(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.FourByteLength + utf8Span.Length);

                this.JsonObjectState.RegisterToken(isFieldName ? JsonTokenType.FieldName : JsonTokenType.String);
                if (JsonBinaryEncoding.TryGetEncodedStringTypeMarker(
                    utf8Span,
                    out JsonBinaryEncoding.MultiByteTypeMarker multiByteTypeMarker))
                {
                    switch (multiByteTypeMarker.Length)
                    {
                        case 1:
                            this.binaryWriter.Write(multiByteTypeMarker.One);
                            break;

                        case 2:
                            this.binaryWriter.Write(multiByteTypeMarker.One);
                            this.binaryWriter.Write(multiByteTypeMarker.Two);
                            break;

                        default:
                            throw new InvalidOperationException($"Unable to serialize a {nameof(JsonBinaryEncoding.MultiByteTypeMarker)} of length: {multiByteTypeMarker.Length}");
                    }
                }
                else if (EnableEncodedStrings 
                    && isFieldName
                    && (utf8Span.Length >= MinReferenceStringLength)
                    && this.TryRegisterStringValue(utf8Span))
                {
                    // Work is done in the check
                }
                else if (EnableEncodedStrings
                    && !isFieldName
                    && (utf8Span.Length == JsonBinaryEncoding.GuidLength)
                    && JsonBinaryEncoding.TryEncodeGuidString(utf8Span.Span, this.binaryWriter.Cursor))
                {
                    // Encoded value as guid string
                    this.binaryWriter.Position += JsonBinaryEncoding.EncodedGuidLength;
                }
                else if (EnableEncodedStrings
                    && !isFieldName
                    && (utf8Span.Length == JsonBinaryEncoding.GuidWithQuotesLength)
                    && (utf8Span.Span[0] == '"')
                    && (utf8Span.Span[JsonBinaryEncoding.GuidWithQuotesLength - 1] == '"')
                    && JsonBinaryEncoding.TryEncodeGuidString(utf8Span.Span.Slice(start: 1), this.binaryWriter.Cursor)
                    && (this.binaryWriter.Cursor[0] == JsonBinaryEncoding.TypeMarker.LowercaseGuidString))
                {
                    // Encoded value as lowercase double quote guid
                    this.binaryWriter.Cursor[0] = JsonBinaryEncoding.TypeMarker.DoubleQuotedLowercaseGuidString;
                    this.binaryWriter.Position += JsonBinaryEncoding.EncodedGuidLength;
                }
                else if (EnableEncodedStrings
                    && !isFieldName
                    && JsonBinaryEncoding.TryEncodeCompressedString(utf8Span.Span, this.binaryWriter.Cursor, out int bytesWritten))
                {
                    // Encoded value as a compressed string
                    this.binaryWriter.Position += bytesWritten;
                }
                else if (
                    EnableEncodedStrings 
                    && !isFieldName
                    && (utf8Span.Length >= MinReferenceStringLength)
                    && (utf8Span.Length <= MaxReferenceStringLength)
                    && this.TryRegisterStringValue(utf8Span))
                {
                    // Work is done in the check
                }
                else if (JsonBinaryEncoding.TypeMarker.TryGetEncodedStringLengthTypeMarker(utf8Span.Length, out byte typeMarker))
                {
                    // Write with type marker that encodes the length
                    this.binaryWriter.Write(typeMarker);
                    this.binaryWriter.Write(utf8Span.Span);
                }
                // Just write it out as a regular string with type marker and length prefix
                else if (utf8Span.Length < byte.MaxValue)
                {
                    this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.String1ByteLength);
                    this.binaryWriter.Write((byte)utf8Span.Length);
                    this.binaryWriter.Write(utf8Span.Span);
                }
                else if (utf8Span.Length < ushort.MaxValue)
                {
                    this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.String2ByteLength);
                    this.binaryWriter.Write((ushort)utf8Span.Length);
                    this.binaryWriter.Write(utf8Span.Span);
                }
                else
                {
                    // (utf8String.Length < uint.MaxValue)
                    this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.String4ByteLength);
                    this.binaryWriter.Write((uint)utf8Span.Length);
                    this.binaryWriter.Write(utf8Span.Span);
                }

                if (!isFieldName)
                {
                    // If we just wrote a string then increment the count (we don't increment for field names, since we need to wait for the corresponding property value).
                    this.bufferedContexts.Peek().Count++;
                }
            }

            private bool TryRegisterStringValue(Utf8Span utf8Span)
            {
                if (!this.sharedStringIndexes.TryGetValue(utf8Span.Span, out (UInt128 hash, ulong index) hashAndIndex))
                {
                    // Not found, add it to the lookup table

                    // In order to avoid having to change the typer marker later on, we need to account for the case
                    // where the buffer might shift as a result of adjusting array/object length.

                    int maxOffset = (this.JsonObjectState.CurrentDepth * 3) + (int)this.CurrentLength;

                    bool shouldAddValue = (utf8Span.Length >= 5) ||
                        ((maxOffset <= byte.MaxValue) && (utf8Span.Length >= 2)) ||
                        ((maxOffset <= ushort.MaxValue) && (utf8Span.Length >= 3)) ||
                        ((maxOffset <= JsonBinaryEncoding.UInt24.MaxValue) && (utf8Span.Length >= 4));

                    if (shouldAddValue)
                    {
                        this.sharedStrings.Add(new SharedStringValue(offset: (int)this.CurrentLength, maxOffset: maxOffset));
                        this.sharedStringIndexes.Add(utf8Span.Length, hashAndIndex.hash, (ulong)(this.sharedStrings.Count - 1));
                    }

                    return false;
                }

                SharedStringValue sharedString = this.sharedStrings[(int)hashAndIndex.index];

                if (sharedString.MaxOffset <= byte.MaxValue)
                {
                    this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.ReferenceString1ByteOffset);
                    this.binaryWriter.Write((byte)hashAndIndex.index);
                }
                else if (sharedString.MaxOffset <= ushort.MaxValue)
                {
                    this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.ReferenceString2ByteOffset);
                    this.binaryWriter.Write((ushort)hashAndIndex.index);
                }
                else if (sharedString.MaxOffset <= JsonBinaryEncoding.UInt24.MaxValue)
                {
                    this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.ReferenceString3ByteOffset);
                    this.binaryWriter.Write((JsonBinaryEncoding.UInt24)(int)hashAndIndex.index);
                }
                else
                {
                    this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.ReferenceString4ByteOffset);
                    this.binaryWriter.Write(hashAndIndex.index);
                }

                return true;
            }

            private void WriteIntegerInternal(long value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Number);
                if (JsonBinaryEncoding.TypeMarker.IsEncodedNumberLiteral(value))
                {
                    this.binaryWriter.Write((byte)(JsonBinaryEncoding.TypeMarker.LiteralIntMin + value));
                }
                else
                {
                    if (value >= 0)
                    {
                        // Non-negative Number
                        if (value <= byte.MaxValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.NumberUInt8);
                            this.binaryWriter.Write((byte)value);
                        }
                        else if (value <= short.MaxValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.NumberInt16);
                            this.binaryWriter.Write((short)value);
                        }
                        else if (value <= int.MaxValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.NumberInt32);
                            this.binaryWriter.Write((int)value);
                        }
                        else
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.NumberInt64);
                            this.binaryWriter.Write(value);
                        }
                    }
                    else
                    {
                        // Negative Number
                        if (value < int.MinValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.NumberInt64);
                            this.binaryWriter.Write(value);
                        }
                        else if (value < short.MinValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.NumberInt32);
                            this.binaryWriter.Write((int)value);
                        }
                        else
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.NumberInt16);
                            this.binaryWriter.Write((short)value);
                        }
                    }
                }
            }

            private void WriteDoubleInternal(double value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Number);
                this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.NumberDouble);
                this.binaryWriter.Write(value);
            }

            public void WriteRawJsonValue(
                ReadOnlyMemory<byte> rootBuffer,
                ReadOnlyMemory<byte> rawJsonValue,
                bool isRootNode,
                bool isFieldName)
            {
                if (isRootNode && (this.binaryWriter.Position == 1))
                {
                    // Other that whether or not this is a field name, the type of the value does not matter here
                    this.JsonObjectState.RegisterToken(isFieldName ? JsonTokenType.FieldName : JsonTokenType.String);
                    this.binaryWriter.Write(rawJsonValue.Span);
                    if (!isFieldName)
                    {
                        this.bufferedContexts.Peek().Count++;
                    }
                }
                else
                {
                    this.ForceRewriteRawJsonValue(rootBuffer, rawJsonValue, isFieldName);
                }
            }

            private void ForceRewriteRawJsonValue(
                ReadOnlyMemory<byte> rootBuffer,
                ReadOnlyMemory<byte> rawJsonValue,
                bool isFieldName)
            {
                byte typeMarker = rawJsonValue.Span[0];
                RawValueType rawType = (RawValueType)RawValueTypes[typeMarker];
                switch (rawType)
                {
                    case RawValueType.Token:
                        {
                            int valueLength = JsonBinaryEncoding.GetValueLength(rawJsonValue.Span);

                            rawJsonValue = rawJsonValue.Slice(start: 0, length: valueLength);

                            // We only care if the type is a fieldname or not
                            this.JsonObjectState.RegisterToken(isFieldName ? JsonTokenType.FieldName : JsonTokenType.String);

                            this.binaryWriter.Write(rawJsonValue.Span);

                            if (!isFieldName)
                            {
                                this.bufferedContexts.Peek().Count++;
                            }
                        }
                        break;

                    case RawValueType.StrUsr:
                    case RawValueType.StrEncLen:
                    case RawValueType.StrL1:
                    case RawValueType.StrL2:
                    case RawValueType.StrL4:
                        this.WriteRawStringValue(rawType, rawJsonValue, isFieldName);
                        break;

                    case RawValueType.StrR1:
                        this.ForceRewriteRawJsonValue(
                            rootBuffer,
                            rootBuffer.Slice(JsonBinaryEncoding.GetFixedSizedValue<byte>(rawJsonValue.Slice(start: 1).Span)),
                            isFieldName);
                        break;
                    case RawValueType.StrR2:
                        this.ForceRewriteRawJsonValue(
                            rootBuffer,
                            rootBuffer.Slice(JsonBinaryEncoding.GetFixedSizedValue<ushort>(rawJsonValue.Slice(start: 1).Span)),
                            isFieldName);
                        break;
                    case RawValueType.StrR3:
                        this.ForceRewriteRawJsonValue(
                            rootBuffer,
                            rootBuffer.Slice(JsonBinaryEncoding.GetFixedSizedValue<JsonBinaryEncoding.UInt24>(rawJsonValue.Slice(start: 1).Span)),
                            isFieldName);
                        break;
                    case RawValueType.StrR4:
                        this.ForceRewriteRawJsonValue(
                            rootBuffer,
                            rootBuffer.Slice(JsonBinaryEncoding.GetFixedSizedValue<int>(rawJsonValue.Slice(start: 1).Span)),
                            isFieldName);
                        break;

                    case RawValueType.Arr1:
                        {
                            this.JsonObjectState.RegisterToken(JsonTokenType.BeginArray);

                            this.binaryWriter.Write(typeMarker);

                            this.ForceRewriteRawJsonValue(rootBuffer, rawJsonValue.Slice(start: 1), isFieldName: false);

                            this.JsonObjectState.RegisterToken(JsonTokenType.EndArray);
                        }
                        break;

                    case RawValueType.Obj1:
                        {
                            this.JsonObjectState.RegisterToken(JsonTokenType.BeginObject);

                            this.binaryWriter.Write(typeMarker);

                            this.ForceRewriteRawJsonValue(rootBuffer, rawJsonValue.Slice(start: 1), isFieldName: true);

                            int nameLength = JsonBinaryEncoding.GetValueLength(rawJsonValue.Slice(start: 1).Span);

                            this.ForceRewriteRawJsonValue(rootBuffer, rawJsonValue.Slice(start: 1 + nameLength), isFieldName: false);

                            this.JsonObjectState.RegisterToken(JsonTokenType.EndObject);
                        }
                        break;

                    case RawValueType.Arr:
                        {
                            this.WriteArrayStart();

                            foreach (ReadOnlyMemory<byte> arrayItem in JsonBinaryEncoding.Enumerator.GetArrayItems(rawJsonValue))
                            {
                                this.ForceRewriteRawJsonValue(rootBuffer, arrayItem, isFieldName);
                            }

                            this.WriteArrayEnd();
                        }
                        break;

                    case RawValueType.Obj:
                        {
                            this.WriteObjectStart();

                            foreach (JsonBinaryEncoding.Enumerator.ObjectProperty property in JsonBinaryEncoding.Enumerator.GetObjectProperties(rawJsonValue))
                            {
                                this.ForceRewriteRawJsonValue(rootBuffer, property.Name, isFieldName: true);
                                this.ForceRewriteRawJsonValue(rootBuffer, property.Value, isFieldName: false);
                            }

                            this.WriteObjectEnd();
                        }
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown {nameof(RawValueType)} {rawType}.");
                }
            }

            private void WriteRawStringValue(RawValueType rawValueType, ReadOnlyMemory<byte> buffer, bool isFieldName)
            {
                Utf8Span rawStringValue;
                switch (rawValueType)
                {
                    case RawValueType.StrUsr:
                        if (!JsonBinaryEncoding.TryGetDictionaryEncodedStringValue(
                            buffer.Span,
                            out UtfAllString value))
                        {
                            throw new InvalidOperationException("Failed to get dictionary encoded string value");
                        }

                        rawStringValue = value.Utf8String.Span;
                        break;

                    case RawValueType.StrEncLen:
                        long encodedStringLength = JsonBinaryEncoding.TypeMarker.GetEncodedStringLength(buffer.Span[0]);
                        if (encodedStringLength > int.MaxValue)
                        {
                            throw new InvalidOperationException("string is too long.");
                        }

                        rawStringValue = Utf8Span.UnsafeFromUtf8BytesNoValidation(buffer.Slice(start: JsonBinaryEncoding.TypeMarkerLength, (int)encodedStringLength).Span);
                        break;

                    case RawValueType.StrL1:
                        byte oneByteLength = JsonBinaryEncoding.GetFixedSizedValue<byte>(buffer.Slice(JsonBinaryEncoding.TypeMarkerLength).Span);
                        rawStringValue = Utf8Span.UnsafeFromUtf8BytesNoValidation(buffer.Slice(start: JsonBinaryEncoding.TypeMarkerLength + sizeof(byte), oneByteLength).Span);
                        break;

                    case RawValueType.StrL2:
                        ushort twoByteLength = JsonBinaryEncoding.GetFixedSizedValue<ushort>(buffer.Slice(JsonBinaryEncoding.TypeMarkerLength).Span);
                        rawStringValue = Utf8Span.UnsafeFromUtf8BytesNoValidation(buffer.Slice(start: JsonBinaryEncoding.TypeMarkerLength + sizeof(ushort), twoByteLength).Span);
                        break;

                    case RawValueType.StrL4:
                        uint fourByteLength = JsonBinaryEncoding.GetFixedSizedValue<uint>(buffer.Slice(JsonBinaryEncoding.TypeMarkerLength).Span);
                        if (fourByteLength > int.MaxValue)
                        {
                            throw new InvalidOperationException("string is too long.");
                        }

                        rawStringValue = Utf8Span.UnsafeFromUtf8BytesNoValidation(buffer.Slice(start: JsonBinaryEncoding.TypeMarkerLength + sizeof(uint), (int)fourByteLength).Span);
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown {nameof(rawValueType)}: {rawValueType}.");
                }

                this.WriteFieldNameOrString(isFieldName, rawStringValue);
            }

            private sealed class ArrayAndObjectInfo
            {
                public ArrayAndObjectInfo(long offset)
                {
                    this.Offset = offset;
                    this.Count = 0;
                    this.StringStartIndex = 0;
                }

                public long Offset { get; }

                public long Count { get; set; }

                public long StringStartIndex { get; set; }
            }

            private sealed class JsonBinaryMemoryWriter : JsonMemoryWriter
            {
                public JsonBinaryMemoryWriter(int initialCapacity = 256)
                    : base(initialCapacity)
                {
                }

                public void Write(byte value)
                {
                    this.EnsureRemainingBufferSpace(sizeof(byte));
                    this.buffer[this.Position] = value;
                    this.Position++;
                }

                public void Write(sbyte value)
                {
                    this.Write((byte)value);
                }

                public void Write(short value)
                {
                    this.EnsureRemainingBufferSpace(sizeof(short));
                    BinaryPrimitives.WriteInt16LittleEndian(this.Cursor, value);
                    this.Position += sizeof(short);
                }

                public void Write(ushort value)
                {
                    this.EnsureRemainingBufferSpace(sizeof(ushort));
                    BinaryPrimitives.WriteUInt16LittleEndian(this.Cursor, value);
                    this.Position += sizeof(ushort);
                }

                public void Write(JsonBinaryEncoding.UInt24 value)
                {
                    this.EnsureRemainingBufferSpace(size: 3);
                    this.Write(value.Byte1);
                    this.Write(value.Byte2);
                    this.Write(value.Byte3);
                }

                public void Write(int value)
                {
                    this.EnsureRemainingBufferSpace(sizeof(int));
                    BinaryPrimitives.WriteInt32LittleEndian(this.Cursor, value);
                    this.Position += sizeof(int);
                }

                public void Write(uint value)
                {
                    this.EnsureRemainingBufferSpace(sizeof(uint));
                    BinaryPrimitives.WriteUInt32LittleEndian(this.Cursor, value);
                    this.Position += sizeof(uint);
                }

                public void Write(long value)
                {
                    this.EnsureRemainingBufferSpace(sizeof(long));
                    BinaryPrimitives.WriteInt64LittleEndian(this.Cursor, value);
                    this.Position += sizeof(long);
                }

                public void Write(float value)
                {
                    this.EnsureRemainingBufferSpace(sizeof(float));
                    MemoryMarshal.Write<float>(this.Cursor, ref value);
                    this.Position += sizeof(float);
                }

                public void Write(double value)
                {
                    this.EnsureRemainingBufferSpace(sizeof(double));
                    MemoryMarshal.Write<double>(this.Cursor, ref value);
                    this.Position += sizeof(double);
                }

                public void Write(Guid value)
                {
                    int sizeOfGuid = Marshal.SizeOf(Guid.Empty);
                    this.EnsureRemainingBufferSpace(sizeOfGuid);
                    MemoryMarshal.Write<Guid>(this.Cursor, ref value);
                    this.Position += sizeOfGuid;
                }
            }

            private sealed class ReferenceStringDictionary
            {
                private readonly Dictionary<UInt128, ulong> stringLiteralDictionary;
                private readonly Dictionary<UInt128, ulong> stringHashDictionary;

                public ReferenceStringDictionary()
                {
                    this.stringLiteralDictionary = new Dictionary<UInt128, ulong>();
                    this.stringHashDictionary = new Dictionary<UInt128, ulong>();
                }

                public bool TryGetValue(ReadOnlySpan<byte> stringValue, out (UInt128 key, ulong value) keyValue)
                {
                    if (stringValue.Length < UInt128.Length)
                    {
                        // Literal strings
                        Span<byte> keyBuffer = stackalloc byte[16];
                        keyBuffer[0] = (byte)stringValue.Length;
                        stringValue.CopyTo(keyBuffer.Slice(start: 1));

                        UInt128 key = MemoryMarshal.Cast<byte, UInt128>(keyBuffer)[0];

                        if (!this.stringLiteralDictionary.TryGetValue(key, out ulong value))
                        {
                            keyValue = (key, value);
                            return false;
                        }

                        keyValue = (key, value);
                        return true;
                    }

                    // Hash strings
                    {
                        UInt128 key = MurmurHash3.Hash128(stringValue, seed: UInt128.Create((ulong)stringValue.Length, (ulong)stringValue.Length));
                        if (!this.stringHashDictionary.TryGetValue(key, out ulong value))
                        {
                            keyValue = (key, value);
                            return false;
                        }

                        keyValue = (key, value);
                        return true;
                    }
                }

                public void Add(int stringLength, UInt128 key, ulong value)
                {
                    if (stringLength < UInt128.Length)
                    {
                        this.stringLiteralDictionary.Add(key, value);
                    }
                    else
                    {
                        this.stringHashDictionary.Add(key, value);
                    }
                }
            }

            private readonly struct SharedStringValue
            {
                public SharedStringValue(int offset, int maxOffset)
                {
                    this.Offset = offset;
                    this.MaxOffset = maxOffset;
                }

                public int Offset { get; }
                public int MaxOffset { get; }
            }
        }
    }
}
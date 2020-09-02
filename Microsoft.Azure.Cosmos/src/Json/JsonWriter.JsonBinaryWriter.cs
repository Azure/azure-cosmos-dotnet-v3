//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
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
        /// <summary>
        /// Concrete implementation of <see cref="JsonWriter"/> that knows how to serialize to binary encoding.
        /// </summary>
        private sealed class JsonBinaryWriter : JsonWriter
        {
            private const int MaxStackAllocSize = 4 * 1024;

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
            private readonly Stack<BeginOffsetAndCount> bufferedContexts;

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

            /// <summary>
            /// The string dictionary used for user string encoding.
            /// </summary>
            private readonly JsonStringDictionary jsonStringDictionary;

            /// <summary>
            /// Initializes a new instance of the JsonBinaryWriter class.
            /// </summary>
            /// <param name="jsonStringDictionary">The JSON string dictionary used for user string encoding.</param>
            /// <param name="initialCapacity">The initial capacity to avoid intermediary allocations.</param>
            /// <param name="serializeCount">Whether to serialize the count for object and array typemarkers.</param>
            public JsonBinaryWriter(
                JsonStringDictionary jsonStringDictionary = null,
                int initialCapacity = 256,
                bool serializeCount = false)
            {
                this.binaryWriter = new JsonBinaryMemoryWriter(initialCapacity);
                this.bufferedContexts = new Stack<BeginOffsetAndCount>();
                this.serializeCount = serializeCount;
                this.reservationSize = JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.OneByteLength + (this.serializeCount ? JsonBinaryEncoding.OneByteCount : 0);

                // Write the serialization format as the very first byte
                byte binaryTypeMarker = (byte)JsonSerializationFormat.Binary;
                this.binaryWriter.Write(binaryTypeMarker);

                // Push on the outermost context
                this.bufferedContexts.Push(new BeginOffsetAndCount(this.CurrentLength));
                this.jsonStringDictionary = jsonStringDictionary;
            }

            /// <inheritdoc />
            public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Binary;

            /// <inheritdoc />
            public override long CurrentLength => this.binaryWriter.Position;

            /// <inheritdoc />
            public override void WriteObjectStart()
            {
                this.WriterArrayOrObjectStart(isArray: false);
            }

            /// <inheritdoc />
            public override void WriteObjectEnd()
            {
                this.WriteArrayOrObjectEnd(isArray: false);
            }

            /// <inheritdoc />
            public override void WriteArrayStart()
            {
                this.WriterArrayOrObjectStart(isArray: true);
            }

            /// <inheritdoc />
            public override void WriteArrayEnd()
            {
                this.WriteArrayOrObjectEnd(isArray: true);
            }

            /// <inheritdoc />
            public override void WriteFieldName(Utf8Span fieldName)
            {
                this.WriteFieldNameOrString(isFieldName: true, fieldName);
            }

            /// <inheritdoc />
            public override void WriteStringValue(Utf8Span value)
            {
                this.WriteFieldNameOrString(isFieldName: false, value);
            }

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

            /// <inheritdoc />
            public override void WriteRawJsonToken(
                JsonTokenType jsonTokenType,
                ReadOnlySpan<byte> rawJsonToken)
            {
                if (rawJsonToken == null)
                {
                    throw new ArgumentNullException(nameof(rawJsonToken));
                }

                switch (jsonTokenType)
                {
                    // Supported JsonTokenTypes
                    case JsonTokenType.String:
                    case JsonTokenType.Number:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                    case JsonTokenType.FieldName:
                    case JsonTokenType.Int8:
                    case JsonTokenType.Int16:
                    case JsonTokenType.Int32:
                    case JsonTokenType.UInt32:
                    case JsonTokenType.Int64:
                    case JsonTokenType.Float32:
                    case JsonTokenType.Float64:
                    case JsonTokenType.Guid:
                    case JsonTokenType.Binary:
                        break;
                    default:
                        throw new ArgumentException($"{nameof(JsonBinaryWriter)}.{nameof(WriteRawJsonToken)} can not write a {nameof(JsonTokenType)}: {jsonTokenType}");
                }

                this.JsonObjectState.RegisterToken(jsonTokenType);
                this.binaryWriter.Write(rawJsonToken);
                this.bufferedContexts.Peek().Count++;
            }

            private void WriterArrayOrObjectStart(bool isArray)
            {
                this.JsonObjectState.RegisterToken(isArray ? JsonTokenType.BeginArray : JsonTokenType.BeginObject);

                // Save the start index
                this.bufferedContexts.Push(new BeginOffsetAndCount(this.CurrentLength));

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
                BeginOffsetAndCount nestedContext = this.bufferedContexts.Pop();

                // Do some math
                int typeMarkerIndex = (int)nestedContext.Offset;
                int payloadIndex = typeMarkerIndex + this.reservationSize;
                int originalCursor = (int)this.CurrentLength;
                int payloadLength = originalCursor - payloadIndex;
                int count = (int)nestedContext.Count;

                // Figure out what the typemarker and length should be and do any corrections needed
                if (count == 0)
                {
                    // Empty object

                    // Move the cursor back
                    this.binaryWriter.Position = typeMarkerIndex;

                    // Write the type marker
                    this.binaryWriter.Write(
                        isArray ? JsonBinaryEncoding.TypeMarker.EmptyArray : JsonBinaryEncoding.TypeMarker.EmptyObject);
                }
                else if (count == 1)
                {
                    // Single-property object

                    // Move the buffer back but leave one byte for the typemarker
                    Span<byte> buffer = this.binaryWriter.BufferAsSpan;
                    buffer.Slice(payloadIndex).CopyTo(buffer.Slice(typeMarkerIndex + JsonBinaryEncoding.TypeMarkerLength));

                    // Move the cursor back
                    this.binaryWriter.Position = typeMarkerIndex;

                    // Write the type marker
                    this.binaryWriter.Write(
                        isArray ? JsonBinaryEncoding.TypeMarker.SingleItemArray : JsonBinaryEncoding.TypeMarker.SinglePropertyObject);

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
                            this.binaryWriter.Write(
                                isArray ? JsonBinaryEncoding.TypeMarker.Array1ByteLengthAndCount : JsonBinaryEncoding.TypeMarker.Object1ByteLengthAndCount);
                            this.binaryWriter.Write((byte)payloadLength);
                            this.binaryWriter.Write((byte)count);
                        }
                        else
                        {
                            this.binaryWriter.Write(
                                isArray ? JsonBinaryEncoding.TypeMarker.Array1ByteLength : JsonBinaryEncoding.TypeMarker.Object1ByteLength);
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
                        Span<byte> payload = buffer.Slice(payloadIndex, payloadLength);
                        Span<byte> newPayloadStart = buffer.Slice(typeMarkerIndex + bytesToWrite);
                        payload.CopyTo(newPayloadStart);

                        // Move the cursor back
                        this.binaryWriter.Position = typeMarkerIndex;

                        // Write the type marker
                        if (this.serializeCount)
                        {
                            this.binaryWriter.Write(
                                isArray ? JsonBinaryEncoding.TypeMarker.Array2ByteLengthAndCount : JsonBinaryEncoding.TypeMarker.Object2ByteLengthAndCount);
                            this.binaryWriter.Write((ushort)payloadLength);
                            this.binaryWriter.Write((ushort)count);
                        }
                        else
                        {
                            this.binaryWriter.Write(
                                isArray ? JsonBinaryEncoding.TypeMarker.Array2ByteLength : JsonBinaryEncoding.TypeMarker.Object2ByteLength);
                            this.binaryWriter.Write((ushort)payloadLength);
                        }

                        // Move the cursor forward
                        this.binaryWriter.Position = typeMarkerIndex + bytesToWrite + payloadLength;
                    }
                    else
                    {
                        // (payloadLength <= uint.MaxValue)

                        // 4 byte length - make space for an extra 2 byte length (and 2 byte count)
                        this.binaryWriter.Write((ushort)0);
                        if (this.serializeCount)
                        {
                            this.binaryWriter.Write((ushort)0);
                        }

                        // Move the buffer forward
                        Span<byte> buffer = this.binaryWriter.BufferAsSpan;
                        int bytesToWrite = JsonBinaryEncoding.TypeMarkerLength
                            + JsonBinaryEncoding.FourByteLength
                            + (this.serializeCount ? JsonBinaryEncoding.FourByteCount : 0);
                        Span<byte> payload = buffer.Slice(payloadIndex, payloadLength);
                        Span<byte> newPayloadStart = buffer.Slice(typeMarkerIndex + bytesToWrite);
                        payload.CopyTo(newPayloadStart);

                        // Move the cursor back
                        this.binaryWriter.Position = typeMarkerIndex;

                        // Write the type marker
                        if (this.serializeCount)
                        {
                            this.binaryWriter.Write(
                                isArray ? JsonBinaryEncoding.TypeMarker.Array4ByteLengthAndCount : JsonBinaryEncoding.TypeMarker.Object4ByteLengthAndCount);
                            this.binaryWriter.Write((uint)payloadLength);
                            this.binaryWriter.Write((uint)count);
                        }
                        else
                        {
                            this.binaryWriter.Write(
                                isArray ? JsonBinaryEncoding.TypeMarker.Array4ByteLength : JsonBinaryEncoding.TypeMarker.Object4ByteLength);
                            this.binaryWriter.Write((uint)payloadLength);
                        }

                        // Move the cursor forward
                        this.binaryWriter.Position = typeMarkerIndex + bytesToWrite + payloadLength;
                    }
                }

                this.bufferedContexts.Peek().Count++;
            }

            private void WriteFieldNameOrString(bool isFieldName, Utf8Span utf8Span)
            {
                // String dictionary encoding is currently performed only for field names. 
                // This would be changed later, so that the writer can control which strings need to be encoded.
                this.JsonObjectState.RegisterToken(isFieldName ? JsonTokenType.FieldName : JsonTokenType.String);
                if (JsonBinaryEncoding.TryGetEncodedStringTypeMarker(
                    utf8Span,
                    this.JsonObjectState.CurrentTokenType == JsonTokenType.FieldName ? this.jsonStringDictionary : null,
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
                            throw new ArgumentOutOfRangeException($"Unable to serialize a {nameof(JsonBinaryEncoding.MultiByteTypeMarker)} of length: {multiByteTypeMarker.Length}");
                    }
                }
                else
                {
                    // See if the string length can be encoded into a single type marker
                    byte typeMarker = JsonBinaryEncoding.TypeMarker.GetEncodedStringLengthTypeMarker(utf8Span.Length);
                    if (JsonBinaryEncoding.TypeMarker.IsValid(typeMarker))
                    {
                        this.binaryWriter.Write(typeMarker);
                    }
                    else
                    {
                        // Just write the type marker and the corresponding length
                        if (utf8Span.Length < byte.MaxValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.String1ByteLength);
                            this.binaryWriter.Write((byte)utf8Span.Length);
                        }
                        else if (utf8Span.Length < ushort.MaxValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.String2ByteLength);
                            this.binaryWriter.Write((ushort)utf8Span.Length);
                        }
                        else
                        {
                            // (utf8String.Length < uint.MaxValue)
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.String4ByteLength);
                            this.binaryWriter.Write((uint)utf8Span.Length);
                        }
                    }

                    // Finally write the string itself.
                    this.binaryWriter.Write(utf8Span.Span);
                }

                if (!isFieldName)
                {
                    // If we just wrote a string then increment the count (we don't increment for field names, since we need to wait for the corresponding property value).
                    this.bufferedContexts.Peek().Count++;
                }
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

            private sealed class BeginOffsetAndCount
            {
                public BeginOffsetAndCount(long offset)
                {
                    this.Offset = offset;
                    this.Count = 0;
                }

                public long Offset { get; }

                public long Count { get; set; }
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
        }
    }
}
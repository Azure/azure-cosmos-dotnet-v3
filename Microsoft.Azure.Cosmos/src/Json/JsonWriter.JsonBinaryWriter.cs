//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

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
            private const int TypeMarker = 1;
            private const int OneByteLength = 1;
            private const int OneByteCount = 1;
            private const int TwoByteLength = 2;
            private const int TwoByteCount = 2;
            private const int FourByteLength = 4;
            private const int FourByteCount = 4;

            /// <summary>
            /// Writer used to write fully materialized context to the internal stream.
            /// </summary>
            private readonly BinaryWriter binaryWriter;

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
            /// <param name="skipValidation">Whether to skip validation on the JsonObjectState.</param>
            /// <param name="jsonStringDictionary">The JSON string dictionary used for user string encoding.</param>
            /// <param name="serializeCount">Whether to serialize the count for object and array typemarkers.</param>
            public JsonBinaryWriter(
                bool skipValidation,
                JsonStringDictionary jsonStringDictionary = null,
                bool serializeCount = false)
                : base(skipValidation)
            {
                this.binaryWriter = new BinaryWriter(new MemoryStream());
                this.bufferedContexts = new Stack<BeginOffsetAndCount>();
                this.serializeCount = serializeCount;
                this.reservationSize = TypeMarker + TwoByteLength + (this.serializeCount ? TwoByteCount : 0);

                // Write the serialization format as the very first byte
                this.binaryWriter.Write((byte)JsonSerializationFormat.Binary);

                // Push on the outermost context
                this.bufferedContexts.Push(new BeginOffsetAndCount(this.CurrentLength));
                this.jsonStringDictionary = jsonStringDictionary;
            }

            /// <summary>
            /// Gets the SerializationFormat of the JsonWriter.
            /// </summary>
            public override JsonSerializationFormat SerializationFormat
            {
                get
                {
                    return JsonSerializationFormat.Binary;
                }
            }

            /// <summary>
            /// Gets the current length of the internal buffer.
            /// </summary>
            public override long CurrentLength
            {
                get
                {
                    this.binaryWriter.Flush();
                    return this.binaryWriter.BaseStream.Position;
                }
            }

            /// <summary>
            /// Writes the object start symbol to internal buffer.
            /// </summary>
            public override void WriteObjectStart()
            {
                this.WriterArrayOrObjectStart(false);
            }

            /// <summary>
            /// Writes the object end symbol to the internal buffer.
            /// </summary>
            public override void WriteObjectEnd()
            {
                this.WriteArrayOrObjectEnd(false);
            }

            /// <summary>
            /// Writes the array start symbol to the internal buffer.
            /// </summary>
            public override void WriteArrayStart()
            {
                this.WriterArrayOrObjectStart(true);
            }

            /// <summary>
            /// Writes the array end token to the internal buffer.
            /// </summary>
            public override void WriteArrayEnd()
            {
                this.WriteArrayOrObjectEnd(true);
            }

            /// <summary>
            /// Writes a field name to the the internal buffer.
            /// </summary>
            /// <param name="fieldName">The name of the field to write.</param>
            public override void WriteFieldName(string fieldName)
            {
                this.WriteFieldNameOrString(true, fieldName);
            }

            /// <summary>
            /// Writes a string to the internal buffer.
            /// </summary>
            /// <param name="value">The value of the string to write.</param>
            public override void WriteStringValue(string value)
            {
                this.WriteFieldNameOrString(false, value);
            }

            /// <summary>
            /// Writes an integer to the internal buffer.
            /// </summary>
            /// <param name="value">The value of the integer to write.</param>
            public override void WriteIntValue(long value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Number);

                this.WriteIntegerInternal(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <summary>
            /// Writes a number to the internal buffer.
            /// </summary>
            /// <param name="value">The value of the number to write.</param>
            public override void WriteNumberValue(double value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Number);

                // The maximum integer value that can be stored in an IEEE 754 double type w/o losing precision
                const double MaxFullPrecisionValue = ((long)1) << 53;

                // Check if the number is an integer
                double truncatedValue = Math.Floor(value);
                if ((truncatedValue == value) && (value >= -MaxFullPrecisionValue) && (value <= MaxFullPrecisionValue))
                {
                    // The number does not have any decimals and fits in a 64-bit value
                    this.WriteIntegerInternal((long)value);
                }
                else
                {
                    this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Double);
                    this.binaryWriter.Write(value);
                }

                this.bufferedContexts.Peek().Count++;
            }

            /// <summary>
            /// Writes a boolean to the internal buffer.
            /// </summary>
            /// <param name="value">The value of the boolean to write.</param>
            public override void WriteBoolValue(bool value)
            {
                this.JsonObjectState.RegisterToken(value ? JsonTokenType.True : JsonTokenType.False);
                this.binaryWriter.Write(value ? JsonBinaryEncoding.TypeMarker.True : JsonBinaryEncoding.TypeMarker.False);
                this.bufferedContexts.Peek().Count++;
            }

            /// <summary>
            /// Writes a null to the internal buffer.
            /// </summary>
            public override void WriteNullValue()
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Null);
                this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Null);
                this.bufferedContexts.Peek().Count++;
            }

            public override void WriteInt8Value(sbyte value)
            {
                throw new NotImplementedException();
            }

            public override void WriteInt16Value(short value)
            {
                throw new NotImplementedException();
            }

            public override void WriteInt32Value(int value)
            {
                throw new NotImplementedException();
            }

            public override void WriteInt64Value(long value)
            {
                throw new NotImplementedException();
            }

            public override void WriteFloat32Value(float value)
            {
                throw new NotImplementedException();
            }

            public override void WriteFloat64Value(double value)
            {
                throw new NotImplementedException();
            }

            public override void WriteUInt32Value(uint value)
            {
                throw new NotImplementedException();
            }

            public override void WriteGuidValue(Guid value)
            {
                throw new NotImplementedException();
            }

            public override void WriteBinaryValue(IReadOnlyList<byte> value)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Gets the result of the JsonWriter.
            /// </summary>
            /// <returns>The result of the JsonWriter as an array of bytes.</returns>
            public override byte[] GetResult()
            {
                if (this.bufferedContexts.Count > 1)
                {
                    throw new JsonNotCompleteException();
                }

                this.binaryWriter.Flush();
                long bytesWritten = this.CurrentLength;
                byte[] result = new byte[bytesWritten];
                this.binaryWriter.BaseStream.Seek(0, SeekOrigin.Begin);

                if (bytesWritten > int.MaxValue)
                {
                    throw new InvalidOperationException("Can not get back a buffer more than int.MaxValue");
                }

                this.binaryWriter.BaseStream.Read(result, 0, (int)bytesWritten);
                return result;
            }

            /// <summary>
            /// Writes a raw json token to the internal buffer.
            /// </summary>
            /// <param name="jsonTokenType">The JsonTokenType of the rawJsonToken</param>
            /// <param name="rawJsonToken">The raw json token.</param>
            protected override void WriteRawJsonToken(
                JsonTokenType jsonTokenType,
                IReadOnlyList<byte> rawJsonToken)
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
                        break;
                    default:
                        throw new ArgumentException($"{nameof(JsonBinaryWriter)}.{nameof(WriteRawJsonToken)} can not write a {nameof(JsonTokenType)}: {jsonTokenType}");
                }

                this.JsonObjectState.RegisterToken(jsonTokenType);

                if (rawJsonToken is ArraySegment<byte> jsonArraySegment)
                {
                    this.binaryWriter.Write(jsonArraySegment.Array, jsonArraySegment.Offset, jsonArraySegment.Count);
                }
                else
                {
                    this.binaryWriter.Write(rawJsonToken.ToArray());
                }

                this.bufferedContexts.Peek().Count++;
            }

            private void WriterArrayOrObjectStart(bool isArray)
            {
                this.JsonObjectState.RegisterToken(isArray ? JsonTokenType.BeginArray : JsonTokenType.BeginObject);

                // Save the start index
                this.bufferedContexts.Push(new BeginOffsetAndCount(this.CurrentLength));

                // Assume 2-byte value length; as such, we need to reserve upto 5 bytes (1 byte type marker, 2 byte length, 2 byte count).
                // We'll adjust this as needed when writing the end of the array/object.
                this.binaryWriter.Write((byte)0);
                this.binaryWriter.Write((ushort)0);
                if (this.serializeCount)
                {
                    this.binaryWriter.Write((ushort)0);
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
                    this.binaryWriter.BaseStream.Seek(typeMarkerIndex, SeekOrigin.Begin);

                    // Write the type marker
                    this.binaryWriter.Write(
                        isArray ? JsonBinaryEncoding.TypeMarker.EmptyArray : JsonBinaryEncoding.TypeMarker.EmptyObject);
                }
                else if (count == 1)
                {
                    // Single-property object

                    // Move the buffer back 2 or 4 bytes since we don't need to encode a length
                    byte[] buffer = ((MemoryStream)this.binaryWriter.BaseStream).GetBuffer();
                    Array.Copy(buffer, payloadIndex, buffer, payloadIndex - (TwoByteLength + (this.serializeCount ? TwoByteCount : 0)), payloadLength);

                    // Move the cursor back
                    this.binaryWriter.BaseStream.Seek(typeMarkerIndex, SeekOrigin.Begin);

                    // Write the type marker
                    this.binaryWriter.Write(
                        isArray ? JsonBinaryEncoding.TypeMarker.SingleItemArray : JsonBinaryEncoding.TypeMarker.SinglePropertyObject);

                    // Move the cursor forward
                    this.binaryWriter.BaseStream.Seek(typeMarkerIndex + TypeMarker + payloadLength, SeekOrigin.Begin);
                }
                else
                {
                    // Need to figure out how many bytes to encode the length and the count
                    if (payloadLength <= byte.MaxValue)
                    {
                        // 1 byte length - move the buffer back
                        byte[] buffer = ((MemoryStream)this.binaryWriter.BaseStream).GetBuffer();
                        Array.Copy(buffer, payloadIndex, buffer, payloadIndex - (OneByteLength + (this.serializeCount ? OneByteCount : 0)), payloadLength);

                        // Move the cursor back
                        this.binaryWriter.BaseStream.Seek(typeMarkerIndex, SeekOrigin.Begin);

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
                        this.binaryWriter.BaseStream.Seek(typeMarkerIndex + TypeMarker + OneByteLength + (this.serializeCount ? OneByteCount : 0) + payloadLength, SeekOrigin.Begin);
                    }
                    else if (payloadLength <= ushort.MaxValue)
                    {
                        // 2 byte length - don't need to move the buffer
                        // Move the cursor back
                        this.binaryWriter.BaseStream.Seek(typeMarkerIndex, SeekOrigin.Begin);

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
                        this.binaryWriter.BaseStream.Seek(typeMarkerIndex + TypeMarker + TwoByteLength + (this.serializeCount ? TwoByteCount : 0) + payloadLength, SeekOrigin.Begin);
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
                        byte[] buffer = ((MemoryStream)this.binaryWriter.BaseStream).GetBuffer();
                        Array.Copy(buffer, payloadIndex, buffer, payloadIndex + TwoByteLength + (this.serializeCount ? TwoByteCount : 0), payloadLength);

                        // Move the cursor back
                        this.binaryWriter.BaseStream.Seek(typeMarkerIndex, SeekOrigin.Begin);

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
                        this.binaryWriter.BaseStream.Seek(typeMarkerIndex + TypeMarker + FourByteLength + (this.serializeCount ? FourByteCount : 0) + payloadLength, SeekOrigin.Begin);
                    }
                }

                this.bufferedContexts.Peek().Count++;
            }

            private void WriteFieldNameOrString(bool isFieldName, string value)
            {
                // String dictionary encoding is currently performed only for field names. 
                // This would be changed later, so that the writer can control which strings need to be encoded.
                this.JsonObjectState.RegisterToken(isFieldName ? JsonTokenType.FieldName : JsonTokenType.String);
                if (JsonBinaryEncoding.TryGetEncodedStringTypeMarker(
                    value,
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
                    byte[] utf8String = Encoding.UTF8.GetBytes(value);

                    // See if the string length can be encoded into a single type marker
                    byte typeMarker = JsonBinaryEncoding.TypeMarker.GetEncodedStringLengthTypeMarker(utf8String.Length);
                    if (JsonBinaryEncoding.TypeMarker.IsValid(typeMarker))
                    {
                        this.binaryWriter.Write(typeMarker);
                    }
                    else
                    {
                        // Just write the type marker and the corresponding length
                        if (utf8String.Length < byte.MaxValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.String1ByteLength);
                            this.binaryWriter.Write((byte)utf8String.Length);
                        }
                        else if (utf8String.Length < ushort.MaxValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.String2ByteLength);
                            this.binaryWriter.Write((ushort)utf8String.Length);
                        }
                        else
                        {
                            // (utf8String.Length < uint.MaxValue)
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.String4ByteLength);
                            this.binaryWriter.Write((uint)utf8String.Length);
                        }
                    }

                    // Finally write the string itself.
                    this.binaryWriter.Write(utf8String);
                }

                if (!isFieldName)
                {
                    // If we just wrote a string then increment the count (we don't increment for field names, since we need to wait for the corresponding property value).
                    this.bufferedContexts.Peek().Count++;
                }
            }

            private void WriteIntegerInternal(long value)
            {
                if (JsonBinaryEncoding.TypeMarker.IsEncodedIntegerLiteral(value))
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
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.UInt8);
                            this.binaryWriter.Write((byte)value);
                        }
                        else if (value <= short.MaxValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Int16);
                            this.binaryWriter.Write((short)value);
                        }
                        else if (value <= int.MaxValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Int32);
                            this.binaryWriter.Write((int)value);
                        }
                        else
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Int64);
                            this.binaryWriter.Write((long)value);
                        }
                    }
                    else
                    {
                        // Negative Number
                        if (value < int.MinValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Int64);
                            this.binaryWriter.Write((long)value);
                        }
                        else if (value < short.MinValue)
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Int32);
                            this.binaryWriter.Write((int)value);
                        }
                        else
                        {
                            this.binaryWriter.Write(JsonBinaryEncoding.TypeMarker.Int16);
                            this.binaryWriter.Write((short)value);
                        }
                    }
                }
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
        }
    }
}
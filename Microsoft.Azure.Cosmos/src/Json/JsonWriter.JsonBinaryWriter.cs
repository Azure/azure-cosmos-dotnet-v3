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
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using static Microsoft.Azure.Cosmos.Json.JsonBinaryEncoding;

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
        /// Executes the provided lambda and captures a copy of the written bytes for reuse.
        /// The lambda is executed at a field name, and should leave the reader in a state where
        /// it is valid to end the scope.
        /// </summary>
        /// <param name="scopeWriter">Writes the contents of the scope.</param>
        /// <returns>Blitted bytes.</returns>
        public static PreblittedBinaryJsonScope CapturePreblittedBinaryJsonScope(Action<ITypedBinaryJsonWriter> scopeWriter)
        {
            JsonBinaryWriter jsonBinaryWriter = new JsonBinaryWriter(
                initialCapacity: 256,
                enableNumberArrays: false,
                enableEncodedStrings: false);
            Contract.Requires(!jsonBinaryWriter.JsonObjectState.InArrayContext);
            Contract.Requires(!jsonBinaryWriter.JsonObjectState.InObjectContext);
            Contract.Requires(!jsonBinaryWriter.JsonObjectState.IsPropertyExpected);

            jsonBinaryWriter.WriteObjectStart();
            jsonBinaryWriter.WriteFieldName("someFieldName");
            int startPosition = (int)jsonBinaryWriter.CurrentLength;
            scopeWriter(jsonBinaryWriter);

            return jsonBinaryWriter.CapturePreblittedBinaryJsonScope(startPosition);
        }

        /// <summary>
        /// Concrete implementation of <see cref="JsonWriter"/> that knows how to serialize to binary encoding.
        /// </summary>
        private sealed class JsonBinaryWriter :
            JsonWriter,
            IJsonBinaryWriterExtensions,
            ITypedBinaryJsonWriter
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
                ArrNum,
                ArrArrNum,
                UInt64,
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

                // Array and Object Special Type Markers
                RawValueType.ArrNum,     // ArrNumC1 Uniform number array of 1-byte item count
                RawValueType.ArrNum,     // ArrNumC2 Uniform number array of 2-byte item count
                RawValueType.ArrArrNum,  // Array of 1-byte item count of Uniform number array of 1-byte item count
                RawValueType.ArrArrNum,  // Array of 2-byte item count of Uniform number array of 2-byte item count
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
            /// '$t' encoded string.
            /// </summary>
            private static readonly byte DollarTSystemString =
                (byte)(TypeMarker.SystemString1ByteLengthMin
                + JsonBinaryEncoding.SystemStrings.GetSystemStringId(Utf8Span.TranscodeUtf16("$t")).Value);

            /// <summary>
            /// '$v' encoded string.
            /// </summary>
            private static readonly byte DollarVSystemString =
                (byte)(TypeMarker.SystemString1ByteLengthMin
                + JsonBinaryEncoding.SystemStrings.GetSystemStringId(Utf8Span.TranscodeUtf16("$v")).Value);

            /// <summary>
            /// Determines whether to enable reference string encoding.
            /// </summary>
            private readonly bool enableEncodedStrings;

            /// <summary>
            /// Determines whether to allow writing of uniform number arrays.
            /// </summary>
            private readonly bool enableNumberArrays;

            /// <summary>
            /// Writer used to write fully materialized context to the internal stream.
            /// </summary>
            private readonly JsonBinaryMemoryWriter binaryWriter;

            /// <summary>
            /// With binary encoding all the JSON elements are length prefixed,
            /// unfortunately the caller of this class only provides what tokens to write.
            /// This means that whenever a user call WriteObject/ArrayStart we don't know the length of said object or array
            /// until WriteObject/ArrayEnd is invoked.
            /// To get around this we reserve some space for the length and write to it when the user supplies the end token.
            /// This stack remembers for each nesting level where it begins and how many items it has.
            /// </summary>
            private readonly Stack<ArrayAndObjectInfo> bufferedContexts;

            /// <summary>
            /// When a user writes an open array or object we reserve this much space for the type marker + length + count
            /// And correct it later when they write a close array or object.
            /// </summary>
            private readonly int reservationSize;

            private readonly List<SharedStringValue> sharedStrings;

            private readonly ReferenceStringDictionary sharedStringIndexes;

            /// <summary>
            /// Offsets at which string references offsets are stored.
            /// </summary>
            private readonly List<int> stringReferenceOffsets;

            /// <summary>
            /// Initializes a new instance of the JsonBinaryWriter class.
            /// </summary>
            /// <param name="initialCapacity">The initial capacity to avoid intermediary allocations.</param>
            /// <param name="enableNumberArrays">Determines whether to enable writing of uniform number arrays.</param>
            /// <param name="enableEncodedStrings">Determines whether to enable reference string encoding.</param>
            public JsonBinaryWriter(
                int initialCapacity,
                bool enableNumberArrays,
                bool enableEncodedStrings = true)
            {
                this.enableNumberArrays = enableNumberArrays;
                this.enableEncodedStrings = enableEncodedStrings;
                this.binaryWriter = new JsonBinaryMemoryWriter(initialCapacity);
                this.bufferedContexts = new Stack<ArrayAndObjectInfo>();
                this.reservationSize = TypeMarkerLength + JsonBinaryEncoding.OneByteLength;
                this.sharedStrings = new List<SharedStringValue>();
                this.sharedStringIndexes = new ReferenceStringDictionary();
                this.stringReferenceOffsets = new List<int>();

                // Write the serialization format as the very first byte
                byte binaryTypeMarker = (byte)JsonSerializationFormat.Binary;
                this.binaryWriter.Write(binaryTypeMarker);

                // Push on the outermost context
                this.bufferedContexts.Push(new ArrayAndObjectInfo(this.CurrentLength, stringStartIndex: 0, stringReferenceStartIndex: 0, valueCount: 0));
            }

            /// <inheritdoc />
            public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Binary;

            /// <inheritdoc />
            public override long CurrentLength => this.binaryWriter.Position;

            /// <inheritdoc />
            public override void WriteObjectStart() => this.WriteArrayOrObjectStart(isArray: false);

            /// <inheritdoc />
            public override void WriteObjectEnd() => this.WriteArrayOrObjectEnd(isArray: false);

            /// <inheritdoc />
            public override void WriteArrayStart() => this.WriteArrayOrObjectStart(isArray: true);

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
                this.binaryWriter.Write(value ? TypeMarker.True : TypeMarker.False);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteNullValue()
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Null);
                this.binaryWriter.Write(TypeMarker.Null);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteInt8Value(sbyte value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Int8);
                this.binaryWriter.Write(TypeMarker.Int8);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteInt16Value(short value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Int16);
                this.binaryWriter.Write(TypeMarker.Int16);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteInt32Value(int value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Int32);
                this.binaryWriter.Write(TypeMarker.Int32);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteInt64Value(long value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Int64);
                this.binaryWriter.Write(TypeMarker.Int64);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteFloat32Value(float value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Float32);
                this.binaryWriter.Write(TypeMarker.Float32);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteFloat64Value(double value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Float64);
                this.binaryWriter.Write(TypeMarker.Float64);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteUInt32Value(uint value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.UInt32);
                this.binaryWriter.Write(TypeMarker.UInt32);
                this.binaryWriter.Write(value);
                this.bufferedContexts.Peek().Count++;
            }

            /// <inheritdoc />
            public override void WriteGuidValue(Guid value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Guid);
                this.binaryWriter.Write(TypeMarker.Guid);
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
                    this.binaryWriter.Write(TypeMarker.Binary1ByteLength);
                    this.binaryWriter.Write((byte)length);
                }
                else if ((length & ~0xFFFF) == 0)
                {
                    this.binaryWriter.Write(TypeMarker.Binary2ByteLength);
                    this.binaryWriter.Write((ushort)length);
                }
                else if ((length & ~0xFFFFFFFFL) == 0)
                {
                    this.binaryWriter.Write(TypeMarker.Binary4ByteLength);
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
            public override void WriteNumberArray(IReadOnlyList<byte> values)
            {
                if (!this.enableNumberArrays || (values.Count == 0) || (values.Count > ushort.MaxValue))
                {
                    base.WriteNumberArray(values);
                }
                else
                {
                    this.JsonObjectState.RegisterToken(JsonTokenType.Null);

                    if (values.Count <= byte.MaxValue)
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC1);
                        this.binaryWriter.Write(TypeMarker.UInt8);
                        this.binaryWriter.Write((byte)values.Count);
                    }
                    else
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC2);
                        this.binaryWriter.Write(TypeMarker.UInt8);
                        this.binaryWriter.Write((ushort)values.Count);
                    }

                    foreach (byte value in values)
                    {
                        this.binaryWriter.Write(value);
                    }

                    this.bufferedContexts.Peek().Count++;
                }
            }

            /// <inheritdoc />
            public override void WriteNumberArray(IReadOnlyList<sbyte> values)
            {
                if (!this.enableNumberArrays || (values.Count == 0) || (values.Count > ushort.MaxValue))
                {
                    base.WriteNumberArray(values);
                }
                else
                {
                    this.JsonObjectState.RegisterToken(JsonTokenType.Null);

                    if (values.Count <= byte.MaxValue)
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC1);
                        this.binaryWriter.Write(TypeMarker.Int8);
                        this.binaryWriter.Write((byte)values.Count);
                    }
                    else
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC2);
                        this.binaryWriter.Write(TypeMarker.Int8);
                        this.binaryWriter.Write((ushort)values.Count);
                    }

                    foreach (sbyte value in values)
                    {
                        this.binaryWriter.Write(value);
                    }

                    this.bufferedContexts.Peek().Count++;
                }
            }

            /// <inheritdoc />
            public override void WriteNumberArray(IReadOnlyList<short> values)
            {
                if (!this.enableNumberArrays || (values.Count == 0) || (values.Count > ushort.MaxValue))
                {
                    base.WriteNumberArray(values);
                }
                else
                {
                    this.JsonObjectState.RegisterToken(JsonTokenType.Null);

                    if (values.Count <= byte.MaxValue)
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC1);
                        this.binaryWriter.Write(TypeMarker.Int16);
                        this.binaryWriter.Write((byte)values.Count);
                    }
                    else
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC2);
                        this.binaryWriter.Write(TypeMarker.Int16);
                        this.binaryWriter.Write((ushort)values.Count);
                    }

                    foreach (short value in values)
                    {
                        this.binaryWriter.Write(value);
                    }

                    this.bufferedContexts.Peek().Count++;
                }
            }

            /// <inheritdoc />
            public override void WriteNumberArray(IReadOnlyList<int> values)
            {
                if (!this.enableNumberArrays || (values.Count == 0) || (values.Count > ushort.MaxValue))
                {
                    base.WriteNumberArray(values);
                }
                else
                {
                    this.JsonObjectState.RegisterToken(JsonTokenType.Null);

                    if (values.Count <= byte.MaxValue)
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC1);
                        this.binaryWriter.Write(TypeMarker.Int32);
                        this.binaryWriter.Write((byte)values.Count);
                    }
                    else
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC2);
                        this.binaryWriter.Write(TypeMarker.Int32);
                        this.binaryWriter.Write((ushort)values.Count);
                    }

                    foreach (int value in values)
                    {
                        this.binaryWriter.Write(value);
                    }

                    this.bufferedContexts.Peek().Count++;
                }
            }

            /// <inheritdoc />
            public override void WriteNumberArray(IReadOnlyList<long> values)
            {
                if (!this.enableNumberArrays || (values.Count == 0) || (values.Count > ushort.MaxValue))
                {
                    base.WriteNumberArray(values);
                }
                else
                {
                    this.JsonObjectState.RegisterToken(JsonTokenType.Null);

                    if (values.Count <= byte.MaxValue)
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC1);
                        this.binaryWriter.Write(TypeMarker.Int64);
                        this.binaryWriter.Write((byte)values.Count);
                    }
                    else
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC2);
                        this.binaryWriter.Write(TypeMarker.Int64);
                        this.binaryWriter.Write((ushort)values.Count);
                    }

                    foreach (long value in values)
                    {
                        this.binaryWriter.Write(value);
                    }

                    this.bufferedContexts.Peek().Count++;
                }
            }

            /// <inheritdoc />
            public override void WriteNumberArray(IReadOnlyList<float> values)
            {
                if (!this.enableNumberArrays || (values.Count == 0) || (values.Count > ushort.MaxValue))
                {
                    base.WriteNumberArray(values);
                }
                else
                {
                    this.JsonObjectState.RegisterToken(JsonTokenType.Null);

                    if (values.Count <= byte.MaxValue)
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC1);
                        this.binaryWriter.Write(TypeMarker.Float32);
                        this.binaryWriter.Write((byte)values.Count);
                    }
                    else
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC2);
                        this.binaryWriter.Write(TypeMarker.Float32);
                        this.binaryWriter.Write((ushort)values.Count);
                    }

                    foreach (float value in values)
                    {
                        this.binaryWriter.Write(value);
                    }

                    this.bufferedContexts.Peek().Count++;
                }
            }

            /// <inheritdoc />
            public override void WriteNumberArray(IReadOnlyList<double> values)
            {
                if (!this.enableNumberArrays || (values.Count == 0) || (values.Count > ushort.MaxValue))
                {
                    base.WriteNumberArray(values);
                }
                else
                {
                    this.JsonObjectState.RegisterToken(JsonTokenType.Null);

                    if (values.Count <= byte.MaxValue)
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC1);
                        this.binaryWriter.Write(TypeMarker.Float64);
                        this.binaryWriter.Write((byte)values.Count);
                    }
                    else
                    {
                        this.binaryWriter.Write(TypeMarker.ArrNumC2);
                        this.binaryWriter.Write(TypeMarker.Float64);
                        this.binaryWriter.Write((ushort)values.Count);
                    }

                    foreach (double value in values)
                    {
                        this.binaryWriter.Write(value);
                    }

                    this.bufferedContexts.Peek().Count++;
                }
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
            public void WriteDollarTBsonTypeDollarV(byte cosmosBsonTypeByte)
            {
                const int totalSize =
                    sizeof(byte) // Typemaker byte
                    + sizeof(byte) // Length byte reservation
                    + sizeof(byte) // $t
                    + sizeof(byte) // cosmos bson type
                    + sizeof(byte); // $v

                this.binaryWriter.EnsureRemainingBufferSpace(totalSize);
                this.RegisterArrayOrObjectStart(isArray: false, this.binaryWriter.Position, valueCount: 1);
                this.JsonObjectState.RegisterFieldName();

                Span<byte> binaryWriterCursor = this.binaryWriter.Cursor.Slice(2);
                binaryWriterCursor[0] = JsonBinaryWriter.DollarTSystemString;
                binaryWriterCursor[1] = cosmosBsonTypeByte;
                binaryWriterCursor[2] = JsonBinaryWriter.DollarVSystemString;
                this.binaryWriter.Position += totalSize;
            }

            /// <inheritdoc />
            public void WriteDollarTBsonTypeDollarVNestedScope(bool isNestedArray, byte cosmosBsonTypeByte)
            {
                const int totalSize =
                    sizeof(byte) // Typemaker byte
                    + sizeof(byte) // Length byte reservation
                    + sizeof(byte) // $t
                    + sizeof(byte) // cosmos bson type
                    + sizeof(byte) // $v
                    + sizeof(byte) // Nested scope typemaker byte
                    + sizeof(byte); // Nested scope length byte reservation

                this.binaryWriter.EnsureRemainingBufferSpace(totalSize);
                this.RegisterArrayOrObjectStart(isArray: false, this.binaryWriter.Position, valueCount: 1);
                this.JsonObjectState.RegisterFieldName();
                this.RegisterArrayOrObjectStart(isNestedArray, this.binaryWriter.Position + 5, valueCount: 0);

                Span<byte> binaryWriterCursor = this.binaryWriter.Cursor.Slice(2);
                binaryWriterCursor[0] = JsonBinaryWriter.DollarTSystemString;
                binaryWriterCursor[1] = cosmosBsonTypeByte;
                binaryWriterCursor[2] = JsonBinaryWriter.DollarVSystemString;
                this.binaryWriter.Position += totalSize;
            }

            /// <inheritdoc />
            public void Write(PreblittedBinaryJsonScope scope)
            {
                this.binaryWriter.Write(scope.Bytes.Span);
                // Dummy register token
                this.JsonObjectState.RegisterToken(JsonTokenType.Null);
                this.bufferedContexts.Peek().Count++;
            }

            /// <summary>
            /// Captures a preblitted binary JSON scope.
            /// This method is for use by <see cref="JsonWriter.CapturePreblittedBinaryJsonScope"/>.
            /// </summary>
            /// <param name="startPosition">Scope start position.</param>
            /// <returns>A preblitted binary JSON scope.</returns>
            internal PreblittedBinaryJsonScope CapturePreblittedBinaryJsonScope(int startPosition)
            {
                return new PreblittedBinaryJsonScope(
                    this.binaryWriter.BufferAsSpan.Slice(startPosition, this.binaryWriter.Position - startPosition).ToArray());
            }

            private void WriteArrayOrObjectStart(bool isArray)
            {
                this.RegisterArrayOrObjectStart(isArray, this.binaryWriter.Position, valueCount: 0);

                // Assume 1-byte value length; as such, we need to reserve up 3 bytes (1 byte type marker, 1 byte length, 1 byte count).
                // We'll adjust this as needed when writing the end of the array/object.
                this.binaryWriter.Write((byte)0);
                this.binaryWriter.Write((byte)0);
            }

            private void RegisterArrayOrObjectStart(bool isArray, long offset, int valueCount)
            {
                this.JsonObjectState.RegisterToken(isArray ? JsonTokenType.BeginArray : JsonTokenType.BeginObject);

                // Save the start index
                ArrayAndObjectInfo info = new ArrayAndObjectInfo(
                    offset,
                    this.sharedStrings.Count,
                    this.stringReferenceOffsets.Count,
                    valueCount);
                this.bufferedContexts.Push(info);
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
                int stringReferenceStartIndex = (int)nestedContext.StringReferenceStartIndex;

                // Figure out what the typemarker and length should be and do any corrections needed
                if (count == 0)
                {
                    // Empty object

                    // Move the cursor back
                    this.binaryWriter.Position = typeMarkerIndex;

                    // Write the type marker
                    this.binaryWriter.Write(isArray ? TypeMarker.Arr0 : TypeMarker.Obj0);
                }
                else if (count == 1)
                {
                    // Single-property object

                    // Move the buffer back but leave one byte for the typemarker
                    Span<byte> buffer = this.binaryWriter.BufferAsSpan;
                    int bytesToWrite = TypeMarkerLength;
                    this.MoveBuffer(buffer, payloadIndex, payloadLength, typeMarkerIndex, bytesToWrite, stringStartIndex, stringReferenceStartIndex);

                    // Move the cursor back
                    this.binaryWriter.Position = typeMarkerIndex;

                    // Write the type marker
                    this.binaryWriter.Write(isArray ? TypeMarker.Arr1 : TypeMarker.Obj1);

                    // Move the cursor forward
                    this.binaryWriter.Position = typeMarkerIndex + TypeMarkerLength + payloadLength;
                }
                else
                {
                    // Need to figure out how many bytes to encode the length and the count
                    int bytesToWrite;
                    if (payloadLength <= byte.MaxValue)
                    {
                        bool serializeCount = isArray && (count > 16);

                        // 1 byte length - move the buffer forward
                        Span<byte> buffer = this.binaryWriter.BufferAsSpan;
                        bytesToWrite = TypeMarkerLength
                            + JsonBinaryEncoding.OneByteLength
                            + (serializeCount ? JsonBinaryEncoding.OneByteCount : 0); 
                        this.MoveBuffer(buffer, payloadIndex, payloadLength, typeMarkerIndex, bytesToWrite, stringStartIndex, stringReferenceStartIndex);

                        // Move the cursor back
                        this.binaryWriter.Position = typeMarkerIndex;

                        // Write the type marker
                        if (serializeCount)
                        {
                            this.binaryWriter.Write(isArray ? TypeMarker.ArrLC1 : TypeMarker.ObjLC1);
                            this.binaryWriter.Write((byte)payloadLength);
                            this.binaryWriter.Write((byte)count);
                        }
                        else
                        {
                            this.binaryWriter.Write(isArray ? TypeMarker.ArrL1 : TypeMarker.ObjL1);
                            this.binaryWriter.Write((byte)payloadLength);
                        }
                    }
                    else if (payloadLength <= ushort.MaxValue)
                    {
                        bool serializeCount = isArray && ((count > 16) || (payloadLength > 0x1000));

                        // 2 byte length - make space for the extra byte length (and extra byte count)
                        this.binaryWriter.Write((byte)0);
                        if (serializeCount)
                        {
                            this.binaryWriter.Write((byte)0);
                        }

                        // Move the buffer forward
                        Span<byte> buffer = this.binaryWriter.BufferAsSpan;
                        bytesToWrite = TypeMarkerLength
                            + JsonBinaryEncoding.TwoByteLength
                            + (serializeCount ? JsonBinaryEncoding.TwoByteCount : 0);
                        this.MoveBuffer(buffer, payloadIndex, payloadLength, typeMarkerIndex, bytesToWrite, stringStartIndex, stringReferenceStartIndex);

                        // Move the cursor back
                        this.binaryWriter.Position = typeMarkerIndex;

                        // Write the type marker
                        if (serializeCount)
                        {
                            this.binaryWriter.Write(isArray ? TypeMarker.ArrLC2 : TypeMarker.ObjLC2);
                            this.binaryWriter.Write((ushort)payloadLength);
                            this.binaryWriter.Write((ushort)count);
                        }
                        else
                        {
                            this.binaryWriter.Write(isArray ? TypeMarker.ArrL2 : TypeMarker.ObjL2);
                            this.binaryWriter.Write((ushort)payloadLength);
                        }
                    }
                    else
                    {
                        // (payloadLength <= uint.MaxValue)
                        bool serializeCount = isArray;

                        // 4 byte length - make space for an extra 3 byte length (and 3 byte count)
                        this.binaryWriter.Write((byte)0);
                        this.binaryWriter.Write((ushort)0);
                        if (serializeCount)
                        {
                            this.binaryWriter.Write((byte)0);
                            this.binaryWriter.Write((ushort)0);
                        }

                        // Move the buffer forward
                        Span<byte> buffer = this.binaryWriter.BufferAsSpan;
                        bytesToWrite = TypeMarkerLength
                            + JsonBinaryEncoding.FourByteLength
                            + (serializeCount ? JsonBinaryEncoding.FourByteCount : 0);
                        this.MoveBuffer(buffer, payloadIndex, payloadLength, typeMarkerIndex, bytesToWrite, stringStartIndex, stringReferenceStartIndex);

                        // Move the cursor back
                        this.binaryWriter.Position = typeMarkerIndex;

                        // Write the type marker
                        if (serializeCount)
                        {
                            this.binaryWriter.Write(isArray ? TypeMarker.ArrLC4 : TypeMarker.ObjLC4);
                            this.binaryWriter.Write((uint)payloadLength);
                            this.binaryWriter.Write((uint)count);
                        }
                        else
                        {
                            this.binaryWriter.Write(isArray ? TypeMarker.ArrL4 : TypeMarker.ObjL4);
                            this.binaryWriter.Write((uint)payloadLength);
                        }
                    }

                    // For an array, attempt to convert it to a uniform number array
                    bool isUniformArray;
                    if (isArray && (payloadLength > 4) && this.enableNumberArrays)
                    {
                        this.binaryWriter.Position = typeMarkerIndex;
                        isUniformArray = TryWriteUniformNumberArray(
                            this.binaryWriter,
                            bytesToWrite,
                            payloadLength,
                            count);

                        if (!isUniformArray)
                        {
                            this.binaryWriter.Position = typeMarkerIndex;
                            isUniformArray = TryWriteUniformArrayOfNumberArrays(
                                this.binaryWriter,
                                bytesToWrite,
                                payloadLength,
                                count);
                        }
                    }
                    else
                    {
                        isUniformArray = false;
                    }

                    // Move the cursor forward if this is not a uniform array
                    if (!isUniformArray)
                    {
                        this.binaryWriter.Position = typeMarkerIndex + bytesToWrite + payloadLength;
                    }
                }

                this.bufferedContexts.Peek().Count++;

                // If we are closing the outermost array / object, we need to fix up reference string offsets
                if (typeMarkerIndex == 1 && this.sharedStrings.Count > 0)
                {
                    this.FixReferenceStringOffsets(this.binaryWriter.BufferAsSpan);
                }
            }

            static private bool TryWriteUniformNumberArray(
                JsonBinaryMemoryWriter arrayWriter,
                int byteCount,
                int valueLength,
                int itemCount)
            {
                if (arrayWriter == null) throw new ArgumentNullException(nameof(arrayWriter));
                if (byteCount <= 0) throw new ArgumentException($"Value must be greater than 0.", nameof(byteCount));
                if (valueLength <= 0) throw new ArgumentException($"Value must be greater than 0.", nameof(valueLength));

                // Uniform arrays only support 1 and 2-byte item count
                if (itemCount > ushort.MaxValue) return false;

                int floatCount = 0;
                long maxValue = long.MinValue;
                long minValue = long.MaxValue;
                List<Number64> numberValues = new List<Number64>(itemCount);

                ReadOnlySpan<byte> arrayBuffer = arrayWriter.BufferAsSpan.Slice(arrayWriter.Position + byteCount, valueLength);
                while (!arrayBuffer.IsEmpty)
                {
                    if (!TypeMarker.IsNumber(arrayBuffer[0]))
                    {
                        // We encountered a non-number value, so we bail out
                        return false;
                    }

                    if (JsonBinaryEncoding.TryGetNumberValue(
                        arrayBuffer,
                        uniformArrayInfo: null,
                        out Number64 value,
                        out int itemLength))
                    {
                        numberValues.Add(value);

                        if (value.IsInteger)
                        {
                            maxValue = Math.Max(maxValue, Number64.ToLong(value));
                            minValue = Math.Min(minValue, Number64.ToLong(value));
                        }
                        else
                        {
                            floatCount++;
                        }
                    }
                    else
                    {
                        throw new JsonUnexpectedTokenException();
                    }

                    arrayBuffer = arrayBuffer.Slice(itemLength);
                }

                // Assert(numberValues.Count == itemCount);
                // Assert(itemCount >= floatCount);

                byte itemTypeMarker;
                int itemSize;

                if (floatCount > 0)
                {
                    if (floatCount < itemCount)
                    {
                        // Not all items are floating-point values, we need to check for integer values that
                        // cannot be represented as floating-point values without losing precision.

                        long nMaxAbsValue = Math.Max(Math.Abs(minValue), Math.Abs(maxValue));
                        if (nMaxAbsValue > (1L << 53)) return false;
                    }

                    itemTypeMarker = TypeMarker.Float64;
                    itemSize = sizeof(double);
                }
                else
                {
                    if ((minValue >= 0) && (maxValue <= byte.MaxValue))
                    {
                        itemTypeMarker = TypeMarker.UInt8;
                        itemSize = sizeof(byte);
                    }
                    else if ((minValue >= sbyte.MinValue) && (maxValue <= sbyte.MaxValue))
                    {
                        itemTypeMarker = TypeMarker.Int8;
                        itemSize = sizeof(sbyte);
                    }
                    else if ((minValue >= short.MinValue) && (maxValue <= short.MaxValue))
                    {
                        itemTypeMarker = TypeMarker.Int16;
                        itemSize = sizeof(short);
                    }
                    else if ((minValue >= int.MinValue) && (maxValue <= int.MaxValue))
                    {
                        itemTypeMarker = TypeMarker.Int32;
                        itemSize = sizeof(int);
                    }
                    else
                    {
                        itemTypeMarker = TypeMarker.Int64;
                        itemSize = sizeof(long);
                    }
                }

                int newByteCount = 1 /* item TypeMarker */ + (itemCount <= byte.MaxValue ? 1 : 2) /* item count */;
                int newLength = 1 + newByteCount + (itemCount * itemSize);
                int oldLength = 1 + byteCount + valueLength;

                // Verify whether writing a uniform number array is beneficial
                if (newLength > oldLength)
                {
                    return false;
                }

                if (itemCount <= byte.MaxValue)
                {
                    arrayWriter.Write(TypeMarker.ArrNumC1);
                    arrayWriter.Write(itemTypeMarker);
                    arrayWriter.Write((byte)itemCount);
                }
                else
                {
                    arrayWriter.Write(TypeMarker.ArrNumC2);
                    arrayWriter.Write(itemTypeMarker);
                    arrayWriter.Write((short)itemCount);
                }

                // Write the uniform number array beginning at the start offset
                switch (itemTypeMarker)
                {
                    case TypeMarker.Int8:
                        foreach (Number64 value in numberValues)
                        {
                            arrayWriter.Write((sbyte)Number64.ToLong(value));
                        }
                        break;
                    case TypeMarker.UInt8:
                        foreach (Number64 value in numberValues)
                        {
                            arrayWriter.Write((byte)Number64.ToLong(value));
                        }
                        break;
                    case TypeMarker.Int16:
                        foreach (Number64 value in numberValues)
                        {
                            arrayWriter.Write((short)Number64.ToLong(value));
                        }
                        break;
                    case TypeMarker.Int32:
                        foreach (Number64 value in numberValues)
                        {
                            arrayWriter.Write((int)Number64.ToLong(value));
                        }
                        break;
                    case TypeMarker.Int64:
                        foreach (Number64 value in numberValues)
                        {
                            arrayWriter.Write(Number64.ToLong(value));
                        }
                        break;
                    case TypeMarker.Float16:
                        // Currently not supported
                        throw new InvalidOperationException();
                    case TypeMarker.Float32:
                        foreach (Number64 value in numberValues)
                        {
                            arrayWriter.Write((float)Number64.ToDouble(value));
                        }
                        break;
                    case TypeMarker.Float64:
                        foreach (Number64 value in numberValues)
                        {
                            arrayWriter.Write(Number64.ToDouble(value));
                        }
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                return true;
            }

            static private bool TryWriteUniformArrayOfNumberArrays(
                JsonBinaryMemoryWriter arrayWriter,
                int byteCount,
                int valueLength,
                int itemCount)
            {
                if (arrayWriter == null) throw new ArgumentNullException(nameof(arrayWriter));
                if (byteCount <= 0) throw new ArgumentException($"Value must be greater than 0.", nameof(byteCount));
                if (valueLength <= 0) throw new ArgumentException($"Value must be greater than 0.", nameof(valueLength));

                // Uniform arrays only support 1 and 2-byte item count
                if (itemCount > ushort.MaxValue) return false;
                if (itemCount < 2) return false;

                UniformArrayInfo commonArrayInfo = default;
                int commonArrayLength = 0;

                ReadOnlySpan<byte> arrayBuffer = arrayWriter.BufferAsSpan.Slice(arrayWriter.Position + byteCount, valueLength);
                while (!arrayBuffer.IsEmpty)
                {
                    UniformArrayInfo numberArrayInfo = GetUniformArrayInfo(arrayBuffer, isNested: false);
                    if (numberArrayInfo == default)
                    {
                        // Not a uniform number array
                        return false;
                    }

                    if (numberArrayInfo.NestedArrayInfo != default)
                    {
                        // Already an array of uniform number arrays
                        return false;
                    }

                    if (commonArrayInfo == null)
                    {
                        commonArrayInfo = numberArrayInfo;
                        commonArrayLength = commonArrayInfo.PrefixSize + (commonArrayInfo.ItemCount * commonArrayInfo.ItemSize);
                    }

                    if (!JsonBinaryEncoding.Equals(numberArrayInfo, commonArrayInfo))
                    {
                        // A uniform number array that is different from the common one so far
                        return false;
                    }

                    arrayBuffer = arrayBuffer.Slice(commonArrayLength);
                }

                bool oneByteCount = (commonArrayInfo.ItemCount <= byte.MaxValue) && (itemCount <= byte.MaxValue);
                int newByteCount = oneByteCount ? 5 : 7;

                // This condition should never happen but still needs to be covered
                if (newByteCount > (byteCount + (commonArrayInfo.PrefixSize * 2)))
                {
                    return false;
                }

                int initialPosition = arrayWriter.Position;
                arrayWriter.Position += newByteCount;

                ReadOnlySpan<byte> arrayItems = arrayWriter.BufferAsSpan.Slice(initialPosition + byteCount, valueLength);
                while (!arrayItems.IsEmpty)
                {
                    arrayWriter.Write(arrayItems.Slice(commonArrayInfo.PrefixSize, commonArrayLength - commonArrayInfo.PrefixSize));
                    arrayItems = arrayItems.Slice(commonArrayLength);
                }

                int finalPosition = arrayWriter.Position;
                arrayWriter.Position = initialPosition;

                if (oneByteCount)
                {
                    arrayWriter.Write(TypeMarker.ArrArrNumC1C1);
                    arrayWriter.Write(TypeMarker.ArrNumC1);
                    arrayWriter.Write(commonArrayInfo.ItemTypeMarker);
                    arrayWriter.Write((byte)commonArrayInfo.ItemCount);
                    arrayWriter.Write((byte)itemCount);
                }
                else
                {
                    arrayWriter.Write(TypeMarker.ArrArrNumC2C2);
                    arrayWriter.Write(TypeMarker.ArrNumC2);
                    arrayWriter.Write(commonArrayInfo.ItemTypeMarker);
                    arrayWriter.Write((ushort)commonArrayInfo.ItemCount);
                    arrayWriter.Write((ushort)itemCount);
                }

                arrayWriter.Position = finalPosition;
                return true;
            }

            private void MoveBuffer(
                Span<byte> buffer,
                int payloadIndex,
                int payloadLength,
                int typeMarkerIndex,
                int bytesToWrite,
                int stringStartIndex,
                int stringReferenceOffsetLow)
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

                for (int i = stringReferenceOffsetLow; i < this.stringReferenceOffsets.Count; ++i)
                {
                    this.stringReferenceOffsets[i] += delta;
                }
            }

            private void FixReferenceStringOffsets(Span<byte> binaryWriterRawBuffer)
            {
                foreach (int stringReferenceOffset in this.stringReferenceOffsets)
                {
                    byte typeMarker = binaryWriterRawBuffer[stringReferenceOffset];

                    JsonNodeType nodeType = JsonBinaryEncoding.NodeTypes.Lookup[typeMarker];
                    switch (nodeType)
                    {
                        case JsonNodeType.String:
                        case JsonNodeType.FieldName:
                        {
                            Span<byte> offsetBuffer = binaryWriterRawBuffer.Slice(stringReferenceOffset + 1);
                            switch (typeMarker)
                            {
                                case TypeMarker.ReferenceString1ByteOffset:
                                {
                                    byte stringIndex = offsetBuffer[0];
                                    SharedStringValue sharedStringValue = this.sharedStrings[stringIndex];
                                    JsonBinaryEncoding.SetFixedSizedValue<byte>(offsetBuffer, (byte)sharedStringValue.Offset);
                                    break;
                                }

                                case TypeMarker.ReferenceString2ByteOffset:
                                {
                                    ushort stringIndex = JsonBinaryEncoding.GetFixedSizedValue<ushort>(offsetBuffer);
                                    SharedStringValue sharedStringValue = this.sharedStrings[stringIndex];
                                    JsonBinaryEncoding.SetFixedSizedValue<ushort>(offsetBuffer, (ushort)sharedStringValue.Offset);
                                    break;
                                }

                                case TypeMarker.ReferenceString3ByteOffset:
                                {
                                    JsonBinaryEncoding.UInt24 stringIndex =
                                        JsonBinaryEncoding.GetFixedSizedValue<JsonBinaryEncoding.UInt24>(offsetBuffer);
                                    SharedStringValue sharedStringValue = this.sharedStrings[stringIndex];
                                    JsonBinaryEncoding.SetFixedSizedValue<JsonBinaryEncoding.UInt24>(
                                        offsetBuffer,
                                        (JsonBinaryEncoding.UInt24)sharedStringValue.Offset);
                                    break;
                                }

                                case TypeMarker.ReferenceString4ByteOffset:
                                {
                                    int stringIndex = JsonBinaryEncoding.GetFixedSizedValue<int>(offsetBuffer);
                                    SharedStringValue sharedStringValue = this.sharedStrings[stringIndex];
                                    JsonBinaryEncoding.SetFixedSizedValue<int>(offsetBuffer, (int)sharedStringValue.Offset);
                                    break;
                                }
                            }

                            break;
                        }
                        default:
                            throw new InvalidOperationException($"Unknown {nameof(nodeType)}: {nodeType}.");
                    }
                }
            }

            private void WriteFieldNameOrString(bool isFieldName, Utf8Span utf8Span)
            {
                this.binaryWriter.EnsureRemainingBufferSpace(TypeMarkerLength + JsonBinaryEncoding.FourByteLength + utf8Span.Length);

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
                else if (this.enableEncodedStrings
                    && isFieldName
                    && (utf8Span.Length >= MinReferenceStringLength)
                    && this.TryRegisterStringValue(utf8Span))
                {
                    // Work is done in the check
                }
                else if (this.enableEncodedStrings
                    && !isFieldName
                    && (utf8Span.Length == JsonBinaryEncoding.GuidLength)
                    && JsonBinaryEncoding.TryEncodeGuidString(utf8Span.Span, this.binaryWriter.Cursor))
                {
                    // Encoded value as guid string
                    this.binaryWriter.Position += JsonBinaryEncoding.EncodedGuidLength;
                }
                else if (this.enableEncodedStrings
                    && !isFieldName
                    && (utf8Span.Length == JsonBinaryEncoding.GuidWithQuotesLength)
                    && (utf8Span.Span[0] == '"')
                    && (utf8Span.Span[JsonBinaryEncoding.GuidWithQuotesLength - 1] == '"')
                    && JsonBinaryEncoding.TryEncodeGuidString(utf8Span.Span.Slice(start: 1), this.binaryWriter.Cursor)
                    && (this.binaryWriter.Cursor[0] == TypeMarker.LowercaseGuidString))
                {
                    // Encoded value as lowercase double quote guid
                    this.binaryWriter.Cursor[0] = TypeMarker.DoubleQuotedLowercaseGuidString;
                    this.binaryWriter.Position += JsonBinaryEncoding.EncodedGuidLength;
                }
                else if (this.enableEncodedStrings
                    && !isFieldName
                    && JsonBinaryEncoding.TryEncodeCompressedString(utf8Span.Span, this.binaryWriter.Cursor, out int bytesWritten))
                {
                    // Encoded value as a compressed string
                    this.binaryWriter.Position += bytesWritten;
                }
                else if (this.enableEncodedStrings
                    && !isFieldName
                    && (utf8Span.Length >= MinReferenceStringLength)
                    && (utf8Span.Length <= MaxReferenceStringLength)
                    && this.TryRegisterStringValue(utf8Span))
                {
                    // Work is done in the check
                }
                else if (TypeMarker.TryGetEncodedStringLengthTypeMarker(utf8Span.Length, out byte typeMarker))
                {
                    // Write with type marker that encodes the length
                    this.binaryWriter.Write(typeMarker);
                    this.binaryWriter.Write(utf8Span.Span);
                }
                // Just write it out as a regular string with type marker and length prefix
                else if (utf8Span.Length < byte.MaxValue)
                {
                    this.binaryWriter.Write(TypeMarker.String1ByteLength);
                    this.binaryWriter.Write((byte)utf8Span.Length);
                    this.binaryWriter.Write(utf8Span.Span);
                }
                else if (utf8Span.Length < ushort.MaxValue)
                {
                    this.binaryWriter.Write(TypeMarker.String2ByteLength);
                    this.binaryWriter.Write((ushort)utf8Span.Length);
                    this.binaryWriter.Write(utf8Span.Span);
                }
                else
                {
                    // (utf8String.Length < uint.MaxValue)
                    this.binaryWriter.Write(TypeMarker.String4ByteLength);
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

                    int maxOffset = (this.JsonObjectState.CurrentDepth * 7) + (int)this.CurrentLength;

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
                this.stringReferenceOffsets.Add(this.binaryWriter.Position);

                if (sharedString.MaxOffset <= byte.MaxValue)
                {
                    this.binaryWriter.Write(TypeMarker.ReferenceString1ByteOffset);
                    this.binaryWriter.Write((byte)hashAndIndex.index);
                }
                else if (sharedString.MaxOffset <= ushort.MaxValue)
                {
                    this.binaryWriter.Write(TypeMarker.ReferenceString2ByteOffset);
                    this.binaryWriter.Write((ushort)hashAndIndex.index);
                }
                else if (sharedString.MaxOffset <= JsonBinaryEncoding.UInt24.MaxValue)
                {
                    this.binaryWriter.Write(TypeMarker.ReferenceString3ByteOffset);
                    this.binaryWriter.Write((JsonBinaryEncoding.UInt24)(int)hashAndIndex.index);
                }
                else if (sharedString.MaxOffset <= int.MaxValue)
                {
                    this.binaryWriter.Write(TypeMarker.ReferenceString4ByteOffset);
                    this.binaryWriter.Write((int)hashAndIndex.index);
                }
                else
                {
                    return false;
                }

                return true;
            }

            private void WriteIntegerInternal(long value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Number);
                if (TypeMarker.IsEncodedNumberLiteral(value))
                {
                    this.binaryWriter.Write((byte)(TypeMarker.LiteralIntMin + value));
                }
                else
                {
                    if (value >= 0)
                    {
                        // Non-negative Number
                        if (value <= byte.MaxValue)
                        {
                            this.binaryWriter.Write(TypeMarker.NumberUInt8);
                            this.binaryWriter.Write((byte)value);
                        }
                        else if (value <= short.MaxValue)
                        {
                            this.binaryWriter.Write(TypeMarker.NumberInt16);
                            this.binaryWriter.Write((short)value);
                        }
                        else if (value <= int.MaxValue)
                        {
                            this.binaryWriter.Write(TypeMarker.NumberInt32);
                            this.binaryWriter.Write((int)value);
                        }
                        else
                        {
                            this.binaryWriter.Write(TypeMarker.NumberInt64);
                            this.binaryWriter.Write(value);
                        }
                    }
                    else
                    {
                        // Negative Number
                        if (value < int.MinValue)
                        {
                            this.binaryWriter.Write(TypeMarker.NumberInt64);
                            this.binaryWriter.Write(value);
                        }
                        else if (value < short.MinValue)
                        {
                            this.binaryWriter.Write(TypeMarker.NumberInt32);
                            this.binaryWriter.Write((int)value);
                        }
                        else
                        {
                            this.binaryWriter.Write(TypeMarker.NumberInt16);
                            this.binaryWriter.Write((short)value);
                        }
                    }
                }
            }

            private void WriteDoubleInternal(double value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Number);
                this.binaryWriter.Write(TypeMarker.NumberDouble);
                this.binaryWriter.Write(value);
            }

            public void WriteRawJsonValue(
                ReadOnlyMemory<byte> rootBuffer,
                int valueOffset,
                UniformArrayInfo externalArrayInfo,
                bool isFieldName)
            {
                this.ForceRewriteRawJsonValue(rootBuffer, valueOffset, externalArrayInfo, isFieldName);
            }

            private void ForceRewriteRawJsonValue(
                ReadOnlyMemory<byte> rootBuffer,
                int valueOffset,
                UniformArrayInfo externalArrayInfo,
                bool isFieldName)
            {
                ReadOnlyMemory<byte> rawJsonValue = rootBuffer.Slice(valueOffset);
                byte typeMarker = rawJsonValue.Span[0];

                if (externalArrayInfo != null)
                {
                    this.WriteRawUniformArrayItem(rawJsonValue.Span, externalArrayInfo);
                }
                else
                {
                    RawValueType rawType = (RawValueType)RawValueTypes[typeMarker];

                    // If the writer supports uniform-number arrays then we treat them as a value token
                    if (this.enableEncodedStrings && ((rawType == RawValueType.ArrNum) || (rawType == RawValueType.ArrArrNum)))
                    {
                        rawType = RawValueType.Token;
                    }

                    switch (rawType)
                    {
                        case RawValueType.Token:
                            {
                                int valueLength = JsonBinaryEncoding.GetValueLength(rawJsonValue.Span);

                                rawJsonValue = rawJsonValue.Slice(start: 0, length: valueLength);

                                // We only care if the type is a field name or not
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
                                JsonBinaryEncoding.GetFixedSizedValue<byte>(rawJsonValue.Slice(start: 1).Span),
                                default,
                                isFieldName);
                            break;
                        case RawValueType.StrR2:
                            this.ForceRewriteRawJsonValue(
                                rootBuffer,
                                JsonBinaryEncoding.GetFixedSizedValue<ushort>(rawJsonValue.Slice(start: 1).Span),
                                default,
                                isFieldName);
                            break;
                        case RawValueType.StrR3:
                            this.ForceRewriteRawJsonValue(
                                rootBuffer,
                                JsonBinaryEncoding.GetFixedSizedValue<JsonBinaryEncoding.UInt24>(rawJsonValue.Slice(start: 1).Span),
                                default,
                                isFieldName);
                            break;
                        case RawValueType.StrR4:
                            this.ForceRewriteRawJsonValue(
                                rootBuffer,
                                JsonBinaryEncoding.GetFixedSizedValue<int>(rawJsonValue.Slice(start: 1).Span),
                                default,
                                isFieldName);
                            break;

                        case RawValueType.Arr1:
                            {
                                this.JsonObjectState.RegisterToken(JsonTokenType.BeginArray);

                                this.binaryWriter.Write(typeMarker);

                                this.ForceRewriteRawJsonValue(
                                    rootBuffer,
                                    valueOffset: valueOffset + 1,
                                    externalArrayInfo: default,
                                    isFieldName: false);

                                this.JsonObjectState.RegisterToken(JsonTokenType.EndArray);
                            }
                            break;

                        case RawValueType.Obj1:
                            {
                                this.JsonObjectState.RegisterToken(JsonTokenType.BeginObject);

                                this.binaryWriter.Write(typeMarker);

                                this.ForceRewriteRawJsonValue(
                                    rootBuffer,
                                    valueOffset: valueOffset + 1,
                                    externalArrayInfo: default,
                                    isFieldName: true);

                                int nameLength = JsonBinaryEncoding.GetValueLength(rawJsonValue.Slice(start: 1).Span);

                                this.ForceRewriteRawJsonValue(
                                    rootBuffer,
                                    valueOffset: valueOffset + 1 + nameLength,
                                    externalArrayInfo: default,
                                    isFieldName: false);

                                this.JsonObjectState.RegisterToken(JsonTokenType.EndObject);
                            }
                            break;

                        case RawValueType.Arr:
                            {
                                this.WriteArrayStart();

                                foreach (JsonBinaryEncoding.Enumerator.ArrayItem arrayItem in JsonBinaryEncoding.Enumerator.GetArrayItems(rootBuffer, valueOffset, externalArrayInfo))
                                {
                                    this.ForceRewriteRawJsonValue(
                                        rootBuffer,
                                        arrayItem.Offset,
                                        arrayItem.ExternalArrayInfo,
                                        isFieldName);
                                }

                                this.WriteArrayEnd();
                            }
                            break;

                        case RawValueType.Obj:
                            {
                                this.WriteObjectStart();

                                foreach (JsonBinaryEncoding.Enumerator.ObjectProperty property in JsonBinaryEncoding.Enumerator.GetObjectProperties(rootBuffer, valueOffset))
                                {
                                    this.ForceRewriteRawJsonValue(rootBuffer, property.NameOffset, externalArrayInfo: default, isFieldName: true);
                                    this.ForceRewriteRawJsonValue(rootBuffer, property.ValueOffset, externalArrayInfo: default, isFieldName: false);
                                }

                                this.WriteObjectEnd();
                            }
                            break;

                        case RawValueType.ArrNum:
                            this.WriteRawNumberArray(rawJsonValue.Span, GetUniformArrayInfo(rawJsonValue.Span));
                            break;

                        case RawValueType.ArrArrNum:
                            this.WriteRawNumberArrayArray(rawJsonValue.Span, GetUniformArrayInfo(rawJsonValue.Span));
                            break;

                        default:
                            throw new InvalidOperationException($"Unknown {nameof(RawValueType)} {rawType}.");
                    }
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
                        long encodedStringLength = TypeMarker.GetEncodedStringLength(buffer.Span[0]);
                        if (encodedStringLength > int.MaxValue)
                        {
                            throw new InvalidOperationException("string is too long.");
                        }

                        rawStringValue = Utf8Span.UnsafeFromUtf8BytesNoValidation(buffer.Slice(start: TypeMarkerLength, (int)encodedStringLength).Span);
                        break;

                    case RawValueType.StrL1:
                        byte oneByteLength = JsonBinaryEncoding.GetFixedSizedValue<byte>(buffer.Slice(TypeMarkerLength).Span);
                        rawStringValue = Utf8Span.UnsafeFromUtf8BytesNoValidation(buffer.Slice(start: TypeMarkerLength + sizeof(byte), oneByteLength).Span);
                        break;

                    case RawValueType.StrL2:
                        ushort twoByteLength = JsonBinaryEncoding.GetFixedSizedValue<ushort>(buffer.Slice(TypeMarkerLength).Span);
                        rawStringValue = Utf8Span.UnsafeFromUtf8BytesNoValidation(buffer.Slice(start: TypeMarkerLength + sizeof(ushort), twoByteLength).Span);
                        break;

                    case RawValueType.StrL4:
                        uint fourByteLength = JsonBinaryEncoding.GetFixedSizedValue<uint>(buffer.Slice(TypeMarkerLength).Span);
                        if (fourByteLength > int.MaxValue)
                        {
                            throw new InvalidOperationException("string is too long.");
                        }

                        rawStringValue = Utf8Span.UnsafeFromUtf8BytesNoValidation(buffer.Slice(start: TypeMarkerLength + sizeof(uint), (int)fourByteLength).Span);
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown {nameof(rawValueType)}: {rawValueType}.");
                }

                this.WriteFieldNameOrString(isFieldName, rawStringValue);
            }

            private void WriteRawUniformArrayItem(
                ReadOnlySpan<byte> rawValue,
                UniformArrayInfo arrayInfo)
            {
                if (arrayInfo == null) throw new ArgumentNullException(nameof(arrayInfo));

                switch (arrayInfo.ItemTypeMarker)
                {
                    case TypeMarker.Int8:
                        this.WriteNumber64Value(GetFixedSizedValue<sbyte>(rawValue));
                        break;
                    case TypeMarker.UInt8:
                        this.WriteNumber64Value(GetFixedSizedValue<byte>(rawValue));
                        break;
                    case TypeMarker.Int16:
                        this.WriteNumber64Value(GetFixedSizedValue<short>(rawValue));
                        break;
                    case TypeMarker.Int32:
                        this.WriteNumber64Value(GetFixedSizedValue<int>(rawValue));
                        break;
                    case TypeMarker.Int64:
                        this.WriteNumber64Value(GetFixedSizedValue<long>(rawValue));
                        break;
                    case TypeMarker.Float32:
                        this.WriteNumber64Value(GetFixedSizedValue<float>(rawValue));
                        break;
                    case TypeMarker.Float64:
                        this.WriteNumber64Value(GetFixedSizedValue<double>(rawValue));
                        break;

                    case TypeMarker.ArrNumC1:
                    case TypeMarker.ArrNumC2:
                        this.WriteRawNumberArray(rawValue, arrayInfo.NestedArrayInfo);
                        break;

                    default:
                        throw new JsonInvalidTokenException();
                }
            }

            private void WriteRawNumberArray(
                ReadOnlySpan<byte> rawValue,
                UniformArrayInfo arrayInfo)
            {
                if (arrayInfo == null) throw new ArgumentNullException(nameof(arrayInfo));

                this.WriteArrayStart();

                int endOffset = arrayInfo.ItemCount * arrayInfo.ItemSize;
                switch (arrayInfo.ItemTypeMarker)
                {
                    case TypeMarker.Int8:
                        for (int offset = 0; offset < endOffset; offset += arrayInfo.ItemSize)
                        {
                            this.WriteNumber64Value(GetFixedSizedValue<sbyte>(rawValue.Slice(offset)));
                        }
                        break;

                    case TypeMarker.UInt8:
                        for (int offset = 0; offset < endOffset; offset += arrayInfo.ItemSize)
                        {
                            this.WriteNumber64Value(GetFixedSizedValue<byte>(rawValue.Slice(offset)));
                        }
                        break;

                    case TypeMarker.Int16:
                        for (int offset = 0; offset < endOffset; offset += arrayInfo.ItemSize)
                        {
                            this.WriteNumber64Value(GetFixedSizedValue<short>(rawValue.Slice(offset)));
                        }
                        break;

                    case TypeMarker.Int32:
                        for (int offset = 0; offset < endOffset; offset += arrayInfo.ItemSize)
                        {
                            this.WriteNumber64Value(GetFixedSizedValue<int>(rawValue.Slice(offset)));
                        }
                        break;

                    case TypeMarker.Int64:
                        for (int offset = 0; offset < endOffset; offset += arrayInfo.ItemSize)
                        {
                            this.WriteNumber64Value(GetFixedSizedValue<long>(rawValue.Slice(offset)));
                        }
                        break;

                    case TypeMarker.Float32:
                        for (int offset = 0; offset < endOffset; offset += arrayInfo.ItemSize)
                        {
                            this.WriteNumber64Value(GetFixedSizedValue<float>(rawValue.Slice(offset)));
                        }
                        break;

                    case TypeMarker.Float64:
                        for (int offset = 0; offset < endOffset; offset += arrayInfo.ItemSize)
                        {
                            this.WriteNumber64Value(GetFixedSizedValue<double>(rawValue.Slice(offset)));
                        }
                        break;

                    default:
                        throw new JsonInvalidTokenException();
                }

                this.WriteArrayEnd();
            }

            private void WriteRawNumberArrayArray(
                ReadOnlySpan<byte> rawValue,
                UniformArrayInfo arrayInfo)
            {
                if (arrayInfo == null) throw new ArgumentNullException(nameof(arrayInfo));

                this.WriteArrayStart();

                int endOffset = arrayInfo.ItemCount * arrayInfo.ItemSize;
                for (int offset = 0; offset < endOffset; offset += arrayInfo.ItemSize)
                {
                    this.WriteRawNumberArray(rawValue.Slice(offset), arrayInfo.NestedArrayInfo);
                }

                this.WriteArrayEnd();
            }

            private sealed class ArrayAndObjectInfo
            {
                public ArrayAndObjectInfo(long offset, int stringStartIndex, long stringReferenceStartIndex, int valueCount)
                {
                    this.Offset = offset;
                    this.Count = valueCount;
                    this.StringStartIndex = stringStartIndex;
                    this.StringReferenceStartIndex = stringReferenceStartIndex;
                }

                public long Offset { get; }

                public long Count { get; set; }

                public long StringStartIndex { get; }

                public long StringReferenceStartIndex { get; }
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
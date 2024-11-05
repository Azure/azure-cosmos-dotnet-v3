//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using RMResources = Documents.RMResources;

    /// <summary>
    /// Base abstract class for JSON writers.
    /// The writer defines methods that allow for writing a JSON encoded value to a buffer.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract partial class JsonWriter : IJsonWriter
    {
        private const int MaxStackAlloc = 4 * 1024;

        /// <summary>
        /// The <see cref="JsonObjectState"/>
        /// </summary>
        protected readonly JsonObjectState JsonObjectState;

        /// <summary>
        /// Initializes a new instance of the JsonWriter class.
        /// </summary>
        protected JsonWriter()
        {
            this.JsonObjectState = new JsonObjectState(false);
        }

        /// <inheritdoc />
        public abstract JsonSerializationFormat SerializationFormat { get; }

        /// <inheritdoc />
        public abstract long CurrentLength { get; }

        /// <summary>
        /// Creates a JsonWriter that can write in a particular JsonSerializationFormat (utf8 if text)
        /// </summary>
        /// <param name="jsonSerializationFormat">The JsonSerializationFormat of the writer.</param>
        /// <param name="writeOptions">The write options the control the write behavior.</param>
        /// <param name="initialCapacity">Initial capacity to help avoid intermediary allocations.</param>
        /// <returns>A JsonWriter that can write in a particular JsonSerializationFormat</returns>
        public static IJsonWriter Create(
            JsonSerializationFormat jsonSerializationFormat,
            JsonWriteOptions writeOptions = JsonWriteOptions.None,
            int initialCapacity = 256)
        {
            return jsonSerializationFormat switch
            {
                JsonSerializationFormat.Text => new JsonTextWriter(initialCapacity),
                JsonSerializationFormat.Binary => new JsonBinaryWriter(
                    enableNumberArrays: writeOptions.HasFlag(JsonWriteOptions.EnableNumberArrays),
                    initialCapacity: initialCapacity),
                _ => throw new ArgumentException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            RMResources.UnexpectedJsonSerializationFormat,
                            jsonSerializationFormat)),
            };
        }

        /// <inheritdoc />
        public abstract void WriteObjectStart();

        /// <inheritdoc />
        public abstract void WriteObjectEnd();

        /// <inheritdoc />
        public abstract void WriteArrayStart();

        /// <inheritdoc />
        public abstract void WriteArrayEnd();

        /// <inheritdoc />
        public virtual void WriteFieldName(string fieldName)
        {
            int utf8Length = Encoding.UTF8.GetByteCount(fieldName);
            Span<byte> utf8Buffer = utf8Length < JsonTextWriter.MaxStackAlloc ? stackalloc byte[utf8Length] : new byte[utf8Length];
            Encoding.UTF8.GetBytes(fieldName, utf8Buffer);
            Utf8Span utf8FieldName = Utf8Span.UnsafeFromUtf8BytesNoValidation(utf8Buffer);

            this.WriteFieldName(utf8FieldName);
        }

        /// <inheritdoc />
        public abstract void WriteFieldName(Utf8Span fieldName);

        /// <inheritdoc />
        public virtual void WriteStringValue(string value)
        {
            int utf8Length = Encoding.UTF8.GetByteCount(value);
            Span<byte> utf8Buffer = utf8Length < JsonTextWriter.MaxStackAlloc ? stackalloc byte[utf8Length] : new byte[utf8Length];
            Encoding.UTF8.GetBytes(value, utf8Buffer);
            Utf8Span utf8Value = Utf8Span.UnsafeFromUtf8BytesNoValidation(utf8Buffer);

            this.WriteStringValue(utf8Value);
        }

        /// <inheritdoc />
        public abstract void WriteStringValue(Utf8Span value);

        /// <inheritdoc />
        public abstract void WriteNumber64Value(Number64 value);

        /// <inheritdoc />
        public abstract void WriteBoolValue(bool value);

        /// <inheritdoc />
        public abstract void WriteNullValue();

        /// <inheritdoc />
        public abstract void WriteInt8Value(sbyte value);

        /// <inheritdoc />
        public abstract void WriteInt16Value(short value);

        /// <inheritdoc />
        public abstract void WriteInt32Value(int value);

        /// <inheritdoc />
        public abstract void WriteInt64Value(long value);

        /// <inheritdoc />
        public abstract void WriteFloat32Value(float value);

        /// <inheritdoc />
        public abstract void WriteFloat64Value(double value);

        /// <inheritdoc />
        public abstract void WriteUInt32Value(uint value);

        /// <inheritdoc />
        public abstract void WriteGuidValue(Guid value);

        /// <inheritdoc />
        public abstract void WriteBinaryValue(ReadOnlySpan<byte> value);

        /// <inheritdoc />
        public virtual void WriteNumberArray(IReadOnlyList<byte> values)
        {
            this.WriteArrayStart();

            foreach (byte value in values)
            {
                Number64 number64 = value;
                this.WriteNumber64Value(number64);
            }

            this.WriteArrayEnd();
        }

        /// <inheritdoc />
        public virtual void WriteNumberArray(IReadOnlyList<sbyte> values)
        {
            this.WriteArrayStart();

            foreach (sbyte value in values)
            {
                Number64 number64 = value;
                this.WriteNumber64Value(number64);
            }

            this.WriteArrayEnd();
        }

        /// <inheritdoc />
        public virtual void WriteNumberArray(IReadOnlyList<short> values)
        {
            this.WriteArrayStart();

            foreach (short value in values)
            {
                Number64 number64 = value;
                this.WriteNumber64Value(number64);
            }

            this.WriteArrayEnd();
        }

        /// <inheritdoc />
        public virtual void WriteNumberArray(IReadOnlyList<int> values)
        {
            this.WriteArrayStart();

            foreach (int value in values)
            {
                Number64 number64 = value;
                this.WriteNumber64Value(number64);
            }

            this.WriteArrayEnd();
        }

        /// <inheritdoc />
        public virtual void WriteNumberArray(IReadOnlyList<long> values)
        {
            this.WriteArrayStart();

            foreach (long value in values)
            {
                Number64 number64 = value;
                this.WriteNumber64Value(number64);
            }

            this.WriteArrayEnd();
        }

        /// <inheritdoc />
        public virtual void WriteNumberArray(IReadOnlyList<float> values)
        {
            this.WriteArrayStart();

            foreach (float value in values)
            {
                Number64 number64 = value;
                this.WriteNumber64Value(number64);
            }

            this.WriteArrayEnd();
        }

        /// <inheritdoc />
        public virtual void WriteNumberArray(IReadOnlyList<double> values)
        {
            this.WriteArrayStart();

            foreach (double value in values)
            {
                Number64 number64 = value;
                this.WriteNumber64Value(number64);
            }

            this.WriteArrayEnd();
        }

        /// <inheritdoc />
        public abstract ReadOnlyMemory<byte> GetResult();
    }
}

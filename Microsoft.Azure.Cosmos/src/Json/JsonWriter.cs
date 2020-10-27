//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Globalization;
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
        /// <param name="jsonStringDictionary">The dictionary to use for user string encoding.</param>
        /// <param name="initalCapacity">Initial capacity to help avoid intermeidary allocations.</param>
        /// <returns>A JsonWriter that can write in a particular JsonSerializationFormat</returns>
        public static IJsonWriter Create(
            JsonSerializationFormat jsonSerializationFormat,
            JsonStringDictionary jsonStringDictionary = null,
            int initalCapacity = 256)
        {
            return jsonSerializationFormat switch
            {
                JsonSerializationFormat.Text => new JsonTextWriter(initalCapacity),
                JsonSerializationFormat.Binary => new JsonBinaryWriter(
                    jsonStringDictionary,
                    serializeCount: false),
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
        public abstract ReadOnlyMemory<byte> GetResult();
    }
}

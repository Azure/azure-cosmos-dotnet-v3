//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using Microsoft.Azure.Cosmos.Core.Utf8;

    /// <summary>
    /// Base abstract class for JSON readers.
    /// The reader defines methods that allow for reading a JSON encoded value as a stream of tokens.
    /// The tokens are traversed in the same order as they appear in the JSON document.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract partial class JsonReader : IJsonReader
    {
        /// <summary>
        /// The <see cref="JsonObjectState"/>
        /// </summary>
        protected readonly JsonObjectState JsonObjectState;

        /// <summary>
        /// Initializes a new instance of the JsonReader class.
        /// </summary>
        protected JsonReader()
        {
            this.JsonObjectState = new JsonObjectState(readMode: true);
        }

        /// <inheritdoc />
        public abstract JsonSerializationFormat SerializationFormat { get; }

        /// <inheritdoc />
        public int CurrentDepth => this.JsonObjectState.CurrentDepth;

        /// <inheritdoc />
        public JsonTokenType CurrentTokenType => this.JsonObjectState.CurrentTokenType;

        /// <summary>
        /// Creates a JsonReader that can read from the supplied byte array (assumes utf-8 encoding) with format marker.
        /// </summary>
        /// <param name="buffer">The byte array (with format marker) to read from.</param>
        /// <returns>A concrete JsonReader that can read the supplied byte array.</returns>
        public static IJsonReader Create(ReadOnlyMemory<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                throw new ArgumentOutOfRangeException($"{nameof(buffer)} can not be empty.");
            }

            byte firstByte = buffer.Span[0];

            // Explicitly pick from the set of supported formats, or otherwise assume text format
            JsonSerializationFormat jsonSerializationFormat = (firstByte == (byte)JsonSerializationFormat.Binary) ? JsonSerializationFormat.Binary : JsonSerializationFormat.Text;
            return JsonReader.Create(jsonSerializationFormat, buffer);
        }

        /// <summary>
        /// Creates a JsonReader with a given serialization format and byte array.
        /// </summary>
        /// <param name="jsonSerializationFormat">The serialization format of the payload.</param>
        /// <param name="buffer">The buffer to read from.</param>
        /// <returns>An <see cref="IJsonReader"/> for the buffer, format, and dictionary.</returns>
        public static IJsonReader Create(
            JsonSerializationFormat jsonSerializationFormat,
            ReadOnlyMemory<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                throw new ArgumentOutOfRangeException($"{nameof(buffer)} can not be empty.");
            }

            // Explicitly pick from the set of supported formats, or otherwise assume text format
            return jsonSerializationFormat switch
            {
                JsonSerializationFormat.Binary => new JsonBinaryReader(buffer),
                JsonSerializationFormat.Text => new JsonTextReader(buffer),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(JsonSerializationFormat)}: {jsonSerializationFormat}."),
            };
        }

        internal static IJsonReader CreateBinaryFromOffset(
            ReadOnlyMemory<byte> buffer,
            int offset) => new JsonBinaryReader(buffer, offset);

        #region IJsonReader
        /// <inheritdoc />
        public abstract bool Read();

        /// <inheritdoc />
        public abstract Number64 GetNumberValue();

        /// <inheritdoc />
        public abstract UtfAnyString GetStringValue();

        /// <inheritdoc />
        public abstract bool TryGetBufferedStringValue(out Utf8Memory value);

        /// <inheritdoc />
        public abstract sbyte GetInt8Value();

        /// <inheritdoc />
        public abstract short GetInt16Value();

        /// <inheritdoc />
        public abstract int GetInt32Value();

        /// <inheritdoc />
        public abstract long GetInt64Value();

        /// <inheritdoc />
        public abstract uint GetUInt32Value();

        /// <inheritdoc />
        public abstract float GetFloat32Value();

        /// <inheritdoc />
        public abstract double GetFloat64Value();

        /// <inheritdoc />
        public abstract Guid GetGuidValue();

        /// <inheritdoc />
        public abstract ReadOnlyMemory<byte> GetBinaryValue();

        /// <inheritdoc />
        public virtual void WriteCurrentToken(IJsonWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            JsonTokenType tokenType = this.CurrentTokenType;
            switch (tokenType)
            {
                case JsonTokenType.NotStarted:
                    break;

                case JsonTokenType.BeginArray:
                    writer.WriteArrayStart();
                    break;

                case JsonTokenType.EndArray:
                    writer.WriteArrayEnd();
                    break;

                case JsonTokenType.BeginObject:
                    writer.WriteObjectStart();
                    break;

                case JsonTokenType.EndObject:
                    writer.WriteObjectEnd();
                    break;

                case JsonTokenType.FieldName:
                case JsonTokenType.String:
                    {
                        bool isFieldName = tokenType == JsonTokenType.FieldName;

                        if (this.TryGetBufferedStringValue(out Utf8Memory bufferedStringValue))
                        {
                            if (isFieldName)
                            {
                                writer.WriteFieldName(bufferedStringValue.Span);
                            }
                            else
                            {
                                writer.WriteStringValue(bufferedStringValue.Span);
                            }
                        }
                        else
                        {
                            string value = this.GetStringValue();
                            if (isFieldName)
                            {
                                writer.WriteFieldName(value);
                            }
                            else
                            {
                                writer.WriteStringValue(value);
                            }
                        }
                    }
                    break;

                case JsonTokenType.Number:
                    if (this.TryGetUInt64NumberValue(out ulong uint64Value))
                    {
                        writer.WriteNumberValue(uint64Value);
                    }
                    else
                    {
                        Number64 value = this.GetNumberValue();
                        writer.WriteNumberValue(value);
                    }
                    break;

                case JsonTokenType.True:
                    writer.WriteBoolValue(true);
                    break;

                case JsonTokenType.False:
                    writer.WriteBoolValue(false);
                    break;

                case JsonTokenType.Null:
                    writer.WriteNullValue();
                    break;

                case JsonTokenType.Int8:
                    {
                        sbyte value = this.GetInt8Value();
                        writer.WriteInt8Value(value);
                    }
                    break;

                case JsonTokenType.Int16:
                    {
                        short value = this.GetInt16Value();
                        writer.WriteInt16Value(value);
                    }
                    break;

                case JsonTokenType.Int32:
                    {
                        int value = this.GetInt32Value();
                        writer.WriteInt32Value(value);
                    }
                    break;

                case JsonTokenType.Int64:
                    {
                        long value = this.GetInt64Value();
                        writer.WriteInt64Value(value);
                    }
                    break;

                case JsonTokenType.UInt32:
                    {
                        uint value = this.GetUInt32Value();
                        writer.WriteUInt32Value(value);
                    }
                    break;

                case JsonTokenType.Float32:
                    {
                        float value = this.GetFloat32Value();
                        writer.WriteFloat32Value(value);
                    }
                    break;

                case JsonTokenType.Float64:
                    {
                        double value = this.GetFloat64Value();
                        writer.WriteFloat64Value(value);
                    }
                    break;

                case JsonTokenType.Guid:
                    {
                        Guid value = this.GetGuidValue();
                        writer.WriteGuidValue(value);
                    }
                    break;

                case JsonTokenType.Binary:
                    {
                        ReadOnlyMemory<byte> value = this.GetBinaryValue();
                        writer.WriteBinaryValue(value.Span);
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown enum type: {tokenType}.");
            }
        }

        /// <inheritdoc />
        public virtual void WriteAll(IJsonWriter writer)
        {
            while (this.Read())
            {
                this.WriteCurrentToken(writer);
            }
        }
        #endregion

        /// <summary>
        /// Attempts to read the current number token as an unsigned 64-bit integer.
        /// </summary>
        /// <param name="value">When this method returns, contains the value of the current number token if it was an unsigned 64-bit integer; otherwise, the default value of <c>ulong</c>.</param>
        /// <returns><c>true</c> if the number token value is an unsigned 64-bit integer; otherwise, <c>false</c>.</returns>
        protected abstract bool TryGetUInt64NumberValue(out ulong value);
    }
}

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
            switch (jsonSerializationFormat)
            {
                case JsonSerializationFormat.Text:
                    return new JsonTextWriter(initalCapacity);
                case JsonSerializationFormat.Binary:
                    return new JsonBinaryWriter(
                        jsonStringDictionary,
                        serializeCount: false);
                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, RMResources.UnexpectedJsonSerializationFormat, jsonSerializationFormat));
            }
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
        public virtual void WriteFieldName(string fieldName, bool skipEscapedStringChecks = false)
        {
            int utf8Length = Encoding.UTF8.GetByteCount(fieldName);
            Span<byte> utf8Buffer = utf8Length < JsonTextWriter.MaxStackAlloc ? stackalloc byte[utf8Length] : new byte[utf8Length];
            Encoding.UTF8.GetBytes(fieldName, utf8Buffer);
            Utf8Span utf8FieldName = Utf8Span.UnsafeFromUtf8BytesNoValidation(utf8Buffer);

            this.WriteFieldName(utf8FieldName, skipEscapedStringChecks);
        }

        /// <inheritdoc />
        public abstract void WriteFieldName(Utf8Span fieldName, bool skipEscapedStringChecks = false);

        /// <inheritdoc />
        public virtual void WriteStringValue(string value, bool skipEscapedStringChecks = false)
        {
            int utf8Length = Encoding.UTF8.GetByteCount(value);
            Span<byte> utf8Buffer = utf8Length < JsonTextWriter.MaxStackAlloc ? stackalloc byte[utf8Length] : new byte[utf8Length];
            Encoding.UTF8.GetBytes(value, utf8Buffer);
            Utf8Span utf8Value = Utf8Span.UnsafeFromUtf8BytesNoValidation(utf8Buffer);

            this.WriteStringValue(utf8Value, skipEscapedStringChecks);
        }

        /// <inheritdoc />
        public abstract void WriteStringValue(Utf8Span value, bool skipEscapedStringChecks = false);

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
        public abstract void WriteRawJsonToken(JsonTokenType jsonTokenType, ReadOnlySpan<byte> rawJsonToken);

        /// <summary>
        /// Writes current token from a json reader to the internal buffer.
        /// </summary>
        /// <param name="jsonReader">The JsonReader to the get the current token from.</param>
        public void WriteCurrentToken(IJsonReader jsonReader)
        {
            if (jsonReader == null)
            {
                throw new ArgumentNullException("jsonReader can not be null");
            }

            bool sameFormat = jsonReader.SerializationFormat == this.SerializationFormat;

            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            switch (jsonTokenType)
            {
                case JsonTokenType.NotStarted:
                    break;

                case JsonTokenType.BeginArray:
                    this.WriteArrayStart();
                    break;

                case JsonTokenType.EndArray:
                    this.WriteArrayEnd();
                    break;

                case JsonTokenType.BeginObject:
                    this.WriteObjectStart();
                    break;

                case JsonTokenType.EndObject:
                    this.WriteObjectEnd();
                    break;

                case JsonTokenType.True:
                    this.WriteBoolValue(true);
                    break;

                case JsonTokenType.False:
                    this.WriteBoolValue(false);
                    break;

                case JsonTokenType.Null:
                    this.WriteNullValue();
                    break;

                default:
                    {
                        if (sameFormat && jsonReader.TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken))
                        {
                            this.WriteRawJsonToken(jsonTokenType, bufferedRawJsonToken.Span);
                        }
                        else
                        {
                            switch (jsonTokenType)
                            {
                                case JsonTokenType.String:
                                case JsonTokenType.FieldName:
                                    {
                                        string value = jsonReader.GetStringValue();
                                        if (jsonTokenType == JsonTokenType.FieldName)
                                        {
                                            this.WriteFieldName(value);
                                        }
                                        else
                                        {
                                            this.WriteStringValue(value);
                                        }

                                        break;
                                    }

                                case JsonTokenType.Number:
                                    {
                                        Number64 value = jsonReader.GetNumberValue();
                                        this.WriteNumber64Value(value);
                                    }
                                    break;

                                case JsonTokenType.Int8:
                                    {
                                        sbyte value = jsonReader.GetInt8Value();
                                        this.WriteInt8Value(value);
                                    }
                                    break;

                                case JsonTokenType.Int16:
                                    {
                                        short value = jsonReader.GetInt16Value();
                                        this.WriteInt16Value(value);
                                    }
                                    break;

                                case JsonTokenType.Int32:
                                    {
                                        int value = jsonReader.GetInt32Value();
                                        this.WriteInt32Value(value);
                                    }
                                    break;

                                case JsonTokenType.Int64:
                                    {
                                        long value = jsonReader.GetInt64Value();
                                        this.WriteInt64Value(value);
                                    }
                                    break;

                                case JsonTokenType.UInt32:
                                    {
                                        uint value = jsonReader.GetUInt32Value();
                                        this.WriteUInt32Value(value);
                                    }
                                    break;

                                case JsonTokenType.Float32:
                                    {
                                        float value = jsonReader.GetFloat32Value();
                                        this.WriteFloat32Value(value);
                                    }
                                    break;

                                case JsonTokenType.Float64:
                                    {
                                        double value = jsonReader.GetFloat64Value();
                                        this.WriteFloat64Value(value);
                                    }
                                    break;

                                case JsonTokenType.Guid:
                                    {
                                        Guid value = jsonReader.GetGuidValue();
                                        this.WriteGuidValue(value);
                                    }
                                    break;

                                case JsonTokenType.Binary:
                                    {
                                        ReadOnlyMemory<byte> value = jsonReader.GetBinaryValue();
                                        this.WriteBinaryValue(value.Span);
                                    }
                                    break;

                                default:
                                    throw new ArgumentException($"Unexpected JsonTokenType: {jsonTokenType}");
                            }
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Writes every token from the JsonReader to the internal buffer.
        /// </summary>
        /// <param name="jsonReader">The JsonReader to get the tokens from.</param>
        public void WriteAll(IJsonReader jsonReader)
        {
            if (jsonReader == null)
            {
                throw new ArgumentNullException("jsonReader can not be null");
            }

            while (jsonReader.Read())
            {
                this.WriteCurrentToken(jsonReader);
            }
        }

        /// <summary>
        /// Writes a fragment of a json to the internal buffer
        /// </summary>
        /// <param name="jsonFragment">A section of a valid json</param>
        public void WriteJsonFragment(ReadOnlyMemory<byte> jsonFragment)
        {
            IJsonReader jsonReader = JsonReader.Create(jsonFragment);
            this.WriteAll(jsonReader);
        }

        /// <inheritdoc />
        public abstract ReadOnlyMemory<byte> GetResult();
    }
}

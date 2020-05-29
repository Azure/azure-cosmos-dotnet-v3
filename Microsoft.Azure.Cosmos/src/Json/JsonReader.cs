//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;

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
        internal readonly JsonObjectState JsonObjectState;

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
        /// <param name="jsonStringDictionary">The dictionary to use for user string encoding.</param>
        /// <returns>A concrete JsonReader that can read the supplied byte array.</returns>
        public static IJsonReader Create(ReadOnlyMemory<byte> buffer, JsonStringDictionary jsonStringDictionary = null)
        {
            if (buffer.IsEmpty)
            {
                throw new ArgumentOutOfRangeException($"{nameof(buffer)} can not be empty.");
            }

            byte firstByte = buffer.Span[0];

            // Explicitly pick from the set of supported formats, or otherwise assume text format
            JsonSerializationFormat jsonSerializationFormat = (firstByte == (byte)JsonSerializationFormat.Binary) ? JsonSerializationFormat.Binary : JsonSerializationFormat.Text;
            if (jsonSerializationFormat == JsonSerializationFormat.Binary)
            {
                // offset for the 0x80 (128) binary serialization type marker.
                buffer = buffer.Slice(1);
            }

            return JsonReader.Create(jsonSerializationFormat, buffer, jsonStringDictionary);
        }

        public static IJsonReader Create(
            JsonSerializationFormat jsonSerializationFormat,
            ReadOnlyMemory<byte> buffer,
            JsonStringDictionary jsonStringDictionary = null)
        {
            if (buffer.IsEmpty)
            {
                throw new ArgumentOutOfRangeException($"{nameof(buffer)} can not be empty.");
            }

            // Explicitly pick from the set of supported formats, or otherwise assume text format
            switch (jsonSerializationFormat)
            {
                case JsonSerializationFormat.Binary:
                    return new JsonBinaryReader(buffer, jsonStringDictionary);

                case JsonSerializationFormat.Text:
                    return new JsonTextReader(buffer);

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(JsonSerializationFormat)}: {jsonSerializationFormat}.");
            }
        }

        /// <inheritdoc />
        public abstract bool Read();

        /// <inheritdoc />
        public abstract Number64 GetNumberValue();

        /// <inheritdoc />
        public abstract string GetStringValue();

        /// <inheritdoc />
        public abstract bool TryGetBufferedStringValue(out Utf8Memory value);

        /// <inheritdoc />
        public abstract bool TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken);

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
    }
}

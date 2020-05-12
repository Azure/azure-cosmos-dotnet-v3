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
        /// Whether to skip validation.
        /// </summary>
        protected readonly bool SkipValidation;

        /// <summary>
        /// Initializes a new instance of the JsonReader class.
        /// </summary>
        /// <param name="skipValidation">Whether or not to skip validation.</param>
        protected JsonReader(bool skipValidation)
        {
            this.JsonObjectState = new JsonObjectState(true);
            this.SkipValidation = skipValidation;
        }

        /// <inheritdoc />
        public abstract JsonSerializationFormat SerializationFormat { get; }

        /// <inheritdoc />
        public int CurrentDepth
        {
            get
            {
                return this.JsonObjectState.CurrentDepth;
            }
        }

        /// <inheritdoc />
        public JsonTokenType CurrentTokenType
        {
            get
            {
                return this.JsonObjectState.CurrentTokenType;
            }
        }

        /// <summary>
        /// Creates a JsonReader that can read from the supplied byte array (assumes utf-8 encoding).
        /// </summary>
        /// <param name="buffer">The byte array to read from.</param>
        /// <param name="jsonStringDictionary">The dictionary to use for user string encoding.</param>
        /// <param name="skipValidation">Whether or not to skip validation.</param>
        /// <returns>A concrete JsonReader that can read the supplied byte array.</returns>
        public static IJsonReader Create(ReadOnlyMemory<byte> buffer, JsonStringDictionary jsonStringDictionary = null, bool skipValidation = false)
        {
            if (buffer.IsEmpty)
            {
                throw new ArgumentOutOfRangeException($"{nameof(buffer)} can not be empty.");
            }

            byte firstByte = buffer.Span[0];

            // Explicitly pick from the set of supported formats, or otherwise assume text format
            switch ((JsonSerializationFormat)firstByte)
            {
                case JsonSerializationFormat.Binary:
                    return new JsonBinaryReader(buffer, jsonStringDictionary, skipValidation);
                default:
                    return new JsonTextReader(buffer, skipValidation);
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

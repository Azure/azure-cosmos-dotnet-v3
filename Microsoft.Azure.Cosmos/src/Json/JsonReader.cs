//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

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

        /// <summary>
        /// Gets the <see cref="JsonSerializationFormat"/> for the JsonReader
        /// </summary>
        public abstract JsonSerializationFormat SerializationFormat { get; }

        /// <summary>
        /// Gets the current level of nesting of the JSON that the JsonReader is reading.
        /// </summary>
        public int CurrentDepth
        {
            get
            {
                return this.JsonObjectState.CurrentDepth;
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonTokenType"/> of the current token that the JsonReader is about to read.
        /// </summary>
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
        public static IJsonReader Create(ArraySegment<byte> buffer, JsonStringDictionary jsonStringDictionary = null, bool skipValidation = false)
        {
            byte firstByte = buffer.AsSpan<byte>()[0];

            // Explicitly pick from the set of supported formats, or otherwise assume text format
            switch ((JsonSerializationFormat)firstByte)
            {
                case JsonSerializationFormat.Binary:
                    return new JsonBinaryReader(buffer, jsonStringDictionary, skipValidation);
                default:
                    return new JsonTextReader(buffer, skipValidation);
            }
        }

        /// <summary>
        /// Advances the JsonReader by one token.
        /// </summary>
        /// <returns><code>true</code> if the JsonReader successfully advanced to the next token; <code>false</code> if the JsonReader has passed the end of the JSON.</returns>
        public abstract bool Read();

        /// <inheritdoc />
        public abstract Number64 GetNumberValue();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a string.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a string.</returns>
        public abstract string GetStringValue();

        /// <summary>
        /// Gets next JSON token from the JsonReader as a raw series of bytes that is buffered.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a raw series of bytes that is buffered.</returns>
        public abstract ReadOnlySpan<byte> GetBufferedRawJsonToken();

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
        public abstract ReadOnlySpan<byte> GetBinaryValue();
    }
}

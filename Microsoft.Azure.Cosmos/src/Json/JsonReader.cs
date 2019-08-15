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
        /// Creates a JsonReader that can read a supplied stream (assumes UTF-8 encoding).
        /// </summary>
        /// <param name="stream">the stream to read.</param>
        /// <param name="jsonStringDictionary">The dictionary to use for binary user string encoding.</param>
        /// <param name="skipvalidation">whether or not to skip validation.</param>
        /// <returns>a concrete JsonReader that can read the supplied stream.</returns>
        public static IJsonReader Create(Stream stream, JsonStringDictionary jsonStringDictionary = null, bool skipvalidation = false)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            BinaryReader tempBinaryReader = new BinaryReader(stream, Encoding.UTF8);

            // examine the first buffer byte to determine the serialization format
            byte firstbyte = tempBinaryReader.ReadByte();

            // you have to rewind the stream since even "peeking" still reads it into the bufferedstream
            stream.Seek(0, SeekOrigin.Begin);

            // explicitly pick from the set of supported formats, or otherwise assume text format
            switch ((JsonSerializationFormat)firstbyte)
            {
                case JsonSerializationFormat.Binary:
                    return new JsonBinaryReader(stream, jsonStringDictionary, skipvalidation);
                default:
                    return new JsonTextReader(stream, Encoding.UTF8, skipvalidation);
            }
        }

        /// <summary>
        /// Creates a JsonTextReader that can read a supplied stream with the specified encoding.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="encoding">The encoding of the text (UTF-8, UTF-16 / Unicode, or UTF-32).</param>
        /// <param name="skipValidation">Whether or not to skip validation.</param>
        /// <returns>A concrete JsonReader that can read the supplied stream.</returns>
        public static IJsonReader CreateTextReaderWithEncoding(Stream stream, Encoding encoding, bool skipValidation = false)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            if (encoding != Encoding.UTF8 && encoding != Encoding.Unicode && encoding != Encoding.UTF32)
            {
                throw new ArgumentException("Json Text only supports UTF8, UTF16/Unicode, or UTF32");
            }

            return new JsonTextReader(stream, encoding, skipValidation);
        }

        /// <summary>
        /// Creates a JsonReader that can read from the supplied byte array (assumes utf-8 encoding).
        /// </summary>
        /// <param name="buffer">The byte array to read from.</param>
        /// <param name="jsonStringDictionary">The dictionary to use for user string encoding.</param>
        /// <param name="skipValidation">Whether or not to skip validation.</param>
        /// <returns>A concrete JsonReader that can read the supplied byte array.</returns>
        public static IJsonReader Create(byte[] buffer, JsonStringDictionary jsonStringDictionary = null, bool skipValidation = false)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            byte firstByte = buffer[0];

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

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a double.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a double.</returns>
        public abstract double GetNumberValue();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a string.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a string.</returns>
        public abstract string GetStringValue();

        /// <summary>
        /// Gets next JSON token from the JsonReader as a raw series of bytes that is buffered.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a raw series of bytes that is buffered.</returns>
        public abstract IReadOnlyList<byte> GetBufferedRawJsonToken();
    }
}

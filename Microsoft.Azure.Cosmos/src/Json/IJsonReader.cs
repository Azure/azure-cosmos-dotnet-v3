//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Interface for all JsonReaders that know how to read jsons.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    interface IJsonReader
    {
        /// <summary>
        /// Gets the <see cref="JsonSerializationFormat"/> for the JsonReader
        /// </summary>
        JsonSerializationFormat SerializationFormat { get; }

        /// <summary>
        /// Gets the current level of nesting of the JSON that the JsonReader is reading.
        /// </summary>
        int CurrentDepth { get; }

        /// <summary>
        /// Gets the <see cref="JsonTokenType"/> of the current token that the JsonReader is about to read.
        /// </summary>
        JsonTokenType CurrentTokenType { get; }

        /// <summary>
        /// Advances the JsonReader by one token.
        /// </summary>
        /// <returns><code>true</code> if the JsonReader successfully advanced to the next token; <code>false</code> if the JsonReader has passed the end of the JSON.</returns>
        bool Read();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a double.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a double.</returns>
        Number64 GetNumberValue();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a string.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a string.</returns>
        string GetStringValue();

        /// <summary>
        /// Tries to get the buffered UTF-8 string value.
        /// </summary>
        /// <param name="bufferedUtf8StringValue">The buffered UTF-8 string value if found.</param>
        /// <returns>true if the buffered UTF-8 string value was retrieved; false otherwise.</returns>
        bool TryGetBufferedStringValue(out Utf8Memory bufferedUtf8StringValue);

        /// <summary>
        /// Tries to get the current JSON token from the JsonReader as a raw series of bytes that is buffered.
        /// </summary>
        /// <returns>true if the current JSON token was retrieved; false otherwise.</returns>
        bool TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken);

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a 1 byte signed integer.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a 1 byte signed integer.</returns>
        sbyte GetInt8Value();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a 2 byte signed integer.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a 2 byte signed integer.</returns>
        short GetInt16Value();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a 4 byte signed integer.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a 4 byte signed integer.</returns>
        int GetInt32Value();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a 8 byte signed integer.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a 8 byte signed integer.</returns>
        long GetInt64Value();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a 4 byte unsigned integer.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a 4 byte unsigned integer.</returns>
        uint GetUInt32Value();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a single precision floating point.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a single precision floating point.</returns>
        float GetFloat32Value();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a double precision floating point.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a double precision floating point.</returns>
        double GetFloat64Value();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a GUID.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a GUID.</returns>
        Guid GetGuidValue();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a binary list.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a binary list.</returns>
        ReadOnlyMemory<byte> GetBinaryValue();
    }
}

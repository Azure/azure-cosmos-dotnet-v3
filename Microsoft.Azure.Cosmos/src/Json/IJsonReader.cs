//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;

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
        /// <param name="value">The buffered UTF-8 string value if found.</param>
        /// <returns>true if the buffered UTF-8 string value was retrieved; false otherwise.</returns>
        bool TryGetBufferedStringValue(out Utf8Memory value);

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

        /// <summary>
        /// Writes the current token on the reader to the writer.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        void WriteCurrentToken(IJsonWriter writer);

        /// <summary>
        /// Writes all the tokens in the reader to the writer.
        /// </summary>
        /// <param name="writer"></param>
        void WriteAll(IJsonWriter writer);

        /// <summary>
        /// Attempt to read a '$t': TYPECODE, '$v' in one call.
        /// If unsuccessful, the reader is left in its original state.
        /// Otherwise it is positioned at the value after the $v.
        /// </summary>
        /// <param name="typeCode">The type code read.</param>
        /// <returns>Success.</returns>
        bool TryReadTypedJsonValueWrapper(out int typeCode);
    }
}

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
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
        double GetNumberValue();

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a string.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a string.</returns>
        string GetStringValue();

        /// <summary>
        /// Gets current JSON token from the JsonReader as a raw series of bytes that is buffered.
        /// </summary>
        /// <returns>The current JSON token from the JsonReader as a raw series of bytes that is buffered.</returns>
        IReadOnlyList<byte> GetBufferedRawJsonToken();
    }
}

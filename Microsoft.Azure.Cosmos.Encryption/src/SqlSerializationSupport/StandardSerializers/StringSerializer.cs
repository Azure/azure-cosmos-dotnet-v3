//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using Microsoft.Azure.Cosmos.Linq;
    using static System.Text.Encoding;

    /// <inheritdoc/>
    public class StringSerializer : Serializer<string>
    {
        /// <inheritdoc/>
        public override string Identifier => "String";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        public override string Deserialize(byte[] bytes)
        {
            return Unicode.GetString(bytes);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> is null.
        /// </exception>
        public override byte[] Serialize(string value)
        {
            return Unicode.GetBytes(value);
        }
    }
}
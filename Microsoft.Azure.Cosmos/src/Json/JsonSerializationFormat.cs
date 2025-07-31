//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    /// <summary>
    /// Defines JSON different serialization Formats
    /// </summary>
    /// <remarks>
    /// Every enumeration type has an underlying type, which can be any integral type except char.
    /// The default underlying type of enumeration elements is integer.
    /// To declare an enum of another integral type, such as byte, use a colon after the identifier followed by the type, as shown in the following example.
    /// </remarks>
#if INTERNAL
    public
#else
    public
#endif
    enum JsonSerializationFormat : byte
    {
        /// <summary>
        /// Plain text
        /// </summary>
        Text = 0,

        /// <summary>
        /// Binary Encoding
        /// </summary>
        Binary = 128,

        /// <summary>
        /// HybridRow Binary Encoding
        /// </summary>
        HybridRow = 129,

        // All other format values need to be > 127,
        // otherwise a valid JSON starting character (0-9, f[alse], t[rue], n[ull],{,[,") might be interpreted as a serialization format.
    }
}

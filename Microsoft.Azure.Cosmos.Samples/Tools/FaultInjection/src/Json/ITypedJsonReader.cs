﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using Microsoft.Azure.Cosmos.Core.Utf8;

    /// <summary>
    /// Interface for all TypedJsonReaders that know how to read typed json.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    interface ITypedJsonReader : IJsonReader
    {
        /// <summary>
        /// Attempt to read a '$t': TYPECODE, '$v' in one call.
        /// If unsuccessful, the reader is left in its original state.
        /// Otherwise it is positioned at the value after the $v.
        /// </summary>
        /// <param name="typeCode">The type code read.</param>
        /// <returns>Success.</returns>
        bool TryReadTypedJsonValueWrapper(out int typeCode);

        /// <summary>
        /// Gets the next JSON token from the JsonReader as a UTF-8 span.
        /// </summary>
        /// <returns>The next JSON token from the JsonReader as a span.</returns>
        Utf8Span GetUtf8SpanValue();
    }
}
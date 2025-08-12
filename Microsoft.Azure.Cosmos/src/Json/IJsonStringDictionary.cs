// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using Microsoft.Azure.Cosmos.Core.Utf8;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    interface IJsonStringDictionary : IEquatable<IJsonStringDictionary>
    {
        bool TryGetString(int stringId, out UtfAllString value);

        bool TryGetStringId(Utf8Span value, out int stringId);

        public int GetCount();
    }
}

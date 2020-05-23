// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using Microsoft.Azure.Cosmos.Core.Utf8;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    interface IReadOnlyJsonStringDictionary
    {
        bool TryGetStringAtIndex(int index, out UtfAllString value);
    }
}

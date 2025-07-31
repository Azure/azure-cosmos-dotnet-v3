// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public static class StackExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Empty<T>(this Stack<T> stack)
        {
            return stack.Count == 0;
        }
    }
}

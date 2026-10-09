//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System.Collections.Generic;

    internal static class JsonBoundaryCases
    {
        public static IEnumerable<object[]> RequiredObjectBodies
        {
            get
            {
                yield return new object[] { "empty", string.Empty, false };
                yield return new object[] { "whitespace", " \r\n\t", false };
                yield return new object[] { "null", "null", false };
                yield return new object[] { "string", "\"value\"", false };
                yield return new object[] { "number", "42", false };
                yield return new object[] { "array", "[]", false };
                yield return new object[] { "malformed", "{\"id\":", true };
                yield return new object[] { "trailing content", "{}{}", true };
            }
        }
    }
}
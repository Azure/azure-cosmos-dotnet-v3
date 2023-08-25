// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Parser
{
    using System;

    internal sealed class ParseException : Exception
    {
        public ParseException(string message = null, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}

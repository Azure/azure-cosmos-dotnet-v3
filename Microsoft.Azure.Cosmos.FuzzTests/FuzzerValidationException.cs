//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FuzzTests
{
    using System;

    /// <summary>
    /// Exception thrown by fuzz harnesses to signal a logical validation failure
    /// (e.g., round-trip inconsistency) as opposed to a parser crash.
    /// </summary>
    internal sealed class FuzzerValidationException : Exception
    {
        public FuzzerValidationException(string message)
            : base(message)
        {
        }

        public FuzzerValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

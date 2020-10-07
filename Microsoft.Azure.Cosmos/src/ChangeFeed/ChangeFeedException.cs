// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;

    internal abstract class ChangeFeedException : Exception
    {
        protected ChangeFeedException()
            : base()
        {
        }

        protected ChangeFeedException(string message)
            : base(message)
        {
        }

        protected ChangeFeedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

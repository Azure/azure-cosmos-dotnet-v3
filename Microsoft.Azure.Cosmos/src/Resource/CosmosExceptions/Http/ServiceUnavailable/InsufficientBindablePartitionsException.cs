// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.ServiceUnavailable
{
    using System;

    internal sealed class InsufficientBindablePartitionsException : ServiceUnavailableException
    {
        public InsufficientBindablePartitionsException()
            : this(message: null)
        {
        }

        public InsufficientBindablePartitionsException(string message)
            : this(message: message, innerException: null)
        {
        }

        public InsufficientBindablePartitionsException(string message, Exception innerException)
            : base(subStatusCode: (int)ServiceUnavailableSubStatusCode.InsufficientBindablePartitions, message: message, innerException: innerException)
        {
        }
    }
}

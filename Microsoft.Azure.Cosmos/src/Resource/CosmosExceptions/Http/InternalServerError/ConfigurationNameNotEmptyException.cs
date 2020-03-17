// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.InternalServerError
{
    using System;

    internal sealed class ConfigurationNameNotEmptyException : InternalServerErrorException
    {
        public ConfigurationNameNotEmptyException()
            : this(message: null)
        {
        }

        public ConfigurationNameNotEmptyException(string message)
            : this(message: message, innerException: null)
        {
        }

        public ConfigurationNameNotEmptyException(string message, Exception innerException)
            : base(subStatusCode: (int)InternalServerErrorSubStatusCode.ConfigurationNameNotEmpty, message: message, innerException: innerException)
        {
        }
    }
}

// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Conflict
{
    using System;

    internal sealed class ConfigurationNameAlreadyExistsException : ConflictException
    {
        public ConfigurationNameAlreadyExistsException()
            : this(message: null)
        {
        }

        public ConfigurationNameAlreadyExistsException(string message)
            : this(message: message, innerException: null)
        {
        }

        public ConfigurationNameAlreadyExistsException(string message, Exception innerException)
            : base(subStatusCode: (int)ConflictSubStatusCode.ConfigurationNameAlreadyExists, message: message, innerException: innerException)
        {
        }
    }
}

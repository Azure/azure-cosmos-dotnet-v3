// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.NotFound
{
    using System;

    internal sealed class ConfigurationPropertyNotFoundException : NotFoundException
    {
        public ConfigurationPropertyNotFoundException()
            : this(message: null)
        {
        }

        public ConfigurationPropertyNotFoundException(string message)
            : this(message: message, innerException: null)
        {
        }

        public ConfigurationPropertyNotFoundException(string message, Exception innerException)
            : base(subStatusCode: (int)NotFoundSubStatusCode.ConfigurationPropertyNotFound, message: message, innerException: innerException)
        {
        }
    }
}

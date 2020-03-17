// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.NotFound
{
    using System;

    internal sealed class ConfigurationNameNotFoundException : NotFoundException
    {
        public ConfigurationNameNotFoundException()
            : this(message: null)
        {
        }

        public ConfigurationNameNotFoundException(string message)
            : this(message: message, innerException: null)
        {
        }

        public ConfigurationNameNotFoundException(string message, Exception innerException)
            : base(subStatusCode: (int)NotFoundSubStatusCode.ConfigurationNameNotFound, message: message, innerException: innerException)
        {
        }
    }
}

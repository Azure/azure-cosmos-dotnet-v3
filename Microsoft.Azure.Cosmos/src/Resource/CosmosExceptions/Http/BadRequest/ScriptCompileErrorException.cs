// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.BadRequest
{
    using System;

    internal sealed class ScriptCompileErrorException : BadRequestException
    {
        public ScriptCompileErrorException()
            : this(message: null)
        {
        }

        public ScriptCompileErrorException(string message)
            : this(message: message, innerException: null)
        {
        }

        public ScriptCompileErrorException(string message, Exception innerException)
            : base(subStatusCode: (int)BadRequestSubStatusCode.ScriptCompileError, message: message, innerException: innerException)
        {
        }
    }
}

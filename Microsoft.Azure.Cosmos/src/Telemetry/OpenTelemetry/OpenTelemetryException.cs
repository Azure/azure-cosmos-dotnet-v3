//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;

    internal class OpenTelemetryException : OpenTelemetryAttributes
    {
        internal OpenTelemetryException(string containerName, string databaseName, Exception exception)
            : base(null/*need to check*/, containerName, databaseName)
        {
            this.OriginalException = exception;
        }
        
        internal Exception OriginalException { get; }
    }
}

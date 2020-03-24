// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 43

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestTimeout
{
    using System;
    using System.Net;

    internal abstract class RequestTimeoutBaseException : CosmosHttpException
    {
        protected RequestTimeoutBaseException(
            int subStatusCode, 
            CosmosDiagnosticsContext cosmosDiagnosticsContext)
            : this(
                subStatusCode, 
                cosmosDiagnosticsContext, 
                message: null)
        {
        }

        protected RequestTimeoutBaseException(
            int subStatusCode, 
            CosmosDiagnosticsContext cosmosDiagnosticsContext, 
            string message)
            : this(
                subStatusCode, 
                cosmosDiagnosticsContext, 
                message: message, 
                innerException: null)
        {
        }

        protected RequestTimeoutBaseException(
            int subStatusCode, 
            CosmosDiagnosticsContext cosmosDiagnosticsContext, 
            string message, 
            Exception innerException)
            : base(
                statusCode: HttpStatusCode.RequestTimeout, 
                subStatusCode: subStatusCode, 
                cosmosDiagnosticsContext: cosmosDiagnosticsContext, 
                message: message, 
                innerException: innerException)
        {
        }
    }
}

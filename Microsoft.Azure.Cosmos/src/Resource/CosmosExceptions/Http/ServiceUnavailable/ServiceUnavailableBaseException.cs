// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 43

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.ServiceUnavailable
{
    using System;
    using System.Net;

    internal abstract class ServiceUnavailableBaseException : CosmosHttpException
    {
        protected ServiceUnavailableBaseException(
            int subStatusCode, 
            CosmosDiagnosticsContext cosmosDiagnosticsContext)
            : this(
                subStatusCode, 
                cosmosDiagnosticsContext, 
                message: null)
        {
        }

        protected ServiceUnavailableBaseException(
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

        protected ServiceUnavailableBaseException(
            int subStatusCode, 
            CosmosDiagnosticsContext cosmosDiagnosticsContext, 
            string message, 
            Exception innerException)
            : base(
                statusCode: HttpStatusCode.ServiceUnavailable, 
                subStatusCode: subStatusCode, 
                cosmosDiagnosticsContext: cosmosDiagnosticsContext, 
                message: message, 
                innerException: innerException)
        {
        }
    }
}

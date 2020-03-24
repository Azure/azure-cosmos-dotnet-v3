// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 43

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestEntityTooLarge
{
    using System;
    using System.Net;

    internal abstract class RequestEntityTooLargeBaseException : CosmosHttpException
    {
        protected RequestEntityTooLargeBaseException(
            int subStatusCode, 
            CosmosDiagnosticsContext cosmosDiagnosticsContext)
            : this(
                subStatusCode, 
                cosmosDiagnosticsContext, 
                message: null)
        {
        }

        protected RequestEntityTooLargeBaseException(
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

        protected RequestEntityTooLargeBaseException(
            int subStatusCode, 
            CosmosDiagnosticsContext cosmosDiagnosticsContext, 
            string message, 
            Exception innerException)
            : base(
                statusCode: HttpStatusCode.RequestEntityTooLarge, 
                subStatusCode: subStatusCode, 
                cosmosDiagnosticsContext: cosmosDiagnosticsContext, 
                message: message, 
                innerException: innerException)
        {
        }
    }
}

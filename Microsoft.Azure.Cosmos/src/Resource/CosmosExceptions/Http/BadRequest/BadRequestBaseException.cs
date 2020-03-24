// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 43

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.BadRequest
{
    using System;
    using System.Net;

    internal abstract class BadRequestBaseException : CosmosHttpException
    {
        protected BadRequestBaseException(
            int subStatusCode, 
            CosmosDiagnosticsContext cosmosDiagnosticsContext)
            : this(
                subStatusCode, 
                cosmosDiagnosticsContext, 
                message: null)
        {
        }

        protected BadRequestBaseException(
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

        protected BadRequestBaseException(
            int subStatusCode, 
            CosmosDiagnosticsContext cosmosDiagnosticsContext, 
            string message, 
            Exception innerException)
            : base(
                statusCode: HttpStatusCode.BadRequest, 
                subStatusCode: subStatusCode, 
                cosmosDiagnosticsContext: cosmosDiagnosticsContext, 
                message: message, 
                innerException: innerException)
        {
        }
    }
}

// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Net;

    /// <summary>
    /// CosmosException used for mocking that supresses the obsolete error.
    /// </summary>
    internal sealed class TestCosmosException : CosmosException
    {
        public TestCosmosException(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string message = null,
            int subStatusCode = 0,
            string stackTrace = null,
            string activityId = null,
            double requestCharge = 0,
            TimeSpan? retryAfter = null,
            Headers headers = null,
            CosmosDiagnosticsContext diagnosticsContext = null,
            Exception innerException = null)
#pragma warning disable CS0618 // Type or member is obsolete
            : base(statusCode, message, subStatusCode, stackTrace, activityId, requestCharge, retryAfter, headers, diagnosticsContext, error: null, innerException)
#pragma warning restore CS0618 // Type or member is obsolete
        {
        }
    }
}

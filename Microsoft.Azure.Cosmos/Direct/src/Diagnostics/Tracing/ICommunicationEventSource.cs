//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Net.Http.Headers;

    internal interface ICommunicationEventSource
    {
        void Request(Guid activityId, Guid localId, string uri, string resourceType, HttpRequestHeaders requestHeaders);

        void Response(Guid activityId, Guid localId, short statusCode, double milliseconds, HttpResponseHeaders responseHeaders);
    }
}

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents.Collections;

    [Serializable]
    internal sealed class NotFoundException : DocumentClientException
    {
        public NotFoundException()
            : this(RMResources.NotFound)
        {

        }

        public NotFoundException(string message)
            : this(message, (Exception)null, null)
        {

        }

        public NotFoundException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {

        }

        public NotFoundException(string message, Exception innerException)
            : this(message, innerException, null)
        {

        }

        public NotFoundException(Exception innerException, bool traceCallStack = true)
            : this(RMResources.NotFound, innerException, null, traceCallStack: traceCallStack)
        {

        }

        public NotFoundException(Exception innerException, SubStatusCodes subStatusCode, bool traceCallStack = true)
            : this(RMResources.NotFound, innerException, headers: null, subStatusCode: subStatusCode, traceCallStack: traceCallStack)
        {
        }

        public NotFoundException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.NotFound, requestUri)
        {
            SetDescription();
        }

        public NotFoundException(string message, Exception innerException, HttpResponseHeaders headers, Uri requestUri = null, SubStatusCodes? subStatusCode = null, bool traceCallStack = true)
            : base(message, innerException, headers, HttpStatusCode.NotFound, requestUri, subStatusCode, traceCallStack)
        {
            SetDescription();
        }

        public NotFoundException(string message, Exception innerException, INameValueCollection headers, SubStatusCodes? subStatusCode)
            : base(message, innerException, headers, HttpStatusCode.NotFound, subStatusCode)
        {
            SetDescription();
        }

#if !NETSTANDARD16
        private NotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context, HttpStatusCode.NotFound)
        {

        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.NotFound;
        }
    }
}

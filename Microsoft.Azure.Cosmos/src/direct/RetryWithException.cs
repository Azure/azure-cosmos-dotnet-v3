//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Documents.Collections;
    using System;
    using System.Collections.Specialized;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;

    [Serializable]
    internal sealed class RetryWithException : DocumentClientException
    {
        public RetryWithException(string retryMessage)
            : this(retryMessage, (INameValueCollection)null)
        {

        }

        public RetryWithException(Exception innerException)
            : base(
                  RMResources.RetryWith,
                  innerException,
                  responseHeaders: (HttpResponseHeaders)null,
                  statusCode: (HttpStatusCode)StatusCodes.RetryWith)
        {
        }

        public RetryWithException(string retryMessage, HttpResponseHeaders headers, Uri requestUri = null)
            : base(retryMessage, null, headers, (HttpStatusCode)StatusCodes.RetryWith, requestUri)
        {
            SetDescription();
        }

        public RetryWithException(string retryMessage, INameValueCollection headers, Uri requestUri = null)
            : base(retryMessage, null, headers, (HttpStatusCode)StatusCodes.RetryWith, requestUri)
        {
            SetDescription();
        }

#if !NETSTANDARD16
        private RetryWithException(SerializationInfo info, StreamingContext context)
            : base(info, context, (HttpStatusCode)StatusCodes.RetryWith)
        {
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.RetryWith;
        }
    }
}

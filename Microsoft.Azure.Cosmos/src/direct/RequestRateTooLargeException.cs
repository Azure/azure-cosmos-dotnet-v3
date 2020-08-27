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
    internal sealed class RequestRateTooLargeException : DocumentClientException
    {
        public RequestRateTooLargeException()
            : this(RMResources.TooManyRequests)
        {

        }

        public RequestRateTooLargeException(string message)
            : this(message, (Exception)null, null)
        {

        }

        public RequestRateTooLargeException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {

        }

        public RequestRateTooLargeException(string message, SubStatusCodes subStatus)
            : base(message, (HttpStatusCode)StatusCodes.TooManyRequests, subStatus) { }

        public RequestRateTooLargeException(Exception innerException)
            : this(RMResources.TooManyRequests, innerException, null)
        {

        }

        public RequestRateTooLargeException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, (HttpStatusCode)StatusCodes.TooManyRequests, requestUri)
        {
            SetDescription();
        }

        public RequestRateTooLargeException(string message, Exception innerException, HttpResponseHeaders headers, Uri requestUri = null)
            : base(message, innerException, headers, (HttpStatusCode)StatusCodes.TooManyRequests, requestUri)
        {
            SetDescription();
        }

#if !NETSTANDARD16
        private RequestRateTooLargeException(SerializationInfo info, StreamingContext context)
            : base(info, context, (HttpStatusCode)StatusCodes.TooManyRequests)
        {
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.TooManyRequests;
        }
    }
}

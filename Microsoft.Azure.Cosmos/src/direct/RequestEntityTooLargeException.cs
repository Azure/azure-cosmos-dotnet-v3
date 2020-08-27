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
    internal sealed class RequestEntityTooLargeException : DocumentClientException
    {
        public RequestEntityTooLargeException()
            : this(RMResources.RequestEntityTooLarge)
        {

        }

        public RequestEntityTooLargeException(string message)
            : this(message, (Exception)null, null)
        {

        }

        public RequestEntityTooLargeException(string message, HttpResponseHeaders httpHeaders, Uri requestUri = null)
            : this(message, null, httpHeaders, requestUri)
        {

        }

        public RequestEntityTooLargeException(Exception innerException)
            : this(RMResources.RequestEntityTooLarge, innerException, null)
        {

        }

        public RequestEntityTooLargeException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.RequestEntityTooLarge, requestUri)
        {
            SetDescription();
        }

        public RequestEntityTooLargeException(string message, Exception innerException, HttpResponseHeaders headers, Uri requestUri = null)
            : base(message, innerException, headers, HttpStatusCode.RequestEntityTooLarge, requestUri)
        {
            SetDescription();
        }

#if !NETSTANDARD16
        private RequestEntityTooLargeException(SerializationInfo info, StreamingContext context)
            : base(info, context, HttpStatusCode.RequestEntityTooLarge)
        {
            SetDescription();
        }

#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.RequestEntityTooLarge;
        }
    }
}

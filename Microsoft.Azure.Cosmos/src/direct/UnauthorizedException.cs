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
    internal sealed class UnauthorizedException : DocumentClientException
    {
        public UnauthorizedException()
            : this(RMResources.Unauthorized)
        {

        }

        public UnauthorizedException(string message)
            : this(message, (Exception)null, (HttpResponseHeaders)null)
        {

        }

        public UnauthorizedException(string message, SubStatusCodes subStatusCode)
            : this(message, (Exception)null, (HttpResponseHeaders)null, null, subStatusCode)
        {

        }

        public UnauthorizedException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {

        }

        public UnauthorizedException(Exception innerException)
            : this(RMResources.Unauthorized, innerException, null)
        {

        }

        public UnauthorizedException(string message, Exception innerException)
            : this(message, innerException, null)
        {

        }

        public UnauthorizedException(string message, Exception innerException, SubStatusCodes subStatusCode)
            : this(message, innerException, null, null, subStatusCode)
        {

        }

        public UnauthorizedException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.Unauthorized, requestUri)
        {
            SetDescription();
        }

        public UnauthorizedException(string message,
            Exception innerException,
            HttpResponseHeaders headers,
            Uri requestUri = null,
            SubStatusCodes? subStatusCode = null)
            : base(message, innerException, headers, HttpStatusCode.Unauthorized, requestUri, subStatusCode)
        {
            SetDescription();
        }

#if !NETSTANDARD16
        private UnauthorizedException(SerializationInfo info, StreamingContext context)
            : base(info, context, HttpStatusCode.Unauthorized)
        {
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.Unauthorized;
        }
    }
}

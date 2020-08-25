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
    internal sealed class InternalServerErrorException : DocumentClientException
    {
        public InternalServerErrorException()
            : this(RMResources.InternalServerError)
        {

        }

        public InternalServerErrorException(string message, Uri requestUri = null)
            : this(message, null, null, requestUri)
        {

        }

        public InternalServerErrorException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {

        }

        public InternalServerErrorException(Exception innerException)
            : this(RMResources.InternalServerError, innerException, null)
        {

        }

        public InternalServerErrorException(string message, Exception innerException, Uri requestUri = null)
            : this(message, innerException, null, requestUri)
        {

        }

        public InternalServerErrorException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.InternalServerError, requestUri)
        {
            SetDescription();
        }

        public InternalServerErrorException(string message,
            Exception innerException,
            HttpResponseHeaders headers,
            Uri requestUri = null)
            : base(message, innerException, headers, HttpStatusCode.InternalServerError, requestUri)
        {
            SetDescription();
        }

#if !NETSTANDARD16
        private InternalServerErrorException(SerializationInfo info, StreamingContext context) 
            : base(info, context, HttpStatusCode.InternalServerError)
        {
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.InternalServerError;
        }
    }
}

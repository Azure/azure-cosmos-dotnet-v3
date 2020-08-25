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
    internal sealed class ServiceUnavailableException : DocumentClientException
    {
        public ServiceUnavailableException()
            : this(RMResources.ServiceUnavailable)
        {

        }

        public ServiceUnavailableException(string message, SubStatusCodes subStatusCode)
            : base(message, HttpStatusCode.ServiceUnavailable, subStatusCode)
        {

        }

        public ServiceUnavailableException(string message, Uri requestUri = null)
            : this(message, null, null, requestUri)
        {

        }

        public ServiceUnavailableException(string message, Exception innerException, Uri requestUri = null)
            : this(message, innerException, null, requestUri)
        {

        }

        public ServiceUnavailableException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {

        }

        public ServiceUnavailableException(Exception innerException)
            : this(RMResources.ServiceUnavailable, innerException, null)
        {

        }

        public ServiceUnavailableException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.ServiceUnavailable, requestUri)
        {
            SetDescription();
        }

        public ServiceUnavailableException(string message,
            Exception innerException,
            HttpResponseHeaders headers,
            Uri requestUri = null)
            : base(message, innerException, headers, HttpStatusCode.ServiceUnavailable, requestUri)
        {
            SetDescription();
        }
       
#if !NETSTANDARD16
        private ServiceUnavailableException(SerializationInfo info, StreamingContext context)
            : base(info, context, HttpStatusCode.ServiceUnavailable)
        {
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.ServiceUnavailable;
        }
    }
}

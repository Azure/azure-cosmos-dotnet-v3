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
    internal sealed class ServiceUnavailableException : DocumentClientException
    {
        public ServiceUnavailableException()
            : this(RMResources.ServiceUnavailable, SubStatusCodes.Unknown)
        {

        }

        public ServiceUnavailableException(string message)
            : this(message, null, null, SubStatusCodes.Unknown)
        {
        }

        public ServiceUnavailableException(string message, SubStatusCodes subStatusCode, Uri requestUri = null)
            : this(message, null, null, subStatusCode, requestUri)
        {

        }

        public ServiceUnavailableException(string message, Exception innerException, SubStatusCodes subStatusCode, Uri requestUri = null)
            : this(message, innerException, null, subStatusCode, requestUri)
        {

        }

        public ServiceUnavailableException(string message, HttpResponseHeaders headers, SubStatusCodes? subStatusCode, Uri requestUri = null)
            : this(message, null, headers, subStatusCode, requestUri)
        {

        }

        public ServiceUnavailableException(Exception innerException, SubStatusCodes subStatusCode)
            : this(RMResources.ServiceUnavailable, innerException, null, subStatusCode)
        {

        }

        public ServiceUnavailableException(string message, INameValueCollection headers, SubStatusCodes? subStatusCode, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.ServiceUnavailable, subStatusCode, requestUri)
        {
            SetDescription();
        }

        public ServiceUnavailableException(string message,
            Exception innerException,
            HttpResponseHeaders headers,
            SubStatusCodes? subStatusCode,
            Uri requestUri = null)
            : base(message, innerException, headers, HttpStatusCode.ServiceUnavailable, requestUri, subStatusCode)
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

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

        public NotFoundException(Exception innerException)
            : this(RMResources.NotFound, innerException, null)
        {

        }

        public NotFoundException(Exception innerException, SubStatusCodes subStatusCode)
            : this(RMResources.NotFound, innerException, headers: null, subStatusCode: subStatusCode)
        {
        }

        public NotFoundException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.NotFound, requestUri)
        {
            SetDescription();
        }

        public NotFoundException(string message, Exception innerException, HttpResponseHeaders headers, Uri requestUri = null, SubStatusCodes? subStatusCode = null)
            : base(message, innerException, headers, HttpStatusCode.NotFound, requestUri, subStatusCode)
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

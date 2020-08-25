//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Documents.Collections;
    using System;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;

    [Serializable]
    internal sealed class ConflictException : DocumentClientException
    {
        public ConflictException()
            : this(RMResources.EntityAlreadyExists)
        {

        }

        public ConflictException(string message)
            : this(message, (Exception)null, null)
        {

        }

        public ConflictException(string message, SubStatusCodes subStatusCode)
            : base(message, HttpStatusCode.Conflict, subStatusCode)
        {

        }

        public ConflictException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {

        }

        public ConflictException(Exception innerException)
            : this(RMResources.EntityAlreadyExists, innerException, null)
        {

        }

        public ConflictException(Exception innerException, SubStatusCodes subStatusCode)
            : this(RMResources.EntityAlreadyExists, innerException, headers: null, subStatusCode: subStatusCode)
        {
        }

        public ConflictException(string message, Exception innerException)
            : this(message, innerException, null)
        {

        }

        public ConflictException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.Conflict, requestUri)
        {
            SetDescription();
        }

        public ConflictException(string message, Exception innerException, HttpResponseHeaders headers, Uri requestUri = null, SubStatusCodes? subStatusCode = null)
            : base(message, innerException, headers, HttpStatusCode.Conflict, requestUri, subStatusCode)
        {
            SetDescription();
        }

#if !NETSTANDARD16
        private ConflictException(SerializationInfo info, StreamingContext context)
            : base(info, context, HttpStatusCode.Conflict)
        {
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.Conflict;
        }
    }
}

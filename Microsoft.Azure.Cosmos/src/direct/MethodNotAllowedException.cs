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
    internal sealed class MethodNotAllowedException : DocumentClientException
    {
        public MethodNotAllowedException()
            : this(RMResources.MethodNotAllowed)
        {

        }

        public MethodNotAllowedException(string message)
            : this(message, (Exception)null, null)
        {

        }

        public MethodNotAllowedException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {

        }

        public MethodNotAllowedException(Exception innerException)
            : this(RMResources.MethodNotAllowed, innerException)
        {

        }

        public MethodNotAllowedException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.MethodNotAllowed, requestUri)
        {
            SetDescription();
        }

        public MethodNotAllowedException(string message, Exception innerException)
            : base(message, innerException, HttpStatusCode.MethodNotAllowed)
        {
            SetDescription();
        }

        public MethodNotAllowedException(string message, Exception innerException, HttpResponseHeaders headers, Uri requestUri = null)
            : base(message, innerException, headers, HttpStatusCode.MethodNotAllowed, requestUri)
        {
            SetDescription();
        }

#if !NETSTANDARD16
        private MethodNotAllowedException(SerializationInfo info, StreamingContext context) :
            base(info, context, HttpStatusCode.MethodNotAllowed)
        {
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.MethodNotAllowed;
        }
    }
}

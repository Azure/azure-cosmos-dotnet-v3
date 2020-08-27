//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Specialized;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents.Collections;

    [Serializable]
    internal sealed class LockedException : DocumentClientException
    {
        public LockedException()
            : this(RMResources.Locked)
        {

        }

        public LockedException(string message, SubStatusCodes subStatusCode)
            : base(message, (HttpStatusCode)StatusCodes.Locked, subStatusCode)
        {

        }

        public LockedException(string message, Uri requestUri = null)
            : this(message, null, null, requestUri)
        {

        }

        public LockedException(string message, Exception innerException, Uri requestUri = null)
            : this(message, innerException, null, requestUri)
        {

        }

        public LockedException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {

        }

        public LockedException(Exception innerException)
            : this(RMResources.Locked, innerException, null)
        {

        }

        public LockedException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, (HttpStatusCode)StatusCodes.Locked, requestUri)
        {
            SetDescription();
        }

        public LockedException(string message,
            Exception innerException,
            HttpResponseHeaders headers,
            Uri requestUri = null)
            : base(message, innerException, headers, (HttpStatusCode)StatusCodes.Locked, requestUri)
        {
            SetDescription();
        }

#if !NETSTANDARD16
        private LockedException(SerializationInfo info, StreamingContext context)
            : base(info, context, (HttpStatusCode)StatusCodes.Locked)
        {
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.Locked;
        }
    }
}

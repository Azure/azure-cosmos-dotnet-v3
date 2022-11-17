﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents.Collections;

    [Serializable]
    internal sealed class RequestTimeoutException : DocumentClientException
    {
        public RequestTimeoutException()
            : this(RMResources.RequestTimeout)
        {

        }

        public RequestTimeoutException(string message, Uri requestUri = null)
            : this(message, null, null, requestUri)
        {

        }

        public RequestTimeoutException(string message, Exception innerException, Uri requestUri = null)
            : this(message, innerException, null, requestUri)
        {

        }

        public RequestTimeoutException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {

        }

        public RequestTimeoutException(Exception innerException, Uri requestUri = null)
            : this(RMResources.RequestTimeout, innerException, requestUri)
        {

        }

        public RequestTimeoutException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.RequestTimeout, requestUri)
        {
            SetDescription();
        }

        public RequestTimeoutException(string message, Exception innerException, Uri requestUri = null, string localIpAddress = null)
            : this(message, innerException, (HttpResponseHeaders)null, requestUri)
        {
            this.LocalIp = localIpAddress;
        }


        public RequestTimeoutException(string message,
            Exception innerException,
            HttpResponseHeaders headers,
            Uri requestUri = null)
            : base(message, innerException, headers, HttpStatusCode.RequestTimeout, requestUri)
        {
            SetDescription();
        }

        /// <summary>
        ///
        /// Summary:
        ///     Gets a message that describes the current exception.
        ///
        /// </summary>
        public override string Message
        {
            get
            {
                if (!string.IsNullOrEmpty(this.LocalIp))
                {
                    return string.Format(CultureInfo.CurrentUICulture,
                        RMResources.ExceptionMessageAddIpAddress,
                        base.Message,
                        this.LocalIp);
                }
                return base.Message;
            }
        }

        internal string LocalIp { get; set; }

#if !NETSTANDARD16
        private RequestTimeoutException(SerializationInfo info, StreamingContext context)
            : base(info, context, HttpStatusCode.RequestTimeout)
        {
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.RequestTimeout;
        }
    }
}

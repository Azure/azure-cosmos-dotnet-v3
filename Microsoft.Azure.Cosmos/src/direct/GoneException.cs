//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Documents.Collections;
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;

    [Serializable]
    internal sealed class GoneException : DocumentClientException
    {
        public GoneException()
            : this(RMResources.Gone)
        {

        }

        public GoneException(string message, Uri requestUri = null)
            : this(message, (Exception)null, (HttpResponseHeaders)null, requestUri)
        {
        }

        public GoneException(string message,
            HttpResponseHeaders headers,
            Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {
        }

        public GoneException(string message,
            Exception innerException,
            Uri requestUri = null,
            string localIpAddress = null)
            : this(message, innerException, (HttpResponseHeaders)null, requestUri)
        {
            this.LocalIp = localIpAddress;
        }

        public GoneException(Exception innerException)
            : this(RMResources.Gone, innerException, (HttpResponseHeaders)null)
        {
        }

        public GoneException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.Gone, requestUri)
        {
            SetDescription();
        }

        public GoneException(string message,
            Exception innerException,
            HttpResponseHeaders headers,
            Uri requestUri = null)
            : base(message, innerException, headers, HttpStatusCode.Gone, requestUri)
        {
            SetDescription();
        }

        internal string LocalIp { get; set; }

#if !NETSTANDARD16
        private GoneException(SerializationInfo info, StreamingContext context) 
            : base(info, context, HttpStatusCode.Gone)
        {
        }
#endif

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

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.Gone;
        }
    }
}

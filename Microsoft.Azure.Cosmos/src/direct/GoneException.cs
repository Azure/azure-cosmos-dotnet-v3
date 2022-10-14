//------------------------------------------------------------
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
    internal sealed class GoneException : DocumentClientException
    {
        public GoneException()
            : this(RMResources.Gone, SubStatusCodes.Unknown)
        {

        }

        public GoneException(string message)
            : this(message, (Exception)null, (HttpResponseHeaders)null, SubStatusCodes.Unknown)
        {
        }

        public GoneException(string message, SubStatusCodes subStatusCode, Uri requestUri = null)
            : this(message, (Exception)null, (HttpResponseHeaders)null, subStatusCode, requestUri)
        {
        }

        public GoneException(string message,
            HttpResponseHeaders headers,
            SubStatusCodes? subStatusCode,
            Uri requestUri = null)
            : this(message, null, headers, subStatusCode, requestUri)
        {
        }

        public GoneException(string message,
            Exception innerException,
            SubStatusCodes subStatusCode,
            Uri requestUri = null,
            string localIpAddress = null)
            : this(message, innerException, (HttpResponseHeaders)null, subStatusCode, requestUri)
        {
            this.LocalIp = localIpAddress;
        }

        public GoneException(Exception innerException, SubStatusCodes subStatusCode)
            : this(RMResources.Gone, innerException, (HttpResponseHeaders)null, subStatusCode)
        {
        }

        public GoneException(string message, INameValueCollection headers, SubStatusCodes? substatusCode, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.Gone, substatusCode, requestUri)
        {
            SetDescription();
        }

        public GoneException(string message,
            Exception innerException,
            HttpResponseHeaders headers,
            SubStatusCodes? subStatusCode,
            Uri requestUri = null)
            : base(message, innerException, headers, HttpStatusCode.Gone, requestUri, subStatusCode)
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

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using Collections;

    /// <summary>
    /// This exception is thrown when DocumentServiceRequest receives a gone exception
    /// with substatus code 1022, lease not found.
    /// 
    /// Gateway/SDK can transparently refresh routing map and does a cross regioregional retry immidiately.
    /// </summary>
    [Serializable]
    internal sealed class LeaseNotFoundException : DocumentClientException
    {
        public LeaseNotFoundException()
            : this(RMResources.Gone)
        {

        }

        public LeaseNotFoundException(string message)
            : this(message, (Exception)null, null)
        {
        }

        public LeaseNotFoundException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {
        }

        public LeaseNotFoundException(string message, Exception innerException)
            : this(message, innerException, null)
        {
        }

        public LeaseNotFoundException(Exception innerException)
            : this(RMResources.Gone, innerException, null)
        {
        }

        public LeaseNotFoundException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, (HttpStatusCode)StatusCodes.Gone, requestUri)
        {
            SetSubstatus();
            SetDescription();
        }

        public LeaseNotFoundException(string message, Exception innerException, HttpResponseHeaders headers, Uri requestUri = null)
            : base(message, innerException, headers, (HttpStatusCode)StatusCodes.Gone, requestUri)
        {
            SetSubstatus();
            SetDescription();
        }

#if !NETSTANDARD16
        private LeaseNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context, (HttpStatusCode)StatusCodes.Gone)
        {
            SetSubstatus();
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.LeaseNotFound;
        }

        private void SetSubstatus()
        {
            this.Headers[WFConstants.BackendHeaders.SubStatus] =
                    ((uint)SubStatusCodes.LeaseNotFound).ToString(CultureInfo.InvariantCulture);
        }
    }
}

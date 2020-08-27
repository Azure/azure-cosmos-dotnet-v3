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
    internal sealed class InvalidPartitionException : DocumentClientException
    {
        public InvalidPartitionException()
            : this(RMResources.Gone)
        {

        }

        public InvalidPartitionException(string message)
            : this(message, (Exception)null, null)
        {
        }

        public InvalidPartitionException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {
        }

        public InvalidPartitionException(string message, Exception innerException)
            : this(message, innerException, null)
        {
        }

        public InvalidPartitionException(Exception innerException)
            : this(RMResources.Gone, innerException, null)
        {
        }

        public InvalidPartitionException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, (HttpStatusCode)StatusCodes.Gone, requestUri)
        {
            SetDescription();
            SetSubStatus();
        }

        public InvalidPartitionException(string message, Exception innerException, HttpResponseHeaders headers, Uri requestUri = null)
            : base(message, innerException, headers, (HttpStatusCode)StatusCodes.Gone, requestUri)
        {
            SetDescription();
            SetSubStatus();
        }

#if !NETSTANDARD16
        private InvalidPartitionException(SerializationInfo info, StreamingContext context)
            : base(info, context, (HttpStatusCode)StatusCodes.Gone)
        {
            SetDescription();
            SetSubStatus();
        }
#endif

        private void SetSubStatus()
        {
            this.Headers[WFConstants.BackendHeaders.SubStatus] =
                    ((uint)SubStatusCodes.NameCacheIsStale).ToString(CultureInfo.InvariantCulture);
        }

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.InvalidPartition;
        }
    }
}

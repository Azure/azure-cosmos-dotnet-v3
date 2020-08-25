//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// This exception is thrown when DocumentServiceRequest reaches partition which is being split
    /// and was made unavailable for reads/writes.
    /// 
    /// Gateway/SDK can transparently refresh routing map and retry after some delay.
    /// </summary>
    [Serializable]
    internal sealed class PartitionKeyRangeIsSplittingException : DocumentClientException
    {
        public PartitionKeyRangeIsSplittingException()
            : this(RMResources.Gone)
        {

        }

        public PartitionKeyRangeIsSplittingException(string message)
            : this(message, (Exception)null, null)
        {
        }

        public PartitionKeyRangeIsSplittingException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {
        }

        public PartitionKeyRangeIsSplittingException(string message, Exception innerException)
            : this(message, innerException, null)
        {
        }

        public PartitionKeyRangeIsSplittingException(Exception innerException)
            : this(RMResources.Gone, innerException, null)
        {
        }

        public PartitionKeyRangeIsSplittingException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, (HttpStatusCode)StatusCodes.Gone, requestUri)
        {
            SetSubstatus();
            SetDescription();
        }

        public PartitionKeyRangeIsSplittingException(string message, Exception innerException, HttpResponseHeaders headers, Uri requestUri = null)
            : base(message, innerException, headers, (HttpStatusCode)StatusCodes.Gone, requestUri)
        {
            SetSubstatus();
            SetDescription();
        }

#if !NETSTANDARD16
        private PartitionKeyRangeIsSplittingException(SerializationInfo info, StreamingContext context)
            : base(info, context, (HttpStatusCode)StatusCodes.Gone)
        {
            SetSubstatus();
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.InvalidPartition;
        }

        private void SetSubstatus()
        {
            this.Headers[WFConstants.BackendHeaders.SubStatus] =
                    ((uint)SubStatusCodes.CompletingSplit).ToString(CultureInfo.InvariantCulture);
        }
    }
}

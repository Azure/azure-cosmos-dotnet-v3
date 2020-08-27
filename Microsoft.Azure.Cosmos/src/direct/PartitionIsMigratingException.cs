//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using Collections;
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;

    /// <summary>
    /// This exception is thrown when DocumentServiceRequest reaches partition which is being migrated
    /// and was made unavailable for reads/writes.
    /// 
    /// Gateway/SDK can transparently refresh routing map and retry after some delay.
    /// </summary>
    [Serializable]
    internal sealed class PartitionIsMigratingException : DocumentClientException
    {
        public PartitionIsMigratingException()
            : this(RMResources.Gone)
        {

        }

        public PartitionIsMigratingException(string message)
            : this(message, (Exception)null, null)
        {
        }

        public PartitionIsMigratingException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {
        }

        public PartitionIsMigratingException(string message, Exception innerException)
            : this(message, innerException, null)
        {
        }

        public PartitionIsMigratingException(Exception innerException)
            : this(RMResources.Gone, innerException, null)
        {
        }

        public PartitionIsMigratingException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, (HttpStatusCode)StatusCodes.Gone, requestUri)
        {
            SetSubstatus();
            SetDescription();
        }

        public PartitionIsMigratingException(string message, Exception innerException, HttpResponseHeaders headers, Uri requestUri = null)
            : base(message, innerException, headers, (HttpStatusCode)StatusCodes.Gone, requestUri)
        {
            SetSubstatus();
            SetDescription();
        }

#if !NETSTANDARD16
        private PartitionIsMigratingException(SerializationInfo info, StreamingContext context)
            : base(info, context, (HttpStatusCode)StatusCodes.Gone)
        {
            SetSubstatus();
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.PartitionMigrating;
        }

        private void SetSubstatus()
        {
            this.Headers[WFConstants.BackendHeaders.SubStatus] =
                    ((uint)SubStatusCodes.CompletingPartitionMigration).ToString(CultureInfo.InvariantCulture);
        }
    }
}

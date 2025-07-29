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
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// This exception is thrown when request receives a gone exception
    /// with substatus code 1024, archival partition not present.
    /// </summary>
    [Serializable]
    internal sealed class ArchivalPartitionNotPresentException : GoneException
    {
        public ArchivalPartitionNotPresentException()
            : this(RMResources.Gone)
        {
        }

        public ArchivalPartitionNotPresentException(string message)
            : this(message, (Exception)null)
        {
        }

        public ArchivalPartitionNotPresentException(string message, Exception innerException)
            : this(message, innerException, (HttpResponseHeaders)null, null)
        {
        }

        public ArchivalPartitionNotPresentException(
            string message,
            INameValueCollection headers,
            Uri requestUri = null)
            : base(
                message,
                headers,
                SubStatusCodes.ArchivalPartitionNotPresent,
                requestUri)
        {
            this.SetSubstatus();
            this.SetDescription();
        }

        public ArchivalPartitionNotPresentException(
            string message,
            HttpResponseHeaders headers,
            Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {
        }

        public ArchivalPartitionNotPresentException(
            string message,
            Exception innerException,
            HttpResponseHeaders headers,
            Uri requestUri = null)
            : base(
                message,
                innerException,
                headers,
                SubStatusCodes.ArchivalPartitionNotPresent,
                requestUri)
        {
            this.SetSubstatus();
            this.SetDescription();
        }

        private ArchivalPartitionNotPresentException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.SetSubstatus();
            this.SetDescription();
        }

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.Gone;
        }

        private void SetSubstatus()
        {
            this.Headers[WFConstants.BackendHeaders.SubStatus] =
                ((uint)SubStatusCodes.ArchivalPartitionNotPresent).ToString(CultureInfo.InvariantCulture);
        }
    }
}

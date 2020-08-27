//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Documents.Collections;

    internal sealed class StoreResponse : IRetriableResponse
    {
        public int Status { get; set; }

        public INameValueCollection Headers { get; set; }

        public Stream ResponseBody { get; set; }

        public long LSN
        {
            get
            {
                string value;
                long result = -1;
                if (this.TryGetHeaderValue(WFConstants.BackendHeaders.LSN, out value))
                {
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                    {
                        return result;
                    }
                }

                return -1;
            }
        }

        public string PartitionKeyRangeId
        {
            get
            {
                string value;
                if (this.TryGetHeaderValue(WFConstants.BackendHeaders.PartitionKeyRangeId, out value))
                {
                    return value;
                }

                return null;
            }
        }

        public long CollectionPartitionIndex
        {
            get
            {
                string value;
                long result = -1;
                if (this.TryGetHeaderValue(WFConstants.BackendHeaders.CollectionPartitionIndex, out value))
                {
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                    {
                        return result;
                    }
                }

                return -1;
            }
        }

        public long CollectionServiceIndex
        {
            get
            {
                string value;
                long result = -1;
                if (this.TryGetHeaderValue(WFConstants.BackendHeaders.CollectionServiceIndex, out value))
                {
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                    {
                        return result;
                    }
                }

                return -1;
            }
        }

        public string Continuation
        {
            get
            {
                string value;
                if (this.TryGetHeaderValue(HttpConstants.HttpHeaders.Continuation, out value))
                {
                    return value;
                }

                return null;
            }
        }

        public SubStatusCodes SubStatusCode => subStatusCode.Value;

        public HttpStatusCode StatusCode => (HttpStatusCode)this.Status;

        private Lazy<SubStatusCodes> subStatusCode;

        public StoreResponse()
        {
            this.subStatusCode = new Lazy<SubStatusCodes>(this.GetSubStatusCode);
        }

        public bool TryGetHeaderValue(
            string attribute,
            out string value)
        {
            value = null;
            if (this.Headers == null)
            {
                return false;
            }

            value = this.Headers.Get(attribute);
            return value != null;
        }

        public void UpsertHeaderValue(
            string headerName,
            string headerValue)
        {
            this.Headers[headerName] = headerValue;
        }

        private SubStatusCodes GetSubStatusCode()
        {
            SubStatusCodes value = SubStatusCodes.Unknown;
            string valueSubStatus;
            if (this.TryGetHeaderValue(WFConstants.BackendHeaders.SubStatus, out valueSubStatus))
            {
                uint subStatus = 0;
                if (uint.TryParse(valueSubStatus, NumberStyles.Integer, CultureInfo.InvariantCulture, out subStatus))
                {
                    value = (SubStatusCodes)subStatus;
                }
            }

            return value;
        }
    }
}

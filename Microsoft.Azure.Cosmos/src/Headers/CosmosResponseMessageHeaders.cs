//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// HTTP headers in a <see cref="CosmosResponseMessage"/>.
    /// </summary>
    public class CosmosResponseMessageHeaders : CosmosMessageHeadersBase, IEnumerable
    {
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.RequestCharge)]
        private string _requestCharge
        {
            get
            {
                return this.RequestCharge == 0 ? null : this.RequestCharge.ToString();
            }
            set
            {
                this.RequestCharge = string.IsNullOrEmpty(value) ? 0 : double.Parse(value, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        
        public virtual double RequestCharge { get; internal set; }

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.ActivityId)]
        public virtual string ActivityId { get; internal set; }

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.ETag)]
        public virtual string ETag { get; internal set; }

        /// <summary>
        /// Gets the Continuation Token in the current <see cref="CosmosResponseMessage"/>.
        /// </summary>
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.Continuation)]
        public virtual string Continuation { get; internal set; }

        /// <summary>
        /// Gets the Content Type for the current content in the <see cref="CosmosResponseMessage"/>.
        /// </summary>
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.ContentType)]
        public virtual string ContentType { get; internal set; }

        /// <summary>
        /// Gets the Content Length for the current content in the <see cref="CosmosResponseMessage"/>.
        /// </summary>
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.ContentLength)]
        public virtual string ContentLength
        {
            get
            {
                return this._contentLength;
            }
            set
            {
                this.ContentLengthAsLong = value != null ? long.Parse(value, CultureInfo.InvariantCulture) : 0;
                this._contentLength = value;
            }
        }

        /// <summary>
        /// Gets the Location for the current content in the <see cref="CosmosResponseMessage"/>.
        /// </summary>
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.Location)]
        public virtual string Location { get; internal set; }

        internal TimeSpan? RetryAfter;

        internal SubStatusCodes SubStatusCode = SubStatusCodes.Unknown;

        internal long ContentLengthAsLong;

        [CosmosKnownHeaderAttribute(HeaderName = WFConstants.BackendHeaders.SubStatus)]
        internal string SubStatusCodeLiteral
        {
            get
            {
                return this._subStatusCodeLiteral;
            }
            set
            {
                this.SubStatusCode = CosmosResponseMessageHeaders.GetSubStatusCodes(value);
                this._subStatusCodeLiteral = value;
            }
        }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.RetryAfterInMilliseconds)]
        internal string RetryAfterLiteral
        {
            get
            {
                return this._retryAfterInternal;
            }
            set
            {
                this.RetryAfter = CosmosResponseMessageHeaders.GetRetryAfter(value);
                this._retryAfterInternal = value;
            }
        }

        private string _contentLength;

        private string _subStatusCodeLiteral;

        private string _retryAfterInternal;

        private static KeyValuePair<string, PropertyInfo>[] knownHeaderProperties = CosmosMessageHeadersInternal.GetHeaderAttributes<CosmosResponseMessageHeaders>();

        internal override Dictionary<string, CosmosCustomHeader> CreateKnownDictionary()
        {
            return CosmosResponseMessageHeaders.knownHeaderProperties.ToDictionary(
                    knownProperty => knownProperty.Key,
                    knownProperty => new CosmosCustomHeader(
                            () => (string)knownProperty.Value.GetValue(this),
                            (string value) => { knownProperty.Value.SetValue(this, value); }
                        )
                , StringComparer.OrdinalIgnoreCase);
        }

        internal static SubStatusCodes GetSubStatusCodes(string value)
        {
            uint nSubStatus = 0;
            if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out nSubStatus))
            {
                return (SubStatusCodes)nSubStatus;
            }

            return SubStatusCodes.Unknown;
        }

        internal static TimeSpan? GetRetryAfter(string value)
        {
            long retryIntervalInMilliseconds = 0;
            if (long.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out retryIntervalInMilliseconds))
            {
                return TimeSpan.FromMilliseconds(retryIntervalInMilliseconds);
            }

            return null;
        }
    }
}
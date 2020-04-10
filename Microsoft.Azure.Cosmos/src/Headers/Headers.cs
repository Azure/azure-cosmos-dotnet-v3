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
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Header implementation used for Request and Responses
    /// </summary>
    /// <seealso cref="ResponseMessage"/>
    /// <seealso cref="RequestMessage"/>
    public class Headers : IEnumerable
    {
        private static KeyValuePair<string, PropertyInfo>[] knownHeaderProperties = CosmosMessageHeadersInternal.GetHeaderAttributes<Headers>();

        private readonly Lazy<CosmosMessageHeadersInternal> messageHeaders;

        private string contentLength;

        private SubStatusCodes? subStatusCode;

        private string retryAfterInternal;

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.RequestCharge)]
        private string requestCharge
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

        internal TimeSpan? RetryAfter;

        internal SubStatusCodes SubStatusCode
        {
            get
            {
                return this.subStatusCode.GetValueOrDefault(SubStatusCodes.Unknown);
            }
            set
            {
                this.subStatusCode = value;
            }
        }

        internal long ContentLengthAsLong;

        /// <summary>
        /// Gets the Continuation Token in the current <see cref="ResponseMessage"/>.
        /// </summary>
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.Continuation)]
        public virtual string ContinuationToken { get; internal set; }

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
        /// Gets the Content Type for the current content in the <see cref="ResponseMessage"/>.
        /// </summary>
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.ContentType)]
        public virtual string ContentType { get; internal set; }

        /// <summary>
        /// Gets the Session Token for the current <see cref="ResponseMessage"/>.
        /// </summary>
        /// <remarks>
        /// Session Token is used along with Session Consistency.
        /// </remarks>
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.SessionToken)]
        public virtual string Session { get; internal set; }

        /// <summary>
        /// Gets the Content Length for the current content in the <see cref="ResponseMessage"/>.
        /// </summary>
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.ContentLength)]
        public virtual string ContentLength
        {
            get
            {
                return this.contentLength;
            }
            set
            {
                this.ContentLengthAsLong = value != null ? long.Parse(value, CultureInfo.InvariantCulture) : 0;
                this.contentLength = value;
            }
        }

        /// <summary>
        /// Gets the Location for the current content in the <see cref="ResponseMessage"/>.
        /// </summary>
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.Location)]
        public virtual string Location { get; internal set; }

        [CosmosKnownHeaderAttribute(HeaderName = WFConstants.BackendHeaders.SubStatus)]
        internal string SubStatusCodeLiteral
        {
            get
            {
                return this.subStatusCode.HasValue ? ((int)this.SubStatusCode).ToString(CultureInfo.InvariantCulture) : null;
            }
            set
            {
                this.SubStatusCode = Headers.GetSubStatusCodes(value);
            }
        }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.RetryAfterInMilliseconds)]
        internal string RetryAfterLiteral
        {
            get
            {
                return this.retryAfterInternal;
            }
            set
            {
                this.RetryAfter = Headers.GetRetryAfter(value);
                this.retryAfterInternal = value;
            }
        }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.PartitionKey)]
        internal string PartitionKey { get; set; }

        [CosmosKnownHeaderAttribute(HeaderName = WFConstants.BackendHeaders.PartitionKeyRangeId)]
        internal string PartitionKeyRangeId { get; set; }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.IsUpsert)]
        internal string IsUpsert { get; set; }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.OfferThroughput)]
        internal string OfferThroughput { get; set; }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.OfferAutopilotSettings)]
        internal string OfferAutoscaleThroughput { get; set; }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.IfNoneMatch)]
        internal string IfNoneMatch { get; set; }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.PageSize)]
        internal string PageSize { get; set; }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.QueryMetrics)]
        internal string QueryMetricsText { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="Headers"/>.
        /// </summary>
        public Headers()
        {
            this.messageHeaders = new Lazy<CosmosMessageHeadersInternal>(this.CreateCosmosMessageHeaders);
        }

        /// <summary>
        /// Gets the value of a particular header.
        /// </summary>
        /// <param name="headerName">Header name to look for.</param>
        /// <returns>The header value.</returns>
        public virtual string this[string headerName]
        {
            get => this.messageHeaders.Value[headerName];
            set => this.messageHeaders.Value[headerName] = value;
        }

        /// <summary>
        /// Enumerates all the HTTP headers names in the <see cref="Headers"/>.
        /// </summary>
        /// <returns>An enumator for all headers.</returns>
        public virtual IEnumerator<string> GetEnumerator()
        {
            return this.messageHeaders.Value.GetEnumerator();
        }

        /// <summary>
        /// Adds a header to the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        public virtual void Add(string headerName, string value)
        {
            this.messageHeaders.Value.Add(headerName, value);
        }

        /// <summary>
        /// Adds a header to the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="values">List of values to be added as a comma-separated list.</param>
        public virtual void Add(string headerName, IEnumerable<string> values)
        {
            this.messageHeaders.Value.Add(headerName, values);
        }

        /// <summary>
        /// Adds or updates a header in the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        public virtual void Set(string headerName, string value)
        {
            this.messageHeaders.Value.Set(headerName, value);
        }

        /// <summary>
        /// Gets the value of a particular header.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <returns>The header value.</returns>
        public virtual string Get(string headerName)
        {
            return this.messageHeaders.Value.Get(headerName);
        }

        /// <summary>
        /// Tries to get the value for a particular header.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        /// <returns>True or false if the header name existed in the header collection.</returns>
        public virtual bool TryGetValue(string headerName, out string value)
        {
            return this.messageHeaders.Value.TryGetValue(headerName, out value);
        }

        /// <summary>
        /// Returns the header value or the default(string)
        /// </summary>
        /// <param name="headerName">Header Name</param>
        /// <returns>Returns the header value or the default(string).</returns>
        public virtual string GetValueOrDefault(string headerName)
        {
            if (this.TryGetValue(headerName, out string value))
            {
                return value;
            }

            return default(string);
        }

        /// <summary>
        /// Removes a header from the header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        public virtual void Remove(string headerName)
        {
            this.messageHeaders.Value.Remove(headerName);
        }

        /// <summary>
        /// Obtains a list of all header names.
        /// </summary>
        /// <returns>An array with all the header names.</returns>
        public virtual string[] AllKeys()
        {
            return this.messageHeaders.Value.AllKeys();
        }

        /// <summary>
        /// Gets a header value with a particular type.
        /// </summary>
        /// <typeparam name="T">Type of the header value.</typeparam>
        /// <param name="headerName">Header name.</param>
        /// <returns>The header value parsed for a particular type.</returns>
        public virtual T GetHeaderValue<T>(string headerName)
        {
            return this.messageHeaders.Value.GetHeaderValue<T>(headerName);
        }

        /// <summary>
        /// Enumerates all the HTTP headers names in the <see cref="Headers"/>.
        /// </summary>
        /// <returns>An enumator for all headers.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal string[] GetValues(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            string value = this[key];
            if (value == null)
            {
                return null;
            }

            return new string[1] { this[key] };
        }

        internal CosmosMessageHeadersInternal CosmosMessageHeaders => this.messageHeaders.Value;

        private CosmosMessageHeadersInternal CreateCosmosMessageHeaders()
        {
            return new CosmosMessageHeadersInternal(this.CreateKnownDictionary());
        }

        internal Dictionary<string, CosmosCustomHeader> CreateKnownDictionary()
        {
            return Headers.knownHeaderProperties.ToDictionary(
                    knownProperty => knownProperty.Key,
                    knownProperty => new CosmosCustomHeader(
                            () => (string)knownProperty.Value.GetValue(this),
                            (string value) => { knownProperty.Value.SetValue(this, value); }),
                    StringComparer.OrdinalIgnoreCase);
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
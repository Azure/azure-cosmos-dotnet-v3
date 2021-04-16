//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Header implementation used for Request and Responses
    /// </summary>
    /// <seealso cref="ResponseMessage"/>
    /// <seealso cref="RequestMessage"/>
    public class Headers : IEnumerable
    {
        internal virtual SubStatusCodes SubStatusCode
        {
            get => Headers.GetSubStatusCodes(this.SubStatusCodeLiteral);
            set => this.SubStatusCodeLiteral = ((uint)value).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the Continuation Token in the current <see cref="ResponseMessage"/>.
        /// </summary>
        public virtual string ContinuationToken
        {
            get => this.CosmosMessageHeaders.Continuation;
            internal set => this.CosmosMessageHeaders.Continuation = value;
        }

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public virtual double RequestCharge
        {
            get
            {
                string value = this.CosmosMessageHeaders.RequestCharge;
                if (value == null)
                {
                    return 0;
                }

                return double.Parse(value, CultureInfo.InvariantCulture);
            }
            internal set => this.CosmosMessageHeaders.RequestCharge = value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public virtual string ActivityId
        {
            get => this.CosmosMessageHeaders.ActivityId;
            internal set => this.CosmosMessageHeaders.ActivityId = value;
        }

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        public virtual string ETag
        {
            get => this.CosmosMessageHeaders.ETag;
            internal set => this.CosmosMessageHeaders.ETag = value;
        }

        /// <summary>
        /// Gets the Content Type for the current content in the <see cref="ResponseMessage"/>.
        /// </summary>
        public virtual string ContentType
        {
            get => this.CosmosMessageHeaders.ContentType;
            internal set => this.CosmosMessageHeaders.ContentType = value;
        }

        /// <summary>
        /// Gets the Session Token for the current <see cref="ResponseMessage"/>.
        /// </summary>
        /// <remarks>
        /// Session Token is used along with Session Consistency.
        /// </remarks>
        public virtual string Session
        {
            get => this.CosmosMessageHeaders.SessionToken;
            internal set => this.CosmosMessageHeaders.SessionToken = value;
        }

        /// <summary>
        /// Gets the Content Length for the current content in the <see cref="ResponseMessage"/>.
        /// </summary>
        public virtual string ContentLength
        {
            get => this.CosmosMessageHeaders.ContentLength;
            set => this.CosmosMessageHeaders.ContentLength = value;
        }

        /// <summary>
        /// Gets the Location for the current content in the <see cref="ResponseMessage"/>.
        /// </summary>
        public virtual string Location
        {
            get => this.CosmosMessageHeaders.Location;
            internal set => this.CosmosMessageHeaders.Location = value;
        }

        internal virtual string SubStatusCodeLiteral
        {
            get => this.CosmosMessageHeaders.SubStatus;
            set => this.CosmosMessageHeaders.SubStatus = value;
        }

        internal TimeSpan? RetryAfter
        {
            get => Headers.GetRetryAfter(this.RetryAfterLiteral);
            set
            {
                if (value.HasValue)
                {
                    this.RetryAfterLiteral = value.Value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
                    return;
                }

                this.RetryAfterLiteral = null;
            }
        }

        internal virtual string Authorization
        {
            get => this.CosmosMessageHeaders.Authorization;
            set => this.CosmosMessageHeaders.Authorization = value;
        }

        internal virtual string RetryAfterLiteral
        {
            get => this.CosmosMessageHeaders.RetryAfterInMilliseconds;
            set => this.CosmosMessageHeaders.RetryAfterInMilliseconds = value;
        }

        internal virtual string PartitionKey
        {
            get => this.CosmosMessageHeaders.PartitionKey;
            set => this.CosmosMessageHeaders.PartitionKey = value;
        }

        internal virtual string PartitionKeyRangeId
        {
            get => this.CosmosMessageHeaders.PartitionKeyRangeId;
            set => this.CosmosMessageHeaders.PartitionKeyRangeId = value;
        }

        internal virtual string IsUpsert
        {
            get => this.CosmosMessageHeaders.IsUpsert;
            set => this.CosmosMessageHeaders.IsUpsert = value;
        }

        internal virtual string OfferThroughput
        {
            get => this.CosmosMessageHeaders.OfferThroughput;
            set => this.CosmosMessageHeaders.OfferThroughput = value;
        }

        internal virtual string IfNoneMatch
        {
            get => this.CosmosMessageHeaders.IfNoneMatch;
            set => this.CosmosMessageHeaders.IfNoneMatch = value;
        }

        internal virtual string PageSize
        {
            get => this.CosmosMessageHeaders.PageSize;
            set => this.CosmosMessageHeaders.PageSize = value;
        }

        internal virtual string QueryMetricsText
        {
            get => this.CosmosMessageHeaders.QueryMetrics;
            set => this.CosmosMessageHeaders.QueryMetrics = value;
        }

        internal virtual string BackendRequestDurationMilliseconds
        {
            get => this.CosmosMessageHeaders.BackendRequestDurationMilliseconds;
            set => this.CosmosMessageHeaders.BackendRequestDurationMilliseconds = value;
        }

        /// <summary>
        /// Creates a new instance of <see cref="Headers"/>.
        /// </summary>
        public Headers()
        {
            this.CosmosMessageHeaders = new StoreRequestNameValueCollection();
        }

        internal Headers(INameValueCollection nameValueCollection)
        {
            this.CosmosMessageHeaders = nameValueCollection switch
            {
                StoreResponseNameValueCollection storeResponseNameValueCollection => new StoreResponseHeaders(storeResponseNameValueCollection),
                HttpResponseHeadersWrapper httpResponseHeaders => httpResponseHeaders,
                _ => new NameValueResponseHeaders(nameValueCollection),
            };
        }

        /// <summary>
        /// Gets the value of a particular header.
        /// </summary>
        /// <param name="headerName">Header name to look for.</param>
        /// <returns>The header value.</returns>
        public virtual string this[string headerName]
        {
            get => this.CosmosMessageHeaders[headerName];
            set => this.CosmosMessageHeaders[headerName] = value;
        }

        /// <summary>
        /// Enumerates all the HTTP headers names in the <see cref="Headers"/>.
        /// </summary>
        /// <returns>An enumerator for all headers.</returns>
        public virtual IEnumerator<string> GetEnumerator()
        {
            foreach (string key in this.CosmosMessageHeaders.AllKeys())
            {
                yield return key;
            }
        }

        /// <summary>
        /// Adds a header to the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        public virtual void Add(string headerName, string value)
        {
            this.CosmosMessageHeaders.Add(headerName, value);
        }

        /// <summary>
        /// Adds a header to the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="values">List of values to be added as a comma-separated list.</param>
        public virtual void Add(string headerName, IEnumerable<string> values)
        {
            this.CosmosMessageHeaders.Add(headerName, values);
        }

        /// <summary>
        /// Adds or updates a header in the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        public virtual void Set(string headerName, string value)
        {
            this.CosmosMessageHeaders.Set(headerName, value);
        }

        /// <summary>
        /// Gets the value of a particular header.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <returns>The header value.</returns>
        public virtual string Get(string headerName)
        {
            return this.CosmosMessageHeaders.Get(headerName);
        }

        /// <summary>
        /// Tries to get the value for a particular header.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        /// <returns>True or false if the header name existed in the header collection.</returns>
        public virtual bool TryGetValue(string headerName, out string value)
        {
            return this.CosmosMessageHeaders.TryGetValue(headerName, out value);
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

            return default;
        }

        /// <summary>
        /// Removes a header from the header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        public virtual void Remove(string headerName)
        {
            this.CosmosMessageHeaders.Remove(headerName);
        }

        /// <summary>
        /// Obtains a list of all header names.
        /// </summary>
        /// <returns>An array with all the header names.</returns>
        public virtual string[] AllKeys()
        {
            return this.CosmosMessageHeaders.AllKeys();
        }

        /// <summary>
        /// Gets a header value with a particular type.
        /// </summary>
        /// <typeparam name="T">Type of the header value.</typeparam>
        /// <param name="headerName">Header name.</param>
        /// <returns>The header value parsed for a particular type.</returns>
        public virtual T GetHeaderValue<T>(string headerName)
        {
            return this.CosmosMessageHeaders.GetHeaderValue<T>(headerName);
        }

        /// <summary>
        /// Enumerates all the HTTP headers names in the <see cref="Headers"/>.
        /// </summary>
        /// <returns>An enumerator for all headers.</returns>
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

        internal CosmosMessageHeadersInternal CosmosMessageHeaders { get; }

        internal static int GetIntValueOrDefault(string value)
        {
            int.TryParse(value, out int number);
            return number;
        }

        internal static SubStatusCodes GetSubStatusCodes(string value)
        {
            if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint nSubStatus))
            {
                return (SubStatusCodes)nSubStatus;
            }

            return SubStatusCodes.Unknown;
        }

        internal static TimeSpan? GetRetryAfter(string value)
        {
            if (long.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out long retryIntervalInMilliseconds))
            {
                return TimeSpan.FromMilliseconds(retryIntervalInMilliseconds);
            }

            return null;
        }
    }
}
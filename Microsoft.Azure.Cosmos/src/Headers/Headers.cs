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
        private readonly CosmosMessageHeadersInternal messageHeaders;

        private string GetString(string keyName)
        {
            this.TryGetValue(keyName, out string valueTuple);
            return valueTuple;
        }

        internal TimeSpan? RetryAfter;

        internal SubStatusCodes SubStatusCode
        {
            get
            {
                return Headers.GetSubStatusCodes(this.SubStatusCodeLiteral);
            }
            set
            {
                this.SubStatusCodeLiteral = value.ToString();
            }
        }

        /// <summary>
        /// Gets the Continuation Token in the current <see cref="ResponseMessage"/>.
        /// </summary>
        public virtual string ContinuationToken
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.Continuation);
            }

            internal set
            {
                this.Set(HttpConstants.HttpHeaders.Continuation, value);
            }
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
                string value = this.GetString(HttpConstants.HttpHeaders.RequestCharge);
                if (value == null)
                {
                    return 0;
                }

                return double.Parse(value);
            }
            internal set
            {
                this.Set(HttpConstants.HttpHeaders.RequestCharge, value.ToString());
            }
        }

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public virtual string ActivityId
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.ActivityId);
            }
            internal set
            {
                this.Set(HttpConstants.HttpHeaders.ActivityId, value.ToString());
            }
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
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.ETag);
            }
            internal set
            {
                this.Set(HttpConstants.HttpHeaders.ETag, value.ToString());
            }
        }

        /// <summary>
        /// Gets the Content Type for the current content in the <see cref="ResponseMessage"/>.
        /// </summary>
        public virtual string ContentType
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.ContentType);
            }
            internal set
            {
                this.Set(HttpConstants.HttpHeaders.ContentType, value.ToString());
            }
        }

        /// <summary>
        /// Gets the Session Token for the current <see cref="ResponseMessage"/>.
        /// </summary>
        /// <remarks>
        /// Session Token is used along with Session Consistency.
        /// </remarks>
        public virtual string Session
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.SessionToken);
            }
            internal set
            {
                this.Set(HttpConstants.HttpHeaders.SessionToken, value.ToString());
            }
        }

        /// <summary>
        /// Gets the Content Length for the current content in the <see cref="ResponseMessage"/>.
        /// </summary>
        public virtual string ContentLength
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.ContentLength);
            }
            internal set
            {
                this.Set(HttpConstants.HttpHeaders.ContentLength, value.ToString());
            }
        }

        /// <summary>
        /// Gets the Location for the current content in the <see cref="ResponseMessage"/>.
        /// </summary>
        public virtual string Location
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.Location);
            }
            internal set
            {
                this.Set(HttpConstants.HttpHeaders.Location, value.ToString());
            }
        }

        internal string SubStatusCodeLiteral
        {
            get
            {
                return this.GetString(WFConstants.BackendHeaders.SubStatus);
            }
            set
            {
                this.Set(WFConstants.BackendHeaders.SubStatus, value);
            }
        }

        internal string RetryAfterLiteral
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.RetryAfterInMilliseconds);
            }
            set
            {
                this.Set(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, value);
            }
        }

        internal string PartitionKey
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.PartitionKey);
            }
            set
            {
                this.Set(HttpConstants.HttpHeaders.PartitionKey, value);
            }
        }

        internal string PartitionKeyRangeId
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.PartitionKeyRangeId);
            }
            set
            {
                this.Set(HttpConstants.HttpHeaders.PartitionKeyRangeId, value);
            }
        }

        internal string IsUpsert
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.IsUpsert);
            }
            set
            {
                this.Set(HttpConstants.HttpHeaders.IsUpsert, value);
            }
        }

        internal string OfferThroughput
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.OfferThroughput);
            }
            set
            {
                this.Set(HttpConstants.HttpHeaders.OfferThroughput, value);
            }
        }

        internal string IfNoneMatch
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.IfNoneMatch);
            }
            set
            {
                this.Set(HttpConstants.HttpHeaders.IfNoneMatch, value);
            }
        }

        internal string PageSize
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.PageSize);
            }
            set
            {
                this.Set(HttpConstants.HttpHeaders.PageSize, value);
            }
        }

        internal string QueryMetricsText
        {
            get
            {
                return this.GetString(HttpConstants.HttpHeaders.QueryMetrics);
            }
            set
            {
                this.Set(HttpConstants.HttpHeaders.QueryMetrics, value);
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="Headers"/>.
        /// </summary>
        public Headers()
        {
            this.messageHeaders = new CosmosMessageHeadersInternal();
        }

        /// <summary>
        /// Gets the value of a particular header.
        /// </summary>
        /// <param name="headerName">Header name to look for.</param>
        /// <returns>The header value.</returns>
        public virtual string this[string headerName]
        {
            get => this.messageHeaders[headerName];
            set => this.messageHeaders[headerName] = value;
        }

        /// <summary>
        /// Enumerates all the HTTP headers names in the <see cref="Headers"/>.
        /// </summary>
        /// <returns>An enumator for all headers.</returns>
        public virtual IEnumerator<string> GetEnumerator()
        {
            return this.messageHeaders.GetEnumerator();
        }

        /// <summary>
        /// Adds a header to the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        public virtual void Add(string headerName, string value)
        {
            this.messageHeaders.Add(headerName, value);
        }

        /// <summary>
        /// Adds a header to the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="values">List of values to be added as a comma-separated list.</param>
        public virtual void Add(string headerName, IEnumerable<string> values)
        {
            this.messageHeaders.Add(headerName, values);
        }

        /// <summary>
        /// Adds or updates a header in the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        public virtual void Set(string headerName, string value)
        {
            this.messageHeaders.Set(headerName, value);
        }

        /// <summary>
        /// Gets the value of a particular header.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <returns>The header value.</returns>
        public virtual string Get(string headerName)
        {
            return this.messageHeaders.Get(headerName);
        }

        /// <summary>
        /// Tries to get the value for a particular header.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        /// <returns>True or false if the header name existed in the header collection.</returns>
        public virtual bool TryGetValue(string headerName, out string value)
        {
            return this.messageHeaders.TryGetValue(headerName, out value);
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
            this.messageHeaders.Remove(headerName);
        }

        /// <summary>
        /// Obtains a list of all header names.
        /// </summary>
        /// <returns>An array with all the header names.</returns>
        public virtual string[] AllKeys()
        {
            return this.messageHeaders.AllKeys();
        }

        /// <summary>
        /// Gets a header value with a particular type.
        /// </summary>
        /// <typeparam name="T">Type of the header value.</typeparam>
        /// <param name="headerName">Header name.</param>
        /// <returns>The header value parsed for a particular type.</returns>
        public virtual T GetHeaderValue<T>(string headerName)
        {
            return this.messageHeaders.GetHeaderValue<T>(headerName);
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

        internal CosmosMessageHeadersInternal CosmosMessageHeaders => this.messageHeaders;

        internal static SubStatusCodes GetSubStatusCodes(string value)
        {
            uint nSubStatus = 0;
            if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out nSubStatus))
            {
                return (SubStatusCodes)nSubStatus;
            }

            return SubStatusCodes.Unknown;
        }
    }
}
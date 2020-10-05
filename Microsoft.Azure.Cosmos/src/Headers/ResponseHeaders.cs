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
    internal sealed class ResponseHeaders : Headers
    {
        internal OptimizedResponseHeaders InternalResponseHeaders { get; }

        internal override SubStatusCodes SubStatusCode
        {
            get => Headers.GetSubStatusCodes(this.SubStatusCodeLiteral);
            set => this.SubStatusCodeLiteral = ((uint)value).ToString(CultureInfo.InvariantCulture);
        }

        public override string ContinuationToken
        {
            get => this.InternalResponseHeaders.ContinuationToken;
            internal set => this.InternalResponseHeaders.ContinuationToken = value;
        }

        public override double RequestCharge
        {
            get
            {
                string value = this.InternalResponseHeaders.RequestCharge;
                if (value == null)
                {
                    return 0;
                }

                return double.Parse(value, CultureInfo.InvariantCulture);
            }
            internal set => this.InternalResponseHeaders.RequestCharge = value.ToString(CultureInfo.InvariantCulture);
        }

        public override string ActivityId
        {
            get => this.InternalResponseHeaders.ActivityId;
            internal set => this.InternalResponseHeaders.ActivityId = value;
        }

        public override string ETag
        {
            get => this.InternalResponseHeaders.ETag;
            internal set => this.InternalResponseHeaders.ETag = value;
        }

        public override string Session
        {
            get => this.InternalResponseHeaders.SessionToken;
            internal set => this.InternalResponseHeaders.SessionToken = value;
        }

        internal override string SubStatusCodeLiteral
        {
            get => this.InternalResponseHeaders.SubStatus;
            set => this.InternalResponseHeaders.SubStatus = value;
        }

        internal override string RetryAfterLiteral
        {
            get => this.InternalResponseHeaders.RetryAfterInMilliseconds;
            set => this.InternalResponseHeaders.RetryAfterInMilliseconds = value;
        }

        internal override string PartitionKey
        {
            get => this.Get(HttpConstants.HttpHeaders.PartitionKey);
            set => this.Set(HttpConstants.HttpHeaders.PartitionKey, value);
        }

        internal override string PartitionKeyRangeId
        {
            get => this.InternalResponseHeaders.PartitionKeyRangeId;
            set => this.InternalResponseHeaders.PartitionKeyRangeId = value;
        }

        internal override string IfNoneMatch
        {
            get => this.Get(HttpConstants.HttpHeaders.IfNoneMatch);
            set => this.Set(HttpConstants.HttpHeaders.IfNoneMatch, value);
        }

        internal override string PageSize
        {
            get => this.Get(HttpConstants.HttpHeaders.PageSize);
            set => this.Set(HttpConstants.HttpHeaders.PageSize, value);
        }

        internal override string QueryMetricsText
        {
            get => this.InternalResponseHeaders.QueryMetrics;
            set => this.InternalResponseHeaders.QueryMetrics = value;
        }

        internal ResponseHeaders(OptimizedResponseHeaders nameValue)
        {
            this.InternalResponseHeaders = nameValue;
        }

        /// <summary>
        /// Gets the value of a particular header.
        /// </summary>
        /// <param name="headerName">Header name to look for.</param>
        /// <returns>The header value.</returns>
        public override string this[string headerName]
        {
            get => this.InternalResponseHeaders[headerName];
            set => this.InternalResponseHeaders[headerName] = value;
        }

        /// <summary>
        /// Enumerates all the HTTP headers names in the <see cref="Headers"/>.
        /// </summary>
        /// <returns>An enumerator for all headers.</returns>
        public override IEnumerator<string> GetEnumerator()
        {
            foreach (string key in this.InternalResponseHeaders.AllKeys())
            {
                yield return key;
            }
        }

        /// <summary>
        /// Adds a header to the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        public override void Add(string headerName, string value)
        {
            this.InternalResponseHeaders.Add(headerName, value);
        }

        /// <summary>
        /// Adds a header to the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="values">List of values to be added as a comma-separated list.</param>
        public override void Add(string headerName, IEnumerable<string> values)
        {
            this.InternalResponseHeaders.Add(headerName, values);
        }

        /// <summary>
        /// Adds or updates a header in the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        public override void Set(string headerName, string value)
        {
            this.InternalResponseHeaders.Set(headerName, value);
        }

        /// <summary>
        /// Gets the value of a particular header.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <returns>The header value.</returns>
        public override string Get(string headerName)
        {
            return this.InternalResponseHeaders.Get(headerName);
        }

        /// <summary>
        /// Tries to get the value for a particular header.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        /// <returns>True or false if the header name existed in the header collection.</returns>
        public override bool TryGetValue(string headerName, out string value)
        {
            value = this.InternalResponseHeaders.Get(headerName);
            return value != null;
        }

        /// <summary>
        /// Returns the header value or the default(string)
        /// </summary>
        /// <param name="headerName">Header Name</param>
        /// <returns>Returns the header value or the default(string).</returns>
        public override string GetValueOrDefault(string headerName)
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
        public override void Remove(string headerName)
        {
            this.InternalResponseHeaders.Remove(headerName);
        }

        /// <summary>
        /// Obtains a list of all header names.
        /// </summary>
        /// <returns>An array with all the header names.</returns>
        public override string[] AllKeys()
        {
            return this.InternalResponseHeaders.AllKeys();
        }

        /// <summary>
        /// Gets a header value with a particular type.
        /// </summary>
        /// <typeparam name="T">Type of the header value.</typeparam>
        /// <param name="headerName">Header name.</param>
        /// <returns>The header value parsed for a particular type.</returns>
        public override T GetHeaderValue<T>(string headerName)
        {
            return this.InternalResponseHeaders.GetHeaderValue<T>(headerName);
        }
    }
}
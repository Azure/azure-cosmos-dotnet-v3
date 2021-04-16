//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal abstract class CosmosMessageHeadersInternal : INameValueCollection
    {
        public virtual string Authorization
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.Authorization);
            set => this.SetProperty(HttpConstants.HttpHeaders.Authorization, value);
        }

        public virtual string XDate
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.XDate);
            set => this.SetProperty(HttpConstants.HttpHeaders.XDate, value);
        }

        public virtual string RequestCharge
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.RequestCharge);
            set => this.SetProperty(HttpConstants.HttpHeaders.RequestCharge, value);
        }

        public virtual string ActivityId
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.ActivityId);
            set => this.SetProperty(HttpConstants.HttpHeaders.ActivityId, value);
        }

        public virtual string ETag
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.ETag);
            set => this.SetProperty(HttpConstants.HttpHeaders.ETag, value);
        }

        public virtual string ContentType
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.ContentType);
            set => this.SetProperty(HttpConstants.HttpHeaders.ContentType, value);
        }

        public virtual string ContentLength
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.ContentLength);
            set => this.SetProperty(HttpConstants.HttpHeaders.ContentLength, value);
        }

        public virtual string SubStatus
        {
            get => this.GetValueOrDefault(WFConstants.BackendHeaders.SubStatus);
            set => this.SetProperty(WFConstants.BackendHeaders.SubStatus, value);
        }

        public virtual string RetryAfterInMilliseconds
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.RetryAfterInMilliseconds);
            set => this.SetProperty(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, value);
        }

        public virtual string IsUpsert
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.IsUpsert);
            set => this.SetProperty(HttpConstants.HttpHeaders.IsUpsert, value);
        }

        public virtual string OfferThroughput
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.OfferThroughput);
            set => this.SetProperty(HttpConstants.HttpHeaders.OfferThroughput, value);
        }

        public virtual string QueryMetrics
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.QueryMetrics);
            set => this.SetProperty(HttpConstants.HttpHeaders.QueryMetrics, value);
        }

        public virtual string BackendRequestDurationMilliseconds
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.BackendRequestDurationMilliseconds);
            set => this.SetProperty(HttpConstants.HttpHeaders.BackendRequestDurationMilliseconds, value);
        }

        public virtual string Location
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.Location);
            set => this.SetProperty(HttpConstants.HttpHeaders.Location, value);
        }

        public virtual string Continuation
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.Continuation);
            set => this.SetProperty(HttpConstants.HttpHeaders.Continuation, value);
        }

        public virtual string SessionToken
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.SessionToken);
            set => this.SetProperty(HttpConstants.HttpHeaders.SessionToken, value);
        }

        public virtual string PartitionKey
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.PartitionKey);
            set => this.SetProperty(HttpConstants.HttpHeaders.PartitionKey, value);
        }

        public virtual string PartitionKeyRangeId
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.PartitionKeyRangeId);
            set => this.SetProperty(HttpConstants.HttpHeaders.PartitionKeyRangeId, value);
        }

        public virtual string IfNoneMatch
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.IfNoneMatch);
            set => this.SetProperty(HttpConstants.HttpHeaders.IfNoneMatch, value);
        }

        public virtual string PageSize
        {
            get => this.GetValueOrDefault(HttpConstants.HttpHeaders.PageSize);
            set => this.SetProperty(HttpConstants.HttpHeaders.PageSize, value);
        }

        public virtual string this[string headerName] 
        {
            get
            {
                if (!this.TryGetValue(headerName, out string value))
                {
                    return null;
                }

                return value;
            }

            set => this.Set(headerName, value);
        }

        public abstract IEnumerator<string> GetEnumerator();

        public abstract void Add(string headerName, string value);

        public abstract void Set(string headerName, string value);

        public abstract string Get(string headerName);

        public abstract bool TryGetValue(string headerName, out string value);

        public abstract void Remove(string headerName);

        public abstract string[] AllKeys();

        public abstract void Clear();

        public abstract int Count();

        public abstract INameValueCollection Clone();

        public abstract string[] GetValues(string key);

        public abstract IEnumerable<string> Keys();

        public abstract NameValueCollection ToNameValueCollection();

        protected void SetProperty(
           string headerName,
           string value)
        {
            if (value == null)
            {
                this.Remove(headerName);
            }
            else
            {
                this.Set(headerName, value);
            }
        }

        public virtual string GetValueOrDefault(string headerName)
        {
            if (this.TryGetValue(headerName, out string value))
            {
                return value;
            }

            return default;
        }

        public virtual void Add(string headerName, IEnumerable<string> values)
        {
            if (headerName == null)
            {
                throw new ArgumentNullException(nameof(headerName));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            this.Add(headerName, string.Join(",", values));
        }

        public virtual T GetHeaderValue<T>(string key)
        {
            string value = this[key];

            if (string.IsNullOrEmpty(value))
            {
                return default;
            }

            if (typeof(T) == typeof(double))
            {
                return (T)(object)double.Parse(value, CultureInfo.InvariantCulture);
            }

            return (T)(object)value;
        }

        public virtual void Add(INameValueCollection collection)
        {
            foreach (string key in collection.Keys())
            {
                this.Set(key, collection[key]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
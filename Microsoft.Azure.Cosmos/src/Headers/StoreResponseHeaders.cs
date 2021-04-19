//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Header implementation used for Responses
    /// </summary>
    internal sealed class StoreResponseHeaders : CosmosMessageHeadersInternal
    {
        private readonly StoreResponseNameValueCollection storeResponseNameValueCollection;

        public override string RequestCharge
        {
            get => this.storeResponseNameValueCollection.RequestCharge;
            set => this.storeResponseNameValueCollection.RequestCharge = value;
        }

        public override string ActivityId
        {
            get => this.storeResponseNameValueCollection.ActivityId;
            set => this.storeResponseNameValueCollection.ActivityId = value;
        }

        public override string ETag
        {
            get => this.storeResponseNameValueCollection.ETag;
            set => this.storeResponseNameValueCollection.ETag = value;
        }

        public override string SubStatus
        {
            get => this.storeResponseNameValueCollection.SubStatus;
            set => this.storeResponseNameValueCollection.SubStatus = value;
        }

        public override string QueryMetrics
        {
            get => this.storeResponseNameValueCollection.QueryMetrics;
            set => this.storeResponseNameValueCollection.QueryMetrics = value;
        }

        public override string BackendRequestDurationMilliseconds
        {
            get => this.storeResponseNameValueCollection.BackendRequestDurationMilliseconds;
            set => this.storeResponseNameValueCollection.BackendRequestDurationMilliseconds = value;
        }

        public override string Continuation
        {
            get => this.storeResponseNameValueCollection.Continuation;
            set => this.storeResponseNameValueCollection.Continuation = value;
        }

        public override string SessionToken
        {
            get => this.storeResponseNameValueCollection.SessionToken;
            set => this.storeResponseNameValueCollection.SessionToken = value;
        }

        public override string PartitionKeyRangeId
        {
            get => this.storeResponseNameValueCollection.PartitionKeyRangeId;
            set => this.storeResponseNameValueCollection.PartitionKeyRangeId = value;
        }

        public StoreResponseHeaders(StoreResponseNameValueCollection storeResponseNameValueCollection)
        {
            this.storeResponseNameValueCollection = storeResponseNameValueCollection ?? throw new ArgumentNullException(nameof(storeResponseNameValueCollection));
        }

        public override void Add(string headerName, string value)
        {
            this.storeResponseNameValueCollection.Add(headerName, value);
        }

        public override void Add(string headerName, IEnumerable<string> values)
        {
            this.storeResponseNameValueCollection.Add(headerName, values);
        }

        public override void Set(string headerName, string value)
        {
            this.storeResponseNameValueCollection.Set(headerName, value);
        }

        public override string Get(string headerName)
        {
            return this.storeResponseNameValueCollection.Get(headerName);
        }

        public override bool TryGetValue(string headerName, out string value)
        {
            value = this.storeResponseNameValueCollection.Get(headerName);
            return value != null;
        }

        public override void Remove(string headerName)
        {
            this.storeResponseNameValueCollection.Remove(headerName);
        }

        public override string[] AllKeys()
        {
            return this.storeResponseNameValueCollection.AllKeys();
        }

        public override IEnumerator<string> GetEnumerator()
        {
            return this.storeResponseNameValueCollection.Keys().GetEnumerator();
        }

        public override void Clear()
        {
            this.storeResponseNameValueCollection.Clear();
        }

        public override int Count()
        {
            return this.storeResponseNameValueCollection.Count();
        }

        public override INameValueCollection Clone()
        {
            return this.storeResponseNameValueCollection.Clone();
        }

        public override string[] GetValues(string key)
        {
            return this.storeResponseNameValueCollection.GetValues(key);
        }

        public override IEnumerable<string> Keys()
        {
            return this.storeResponseNameValueCollection.Keys();
        }

        public override NameValueCollection ToNameValueCollection()
        {
            return this.storeResponseNameValueCollection.ToNameValueCollection();
        }
    }
}
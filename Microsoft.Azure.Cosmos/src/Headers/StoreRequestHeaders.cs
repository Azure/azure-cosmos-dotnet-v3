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
    internal sealed class StoreRequestHeaders : CosmosMessageHeadersInternal
    {
        private readonly RequestNameValueCollection requestNameValueCollection;

        public override string Continuation
        {
            get => this.requestNameValueCollection.ContinuationToken;
            set => this.requestNameValueCollection.ContinuationToken = value;
        }

        public override string SessionToken
        {
            get => this.requestNameValueCollection.SessionToken;
            set => this.requestNameValueCollection.SessionToken = value;
        }

        public override string PartitionKeyRangeId
        {
            get => this.requestNameValueCollection.PartitionKeyRangeId;
            set => this.requestNameValueCollection.PartitionKeyRangeId = value;
        }

        public override string PartitionKey
        {
            get => this.requestNameValueCollection.PartitionKey;
            set => this.requestNameValueCollection.PartitionKey = value;
        }

        public override string XDate
        {
            get => this.requestNameValueCollection.XDate;
            set => this.requestNameValueCollection.XDate = value;
        }

        public override string ConsistencyLevel
        {
            get => this.requestNameValueCollection.ConsistencyLevel;
            set => this.requestNameValueCollection.ConsistencyLevel = value;
        }

        public override string IfNoneMatch
        {
            get => this.requestNameValueCollection.IfNoneMatch;
            set => this.requestNameValueCollection.IfNoneMatch = value;
        }

        public override string IndexUtilization
        {
            get => this.requestNameValueCollection.PopulateIndexMetrics;
            set => this.requestNameValueCollection.PopulateIndexMetrics = value;
        }

        public override string SDKSupportedCapabilities
        {
            get => this.requestNameValueCollection.SDKSupportedCapabilities;
            set => this.requestNameValueCollection.SDKSupportedCapabilities = value;
        }

        public override string ContentSerializationFormat
        {
            get => this.requestNameValueCollection.ContentSerializationFormat;
            set => this.requestNameValueCollection.ContentSerializationFormat = value;
        }

        public override string ReadFeedKeyType
        {
            get => this.requestNameValueCollection.ReadFeedKeyType;
            set => this.requestNameValueCollection.ReadFeedKeyType = value;
        }

        public override string StartEpk
        {
            get => this.requestNameValueCollection.StartEpk;
            set => this.requestNameValueCollection.StartEpk = value;
        }

        public override string EndEpk
        {
            get => this.requestNameValueCollection.EndEpk;
            set => this.requestNameValueCollection.EndEpk = value;
        }

        public override string PageSize
        {
            get => this.requestNameValueCollection.PageSize;
            set => this.requestNameValueCollection.PageSize = value;
        }

        public override INameValueCollection INameValueCollection => this.requestNameValueCollection;

        public StoreRequestHeaders()
        {
            this.requestNameValueCollection = new RequestNameValueCollection();
        }

        public override void Add(string headerName, string value)
        {
            this.requestNameValueCollection.Add(headerName, value);
        }

        public override void Add(string headerName, IEnumerable<string> values)
        {
            this.requestNameValueCollection.Add(headerName, values);
        }

        public override void Set(string headerName, string value)
        {
            this.requestNameValueCollection.Set(headerName, value);
        }

        public override string Get(string headerName)
        {
            return this.requestNameValueCollection.Get(headerName);
        }

        public override bool TryGetValue(string headerName, out string value)
        {
            value = this.requestNameValueCollection.Get(headerName);
            return value != null;
        }

        public override void Remove(string headerName)
        {
            this.requestNameValueCollection.Remove(headerName);
        }

        public override string[] AllKeys()
        {
            return this.requestNameValueCollection.AllKeys();
        }

        public override IEnumerator<string> GetEnumerator()
        {
            return this.requestNameValueCollection.Keys().GetEnumerator();
        }

        public override string[] GetValues(string key)
        {
            return this.requestNameValueCollection.GetValues(key);
        }
    }
}
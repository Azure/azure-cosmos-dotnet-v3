//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code.

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class StoreRequestHeaders : InternalHeaders, INameValueCollection
    {
        private readonly Lazy<Dictionary<string, string>> lazyNotCommonHeaders;
        public override string Authorization { get; set; }
        public string ClientRetryAttemptCount { get; set; }
        public string CollectionRid { get; set; }
        public string ConsistencyLevel { get; set; }
        public override string Continuation { get; set; }
        public string EffectivePartitionKey { get; set; }
        public string ExcludeSystemProperties { get; set; }
        public string HttpDate { get; set; }
        public string IsBatchAtomic { get; set; }
        public string IsBatchOrdered { get; set; }
        public override string IsUpsert { get; set; }
        public override string PartitionKey { get; set; }
        public override string PartitionKeyRangeId { get; set; }
        public string Prefer { get; set; }
        public string RemainingTimeInMsOnClientRequest { get; set; }
        public string ResourceTokenExpiry { get; set; }
        public string ResourceTypes { get; set; }
        public override string SessionToken { get; set; }
        public string ShouldBatchContinueOnError { get; set; }
        public string TargetGlobalCommittedLsn { get; set; }
        public string TargetLsn { get; set; }
        public string TimeToLiveInSeconds { get; set; }
        public string TransactionCommit { get; set; }
        public string TransactionId { get; set; }
        public string TransportRequestID { get; set; }
        public string Version { get; set; }
        public override string XDate { get; set; }

        public StoreRequestHeaders()
            : this(new Lazy<Dictionary<string, string>>(() => new Dictionary<string, string>()))
        {
        }

        private StoreRequestHeaders(Lazy<Dictionary<string, string>> notCommonHeaders)
        {
            this.lazyNotCommonHeaders = notCommonHeaders ?? throw new ArgumentNullException(nameof(notCommonHeaders));
        }

        public override bool TryGetValue(string headerName, out string value)
        {
            value = this.Get(headerName);
            return value != null;
        }

        public override void Add(INameValueCollection collection)
        {
            foreach (string key in collection.Keys())
            {
                this.Set(key, collection[key]);
            }
        }

        public override string[] AllKeys()
        {
            return this.Keys().ToArray();
        }

        public override void Clear()
        {
            if (this.lazyNotCommonHeaders.IsValueCreated)
            {
                this.lazyNotCommonHeaders.Value.Clear();
            }

            this.Authorization = null;
            this.ClientRetryAttemptCount = null;
            this.CollectionRid = null;
            this.ConsistencyLevel = null;
            this.Continuation = null;
            this.EffectivePartitionKey = null;
            this.ExcludeSystemProperties = null;
            this.HttpDate = null;
            this.IsBatchAtomic = null;
            this.IsBatchOrdered = null;
            this.IsUpsert = null;
            this.PartitionKey = null;
            this.PartitionKeyRangeId = null;
            this.Prefer = null;
            this.RemainingTimeInMsOnClientRequest = null;
            this.ResourceTokenExpiry = null;
            this.ResourceTypes = null;
            this.SessionToken = null;
            this.ShouldBatchContinueOnError = null;
            this.TargetGlobalCommittedLsn = null;
            this.TargetLsn = null;
            this.TimeToLiveInSeconds = null;
            this.TransactionCommit = null;
            this.TransactionId = null;
            this.TransportRequestID = null;
            this.Version = null;
            this.XDate = null;

        }

        public override INameValueCollection Clone()
        {
            Lazy<Dictionary<string, string>> cloneNotCommonHeaders = new Lazy<Dictionary<string, string>>(() => new Dictionary<string, string>());
            if (this.lazyNotCommonHeaders.IsValueCreated)
            {
                foreach (KeyValuePair<string, string> notCommonHeader in this.lazyNotCommonHeaders.Value)
                {
                    cloneNotCommonHeaders.Value[notCommonHeader.Key] = notCommonHeader.Value;
                }
            }

            StoreRequestHeaders cloneHeaders = new StoreRequestHeaders(cloneNotCommonHeaders)
            {
                Authorization = this.Authorization,
                ClientRetryAttemptCount = this.ClientRetryAttemptCount,
                CollectionRid = this.CollectionRid,
                ConsistencyLevel = this.ConsistencyLevel,
                Continuation = this.Continuation,
                EffectivePartitionKey = this.EffectivePartitionKey,
                ExcludeSystemProperties = this.ExcludeSystemProperties,
                HttpDate = this.HttpDate,
                IsBatchAtomic = this.IsBatchAtomic,
                IsBatchOrdered = this.IsBatchOrdered,
                IsUpsert = this.IsUpsert,
                PartitionKey = this.PartitionKey,
                PartitionKeyRangeId = this.PartitionKeyRangeId,
                Prefer = this.Prefer,
                RemainingTimeInMsOnClientRequest = this.RemainingTimeInMsOnClientRequest,
                ResourceTokenExpiry = this.ResourceTokenExpiry,
                ResourceTypes = this.ResourceTypes,
                SessionToken = this.SessionToken,
                ShouldBatchContinueOnError = this.ShouldBatchContinueOnError,
                TargetGlobalCommittedLsn = this.TargetGlobalCommittedLsn,
                TargetLsn = this.TargetLsn,
                TimeToLiveInSeconds = this.TimeToLiveInSeconds,
                TransactionCommit = this.TransactionCommit,
                TransactionId = this.TransactionId,
                TransportRequestID = this.TransportRequestID,
                Version = this.Version,
                XDate = this.XDate,
            };

            return cloneHeaders;
        }

        public override int Count()
        {
            return this.Keys().Count();
        }

        public override IEnumerator<string> GetEnumerator()
        {
            return this.Keys().GetEnumerator();
        }

        public override string[] GetValues(string key)
        {
            string value = this.Get(key);
            if (value != null)
            {
                return new string[] { value };
            }
            
            return null;
        }

        public override IEnumerable<string> Keys()
        {
            if (this.Authorization != null)
            {
                yield return HttpConstants.HttpHeaders.Authorization;
            }
            if (this.HttpDate != null)
            {
                yield return HttpConstants.HttpHeaders.HttpDate;
            }
            if (this.XDate != null)
            {
                yield return HttpConstants.HttpHeaders.XDate;
            }
            if (this.Version != null)
            {
                yield return HttpConstants.HttpHeaders.Version;
            }
            if (this.ClientRetryAttemptCount != null)
            {
                yield return HttpConstants.HttpHeaders.ClientRetryAttemptCount;
            }
            if (this.ConsistencyLevel != null)
            {
                yield return HttpConstants.HttpHeaders.ConsistencyLevel;
            }
            if (this.Continuation != null)
            {
                yield return HttpConstants.HttpHeaders.Continuation;
            }
            if (this.IsBatchAtomic != null)
            {
                yield return HttpConstants.HttpHeaders.IsBatchAtomic;
            }
            if (this.IsBatchOrdered != null)
            {
                yield return HttpConstants.HttpHeaders.IsBatchOrdered;
            }
            if (this.IsUpsert != null)
            {
                yield return HttpConstants.HttpHeaders.IsUpsert;
            }
            if (this.PartitionKey != null)
            {
                yield return HttpConstants.HttpHeaders.PartitionKey;
            }
            if (this.Prefer != null)
            {
                yield return HttpConstants.HttpHeaders.Prefer;
            }
            if (this.RemainingTimeInMsOnClientRequest != null)
            {
                yield return HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest;
            }
            if (this.ResourceTokenExpiry != null)
            {
                yield return HttpConstants.HttpHeaders.ResourceTokenExpiry;
            }
            if (this.SessionToken != null)
            {
                yield return HttpConstants.HttpHeaders.SessionToken;
            }
            if (this.ShouldBatchContinueOnError != null)
            {
                yield return HttpConstants.HttpHeaders.ShouldBatchContinueOnError;
            }
            if (this.TargetGlobalCommittedLsn != null)
            {
                yield return HttpConstants.HttpHeaders.TargetGlobalCommittedLsn;
            }
            if (this.TargetLsn != null)
            {
                yield return HttpConstants.HttpHeaders.TargetLsn;
            }
            if (this.TransportRequestID != null)
            {
                yield return HttpConstants.HttpHeaders.TransportRequestID;
            }
            if (this.CollectionRid != null)
            {
                yield return WFConstants.BackendHeaders.CollectionRid;
            }
            if (this.EffectivePartitionKey != null)
            {
                yield return WFConstants.BackendHeaders.EffectivePartitionKey;
            }
            if (this.ExcludeSystemProperties != null)
            {
                yield return WFConstants.BackendHeaders.ExcludeSystemProperties;
            }
            if (this.PartitionKeyRangeId != null)
            {
                yield return WFConstants.BackendHeaders.PartitionKeyRangeId;
            }
            if (this.ResourceTypes != null)
            {
                yield return WFConstants.BackendHeaders.ResourceTypes;
            }
            if (this.TimeToLiveInSeconds != null)
            {
                yield return WFConstants.BackendHeaders.TimeToLiveInSeconds;
            }
            if (this.TransactionCommit != null)
            {
                yield return WFConstants.BackendHeaders.TransactionCommit;
            }
            if (this.TransactionId != null)
            {
                yield return WFConstants.BackendHeaders.TransactionId;
            }

            if (this.lazyNotCommonHeaders.IsValueCreated)
            {
                foreach (string key in this.lazyNotCommonHeaders.Value.Keys)
                {
                    yield return key;
                }
            }
        }

        public override NameValueCollection ToNameValueCollection()
        {
            throw new NotImplementedException();
        }

        public override string Get(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            switch (key.Length)
            {
                case 4:
                    if (string.Equals(HttpConstants.HttpHeaders.HttpDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.HttpDate;
                    }
                
                    break;
                case 6:
                    if (string.Equals(HttpConstants.HttpHeaders.Prefer, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.Prefer;
                    }
                
                    break;
                case 9:
                    if (string.Equals(HttpConstants.HttpHeaders.XDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.XDate;
                    }
                
                    break;
                case 12:
                    if (string.Equals(HttpConstants.HttpHeaders.Version, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.Version;
                    }
                
                    break;
                case 13:
                    if (string.Equals(HttpConstants.HttpHeaders.Authorization, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.Authorization;
                    }
                
                    break;
                case 15:
                    if (string.Equals(HttpConstants.HttpHeaders.TargetLsn, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TargetLsn;
                    }
                
                    break;
                case 17:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Continuation, key))
                    {
                        return this.Continuation;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TransactionId, key))
                    {
                        return this.TransactionId;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.Continuation, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.Continuation;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.TransactionId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TransactionId;
                    }
                
                    break;
                case 18:
                    if (string.Equals(HttpConstants.HttpHeaders.SessionToken, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.SessionToken;
                    }
                
                    break;
                case 21:
                    if (string.Equals(WFConstants.BackendHeaders.TransactionCommit, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TransactionCommit;
                    }
                
                    break;
                case 22:
                    if (string.Equals(HttpConstants.HttpHeaders.ConsistencyLevel, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ConsistencyLevel;
                    }
                
                    break;
                case 24:
                    if (string.Equals(HttpConstants.HttpHeaders.IsBatchAtomic, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsBatchAtomic;
                    }
                
                    break;
                case 25:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsBatchOrdered, key))
                    {
                        return this.IsBatchOrdered;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsUpsert, key))
                    {
                        return this.IsUpsert;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TransportRequestID, key))
                    {
                        return this.TransportRequestID;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ResourceTypes, key))
                    {
                        return this.ResourceTypes;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IsBatchOrdered, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsBatchOrdered;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsUpsert, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsUpsert;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.TransportRequestID, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TransportRequestID;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ResourceTypes, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ResourceTypes;
                    }
                
                    break;
                case 28:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PartitionKey, key))
                    {
                        return this.PartitionKey;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.EffectivePartitionKey, key))
                    {
                        return this.EffectivePartitionKey;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key))
                    {
                        return this.TimeToLiveInSeconds;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PartitionKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PartitionKey;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.EffectivePartitionKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.EffectivePartitionKey;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TimeToLiveInSeconds;
                    }
                
                    break;
                case 30:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ResourceTokenExpiry, key))
                    {
                        return this.ResourceTokenExpiry;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionRid, key))
                    {
                        return this.CollectionRid;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ExcludeSystemProperties, key))
                    {
                        return this.ExcludeSystemProperties;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.ResourceTokenExpiry, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ResourceTokenExpiry;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionRid, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionRid;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ExcludeSystemProperties, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ExcludeSystemProperties;
                    }
                
                    break;
                case 31:
                    if (string.Equals(HttpConstants.HttpHeaders.ClientRetryAttemptCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ClientRetryAttemptCount;
                    }
                
                    break;
                case 32:
                    if (string.Equals(HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TargetGlobalCommittedLsn;
                    }
                
                    break;
                case 35:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, key))
                    {
                        return this.RemainingTimeInMsOnClientRequest;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, key))
                    {
                        return this.ShouldBatchContinueOnError;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PartitionKeyRangeId, key))
                    {
                        return this.PartitionKeyRangeId;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.RemainingTimeInMsOnClientRequest;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ShouldBatchContinueOnError;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PartitionKeyRangeId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PartitionKeyRangeId;
                    }
                
                    break;
                default:
                    break;
            }

            if (this.lazyNotCommonHeaders.IsValueCreated
                && this.lazyNotCommonHeaders.Value.TryGetValue(key, out string value))
            {
                return value;
            }
            
            return null;
        }

        public override void Add(string key, string value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            this.UpdateHelper(
                key: key, 
                value: value, 
                throwIfAlreadyExists: true);
        }

        public override void Remove(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.UpdateHelper(
                key: key, 
                value: null, 
                throwIfAlreadyExists: false);
        }

        public override void Set(string key, string value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                this.Remove(key);
                return;
            }

            this.UpdateHelper(
                key: key, 
                value: value, 
                throwIfAlreadyExists: false);
        }

        public void UpdateHelper(
            string key, 
            string value,
            bool throwIfAlreadyExists)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            switch (key.Length)
            {
                case 4:
                    if (string.Equals(HttpConstants.HttpHeaders.HttpDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.HttpDate != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.HttpDate = value;
                        return;
                    }
                    break;
                case 6:
                    if (string.Equals(HttpConstants.HttpHeaders.Prefer, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.Prefer != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.Prefer = value;
                        return;
                    }
                    break;
                case 9:
                    if (string.Equals(HttpConstants.HttpHeaders.XDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.XDate != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.XDate = value;
                        return;
                    }
                    break;
                case 12:
                    if (string.Equals(HttpConstants.HttpHeaders.Version, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.Version != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.Version = value;
                        return;
                    }
                    break;
                case 13:
                    if (string.Equals(HttpConstants.HttpHeaders.Authorization, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.Authorization != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.Authorization = value;
                        return;
                    }
                    break;
                case 15:
                    if (string.Equals(HttpConstants.HttpHeaders.TargetLsn, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.TargetLsn != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TargetLsn = value;
                        return;
                    }
                    break;
                case 17:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Continuation, key))
                    {
                        if (throwIfAlreadyExists && this.Continuation != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.Continuation = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TransactionId, key))
                    {
                        if (throwIfAlreadyExists && this.TransactionId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TransactionId = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.Continuation, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.Continuation != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.Continuation = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.TransactionId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.TransactionId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TransactionId = value;
                        return;
                    }
                    break;
                case 18:
                    if (string.Equals(HttpConstants.HttpHeaders.SessionToken, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.SessionToken != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SessionToken = value;
                        return;
                    }
                    break;
                case 21:
                    if (string.Equals(WFConstants.BackendHeaders.TransactionCommit, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.TransactionCommit != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TransactionCommit = value;
                        return;
                    }
                    break;
                case 22:
                    if (string.Equals(HttpConstants.HttpHeaders.ConsistencyLevel, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ConsistencyLevel != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ConsistencyLevel = value;
                        return;
                    }
                    break;
                case 24:
                    if (string.Equals(HttpConstants.HttpHeaders.IsBatchAtomic, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IsBatchAtomic != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsBatchAtomic = value;
                        return;
                    }
                    break;
                case 25:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsBatchOrdered, key))
                    {
                        if (throwIfAlreadyExists && this.IsBatchOrdered != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsBatchOrdered = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsUpsert, key))
                    {
                        if (throwIfAlreadyExists && this.IsUpsert != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsUpsert = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TransportRequestID, key))
                    {
                        if (throwIfAlreadyExists && this.TransportRequestID != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TransportRequestID = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ResourceTypes, key))
                    {
                        if (throwIfAlreadyExists && this.ResourceTypes != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ResourceTypes = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IsBatchOrdered, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IsBatchOrdered != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsBatchOrdered = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IsUpsert, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IsUpsert != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsUpsert = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.TransportRequestID, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.TransportRequestID != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TransportRequestID = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.ResourceTypes, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ResourceTypes != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ResourceTypes = value;
                        return;
                    }
                    break;
                case 28:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PartitionKey, key))
                    {
                        if (throwIfAlreadyExists && this.PartitionKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PartitionKey = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.EffectivePartitionKey, key))
                    {
                        if (throwIfAlreadyExists && this.EffectivePartitionKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EffectivePartitionKey = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key))
                    {
                        if (throwIfAlreadyExists && this.TimeToLiveInSeconds != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TimeToLiveInSeconds = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PartitionKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PartitionKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PartitionKey = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.EffectivePartitionKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.EffectivePartitionKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EffectivePartitionKey = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.TimeToLiveInSeconds != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TimeToLiveInSeconds = value;
                        return;
                    }
                    break;
                case 30:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ResourceTokenExpiry, key))
                    {
                        if (throwIfAlreadyExists && this.ResourceTokenExpiry != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ResourceTokenExpiry = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionRid, key))
                    {
                        if (throwIfAlreadyExists && this.CollectionRid != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionRid = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ExcludeSystemProperties, key))
                    {
                        if (throwIfAlreadyExists && this.ExcludeSystemProperties != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ExcludeSystemProperties = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.ResourceTokenExpiry, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ResourceTokenExpiry != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ResourceTokenExpiry = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.CollectionRid, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.CollectionRid != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionRid = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.ExcludeSystemProperties, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ExcludeSystemProperties != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ExcludeSystemProperties = value;
                        return;
                    }
                    break;
                case 31:
                    if (string.Equals(HttpConstants.HttpHeaders.ClientRetryAttemptCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ClientRetryAttemptCount != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ClientRetryAttemptCount = value;
                        return;
                    }
                    break;
                case 32:
                    if (string.Equals(HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.TargetGlobalCommittedLsn != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TargetGlobalCommittedLsn = value;
                        return;
                    }
                    break;
                case 35:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, key))
                    {
                        if (throwIfAlreadyExists && this.RemainingTimeInMsOnClientRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.RemainingTimeInMsOnClientRequest = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, key))
                    {
                        if (throwIfAlreadyExists && this.ShouldBatchContinueOnError != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ShouldBatchContinueOnError = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PartitionKeyRangeId, key))
                    {
                        if (throwIfAlreadyExists && this.PartitionKeyRangeId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PartitionKeyRangeId = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.RemainingTimeInMsOnClientRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.RemainingTimeInMsOnClientRequest = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ShouldBatchContinueOnError != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ShouldBatchContinueOnError = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.PartitionKeyRangeId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PartitionKeyRangeId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PartitionKeyRangeId = value;
                        return;
                    }
                    break;
                default:
                    break;
            }

            if (throwIfAlreadyExists)
            {
                this.lazyNotCommonHeaders.Value.Add(key, value);
            }
            else
            {
                if (value == null)
                {
                    this.lazyNotCommonHeaders.Value.Remove(key);
                }
                else
                {
                    this.lazyNotCommonHeaders.Value[key] = value;
                }
            }
        }
    }
}
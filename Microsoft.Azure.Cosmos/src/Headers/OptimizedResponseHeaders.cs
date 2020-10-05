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
    using System.Linq;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class OptimizedResponseHeaders : INameValueCollection
    {
        private readonly Lazy<Dictionary<string, string>> lazyNotCommonHeaders;

        public string ActivityId { get; set; }
        public string BackendRequestDurationMilliseconds { get; set; }
        public string CollectionIndexTransformationProgress { get; set; }
        public string CollectionLazyIndexingProgress { get; set; }
        public string CollectionPartitionIndex { get; set; }
        public string CollectionSecurityIdentifier { get; set; }
        public string CollectionServiceIndex { get; set; }
        public string ContinuationToken { get; set; }
        public string CurrentReplicaSetSize { get; set; }
        public string CurrentResourceQuotaUsage { get; set; }
        public string CurrentWriteQuorum { get; set; }
        public string DatabaseAccountId { get; set; }
        public string DisableRntbdChannel { get; set; }
        public string ETag { get; set; }
        public string GlobalCommittedLSN { get; set; }
        public string HasTentativeWrites { get; set; }
        public string IndexingDirective { get; set; }
        public string IndexUtilization { get; set; }
        public string IsRUPerMinuteUsed { get; set; }
        public string ItemCount { get; set; }
        public string ItemLocalLSN { get; set; }
        public string ItemLSN { get; set; }
        public string LastStateChangeUtc { get; set; }
        public string LocalLSN { get; set; }
        public string LogResults { get; set; }
        public string LSN { get; set; }
        public string MaxResourceQuota { get; set; }
        public string MinimumRUsForOffer { get; set; }
        public string NumberOfReadRegions { get; set; }
        public string OfferReplacePending { get; set; }
        public string OwnerFullName { get; set; }
        public string OwnerId { get; set; }
        public string PartitionKeyRangeId { get; set; }
        public string QueryExecutionInfo { get; set; }
        public string QueryMetrics { get; set; }
        public string QuorumAckedLocalLSN { get; set; }
        public string QuorumAckedLSN { get; set; }
        public string ReplicaStatusRevoked { get; set; }
        public string ReplicatorLSNToGLSNDelta { get; set; }
        public string ReplicatorLSNToLLSNDelta { get; set; }
        public string RequestCharge { get; set; }
        public string RequestValidationFailure { get; set; }
        public string ResourceId { get; set; }
        public string RestoreState { get; set; }
        public string RetryAfterInMilliseconds { get; set; }
        public string SchemaVersion { get; set; }
        public string SessionToken { get; set; }
        public string ShareThroughput { get; set; }
        public string SoftMaxAllowedThroughput { get; set; }
        public string SubStatus { get; set; }
        public string TimeToLiveInSeconds { get; set; }
        public string TransportRequestID { get; set; }
        public string UnflushedMergLogEntryCount { get; set; }
        public string VectorClockLocalProgress { get; set; }
        public string XDate { get; set; }
        public string XPConfigurationSessionsCount { get; set; }
        public string XPRole { get; set; }

        public OptimizedResponseHeaders()
            : this(new Lazy<Dictionary<string, string>>(() => new Dictionary<string, string>()))
        {
        }

        private OptimizedResponseHeaders(Lazy<Dictionary<string, string>> notCommonHeaders)
        {
            this.lazyNotCommonHeaders = notCommonHeaders ?? throw new ArgumentNullException(nameof(notCommonHeaders));
        }

        public string this[string key] 
        { 
            get => this.Get(key); 
            set => this.Set(key, value);
        }

        public void Add(string key, string value)
        {
            this.Set(key, value);
        }

        public void Add(INameValueCollection collection)
        {
            foreach(string key in collection.Keys())
            {
                this.Set(key, collection[key]);
            }
        }

        public string[] AllKeys()
        {
            return this.Keys().ToArray();
        }

        public void Clear()
        {
            if (this.lazyNotCommonHeaders.IsValueCreated)
            {
                this.lazyNotCommonHeaders.Value.Clear();
            }

            this.ActivityId = null;
            this.BackendRequestDurationMilliseconds = null;
            this.CollectionIndexTransformationProgress = null;
            this.CollectionLazyIndexingProgress = null;
            this.CollectionPartitionIndex = null;
            this.CollectionSecurityIdentifier = null;
            this.CollectionServiceIndex = null;
            this.ContinuationToken = null;
            this.CurrentReplicaSetSize = null;
            this.CurrentResourceQuotaUsage = null;
            this.CurrentWriteQuorum = null;
            this.DatabaseAccountId = null;
            this.DisableRntbdChannel = null;
            this.ETag = null;
            this.GlobalCommittedLSN = null;
            this.HasTentativeWrites = null;
            this.IndexingDirective = null;
            this.IndexUtilization = null;
            this.IsRUPerMinuteUsed = null;
            this.ItemCount = null;
            this.ItemLocalLSN = null;
            this.ItemLSN = null;
            this.LastStateChangeUtc = null;
            this.LocalLSN = null;
            this.LogResults = null;
            this.LSN = null;
            this.MaxResourceQuota = null;
            this.MinimumRUsForOffer = null;
            this.NumberOfReadRegions = null;
            this.OfferReplacePending = null;
            this.OwnerFullName = null;
            this.OwnerId = null;
            this.PartitionKeyRangeId = null;
            this.QueryExecutionInfo = null;
            this.QueryMetrics = null;
            this.QuorumAckedLocalLSN = null;
            this.QuorumAckedLSN = null;
            this.ReplicaStatusRevoked = null;
            this.ReplicatorLSNToGLSNDelta = null;
            this.ReplicatorLSNToLLSNDelta = null;
            this.RequestCharge = null;
            this.RequestValidationFailure = null;
            this.ResourceId = null;
            this.RestoreState = null;
            this.RetryAfterInMilliseconds = null;
            this.SchemaVersion = null;
            this.SessionToken = null;
            this.ShareThroughput = null;
            this.SoftMaxAllowedThroughput = null;
            this.SubStatus = null;
            this.TimeToLiveInSeconds = null;
            this.TransportRequestID = null;
            this.UnflushedMergLogEntryCount = null;
            this.VectorClockLocalProgress = null;
            this.XDate = null;
            this.XPConfigurationSessionsCount = null;
            this.XPRole = null;

        }

        public INameValueCollection Clone()
        {
            Lazy<Dictionary<string, string>> cloneNotCommonHeaders = new Lazy<Dictionary<string, string>>(() => new Dictionary<string, string>());
            if (this.lazyNotCommonHeaders.IsValueCreated)
            {
                foreach (KeyValuePair<string, string> notCommonHeader in this.lazyNotCommonHeaders.Value)
                {
                    cloneNotCommonHeaders.Value[notCommonHeader.Key] = notCommonHeader.Value;
                }
            }

            OptimizedResponseHeaders cloneHeaders = new OptimizedResponseHeaders(cloneNotCommonHeaders)
            {
                ActivityId = this.ActivityId,
                BackendRequestDurationMilliseconds = this.BackendRequestDurationMilliseconds,
                CollectionIndexTransformationProgress = this.CollectionIndexTransformationProgress,
                CollectionLazyIndexingProgress = this.CollectionLazyIndexingProgress,
                CollectionPartitionIndex = this.CollectionPartitionIndex,
                CollectionSecurityIdentifier = this.CollectionSecurityIdentifier,
                CollectionServiceIndex = this.CollectionServiceIndex,
                ContinuationToken = this.ContinuationToken,
                CurrentReplicaSetSize = this.CurrentReplicaSetSize,
                CurrentResourceQuotaUsage = this.CurrentResourceQuotaUsage,
                CurrentWriteQuorum = this.CurrentWriteQuorum,
                DatabaseAccountId = this.DatabaseAccountId,
                DisableRntbdChannel = this.DisableRntbdChannel,
                ETag = this.ETag,
                GlobalCommittedLSN = this.GlobalCommittedLSN,
                HasTentativeWrites = this.HasTentativeWrites,
                IndexingDirective = this.IndexingDirective,
                IndexUtilization = this.IndexUtilization,
                IsRUPerMinuteUsed = this.IsRUPerMinuteUsed,
                ItemCount = this.ItemCount,
                ItemLocalLSN = this.ItemLocalLSN,
                ItemLSN = this.ItemLSN,
                LastStateChangeUtc = this.LastStateChangeUtc,
                LocalLSN = this.LocalLSN,
                LogResults = this.LogResults,
                LSN = this.LSN,
                MaxResourceQuota = this.MaxResourceQuota,
                MinimumRUsForOffer = this.MinimumRUsForOffer,
                NumberOfReadRegions = this.NumberOfReadRegions,
                OfferReplacePending = this.OfferReplacePending,
                OwnerFullName = this.OwnerFullName,
                OwnerId = this.OwnerId,
                PartitionKeyRangeId = this.PartitionKeyRangeId,
                QueryExecutionInfo = this.QueryExecutionInfo,
                QueryMetrics = this.QueryMetrics,
                QuorumAckedLocalLSN = this.QuorumAckedLocalLSN,
                QuorumAckedLSN = this.QuorumAckedLSN,
                ReplicaStatusRevoked = this.ReplicaStatusRevoked,
                ReplicatorLSNToGLSNDelta = this.ReplicatorLSNToGLSNDelta,
                ReplicatorLSNToLLSNDelta = this.ReplicatorLSNToLLSNDelta,
                RequestCharge = this.RequestCharge,
                RequestValidationFailure = this.RequestValidationFailure,
                ResourceId = this.ResourceId,
                RestoreState = this.RestoreState,
                RetryAfterInMilliseconds = this.RetryAfterInMilliseconds,
                SchemaVersion = this.SchemaVersion,
                SessionToken = this.SessionToken,
                ShareThroughput = this.ShareThroughput,
                SoftMaxAllowedThroughput = this.SoftMaxAllowedThroughput,
                SubStatus = this.SubStatus,
                TimeToLiveInSeconds = this.TimeToLiveInSeconds,
                TransportRequestID = this.TransportRequestID,
                UnflushedMergLogEntryCount = this.UnflushedMergLogEntryCount,
                VectorClockLocalProgress = this.VectorClockLocalProgress,
                XDate = this.XDate,
                XPConfigurationSessionsCount = this.XPConfigurationSessionsCount,
                XPRole = this.XPRole,
            };

            return cloneHeaders;
        }

        public int Count()
        {
            return this.Keys().Count();
        }

        public IEnumerator GetEnumerator()
        {
            return this.Keys().GetEnumerator();
        }

        public string[] GetValues(string key)
        {
            string value = this.Get(key);
            if(value != null){
                return new string[] { value };
            }
            
            return null;
        }

        public IEnumerable<string> Keys()
        {
                if (this.ActivityId != null)
                {
                    yield return this.ActivityId;
                }
                if (this.BackendRequestDurationMilliseconds != null)
                {
                    yield return this.BackendRequestDurationMilliseconds;
                }
                if (this.CollectionIndexTransformationProgress != null)
                {
                    yield return this.CollectionIndexTransformationProgress;
                }
                if (this.CollectionLazyIndexingProgress != null)
                {
                    yield return this.CollectionLazyIndexingProgress;
                }
                if (this.CollectionPartitionIndex != null)
                {
                    yield return this.CollectionPartitionIndex;
                }
                if (this.CollectionSecurityIdentifier != null)
                {
                    yield return this.CollectionSecurityIdentifier;
                }
                if (this.CollectionServiceIndex != null)
                {
                    yield return this.CollectionServiceIndex;
                }
                if (this.ContinuationToken != null)
                {
                    yield return this.ContinuationToken;
                }
                if (this.CurrentReplicaSetSize != null)
                {
                    yield return this.CurrentReplicaSetSize;
                }
                if (this.CurrentResourceQuotaUsage != null)
                {
                    yield return this.CurrentResourceQuotaUsage;
                }
                if (this.CurrentWriteQuorum != null)
                {
                    yield return this.CurrentWriteQuorum;
                }
                if (this.DatabaseAccountId != null)
                {
                    yield return this.DatabaseAccountId;
                }
                if (this.DisableRntbdChannel != null)
                {
                    yield return this.DisableRntbdChannel;
                }
                if (this.ETag != null)
                {
                    yield return this.ETag;
                }
                if (this.GlobalCommittedLSN != null)
                {
                    yield return this.GlobalCommittedLSN;
                }
                if (this.HasTentativeWrites != null)
                {
                    yield return this.HasTentativeWrites;
                }
                if (this.IndexingDirective != null)
                {
                    yield return this.IndexingDirective;
                }
                if (this.IndexUtilization != null)
                {
                    yield return this.IndexUtilization;
                }
                if (this.IsRUPerMinuteUsed != null)
                {
                    yield return this.IsRUPerMinuteUsed;
                }
                if (this.ItemCount != null)
                {
                    yield return this.ItemCount;
                }
                if (this.ItemLocalLSN != null)
                {
                    yield return this.ItemLocalLSN;
                }
                if (this.ItemLSN != null)
                {
                    yield return this.ItemLSN;
                }
                if (this.LastStateChangeUtc != null)
                {
                    yield return this.LastStateChangeUtc;
                }
                if (this.LocalLSN != null)
                {
                    yield return this.LocalLSN;
                }
                if (this.LogResults != null)
                {
                    yield return this.LogResults;
                }
                if (this.LSN != null)
                {
                    yield return this.LSN;
                }
                if (this.MaxResourceQuota != null)
                {
                    yield return this.MaxResourceQuota;
                }
                if (this.MinimumRUsForOffer != null)
                {
                    yield return this.MinimumRUsForOffer;
                }
                if (this.NumberOfReadRegions != null)
                {
                    yield return this.NumberOfReadRegions;
                }
                if (this.OfferReplacePending != null)
                {
                    yield return this.OfferReplacePending;
                }
                if (this.OwnerFullName != null)
                {
                    yield return this.OwnerFullName;
                }
                if (this.OwnerId != null)
                {
                    yield return this.OwnerId;
                }
                if (this.PartitionKeyRangeId != null)
                {
                    yield return this.PartitionKeyRangeId;
                }
                if (this.QueryExecutionInfo != null)
                {
                    yield return this.QueryExecutionInfo;
                }
                if (this.QueryMetrics != null)
                {
                    yield return this.QueryMetrics;
                }
                if (this.QuorumAckedLocalLSN != null)
                {
                    yield return this.QuorumAckedLocalLSN;
                }
                if (this.QuorumAckedLSN != null)
                {
                    yield return this.QuorumAckedLSN;
                }
                if (this.ReplicaStatusRevoked != null)
                {
                    yield return this.ReplicaStatusRevoked;
                }
                if (this.ReplicatorLSNToGLSNDelta != null)
                {
                    yield return this.ReplicatorLSNToGLSNDelta;
                }
                if (this.ReplicatorLSNToLLSNDelta != null)
                {
                    yield return this.ReplicatorLSNToLLSNDelta;
                }
                if (this.RequestCharge != null)
                {
                    yield return this.RequestCharge;
                }
                if (this.RequestValidationFailure != null)
                {
                    yield return this.RequestValidationFailure;
                }
                if (this.ResourceId != null)
                {
                    yield return this.ResourceId;
                }
                if (this.RestoreState != null)
                {
                    yield return this.RestoreState;
                }
                if (this.RetryAfterInMilliseconds != null)
                {
                    yield return this.RetryAfterInMilliseconds;
                }
                if (this.SchemaVersion != null)
                {
                    yield return this.SchemaVersion;
                }
                if (this.SessionToken != null)
                {
                    yield return this.SessionToken;
                }
                if (this.ShareThroughput != null)
                {
                    yield return this.ShareThroughput;
                }
                if (this.SoftMaxAllowedThroughput != null)
                {
                    yield return this.SoftMaxAllowedThroughput;
                }
                if (this.SubStatus != null)
                {
                    yield return this.SubStatus;
                }
                if (this.TimeToLiveInSeconds != null)
                {
                    yield return this.TimeToLiveInSeconds;
                }
                if (this.TransportRequestID != null)
                {
                    yield return this.TransportRequestID;
                }
                if (this.UnflushedMergLogEntryCount != null)
                {
                    yield return this.UnflushedMergLogEntryCount;
                }
                if (this.VectorClockLocalProgress != null)
                {
                    yield return this.VectorClockLocalProgress;
                }
                if (this.XDate != null)
                {
                    yield return this.XDate;
                }
                if (this.XPConfigurationSessionsCount != null)
                {
                    yield return this.XPConfigurationSessionsCount;
                }
                if (this.XPRole != null)
                {
                    yield return this.XPRole;
                }
        }

        public NameValueCollection ToNameValueCollection()
        {
            throw new NotImplementedException();
        }

        public void Remove(string key)
        {
            this.Set(key, null);
        }

        public string Get(string key)
        {
            switch (key.Length)
            {
                case 3:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.LSN, key))
                    {
                        return this.LSN;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.LSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.LSN;
                    }
                
                    break;
                case 4:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ETag, key))
                    {
                        return this.ETag;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ETag, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ETag;
                    }
                
                    break;
                case 9:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.XDate, key))
                    {
                        return this.XDate;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.XDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.XDate;
                    }
                
                    break;
                case 12:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.XPRole, key))
                    {
                        return this.XPRole;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.XPRole, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.XPRole;
                    }
                
                    break;
                case 13:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ItemLSN, key))
                    {
                        return this.ItemLSN;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ItemLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ItemLSN;
                    }
                
                    break;
                case 14:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SubStatus, key))
                    {
                        return this.SubStatus;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.SubStatus, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.SubStatus;
                    }
                
                    break;
                case 15:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ItemCount, key))
                    {
                        return this.ItemCount;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ItemCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ItemCount;
                    }
                
                    break;
                case 16:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ActivityId, key))
                    {
                        return this.ActivityId;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.LocalLSN, key))
                    {
                        return this.LocalLSN;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ActivityId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ActivityId;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.LocalLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.LocalLSN;
                    }
                
                    break;
                case 17:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.OwnerId, key))
                    {
                        return this.OwnerId;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.OwnerId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.OwnerId;
                    }
                
                    break;
                case 18:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SchemaVersion, key))
                    {
                        return this.SchemaVersion;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.RestoreState, key))
                    {
                        return this.RestoreState;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SessionToken, key))
                    {
                        return this.SessionToken;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.SchemaVersion, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.SchemaVersion;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.RestoreState, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.RestoreState;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.SessionToken, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.SessionToken;
                    }
                
                    break;
                case 19:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, key))
                    {
                        return this.RetryAfterInMilliseconds;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.MaxResourceQuota, key))
                    {
                        return this.MaxResourceQuota;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CurrentResourceQuotaUsage, key))
                    {
                        return this.CurrentResourceQuotaUsage;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RequestCharge, key))
                    {
                        return this.RequestCharge;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ResourceId, key))
                    {
                        return this.ResourceId;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.RetryAfterInMilliseconds;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.MaxResourceQuota, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.MaxResourceQuota;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.CurrentResourceQuotaUsage, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CurrentResourceQuotaUsage;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.RequestCharge, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.RequestCharge;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ResourceId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ResourceId;
                    }
                
                    break;
                case 21:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.OwnerFullName, key))
                    {
                        return this.OwnerFullName;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.QuorumAckedLSN, key))
                    {
                        return this.QuorumAckedLSN;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ShareThroughput, key))
                    {
                        return this.ShareThroughput;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ItemLocalLSN, key))
                    {
                        return this.ItemLocalLSN;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.OwnerFullName, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.OwnerFullName;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.QuorumAckedLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.QuorumAckedLSN;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ShareThroughput, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ShareThroughput;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ItemLocalLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ItemLocalLSN;
                    }
                
                    break;
                case 22:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ContinuationToken, key))
                    {
                        return this.ContinuationToken;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ContinuationToken, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ContinuationToken;
                    }
                
                    break;
                case 23:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IndexingDirective, key))
                    {
                        return this.IndexingDirective;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IndexingDirective, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IndexingDirective;
                    }
                
                    break;
                case 24:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionServiceIndex, key))
                    {
                        return this.CollectionServiceIndex;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.DatabaseAccountId, key))
                    {
                        return this.DatabaseAccountId;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.BackendRequestDurationMilliseconds, key))
                    {
                        return this.BackendRequestDurationMilliseconds;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionServiceIndex, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionServiceIndex;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.DatabaseAccountId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.DatabaseAccountId;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.BackendRequestDurationMilliseconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.BackendRequestDurationMilliseconds;
                    }
                
                    break;
                case 25:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CurrentWriteQuorum, key))
                    {
                        return this.CurrentWriteQuorum;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.GlobalCommittedLSN, key))
                    {
                        return this.GlobalCommittedLSN;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TransportRequestID, key))
                    {
                        return this.TransportRequestID;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CurrentWriteQuorum, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CurrentWriteQuorum;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.GlobalCommittedLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.GlobalCommittedLSN;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.TransportRequestID, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TransportRequestID;
                    }
                
                    break;
                case 26:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.LastStateChangeUtc, key))
                    {
                        return this.LastStateChangeUtc;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionPartitionIndex, key))
                    {
                        return this.CollectionPartitionIndex;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.OfferReplacePending, key))
                    {
                        return this.OfferReplacePending;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.DisableRntbdChannel, key))
                    {
                        return this.DisableRntbdChannel;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.MinimumRUsForOffer, key))
                    {
                        return this.MinimumRUsForOffer;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.LastStateChangeUtc, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.LastStateChangeUtc;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionPartitionIndex, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionPartitionIndex;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.OfferReplacePending, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.OfferReplacePending;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.DisableRntbdChannel, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.DisableRntbdChannel;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.MinimumRUsForOffer, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.MinimumRUsForOffer;
                    }
                
                    break;
                case 27:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.NumberOfReadRegions, key))
                    {
                        return this.NumberOfReadRegions;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.NumberOfReadRegions, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.NumberOfReadRegions;
                    }
                
                    break;
                case 28:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key))
                    {
                        return this.TimeToLiveInSeconds;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TimeToLiveInSeconds;
                    }
                
                    break;
                case 29:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CurrentReplicaSetSize, key))
                    {
                        return this.CurrentReplicaSetSize;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.QueryMetrics, key))
                    {
                        return this.QueryMetrics;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IndexUtilization, key))
                    {
                        return this.IndexUtilization;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.QuorumAckedLocalLSN, key))
                    {
                        return this.QuorumAckedLocalLSN;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CurrentReplicaSetSize, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CurrentReplicaSetSize;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.QueryMetrics, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.QueryMetrics;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IndexUtilization, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IndexUtilization;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.QuorumAckedLocalLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.QuorumAckedLocalLSN;
                    }
                
                    break;
                case 31:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RequestValidationFailure, key))
                    {
                        return this.RequestValidationFailure;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.RequestValidationFailure, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.RequestValidationFailure;
                    }
                
                    break;
                case 32:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.QueryExecutionInfo, key))
                    {
                        return this.QueryExecutionInfo;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.QueryExecutionInfo, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.QueryExecutionInfo;
                    }
                
                    break;
                case 33:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ReplicatorLSNToGLSNDelta, key))
                    {
                        return this.ReplicatorLSNToGLSNDelta;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ReplicatorLSNToLLSNDelta, key))
                    {
                        return this.ReplicatorLSNToLLSNDelta;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ReplicatorLSNToGLSNDelta, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ReplicatorLSNToGLSNDelta;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ReplicatorLSNToLLSNDelta, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ReplicatorLSNToLLSNDelta;
                    }
                
                    break;
                case 34:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.LogResults, key))
                    {
                        return this.LogResults;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.HasTentativeWrites, key))
                    {
                        return this.HasTentativeWrites;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.LogResults, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.LogResults;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.HasTentativeWrites, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.HasTentativeWrites;
                    }
                
                    break;
                case 35:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PartitionKeyRangeId, key))
                    {
                        return this.PartitionKeyRangeId;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionSecurityIdentifier, key))
                    {
                        return this.CollectionSecurityIdentifier;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PartitionKeyRangeId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PartitionKeyRangeId;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionSecurityIdentifier, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionSecurityIdentifier;
                    }
                
                    break;
                case 37:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsRUPerMinuteUsed, key))
                    {
                        return this.IsRUPerMinuteUsed;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ReplicaStatusRevoked, key))
                    {
                        return this.ReplicaStatusRevoked;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsRUPerMinuteUsed, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsRUPerMinuteUsed;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ReplicaStatusRevoked, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ReplicaStatusRevoked;
                    }
                
                    break;
                case 38:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.VectorClockLocalProgress, key))
                    {
                        return this.VectorClockLocalProgress;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.VectorClockLocalProgress, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.VectorClockLocalProgress;
                    }
                
                    break;
                case 40:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SoftMaxAllowedThroughput, key))
                    {
                        return this.SoftMaxAllowedThroughput;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.SoftMaxAllowedThroughput, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.SoftMaxAllowedThroughput;
                    }
                
                    break;
                case 42:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.XPConfigurationSessionsCount, key))
                    {
                        return this.XPConfigurationSessionsCount;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.XPConfigurationSessionsCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.XPConfigurationSessionsCount;
                    }
                
                    break;
                case 49:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CollectionLazyIndexingProgress, key))
                    {
                        return this.CollectionLazyIndexingProgress;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.CollectionLazyIndexingProgress, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionLazyIndexingProgress;
                    }
                
                    break;
                case 52:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.UnflushedMergLogEntryCount, key))
                    {
                        return this.UnflushedMergLogEntryCount;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.UnflushedMergLogEntryCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.UnflushedMergLogEntryCount;
                    }
                
                    break;
                case 56:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CollectionIndexTransformationProgress, key))
                    {
                        return this.CollectionIndexTransformationProgress;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.CollectionIndexTransformationProgress, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionIndexTransformationProgress;
                    }
                
                    break;
                default:
                    break;
            }

            if(this.lazyNotCommonHeaders.IsValueCreated)
            {
                return this.lazyNotCommonHeaders.Value[key];
            }
            
            return null;
        }

        public void Set(string key, string value)
        {
            switch (key.Length)
            {
                case 3:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.LSN, key))
                    {
                        this.LSN = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.LSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.LSN = value;
                        return;
                    }
                
                    break;
                case 4:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ETag, key))
                    {
                        this.ETag = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ETag, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ETag = value;
                        return;
                    }
                
                    break;
                case 9:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.XDate, key))
                    {
                        this.XDate = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.XDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.XDate = value;
                        return;
                    }
                
                    break;
                case 12:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.XPRole, key))
                    {
                        this.XPRole = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.XPRole, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.XPRole = value;
                        return;
                    }
                
                    break;
                case 13:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ItemLSN, key))
                    {
                        this.ItemLSN = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ItemLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ItemLSN = value;
                        return;
                    }
                
                    break;
                case 14:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SubStatus, key))
                    {
                        this.SubStatus = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.SubStatus, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.SubStatus = value;
                        return;
                    }
                
                    break;
                case 15:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ItemCount, key))
                    {
                        this.ItemCount = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ItemCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ItemCount = value;
                        return;
                    }
                
                    break;
                case 16:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ActivityId, key))
                    {
                        this.ActivityId = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.LocalLSN, key))
                    {
                        this.LocalLSN = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ActivityId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ActivityId = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.LocalLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.LocalLSN = value;
                        return;
                    }
                
                    break;
                case 17:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.OwnerId, key))
                    {
                        this.OwnerId = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.OwnerId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.OwnerId = value;
                        return;
                    }
                
                    break;
                case 18:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SchemaVersion, key))
                    {
                        this.SchemaVersion = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.RestoreState, key))
                    {
                        this.RestoreState = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SessionToken, key))
                    {
                        this.SessionToken = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.SchemaVersion, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.SchemaVersion = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.RestoreState, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.RestoreState = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.SessionToken, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.SessionToken = value;
                        return;
                    }
                
                    break;
                case 19:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, key))
                    {
                        this.RetryAfterInMilliseconds = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.MaxResourceQuota, key))
                    {
                        this.MaxResourceQuota = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CurrentResourceQuotaUsage, key))
                    {
                        this.CurrentResourceQuotaUsage = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RequestCharge, key))
                    {
                        this.RequestCharge = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ResourceId, key))
                    {
                        this.ResourceId = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.RetryAfterInMilliseconds = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.MaxResourceQuota, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.MaxResourceQuota = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.CurrentResourceQuotaUsage, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CurrentResourceQuotaUsage = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.RequestCharge, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.RequestCharge = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ResourceId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ResourceId = value;
                        return;
                    }
                
                    break;
                case 21:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.OwnerFullName, key))
                    {
                        this.OwnerFullName = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.QuorumAckedLSN, key))
                    {
                        this.QuorumAckedLSN = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ShareThroughput, key))
                    {
                        this.ShareThroughput = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ItemLocalLSN, key))
                    {
                        this.ItemLocalLSN = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.OwnerFullName, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.OwnerFullName = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.QuorumAckedLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.QuorumAckedLSN = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ShareThroughput, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ShareThroughput = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ItemLocalLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ItemLocalLSN = value;
                        return;
                    }
                
                    break;
                case 22:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ContinuationToken, key))
                    {
                        this.ContinuationToken = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ContinuationToken, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ContinuationToken = value;
                        return;
                    }
                
                    break;
                case 23:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IndexingDirective, key))
                    {
                        this.IndexingDirective = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IndexingDirective, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IndexingDirective = value;
                        return;
                    }
                
                    break;
                case 24:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionServiceIndex, key))
                    {
                        this.CollectionServiceIndex = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.DatabaseAccountId, key))
                    {
                        this.DatabaseAccountId = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.BackendRequestDurationMilliseconds, key))
                    {
                        this.BackendRequestDurationMilliseconds = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionServiceIndex, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectionServiceIndex = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.DatabaseAccountId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.DatabaseAccountId = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.BackendRequestDurationMilliseconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.BackendRequestDurationMilliseconds = value;
                        return;
                    }
                
                    break;
                case 25:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CurrentWriteQuorum, key))
                    {
                        this.CurrentWriteQuorum = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.GlobalCommittedLSN, key))
                    {
                        this.GlobalCommittedLSN = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TransportRequestID, key))
                    {
                        this.TransportRequestID = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CurrentWriteQuorum, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CurrentWriteQuorum = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.GlobalCommittedLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.GlobalCommittedLSN = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.TransportRequestID, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.TransportRequestID = value;
                        return;
                    }
                
                    break;
                case 26:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.LastStateChangeUtc, key))
                    {
                        this.LastStateChangeUtc = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionPartitionIndex, key))
                    {
                        this.CollectionPartitionIndex = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.OfferReplacePending, key))
                    {
                        this.OfferReplacePending = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.DisableRntbdChannel, key))
                    {
                        this.DisableRntbdChannel = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.MinimumRUsForOffer, key))
                    {
                        this.MinimumRUsForOffer = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.LastStateChangeUtc, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.LastStateChangeUtc = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionPartitionIndex, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectionPartitionIndex = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.OfferReplacePending, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.OfferReplacePending = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.DisableRntbdChannel, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.DisableRntbdChannel = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.MinimumRUsForOffer, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.MinimumRUsForOffer = value;
                        return;
                    }
                
                    break;
                case 27:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.NumberOfReadRegions, key))
                    {
                        this.NumberOfReadRegions = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.NumberOfReadRegions, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.NumberOfReadRegions = value;
                        return;
                    }
                
                    break;
                case 28:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key))
                    {
                        this.TimeToLiveInSeconds = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.TimeToLiveInSeconds = value;
                        return;
                    }
                
                    break;
                case 29:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CurrentReplicaSetSize, key))
                    {
                        this.CurrentReplicaSetSize = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.QueryMetrics, key))
                    {
                        this.QueryMetrics = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IndexUtilization, key))
                    {
                        this.IndexUtilization = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.QuorumAckedLocalLSN, key))
                    {
                        this.QuorumAckedLocalLSN = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CurrentReplicaSetSize, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CurrentReplicaSetSize = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.QueryMetrics, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.QueryMetrics = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IndexUtilization, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IndexUtilization = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.QuorumAckedLocalLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.QuorumAckedLocalLSN = value;
                        return;
                    }
                
                    break;
                case 31:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RequestValidationFailure, key))
                    {
                        this.RequestValidationFailure = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.RequestValidationFailure, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.RequestValidationFailure = value;
                        return;
                    }
                
                    break;
                case 32:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.QueryExecutionInfo, key))
                    {
                        this.QueryExecutionInfo = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.QueryExecutionInfo, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.QueryExecutionInfo = value;
                        return;
                    }
                
                    break;
                case 33:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ReplicatorLSNToGLSNDelta, key))
                    {
                        this.ReplicatorLSNToGLSNDelta = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ReplicatorLSNToLLSNDelta, key))
                    {
                        this.ReplicatorLSNToLLSNDelta = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ReplicatorLSNToGLSNDelta, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ReplicatorLSNToGLSNDelta = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ReplicatorLSNToLLSNDelta, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ReplicatorLSNToLLSNDelta = value;
                        return;
                    }
                
                    break;
                case 34:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.LogResults, key))
                    {
                        this.LogResults = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.HasTentativeWrites, key))
                    {
                        this.HasTentativeWrites = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.LogResults, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.LogResults = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.HasTentativeWrites, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.HasTentativeWrites = value;
                        return;
                    }
                
                    break;
                case 35:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PartitionKeyRangeId, key))
                    {
                        this.PartitionKeyRangeId = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionSecurityIdentifier, key))
                    {
                        this.CollectionSecurityIdentifier = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PartitionKeyRangeId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PartitionKeyRangeId = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionSecurityIdentifier, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectionSecurityIdentifier = value;
                        return;
                    }
                
                    break;
                case 37:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsRUPerMinuteUsed, key))
                    {
                        this.IsRUPerMinuteUsed = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ReplicaStatusRevoked, key))
                    {
                        this.ReplicaStatusRevoked = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsRUPerMinuteUsed, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IsRUPerMinuteUsed = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ReplicaStatusRevoked, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ReplicaStatusRevoked = value;
                        return;
                    }
                
                    break;
                case 38:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.VectorClockLocalProgress, key))
                    {
                        this.VectorClockLocalProgress = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.VectorClockLocalProgress, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.VectorClockLocalProgress = value;
                        return;
                    }
                
                    break;
                case 40:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SoftMaxAllowedThroughput, key))
                    {
                        this.SoftMaxAllowedThroughput = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.SoftMaxAllowedThroughput, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.SoftMaxAllowedThroughput = value;
                        return;
                    }
                
                    break;
                case 42:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.XPConfigurationSessionsCount, key))
                    {
                        this.XPConfigurationSessionsCount = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.XPConfigurationSessionsCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.XPConfigurationSessionsCount = value;
                        return;
                    }
                
                    break;
                case 49:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CollectionLazyIndexingProgress, key))
                    {
                        this.CollectionLazyIndexingProgress = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.CollectionLazyIndexingProgress, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectionLazyIndexingProgress = value;
                        return;
                    }
                
                    break;
                case 52:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.UnflushedMergLogEntryCount, key))
                    {
                        this.UnflushedMergLogEntryCount = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.UnflushedMergLogEntryCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.UnflushedMergLogEntryCount = value;
                        return;
                    }
                
                    break;
                case 56:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CollectionIndexTransformationProgress, key))
                    {
                        this.CollectionIndexTransformationProgress = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.CollectionIndexTransformationProgress, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectionIndexTransformationProgress = value;
                        return;
                    }
                
                    break;
                default:
                    break;
            }

            this.lazyNotCommonHeaders.Value[key] = value;
        }
    }
}
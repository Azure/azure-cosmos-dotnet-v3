//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;
    using System.Threading;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal sealed class SessionContainer : ISessionContainer
    {
        // TODO, devise a mechanism to handle cache coherency during resource id collision
        private readonly string hostName;

        private readonly ReaderWriterLockSlim rwlock = new ReaderWriterLockSlim();
        private readonly ConcurrentDictionary<string, ulong> collectionNameByResourceId = new ConcurrentDictionary<string, ulong>();
        private readonly ConcurrentDictionary<ulong, string> collectionResourceIdByName = new ConcurrentDictionary<ulong, string>();
        // Map of Collection Rid to map of partitionkeyrangeid to SessionToken.
        private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, ISessionToken>> sessionTokensRIDBased = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, ISessionToken>>();

        public SessionContainer(string hostName)
        {
            this.hostName = hostName;
        }

        ~SessionContainer()
        {
            if (this.rwlock != null)
            {
                this.rwlock.Dispose();
            }
        }

        public string HostName
        {
            get { return this.hostName; }
        }

        public string GetSessionToken(string collectionLink)
        {
            bool isNameBased;
            bool isFeed;
            string resourceTypeString;
            string resourceIdOrFullName;
            bool arePathSegmentsParsed = PathsHelper.TryParsePathSegments(collectionLink, out isFeed, out resourceTypeString, out resourceIdOrFullName, out isNameBased);

            ConcurrentDictionary<string, ISessionToken> partitionKeyRangeIdToTokenMap = null;

            if (arePathSegmentsParsed)
            {
                ulong? maybeRID = null;

                if (isNameBased)
                {
                    string collectionName = PathsHelper.GetCollectionPath(resourceIdOrFullName);

                    ulong rid;
                    if (this.collectionNameByResourceId.TryGetValue(collectionName, out rid))
                    {
                        maybeRID = rid;
                    }
                }
                else
                {
                    ResourceId resourceId = ResourceId.Parse(resourceIdOrFullName);
                    if (resourceId.DocumentCollection != 0)
                    {
                        maybeRID = resourceId.UniqueDocumentCollectionId;
                    }
                }

                if (maybeRID.HasValue)
                {
                    this.sessionTokensRIDBased.TryGetValue(maybeRID.Value, out partitionKeyRangeIdToTokenMap);
                }
            }

            if (partitionKeyRangeIdToTokenMap == null)
            {
                return string.Empty;
            }

            return SessionContainer.GetSessionTokenString(partitionKeyRangeIdToTokenMap);
        }

        public string ResolveGlobalSessionToken(DocumentServiceRequest request)
        {
            ConcurrentDictionary<string, ISessionToken> partitionKeyRangeIdToTokenMap = this.GetPartitionKeyRangeIdToTokenMap(request);
            if (partitionKeyRangeIdToTokenMap != null)
            {
                return SessionContainer.GetSessionTokenString(partitionKeyRangeIdToTokenMap);
            }

            return string.Empty;
        }

        public ISessionToken ResolvePartitionLocalSessionToken(DocumentServiceRequest request, string partitionKeyRangeId)
        {
            return SessionTokenHelper.ResolvePartitionLocalSessionToken(request, partitionKeyRangeId, this.GetPartitionKeyRangeIdToTokenMap(request));
        }

        public void ClearTokenByCollectionFullname(string collectionFullname)
        {
            if (!string.IsNullOrEmpty(collectionFullname))
            {
                string collectionName = PathsHelper.GetCollectionPath(collectionFullname);

                this.rwlock.EnterWriteLock();
                try
                {
                    if (collectionNameByResourceId.ContainsKey(collectionName))
                    {
                        string ignoreString;
                        ulong ignoreUlong;

                        ulong rid = this.collectionNameByResourceId[collectionName];
                        ConcurrentDictionary<string, ISessionToken> ignored;
                        this.sessionTokensRIDBased.TryRemove(rid, out ignored);
                        this.collectionResourceIdByName.TryRemove(rid, out ignoreString);
                        this.collectionNameByResourceId.TryRemove(collectionName, out ignoreUlong);
                    }
                }
                finally
                {
                    this.rwlock.ExitWriteLock();
                }
            }
        }

        public void ClearTokenByResourceId(string resourceId)
        {
            if (!string.IsNullOrEmpty(resourceId))
            {
                ResourceId resource = ResourceId.Parse(resourceId);
                if (resource.DocumentCollection != 0)
                {
                    ulong rid = resource.UniqueDocumentCollectionId;

                    this.rwlock.EnterWriteLock();
                    try
                    {
                        if (this.collectionResourceIdByName.ContainsKey(rid))
                        {
                            string ignoreString;
                            ulong ignoreUlong;

                            string collectionName = this.collectionResourceIdByName[rid];
                            ConcurrentDictionary<string, ISessionToken> ignored;
                            this.sessionTokensRIDBased.TryRemove(rid, out ignored);
                            this.collectionResourceIdByName.TryRemove(rid, out ignoreString);
                            this.collectionNameByResourceId.TryRemove(collectionName, out ignoreUlong);
                        }
                    }
                    finally
                    {
                        this.rwlock.ExitWriteLock();
                    }
                }
            }
        }

        public void SetSessionToken(string collectionRid, string collectionFullname, INameValueCollection responseHeaders)
        {
            ResourceId resourceId = ResourceId.Parse(collectionRid);
            string collectionName = PathsHelper.GetCollectionPath(collectionFullname);
            string token = responseHeaders[HttpConstants.HttpHeaders.SessionToken];
            if (!string.IsNullOrEmpty(token))
            {
                this.SetSessionToken(resourceId, collectionName, token);
            }
        }

        public void SetSessionToken(DocumentServiceRequest request, INameValueCollection responseHeaders)
        {
            string token = responseHeaders[HttpConstants.HttpHeaders.SessionToken];

            if (!string.IsNullOrEmpty(token))
            {
                ResourceId resourceId;
                string collectionName;

                if (SessionContainer.ShouldUpdateSessionToken(request, responseHeaders, out resourceId, out collectionName))
                {
                    this.SetSessionToken(resourceId, collectionName, token);
                }
            }
        }

        // used in unit tests to check if two SessionContainer are equal
        // a.ExportState().Equals(b.ExportState())
        public SessionContainerState ExportState()
        {
            rwlock.EnterReadLock();
            try
            {
                return new SessionContainerState(collectionNameByResourceId, collectionResourceIdByName, sessionTokensRIDBased);
            }
            finally
            {
                rwlock.ExitReadLock();
            }
        }

        private ConcurrentDictionary<string, ISessionToken> GetPartitionKeyRangeIdToTokenMap(DocumentServiceRequest request)
        {
            ulong? maybeRID = null;

            if (request.IsNameBased)
            {
                string collectionName = PathsHelper.GetCollectionPath(request.ResourceAddress);

                ulong rid;
                if (this.collectionNameByResourceId.TryGetValue(collectionName, out rid))
                {
                    maybeRID = rid;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(request.ResourceId))
                {
                    ResourceId resourceId = ResourceId.Parse(request.ResourceId);
                    if (resourceId.DocumentCollection != 0)
                    {
                        maybeRID = resourceId.UniqueDocumentCollectionId;
                    }
                }
            }

            ConcurrentDictionary<string, ISessionToken> partitionKeyRangeIdToTokenMap = null;

            if (maybeRID.HasValue)
            {
                this.sessionTokensRIDBased.TryGetValue(maybeRID.Value, out partitionKeyRangeIdToTokenMap);
            }

            return partitionKeyRangeIdToTokenMap;
        }

        private void SetSessionToken(ResourceId resourceId, string collectionName, string encodedToken)
        {
            string partitionKeyRangeId;
            ISessionToken token;
            if (VersionUtility.IsLaterThan(HttpConstants.Versions.CurrentVersion, HttpConstants.Versions.v2015_12_16))
            {
                string[] tokenParts = encodedToken.Split(':');
                partitionKeyRangeId = tokenParts[0];
                token = SessionTokenHelper.Parse(tokenParts[1], HttpConstants.Versions.CurrentVersion);
            }
            else
            {
                //todo: elasticcollections remove after first upgrade.
                partitionKeyRangeId = "0";
                token = SessionTokenHelper.Parse(encodedToken, HttpConstants.Versions.CurrentVersion);
            }

            DefaultTrace.TraceVerbose("Update Session token {0} {1} {2}", resourceId.UniqueDocumentCollectionId, collectionName, token);

            bool isKnownCollection = false;

            this.rwlock.EnterReadLock();
            try
            {
                ulong resolvedCollectionResourceId;
                string resolvedCollectionName;

                isKnownCollection = this.collectionNameByResourceId.TryGetValue(collectionName, out resolvedCollectionResourceId) &&
                                    this.collectionResourceIdByName.TryGetValue(resourceId.UniqueDocumentCollectionId, out resolvedCollectionName) &&
                                    resolvedCollectionResourceId == resourceId.UniqueDocumentCollectionId &&
                                    resolvedCollectionName == collectionName;

                if (isKnownCollection)
                {
                    this.AddSessionToken(resourceId.UniqueDocumentCollectionId, partitionKeyRangeId, token);
                }
            }
            finally
            {
                this.rwlock.ExitReadLock();
            }

            if (!isKnownCollection)
            {
                this.rwlock.EnterWriteLock();
                try
                {
                    ulong resolvedCollectionResourceId;

                    if (this.collectionNameByResourceId.TryGetValue(collectionName, out resolvedCollectionResourceId))
                    {
                        string ignoreString;

                        ConcurrentDictionary<string, ISessionToken> ignored;
                        this.sessionTokensRIDBased.TryRemove(resolvedCollectionResourceId, out ignored);
                        this.collectionResourceIdByName.TryRemove(resolvedCollectionResourceId, out ignoreString);
                    }

                    this.collectionNameByResourceId[collectionName] = resourceId.UniqueDocumentCollectionId;
                    this.collectionResourceIdByName[resourceId.UniqueDocumentCollectionId] = collectionName;

                    this.AddSessionToken(resourceId.UniqueDocumentCollectionId, partitionKeyRangeId, token);
                }
                finally
                {
                    this.rwlock.ExitWriteLock();
                }
            }
        }

        private void AddSessionToken(ulong rid, string partitionKeyRangeId, ISessionToken token)
        {
            this.sessionTokensRIDBased.AddOrUpdate(
                rid,
                (ridKey) =>
                {
                    ConcurrentDictionary<string, ISessionToken> tokens = new ConcurrentDictionary<string, ISessionToken>();
                    tokens[partitionKeyRangeId] = token;
                    return tokens;
                },
                (ridKey, tokens) =>
                {
                    tokens.AddOrUpdate(
                        partitionKeyRangeId,
                        token,
                        (existingPartitionKeyRangeId, existingToken) => existingToken.Merge(token));
                    return tokens;
                });
        }

        private static string GetSessionTokenString(ConcurrentDictionary<string, ISessionToken> partitionKeyRangeIdToTokenMap)
        {
            if (VersionUtility.IsLaterThan(HttpConstants.Versions.CurrentVersion, HttpConstants.Versions.v2015_12_16))
            {
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, ISessionToken> pair in partitionKeyRangeIdToTokenMap)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(",");
                    }

                    sb.Append(pair.Key);
                    sb.Append(":");
                    sb.Append(pair.Value.ConvertToString());
                }

                return sb.ToString();
            }
            else
            {
                //todo:elasticcollections remove after first upgrade.
                ISessionToken lsn;
                if (partitionKeyRangeIdToTokenMap.TryGetValue("0", out lsn))
                {
                    return string.Format(CultureInfo.InvariantCulture, "{0}", lsn);
                }

                return string.Empty;
            }
        }

        private static bool AreDictionariesEqual(Dictionary<string, ISessionToken> first, Dictionary<string, ISessionToken> second)
        {
            if (first.Count != second.Count) return false;

            foreach (KeyValuePair<string, ISessionToken> pair in first)
            {
                ISessionToken tokenValue;
                if (second.TryGetValue(pair.Key, out tokenValue))
                {
                    if (!tokenValue.Equals(pair.Value))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool ShouldUpdateSessionToken(
            DocumentServiceRequest request,
            INameValueCollection responseHeaders,
            out ResourceId resourceId,
            out string collectionName)
        {
            resourceId = null;
            string ownerFullName = responseHeaders[HttpConstants.HttpHeaders.OwnerFullName];
            if (string.IsNullOrEmpty(ownerFullName)) ownerFullName = request.ResourceAddress;

            collectionName = PathsHelper.GetCollectionPath(ownerFullName);
            string resourceIdString;

            if (request.IsNameBased)
            {
                resourceIdString = responseHeaders[HttpConstants.HttpHeaders.OwnerId];
                if (string.IsNullOrEmpty(resourceIdString)) resourceIdString = request.ResourceId;
            }
            else
            {
                resourceIdString = request.ResourceId;
            }

            if (!string.IsNullOrEmpty(resourceIdString))
            {
                resourceId = ResourceId.Parse(resourceIdString);

                if (resourceId.DocumentCollection != 0 &&
                    collectionName != null &&
                    !ReplicatedResourceClient.IsReadingFromMaster(request.ResourceType, request.OperationType))
                {
                    return true;
                }
            }

            return false;
        }

        public class SessionContainerState
        {
            private readonly Dictionary<string, ulong> collectionNameByResourceId;
            private readonly Dictionary<ulong, string> collectionResourceIdByName;
            private readonly Dictionary<ulong, Dictionary<string, ISessionToken>> sessionTokensRIDBased;

            public SessionContainerState(ConcurrentDictionary<string, ulong> collectionNameByResourceId, ConcurrentDictionary<ulong, string> collectionResourceIdByName, ConcurrentDictionary<ulong, ConcurrentDictionary<string, ISessionToken>> sessionTokensRIDBased)
            {
                this.collectionNameByResourceId = new Dictionary<string, ulong>(collectionNameByResourceId);
                this.collectionResourceIdByName = new Dictionary<ulong, string>(collectionResourceIdByName);
                this.sessionTokensRIDBased = new Dictionary<ulong, Dictionary<string, ISessionToken>>();

                foreach (KeyValuePair<ulong, ConcurrentDictionary<string, ISessionToken>> pair in sessionTokensRIDBased)
                {
                    this.sessionTokensRIDBased.Add(pair.Key, new Dictionary<string, ISessionToken>(pair.Value));
                }
            }

            public override int GetHashCode()
            {
                return 1;
            }

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }

                SessionContainerState sibling = (SessionContainerState)obj;

                if (!AreDictionariesEqual(collectionNameByResourceId, sibling.collectionNameByResourceId, (x, y) => x == y)) return false;
                if (!AreDictionariesEqual(collectionResourceIdByName, sibling.collectionResourceIdByName, (x, y) => x == y)) return false;
                if (!AreDictionariesEqual(sessionTokensRIDBased, sibling.sessionTokensRIDBased, (x,y) => AreDictionariesEqual(x,y, (a,b) => a.Equals(b)))) return false;

                return true;
            }

            private static bool AreDictionariesEqual<T, U>(Dictionary<T, U> left, Dictionary<T, U> right, Func<U, U, bool> areEqual)
            {
                if (left.Count != right.Count) return false;

                foreach (T key in left.Keys)
                {
                    if (!right.ContainsKey(key)) return false;
                    if (!areEqual(left[key], right[key])) return false;
                }

                return true;
            }
        }
    }
}
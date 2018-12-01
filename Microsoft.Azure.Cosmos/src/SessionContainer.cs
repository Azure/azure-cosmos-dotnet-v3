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
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;

    internal sealed class SessionContainer : ISessionContainer
    {
        // Map of Collection Rid to map of partitionkeyrangeid to SessionToken.
        // TODO, devise a mechanism to handle cache coherency during resource id collision
        private readonly ConcurrentDictionary<UInt64, ConcurrentDictionary<string, ISessionToken>> sessionTokens;

        // Map of Collection Name to map of partitionkeyrangeid to SessionToken.
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ISessionToken>> sessionTokensNameBased;
        private readonly string hostName;

        public SessionContainer(string hostName)
        {
            this.hostName = hostName;
            this.sessionTokens = new ConcurrentDictionary<UInt64, ConcurrentDictionary<string, ISessionToken>>();
            this.sessionTokensNameBased = new ConcurrentDictionary<string, ConcurrentDictionary<string, ISessionToken>>();
        }

        public SessionContainer(
            string hostName,
            ConcurrentDictionary<UInt64, ConcurrentDictionary<string, ISessionToken>> sessionTokens,
            ConcurrentDictionary<string, ConcurrentDictionary<string, ISessionToken>> sessionTokensNameBased)
        {
            this.hostName = hostName;
            this.sessionTokens = sessionTokens;
            this.sessionTokensNameBased = sessionTokensNameBased;
        }

        public string HostName
        {
            get
            {
                return this.hostName;
            }
        }

        public string GetSessionToken(string collectionLink)
        {
            bool isNameBased;
            bool isFeed;
            string resourceTypeString;
            string resourceIdOrFullName;
            ConcurrentDictionary<string, ISessionToken> partitionKeyRangeIdToTokenMap = null;
            if (PathsHelper.TryParsePathSegments(collectionLink, out isFeed, out resourceTypeString, out resourceIdOrFullName, out isNameBased))
            {
                if (isNameBased)
                {
                    string collectionName = PathsHelper.GetCollectionPath(resourceIdOrFullName);
                    this.sessionTokensNameBased.TryGetValue(collectionName, out partitionKeyRangeIdToTokenMap);
                }
                else
                {
                    ResourceId resourceId = ResourceId.Parse(resourceIdOrFullName);
                    if (resourceId.DocumentCollection != 0)
                    {
                        this.sessionTokens.TryGetValue(resourceId.UniqueDocumentCollectionId, out partitionKeyRangeIdToTokenMap);
                    }
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

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            if (Object.ReferenceEquals(obj, this)) return true;

            SessionContainer objectToCompare = obj as SessionContainer;
            
            // compare counts 
            if(objectToCompare.sessionTokens.Count != this.sessionTokens.Count
                || objectToCompare.sessionTokensNameBased.Count != this.sessionTokensNameBased.Count)
            {
                return false;
            }

            // get keys, and compare entries
            foreach (KeyValuePair<UInt64, ConcurrentDictionary<string, ISessionToken>> pair in objectToCompare.sessionTokens)
            {
                ConcurrentDictionary<string, ISessionToken> Tokens;
                if(this.sessionTokens.TryGetValue(pair.Key, out Tokens))
                {
                    if (!AreDictionariesEqual(pair.Value, Tokens)) return false;
                }
            }

            // get keys, and compare entries
            foreach (KeyValuePair<string, ConcurrentDictionary<string, ISessionToken>> pair in objectToCompare.sessionTokensNameBased)
            {
                ConcurrentDictionary<string, ISessionToken> Tokens;
                if (this.sessionTokensNameBased.TryGetValue(pair.Key, out Tokens))
                {
                    if(!AreDictionariesEqual(pair.Value, Tokens)) return false;
                }
            }

            return true;
        }

        public bool AreDictionariesEqual(ConcurrentDictionary<string, ISessionToken> first, ConcurrentDictionary<string, ISessionToken> second)
        {
            if (first.Count != second.Count) return false;

            foreach(KeyValuePair<string, ISessionToken> pair in first)
            {
                ISessionToken tokenValue;
                if(second.TryGetValue(pair.Key, out tokenValue))
                {
                    if (!tokenValue.Equals(pair.Value))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
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

        private ConcurrentDictionary<string, ISessionToken> GetPartitionKeyRangeIdToTokenMap(DocumentServiceRequest request)
        {
            ConcurrentDictionary<string, ISessionToken> partitionKeyRangeIdToTokenMap = null;
            if (!request.IsNameBased)
            {
                if (!string.IsNullOrEmpty(request.ResourceId))
                {
                    ResourceId resourceId = ResourceId.Parse(request.ResourceId);
                    if (resourceId.DocumentCollection != 0)
                    {
                        this.sessionTokens.TryGetValue(resourceId.UniqueDocumentCollectionId, out partitionKeyRangeIdToTokenMap);
                    }
                }
            }
            else
            {
                string collectionName = PathsHelper.GetCollectionPath(request.ResourceAddress);
                this.sessionTokensNameBased.TryGetValue(collectionName, out partitionKeyRangeIdToTokenMap);
            }

            return partitionKeyRangeIdToTokenMap;
        }

        public void ClearToken(string collectionRid, string collectionFullname, INameValueCollection responseHeader)
        {
            if (!string.IsNullOrEmpty(collectionFullname))
            {
                string collectionName = PathsHelper.GetCollectionPath(collectionFullname);
                ConcurrentDictionary<string, ISessionToken> ignored;
                this.sessionTokensNameBased.TryRemove(collectionName, out ignored);
            }

            if (!string.IsNullOrEmpty(collectionRid))
            {
                ResourceId resourceId = ResourceId.Parse(collectionRid);
                ConcurrentDictionary<string, ISessionToken> ignored;
                this.sessionTokens.TryRemove(resourceId.UniqueDocumentCollectionId, out ignored);
            }
        }

        public void ClearToken(DocumentServiceRequest request, INameValueCollection responseHeaders)
        {
            string ownerFullName = responseHeaders[HttpConstants.HttpHeaders.OwnerFullName];
            string collectionName = PathsHelper.GetCollectionPath(ownerFullName);
            string resourceIdString;

            if (!request.IsNameBased)
            {
                resourceIdString = request.ResourceId;
            }
            else
            {
                resourceIdString = responseHeaders[HttpConstants.HttpHeaders.OwnerId];
            }

            if (!string.IsNullOrEmpty(resourceIdString))
            {
                ResourceId resourceId = ResourceId.Parse(resourceIdString);
                if (resourceId.DocumentCollection != 0 && collectionName != null)
                {
                    ConcurrentDictionary<string, ISessionToken> ignored;
                    this.sessionTokens.TryRemove(resourceId.UniqueDocumentCollectionId, out ignored);
                    this.sessionTokensNameBased.TryRemove(collectionName, out ignored);
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

                if (ShouldUpdateSessionToken(request, responseHeaders, out resourceId, out collectionName))
                {
                    this.SetSessionToken(resourceId, collectionName, token);
                }
            }
        }

        private void SetSessionToken(ResourceId resourceId, string collectionName, string token)
        {
            string partitionKeyRangeId;
            ISessionToken parsedSessionToken;
            if (VersionUtility.IsLaterThan(HttpConstants.Versions.CurrentVersion, HttpConstants.Versions.v2015_12_16))
            {
                string[] tokenParts = token.Split(':');
                partitionKeyRangeId = tokenParts[0];
                parsedSessionToken = SessionTokenHelper.Parse(tokenParts[1]);
            }
            else
            {
                //todo: elasticcollections remove after first upgrade.
                partitionKeyRangeId = "0";
                parsedSessionToken = SessionTokenHelper.Parse(token);
            }

            DefaultTrace.TraceVerbose("Update Session token {0} {1} {2}", resourceId.UniqueDocumentCollectionId, collectionName, parsedSessionToken);

            this.sessionTokens.AddOrUpdate(resourceId.UniqueDocumentCollectionId,
                delegate
                {
                    ConcurrentDictionary<string, ISessionToken> tokens = new ConcurrentDictionary<string, ISessionToken>();
                    tokens[partitionKeyRangeId] = parsedSessionToken;
                    return tokens;
                },
                delegate(ulong key, ConcurrentDictionary<string, ISessionToken> existingTokens)
                {
                    existingTokens.AddOrUpdate(
                        partitionKeyRangeId,
                        parsedSessionToken,
                        (existingPartitionKeyRangeId, existingSessionToken) => existingSessionToken.Merge(parsedSessionToken));
                    return existingTokens;
                });

            // Separate namebased and RID based cache to make sure they don't mess with each other.
            // For example, when collection with same name is created and get higher LSN, we don't want to
            // bump the LSN with resourceId.
            this.sessionTokensNameBased.AddOrUpdate(collectionName,
                delegate
                {
                    ConcurrentDictionary<string, ISessionToken> tokens2 = new ConcurrentDictionary<string, ISessionToken>();
                    tokens2[partitionKeyRangeId] = parsedSessionToken;
                    return tokens2;
                },
                delegate(string key, ConcurrentDictionary<string, ISessionToken> existingTokens)
                {
                    existingTokens.AddOrUpdate(
                        partitionKeyRangeId,
                        parsedSessionToken,
                        (existingPartitionKeyRangeId, existingSessionToken) => existingSessionToken.Merge(parsedSessionToken));
                    return existingTokens;
                });
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

            if (!request.IsNameBased)
            {
                resourceIdString = request.ResourceId;
            }
            else
            {
                resourceIdString = responseHeaders[HttpConstants.HttpHeaders.OwnerId];
                if (string.IsNullOrEmpty(resourceIdString)) resourceIdString = request.ResourceId;
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
    }
}
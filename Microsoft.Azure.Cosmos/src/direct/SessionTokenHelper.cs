//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal static class SessionTokenHelper
    {
        public static void SetOriginalSessionToken(DocumentServiceRequest request, string originalSessionToken)
        {
            if (request == null)
            {
                throw new ArgumentException("request");
            }

            if (originalSessionToken == null)
            {
                request.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
            }
            else
            {
                request.Headers[HttpConstants.HttpHeaders.SessionToken] = originalSessionToken;
            }
        }

        public static void ValidateAndRemoveSessionToken(DocumentServiceRequest request)
        {
            string sessionToken = request.Headers[HttpConstants.HttpHeaders.SessionToken];
            if (!string.IsNullOrEmpty(sessionToken))
            {
                GetLocalSessionToken(request, sessionToken, string.Empty);
                request.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
            }
        }

        public static void SetPartitionLocalSessionToken(DocumentServiceRequest entity, ISessionContainer sessionContainer)
        {
            if (entity == null)
            {
                throw new ArgumentException("entity");
            }

            string originalSessionToken = entity.Headers[HttpConstants.HttpHeaders.SessionToken];
            string partitionKeyRangeId = entity.RequestContext.ResolvedPartitionKeyRange.Id;

            if (string.IsNullOrEmpty(partitionKeyRangeId))
            {
                // AddressCache/address resolution didn't produce partition key range id.
                // In this case it is a bug.
                throw new InternalServerErrorException(RMResources.PartitionKeyRangeIdAbsentInContext);
            }

            if (!string.IsNullOrEmpty(originalSessionToken))
            {
                ISessionToken sessionToken = SessionTokenHelper.GetLocalSessionToken(entity, originalSessionToken, partitionKeyRangeId);
                entity.RequestContext.SessionToken = sessionToken;
            }
            else
            {
                // use ambient session token.
                ISessionToken sessionToken = sessionContainer.ResolvePartitionLocalSessionToken(entity, partitionKeyRangeId);
                entity.RequestContext.SessionToken = sessionToken;
            }

            if (entity.RequestContext.SessionToken == null)
            {
                entity.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
            }
            else
            {
                string version = entity.Headers[HttpConstants.HttpHeaders.Version];
                version = string.IsNullOrEmpty(version) ? HttpConstants.Versions.CurrentVersion : version;

                if (VersionUtility.IsLaterThan(version, HttpConstants.VersionDates.v2015_12_16))
                {
                    entity.Headers[HttpConstants.HttpHeaders.SessionToken] =
                        string.Format(CultureInfo.InvariantCulture, "{0}:{1}", partitionKeyRangeId, entity.RequestContext.SessionToken.ConvertToString());
                }
                else
                {
                    entity.Headers[HttpConstants.HttpHeaders.SessionToken] =
                        entity.RequestContext.SessionToken.ConvertToString();
                }
            }
        }

        internal static ISessionToken GetLocalSessionToken(DocumentServiceRequest request, string globalSessionToken, string partitionKeyRangeId)
        {
            string version = request.Headers[HttpConstants.HttpHeaders.Version];
            version = string.IsNullOrEmpty(version) ? HttpConstants.Versions.CurrentVersion : version;

            if (!VersionUtility.IsLaterThan(version, HttpConstants.VersionDates.v2015_12_16))
            {
                // Pre elastic collection clients send token which is just lsn.
                ISessionToken sessionToken;
                if (!SimpleSessionToken.TryCreate(globalSessionToken, out sessionToken))
                {
                    throw new BadRequestException(string.Format(CultureInfo.InvariantCulture, RMResources.InvalidSessionToken, globalSessionToken));
                }
                else
                {
                    return sessionToken;
                }
            }

            // Convert global session token to local - there's no point in sending global token over the wire to the backend.
            // Global session token is comma separated array of <partitionkeyrangeid>:<lsn> pairs. For example:
            //          2:425344,748:2341234,99:42344
            // Local session token is single <partitionkeyrangeid>:<lsn> pair.
            // Backend only cares about pair which relates to the range owned by the partition.
            string[] partitionKeyRangesToken = globalSessionToken.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            HashSet<string> partitionKeyRangeSet = new HashSet<string>(StringComparer.Ordinal);
            partitionKeyRangeSet.Add(partitionKeyRangeId);

            ISessionToken highestSessionToken = null;

            if (request.RequestContext.ResolvedPartitionKeyRange != null && request.RequestContext.ResolvedPartitionKeyRange.Parents != null)
            {
                partitionKeyRangeSet.UnionWith(request.RequestContext.ResolvedPartitionKeyRange.Parents);
            }

            foreach (string partitionKeyRangeToken in partitionKeyRangesToken)
            {
                string[] items = partitionKeyRangeToken.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

                if (items.Length != 2)
                {
                    throw new BadRequestException(string.Format(CultureInfo.InvariantCulture, RMResources.InvalidSessionToken, partitionKeyRangeToken));
                }

                ISessionToken parsedSessionToken = SessionTokenHelper.Parse(items[1]);

                if (partitionKeyRangeSet.Contains(items[0]))
                {
                    if (highestSessionToken == null)
                    {
                        highestSessionToken = parsedSessionToken;
                    }
                    else
                    {
                        highestSessionToken = highestSessionToken.Merge(parsedSessionToken);
                    }
                }
            }

            return highestSessionToken;
        }

        internal static ISessionToken ResolvePartitionLocalSessionToken(
            DocumentServiceRequest request,
            string partitionKeyRangeId,
            ConcurrentDictionary<string, ISessionToken> partitionKeyRangeIdToTokenMap)
        {
            ISessionToken sessionToken;
            if (partitionKeyRangeIdToTokenMap != null)
            {
                if (partitionKeyRangeIdToTokenMap.TryGetValue(partitionKeyRangeId, out sessionToken))
                {
                    return sessionToken;
                }
                else if (request.RequestContext.ResolvedPartitionKeyRange.Parents != null)
                {
                    for (int parentIndex = request.RequestContext.ResolvedPartitionKeyRange.Parents.Count - 1; parentIndex >= 0; parentIndex--)
                    {
                        if (partitionKeyRangeIdToTokenMap.TryGetValue(request.RequestContext.ResolvedPartitionKeyRange.Parents[parentIndex], out sessionToken))
                        {
                            return sessionToken;
                        }
                    }
                }
            }

            return null;
        }

        internal static ISessionToken Parse(string sessionToken)
        {
            ISessionToken partitionKeyRangeSessionToken = null;

            if (SessionTokenHelper.TryParse(sessionToken, out partitionKeyRangeSessionToken))
            {
                return partitionKeyRangeSessionToken;
            }
            else
            {
                throw new BadRequestException(string.Format(CultureInfo.InvariantCulture, RMResources.InvalidSessionToken, sessionToken));
            }
        }

        internal static bool TryParse(string sessionToken, out ISessionToken parsedSessionToken)
        {
            parsedSessionToken = null;
            if (!string.IsNullOrEmpty(sessionToken))
            {
                string[] sessionTokenSegments = sessionToken.Split(new char[] { ':' });
                return SimpleSessionToken.TryCreate(sessionTokenSegments.Last(), out parsedSessionToken)
                    || VectorSessionToken.TryCreate(sessionTokenSegments.Last(), out parsedSessionToken);
            }
            else
            {
                return false;
            }
        }

        internal static ISessionToken Parse(string sessionToken, string version)
        {
            if (!string.IsNullOrEmpty(sessionToken))
            {
                string[] sessionTokenSegments = sessionToken.Split(new char[] { ':' });

                ISessionToken parsedSessionToken;
                if (VersionUtility.IsLaterThan(version, HttpConstants.VersionDates.v2018_06_18))
                {
                    if (VectorSessionToken.TryCreate(sessionTokenSegments.Last(), out parsedSessionToken))
                    {
                        return parsedSessionToken;
                    }
                }
                else
                {
                    if (SimpleSessionToken.TryCreate(sessionTokenSegments.Last(), out parsedSessionToken))
                    {
                        return parsedSessionToken;
                    }
                }
            }

            DefaultTrace.TraceCritical("Unable to parse session token {0} for version {1}", sessionToken, version);
            throw new InternalServerErrorException(string.Format(CultureInfo.InvariantCulture, RMResources.InvalidSessionToken, sessionToken));
        }
    }
}

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal static class SessionTokenHelper
    {
        public static readonly char[] CharArrayWithColon = new char[] { ':' };
        public static readonly char[] CharArrayWithComma = new char[] { ',' };
        private static readonly char[] CharArrayWithCommaAndColon = new char[] { ',', ':' };

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
                        SessionTokenHelper.SerializeSessionToken(partitionKeyRangeId, entity.RequestContext.SessionToken);
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
            HashSet<string> partitionKeyRangeSet = new HashSet<string>(StringComparer.Ordinal);
            partitionKeyRangeSet.Add(partitionKeyRangeId);

            ISessionToken highestSessionToken = null;

            if (request.RequestContext.ResolvedPartitionKeyRange != null && request.RequestContext.ResolvedPartitionKeyRange.Parents != null)
            {
                partitionKeyRangeSet.UnionWith(request.RequestContext.ResolvedPartitionKeyRange.Parents);
            }

            foreach (string partitionKeyRangeToken in SessionTokenHelper.SplitPartitionLocalSessionTokens(globalSessionToken))
            {
                string[] items = partitionKeyRangeToken.Split(SessionTokenHelper.CharArrayWithColon, StringSplitOptions.RemoveEmptyEntries);

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
            if (SessionTokenHelper.TryParse(sessionToken, out ISessionToken partitionKeyRangeSessionToken))
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
            return SessionTokenHelper.TryParse(sessionToken, out _, out parsedSessionToken);
        }

        internal static bool TryParse(string sessionToken, out string partitionKeyRangeId, out ISessionToken parsedSessionToken)
        {
            parsedSessionToken = null;
            return SessionTokenHelper.TryParse(sessionToken, out partitionKeyRangeId, out string sessionTokenSegment)
                && SessionTokenHelper.TryParseSessionToken(sessionTokenSegment, out parsedSessionToken);
        }

        internal static bool TryParseSessionToken(string sessionToken, out ISessionToken parsedSessionToken)
        {
            parsedSessionToken = null;
            return !string.IsNullOrEmpty(sessionToken) &&
                (VectorSessionToken.TryCreate(sessionToken, out parsedSessionToken)
                || SimpleSessionToken.TryCreate(sessionToken, out parsedSessionToken));
        }

        internal static bool TryParse(string sessionTokenString, out string partitionKeyRangeId, out string sessionToken)
        {
            partitionKeyRangeId = null;
            if (string.IsNullOrEmpty(sessionTokenString))
            {
                sessionToken = null;
                return false;
            }

            int colonIdx = sessionTokenString.IndexOf(':');
            if (colonIdx < 0)
            {
                sessionToken = sessionTokenString;
                return true;
            }

            partitionKeyRangeId = sessionTokenString.Substring(0, colonIdx);
            sessionToken = sessionTokenString.Substring(colonIdx + 1);
            return true;
        }

        internal static ISessionToken Parse(string sessionToken, string version)
        {
            if (SessionTokenHelper.TryParse(sessionToken, out _, out string sessionTokenSegment))
            {
                ISessionToken parsedSessionToken;
                if (VersionUtility.IsLaterThan(version, HttpConstants.VersionDates.v2018_06_18))
                {
                    if (VectorSessionToken.TryCreate(sessionTokenSegment, out parsedSessionToken))
                    {
                        return parsedSessionToken;
                    }
                }
                else
                {
                    if (SimpleSessionToken.TryCreate(sessionTokenSegment, out parsedSessionToken))
                    {
                        return parsedSessionToken;
                    }
                }
            }

            DefaultTrace.TraceCritical("Unable to parse session token {0} for version {1}", sessionToken, version);
            throw new InternalServerErrorException(string.Format(CultureInfo.InvariantCulture, RMResources.InvalidSessionToken, sessionToken));
        }

        internal static bool IsSingleGlobalLsnSessionToken(string sessionToken)
        {
            return sessionToken?.IndexOfAny(SessionTokenHelper.CharArrayWithCommaAndColon) < 0;
        }

        internal static bool TryFindPartitionLocalSessionToken(string sessionTokens, string partitionKeyRangeId, out string partitionLocalSessionToken)
        {
            foreach (string tokenStr in SessionTokenHelper.SplitPartitionLocalSessionTokens(sessionTokens))
            {
                // Assume each id appears only once in the global session token string.
                if (SessionTokenHelper.TryParse(tokenStr, out string currPartitionKeyRangeId, out partitionLocalSessionToken)
                    && currPartitionKeyRangeId == partitionKeyRangeId)
                {
                    return true;
                }
            }

            partitionLocalSessionToken = null;
            return false;
        }

        private static IEnumerable<string> SplitPartitionLocalSessionTokens(string sessionTokens)
        {
            if (sessionTokens != null)
            {
                foreach (string token in sessionTokens.Split(SessionTokenHelper.CharArrayWithComma, StringSplitOptions.RemoveEmptyEntries))
                {
                    yield return token;
                }
            }
        }

        internal static string SerializeSessionToken(string partitionKeyRangeId, ISessionToken parsedSessionToken)
        {
            if (partitionKeyRangeId == null)
            {
                return parsedSessionToken?.ConvertToString();
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", partitionKeyRangeId, parsedSessionToken.ConvertToString());
        }
    }
}
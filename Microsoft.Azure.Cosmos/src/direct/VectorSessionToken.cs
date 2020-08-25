//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// Models vector clock bases session token. Session token has the following format:
    /// {Version}#{GlobalLSN}#{RegionId1}={LocalLsn1}#{RegionId2}={LocalLsn2}....#{RegionIdN}={LocalLsnN}
    /// 'Version' captures the configuration number of the partition which returned this session token.
    /// 'Version' is incremented everytime topology of the partition is updated (say due to Add/Remove/Failover).
    /// 
    /// The choice of separators '#' and '=' is important. Separators ';' and ',' are used to delimit
    /// per-partitionKeyRange session token
    /// session
    /// 
    /// We make assumption that instances of this class are immutable (read only after they are constructed), so if you want to change
    /// this behaviour please review all of its uses and make sure that mutability doesn't break anything.
    /// </summary>
    internal sealed class VectorSessionToken : ISessionToken
    {
        private const char SegmentSeparator = '#';
        private const char RegionProgressSeparator = '=';
        private readonly string sessionToken;
        private readonly long version;
        private readonly long globalLsn;
        private readonly IReadOnlyDictionary<uint, long> localLsnByRegion;

        private VectorSessionToken(long version, long globalLsn, IReadOnlyDictionary<uint, long> localLsnByRegion, string sessionToken = null)
        {
            this.version = version;
            this.globalLsn = globalLsn;
            this.localLsnByRegion = localLsnByRegion;
            this.sessionToken = sessionToken;

            if (this.sessionToken == null)
            {
                string regionProgress = string.Join(
                    VectorSessionToken.SegmentSeparator.ToString(),
                    localLsnByRegion.Select(kvp => string.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", kvp.Key, VectorSessionToken.RegionProgressSeparator, kvp.Value)));

                if (string.IsNullOrEmpty(regionProgress))
                {
                    this.sessionToken = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}{1}{2}",
                        this.version,
                        VectorSessionToken.SegmentSeparator,
                        this.globalLsn);
                }
                else
                {
                    this.sessionToken = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}{1}{2}{3}{4}",
                        this.version,
                        VectorSessionToken.SegmentSeparator,
                        this.globalLsn,
                        VectorSessionToken.SegmentSeparator,
                        regionProgress);
                }
            }
        }

        public VectorSessionToken(VectorSessionToken other, long globalLSN)
            : this(other.version, globalLSN, other.localLsnByRegion.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
        {
        }

        public static bool TryCreate(string sessionToken, out ISessionToken parsedSessionToken)
        {
            parsedSessionToken = null;
            long version = -1;
            long globalLsn = -1;
            IReadOnlyDictionary<uint, long> localLsnByRegion;

            if (VectorSessionToken.TryParseSessionToken(
                sessionToken,
                out version,
                out globalLsn,
                out localLsnByRegion))
            {
                parsedSessionToken = new VectorSessionToken(version, globalLsn, localLsnByRegion, sessionToken);
                return true;
            }
            else
            {
                return false;
            }
        }

        public long LSN
        {
            get
            {
                return this.globalLsn;
            }
        }

        public bool Equals(ISessionToken obj)
        {
            VectorSessionToken other = obj as VectorSessionToken;

            if (other == null)
            {
                return false;
            }

            return this.version == other.version
                && this.globalLsn == other.globalLsn
                && this.AreRegionProgressEqual(other.localLsnByRegion);
        }

        public bool IsValid(ISessionToken otherSessionToken)
        {
            VectorSessionToken other = otherSessionToken as VectorSessionToken;

            if (other == null)
            {
                throw new ArgumentNullException(nameof(otherSessionToken));
            }

            if (other.version < this.version || other.globalLsn < this.globalLsn)
            {
                return false;
            }

            if (other.version == this.version && other.localLsnByRegion.Count != this.localLsnByRegion.Count)
            {
                throw new InternalServerErrorException(
                    string.Format(CultureInfo.InvariantCulture, RMResources.InvalidRegionsInSessionToken, this.sessionToken, other.sessionToken));
            }

            foreach (KeyValuePair<uint, long> kvp in other.localLsnByRegion)
            {
                uint regionId = kvp.Key;
                long otherLocalLsn = kvp.Value;
                long localLsn = -1;

                if (!this.localLsnByRegion.TryGetValue(regionId, out localLsn))
                {
                    // Region mismatch: other session token has progress for a region which is missing in this session token 
                    // Region mismatch can be ignored only if this session token version is smaller than other session token version
                    if (this.version == other.version)
                    {
                        throw new InternalServerErrorException(
                            string.Format(CultureInfo.InvariantCulture, RMResources.InvalidRegionsInSessionToken, this.sessionToken, other.sessionToken));
                    }
                    else
                    {
                        // ignore missing region as other session token version > this session token version
                    }
                }
                else
                {
                    // region is present in both session tokens.
                    if (otherLocalLsn < localLsn)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Merge is commutative operation, so a.Merge(b).Equals(b.Merge(a))
        public ISessionToken Merge(ISessionToken obj)
        {
            VectorSessionToken other = obj as VectorSessionToken;

            if (other == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            if (this.version == other.version && this.localLsnByRegion.Count != other.localLsnByRegion.Count)
            {
                throw new InternalServerErrorException(
                    string.Format(CultureInfo.InvariantCulture, RMResources.InvalidRegionsInSessionToken, this.sessionToken, other.sessionToken));
            }

            VectorSessionToken sessionTokenWithHigherVersion;
            VectorSessionToken sessionTokenWithLowerVersion;

            if (this.version < other.version)
            {
                sessionTokenWithLowerVersion = this;
                sessionTokenWithHigherVersion = other;
            }
            else
            {
                sessionTokenWithLowerVersion = other;
                sessionTokenWithHigherVersion = this;
            }
            
            Dictionary<uint, long> highestLocalLsnByRegion = new Dictionary<uint, long>();

            foreach (KeyValuePair<uint, long> kvp in sessionTokenWithHigherVersion.localLsnByRegion)
            {
                uint regionId = kvp.Key;
                long localLsn1 = kvp.Value;
                long localLsn2 = -1;

                if (sessionTokenWithLowerVersion.localLsnByRegion.TryGetValue(regionId, out localLsn2))
                {
                    highestLocalLsnByRegion[regionId] = Math.Max(localLsn1, localLsn2);
                }
                else if (this.version == other.version)
                {
                    throw new InternalServerErrorException(
                        string.Format(CultureInfo.InvariantCulture, RMResources.InvalidRegionsInSessionToken, this.sessionToken, other.sessionToken));
                }
                else
                {
                    highestLocalLsnByRegion[regionId] = localLsn1;
                }
            }

            return new VectorSessionToken(
                Math.Max(this.version, other.version),
                Math.Max(this.globalLsn, other.globalLsn),
                highestLocalLsnByRegion);
        }

        string ISessionToken.ConvertToString()
        {
            return this.sessionToken;
        }

        private bool AreRegionProgressEqual(IReadOnlyDictionary<uint, long> other)
        {
            if (this.localLsnByRegion.Count != other.Count)
            {
                return false;
            }

            foreach (KeyValuePair<uint, long> kvp in this.localLsnByRegion)
            {
                uint regionId = kvp.Key;
                long localLsn1 = kvp.Value;
                long localLsn2 = -1;

                if (other.TryGetValue(regionId, out localLsn2))
                {
                    if (localLsn1 != localLsn2)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool TryParseSessionToken(
            string sessionToken,
            out long version,
            out long globalLsn,
            out IReadOnlyDictionary<uint, long> localLsnByRegion)
        {
            version = 0;
            localLsnByRegion = null;
            globalLsn = -1;

            if (string.IsNullOrEmpty(sessionToken))
            {
                DefaultTrace.TraceCritical("Session token is empty");
                return false;
            }

            string[] segments = sessionToken.Split(VectorSessionToken.SegmentSeparator);

            if (segments.Length < 2)
            {
                return false;
            }

            if (!long.TryParse(segments[0], NumberStyles.Number, CultureInfo.InvariantCulture, out version)
                || !long.TryParse(segments[1], NumberStyles.Number, CultureInfo.InvariantCulture, out globalLsn))
            {
                DefaultTrace.TraceCritical("Unexpected session token version number '{0}' OR global lsn '{1}'.", segments[0], segments[1]);
                return false;
            }

            Dictionary<uint, long> lsnByRegion = new Dictionary<uint, long>();

            foreach (string regionSegment in segments.Skip(2))
            {
                string[] regionIdWithLsn = regionSegment.Split(VectorSessionToken.RegionProgressSeparator);

                if(regionIdWithLsn.Length != 2)
                {
                    DefaultTrace.TraceCritical("Unexpected region progress segment length '{0}' in session token.", regionIdWithLsn.Length);
                    return false;
                }

                uint regionId = 0;
                long localLsn = -1;

                if (!uint.TryParse(regionIdWithLsn[0], NumberStyles.Number, CultureInfo.InvariantCulture, out regionId)
                    || !long.TryParse(regionIdWithLsn[1], NumberStyles.Number, CultureInfo.InvariantCulture, out localLsn))
                {
                    DefaultTrace.TraceCritical("Unexpected region progress '{0}' for region '{1}' in session token.", regionIdWithLsn[0], regionIdWithLsn[1]);
                    return false;
                }

                lsnByRegion[regionId] = localLsn;
            }

            localLsnByRegion = lsnByRegion;
            return true;
        }
    }
}

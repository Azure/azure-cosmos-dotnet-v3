// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal struct VersionedContinuationToken
    {
        private const string VersionPropertyName = "Version";
        private const string ContinuationTokenPropertyName = "ContinuationToken";
        private static readonly Version CurrentVersion = new Version(major: 1, minor: 0);

        /// <summary>
        /// The default version to use if no continuation token is provided.
        /// At this point in time we support:
        /// * Parallel
        /// * ORDER BY
        /// * TOP
        /// * OFFSET LIMIT
        /// </summary>
        private static readonly Version DefaultVersion = new Version(major: 1, minor: 0);

        private VersionedContinuationToken(
            Version version,
            CosmosObject continuationToken)
        {
            if (version < DefaultVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            if (continuationToken == null)
            {
                throw new ArgumentNullException(nameof(continuationToken));
            }

            this.Version = version;
            this.ContinuationToken = continuationToken;
        }

        public Version Version { get; }
        public CosmosObject ContinuationToken { get; }

        public static bool TryParse(
            string rawContinuationToken,
            out VersionedContinuationToken versionedContinuationToken)
        {
            if (rawContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(rawContinuationToken));
            }

            if (!CosmosElement.TryParse<CosmosObject>(
                rawContinuationToken,
                out CosmosObject parsedVersionedContinuationToken))
            {
                versionedContinuationToken = default(VersionedContinuationToken);
                return false;
            }

            if (!VersionedContinuationToken.TryParseVersion(
                parsedVersionedContinuationToken,
                out Version version))
            {
                versionedContinuationToken = default(VersionedContinuationToken);
                return false;
            }

            if (!VersionedContinuationToken.TryParseContinuationToken(
                parsedVersionedContinuationToken,
                out CosmosObject continuationToken))
            {
                versionedContinuationToken = default(VersionedContinuationToken);
                return false;
            }

            versionedContinuationToken = new VersionedContinuationToken(version, continuationToken);
            return true;
        }

        public static bool IsTokenFromTheFuture(VersionedContinuationToken versionedContinuationToken)
        {
            return versionedContinuationToken.Version > VersionedContinuationToken.CurrentVersion;
        }



        private static bool TryParseVersion(
            CosmosObject parsedVersionedContinuationToken,
            out Version version)
        {
            if (parsedVersionedContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(parsedVersionedContinuationToken));
            }

            if (!parsedVersionedContinuationToken.TryGetValue<CosmosString>(
                VersionedContinuationToken.VersionPropertyName,
                out CosmosString versionString))
            {
                // If there is no version string,
                // then the token was generated before we started versioning.
                // If that is the case, then just use the default version.
                version = VersionedContinuationToken.DefaultVersion;
            }
            else
            {
                if (!Version.TryParse(versionString.Value, out Version parsedVersion))
                {
                    version = default(Version);
                    return false;
                }
                else
                {
                    version = parsedVersion;
                }
            }

            return true;
        }

        private static bool TryParseContinuationToken(
            CosmosObject parsedVersionedContinuationToken,
            out CosmosObject continuationToken)
        {
            if (parsedVersionedContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(parsedVersionedContinuationToken));
            }

            if (!parsedVersionedContinuationToken.TryGetValue<CosmosObject>(
                VersionedContinuationToken.ContinuationTokenPropertyName,
                out continuationToken))
            {
                // If there was no continuation token
                // then the token was from before we started versioning.
                // If that is the case, then just use the full token as the continuation token.
                continuationToken = parsedVersionedContinuationToken;
            }

            return true;
        }
    }
}

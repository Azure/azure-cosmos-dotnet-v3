// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal abstract class PipelineContinuationToken
    {
        protected const string VersionPropertyName = "Version";
        private static readonly Version CurrentVersion = new Version(major: 1, minor: 0);

        protected PipelineContinuationToken(Version version)
        {
            this.Version = version;
        }

        public Version Version { get; }

        public static bool TryParse(
            string rawContinuationToken,
            out PipelineContinuationToken pipelineContinuationToken)
        {
            if (rawContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(rawContinuationToken));
            }

            if (!CosmosElement.TryParse<CosmosObject>(
                rawContinuationToken,
                out CosmosObject parsedContinuationToken))
            {
                // Failed to parse so we need to assume it's a V0 token
                if (!PipelineContinuationTokenV0.TryParse(
                    rawContinuationToken,
                    out PipelineContinuationTokenV0 pipelineContinuationTokenV0))
                {
                    pipelineContinuationToken = default(PipelineContinuationToken);
                    return false;
                }

                pipelineContinuationToken = pipelineContinuationTokenV0;
                return true;
            }

            if (!PipelineContinuationToken.TryParseVersion(
                parsedContinuationToken,
                out Version version))
            {
                pipelineContinuationToken = default(PipelineContinuationToken);
                return false;
            }

            if (version == PipelineContinuationTokenV0.VersionNumber)
            {
                if (!PipelineContinuationTokenV0.TryParse(
                    rawContinuationToken,
                    out PipelineContinuationTokenV0 pipelineContinuationTokenV0))
                {
                    pipelineContinuationToken = default(PipelineContinuationToken);
                    return false;
                }

                pipelineContinuationToken = pipelineContinuationTokenV0;
            }
            else if (version == PipelineContinuationTokenV1.VersionNumber)
            {
                if (!PipelineContinuationTokenV1.TryParse(
                    parsedContinuationToken,
                    out PipelineContinuationTokenV1 pipelineContinuationTokenV1))
                {
                    pipelineContinuationToken = default(PipelineContinuationToken);
                    return false;
                }

                pipelineContinuationToken = pipelineContinuationTokenV1;
            }
            else if (version == PipelineContinuationTokenV1_1.VersionNumber)
            {
                if (!PipelineContinuationTokenV1_1.TryParse(
                    parsedContinuationToken,
                    out PipelineContinuationTokenV1_1 pipelineContinuationTokenV1_1))
                {
                    pipelineContinuationToken = default(PipelineContinuationToken);
                    return false;
                }

                pipelineContinuationToken = pipelineContinuationTokenV1_1;
            }
            else
            {
                pipelineContinuationToken = default(PipelineContinuationToken);
                return false;
            }

            return true;
        }

        public static bool TryConvertToLatest(
            PipelineContinuationToken pipelinedContinuationToken,
            out PipelineContinuationTokenV1_1 pipelineContinuationTokenV2)
        {
            if (pipelinedContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(pipelinedContinuationToken));
            }

            if (pipelinedContinuationToken is PipelineContinuationTokenV0 pipelineContinuationTokenV0)
            {
                pipelinedContinuationToken = new PipelineContinuationTokenV1(pipelineContinuationTokenV0.SourceContinuationToken);
            }

            if (pipelinedContinuationToken is PipelineContinuationTokenV1 pipelineContinuationTokenV1)
            {
                pipelinedContinuationToken = new PipelineContinuationTokenV1_1(
                    queryPlan: null,
                    sourceContinuationToken: pipelineContinuationTokenV1.SourceContinuationToken);
            }

            if (!(pipelinedContinuationToken is PipelineContinuationTokenV1_1 convertedPipelineContinuationTokenV2))
            {
                pipelineContinuationTokenV2 = default(PipelineContinuationTokenV1_1);
                return false;
            }
            else
            {
                pipelineContinuationTokenV2 = convertedPipelineContinuationTokenV2;
            }

            return true;
        }

        public static bool IsTokenFromTheFuture(PipelineContinuationToken versionedContinuationToken)
        {
            return versionedContinuationToken.Version > PipelineContinuationToken.CurrentVersion;
        }

        protected static bool TryParseVersion(
            CosmosObject parsedVersionedContinuationToken,
            out Version version)
        {
            if (parsedVersionedContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(parsedVersionedContinuationToken));
            }

            if (!parsedVersionedContinuationToken.TryGetValue<CosmosString>(
                PipelineContinuationToken.VersionPropertyName,
                out CosmosString versionString))
            {
                // If there is no version string,
                // then the token was generated before we started versioning.
                // If that is the case, then just use the default version.
                version = PipelineContinuationTokenV0.VersionNumber;
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
    }
}

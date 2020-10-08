// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Tokens
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal abstract class PipelineContinuationToken
    {
        protected const string VersionPropertyName = "Version";

        private static readonly Version CurrentVersion = new Version(major: 1, minor: 1);

        protected PipelineContinuationToken(Version version)
        {
            this.Version = version;
        }

        public Version Version { get; }

        public static bool TryCreateFromCosmosElement(
            CosmosElement cosmosElement,
            out PipelineContinuationToken pipelineContinuationToken)
        {
            if (cosmosElement == null)
            {
                throw new ArgumentNullException(nameof(cosmosElement));
            }

            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                // Not a CosmosObject, so it doesn't have a version number, so 
                pipelineContinuationToken = new PipelineContinuationTokenV0(cosmosElement);
                return true;
            }

            if (!PipelineContinuationToken.TryParseVersion(
                cosmosObject,
                out Version version))
            {
                pipelineContinuationToken = default;
                return false;
            }

            if (version == PipelineContinuationTokenV0.VersionNumber)
            {
                if (!PipelineContinuationTokenV0.TryCreateFromCosmosElement(
                    cosmosElement,
                    out PipelineContinuationTokenV0 pipelineContinuationTokenV0))
                {
                    pipelineContinuationToken = default;
                    return false;
                }

                pipelineContinuationToken = pipelineContinuationTokenV0;
            }
            else if (version == PipelineContinuationTokenV1.VersionNumber)
            {
                if (!PipelineContinuationTokenV1.TryCreateFromCosmosElement(
                    cosmosObject,
                    out PipelineContinuationTokenV1 pipelineContinuationTokenV1))
                {
                    pipelineContinuationToken = default;
                    return false;
                }

                pipelineContinuationToken = pipelineContinuationTokenV1;
            }
            else if (version == PipelineContinuationTokenV1_1.VersionNumber)
            {
                if (!PipelineContinuationTokenV1_1.TryCreateFromCosmosElement(
                    cosmosObject,
                    out PipelineContinuationTokenV1_1 pipelineContinuationTokenV1_1))
                {
                    pipelineContinuationToken = default;
                    return false;
                }

                pipelineContinuationToken = pipelineContinuationTokenV1_1;
            }
            else
            {
                pipelineContinuationToken = default;
                return false;
            }

            return true;
        }

        public static bool TryConvertToLatest(
            PipelineContinuationToken pipelinedContinuationToken,
            out PipelineContinuationTokenV1_1 pipelineContinuationTokenV1_1)
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

            if (!(pipelinedContinuationToken is PipelineContinuationTokenV1_1 convertedPipelineContinuationTokenV1_1))
            {
                pipelineContinuationTokenV1_1 = default;
                return false;
            }
            else
            {
                pipelineContinuationTokenV1_1 = convertedPipelineContinuationTokenV1_1;
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
                return true;
            }

            if (!Version.TryParse(versionString.Value, out Version parsedVersion))
            {
                version = default;
                return false;
            }

            version = parsedVersion;
            return true;
        }
    }
}

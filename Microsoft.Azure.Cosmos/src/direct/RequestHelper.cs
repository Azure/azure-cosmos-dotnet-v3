//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents.Collections;

    internal static class RequestHelper
    {
        /// <summary>
        /// Gets the effective ConsistencyLevel for a request.
        /// This is used by both read and write operations.
        /// </summary>
        public static ConsistencyLevel GetConsistencyLevelToUse(
            IServiceConfigurationReader serviceConfigReader,
            DocumentServiceRequest request)
        {
            if (serviceConfigReader == null)
            {
                throw new ArgumentNullException(nameof(serviceConfigReader));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return GetConsistencyLevelToUse(
                request.Headers,
                serviceConfigReader.DefaultConsistencyLevel);
        }

        /// <summary>
        /// Gets the effective ConsistencyLevel from headers or returns default.
        /// </summary>
        private static ConsistencyLevel GetConsistencyLevelToUse(
            INameValueCollection headers,
            ConsistencyLevel defaultAccountConsistency)
        {
            ConsistencyLevel consistencyLevelToUse = defaultAccountConsistency;

            string consistencyLevelHeaderValue = headers[HttpConstants.HttpHeaders.ConsistencyLevel];

            if (!string.IsNullOrEmpty(consistencyLevelHeaderValue))
            {
                if (!Enum.TryParse<ConsistencyLevel>(consistencyLevelHeaderValue, out ConsistencyLevel requestConsistencyLevel))
                {
                    throw new BadRequestException(
                        string.Format(
                            CultureInfo.CurrentUICulture,
                            RMResources.InvalidHeaderValue,
                            consistencyLevelHeaderValue,
                            HttpConstants.HttpHeaders.ConsistencyLevel));
                }

                consistencyLevelToUse = requestConsistencyLevel;
            }

            return consistencyLevelToUse;
        }

        /// <summary>
        /// Gets the effective ReadConsistencyStrategy to use for a READ request.
        /// Uses ReadConsistencyStrategy header if set, otherwise falls back to ConsistencyLevel header, 
        /// and finally to account default consistency level.
        /// </summary>
        public static ReadConsistencyStrategy GetReadConsistencyStrategyToUse(
            IServiceConfigurationReader serviceConfigReader,
            DocumentServiceRequest request)
        {
            if (serviceConfigReader == null)
            {
                throw new ArgumentNullException(nameof(serviceConfigReader));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ReadConsistencyStrategy? requestLevelStrategy = request.RequestContext?.ReadConsistencyStrategy;

            // If explicit strategy is set, validate and use it
            if (requestLevelStrategy.HasValue)
            {
                ValidateReadConsistencyStrategyCompatibility(requestLevelStrategy.Value, serviceConfigReader.DefaultConsistencyLevel);
                return requestLevelStrategy.Value;
            }

            // Fallback: Map from ConsistencyLevel
            ConsistencyLevel consistencyLevelToUse = GetConsistencyLevelToUse(serviceConfigReader, request);

            return MapConsistencyLevelToStrategy(consistencyLevelToUse);
        }

        /// <summary>
        /// Maps ConsistencyLevel to equivalent ReadConsistencyStrategy.
        /// </summary>
        private static ReadConsistencyStrategy MapConsistencyLevelToStrategy(ConsistencyLevel consistencyLevel)
        {
            switch (consistencyLevel)
            {
                case ConsistencyLevel.Eventual:
                case ConsistencyLevel.ConsistentPrefix:
                    return ReadConsistencyStrategy.Eventual;

                case ConsistencyLevel.Session:
                    return ReadConsistencyStrategy.Session;

                case ConsistencyLevel.BoundedStaleness:
                    return ReadConsistencyStrategy.LatestCommitted;

                case ConsistencyLevel.Strong:
                    return ReadConsistencyStrategy.GlobalStrong;

                default:
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, "Unknown ConsistencyLevel: {0}", consistencyLevel));
            }
        }

        /// <summary>
        /// Validates if the requested ReadConsistencyStrategy is compatible with the account's default consistency level.
        /// </summary>
        private static void ValidateReadConsistencyStrategyCompatibility(
            ReadConsistencyStrategy requestedStrategy,
            ConsistencyLevel defaultAccountConsistency)
        {
            // GlobalStrong is only valid with Strong account consistency
            if (requestedStrategy == ReadConsistencyStrategy.GlobalStrong &&
                defaultAccountConsistency != ConsistencyLevel.Strong)
            {
                throw new BadRequestException(
                    string.Format(
                        CultureInfo.CurrentUICulture,
                        "ReadConsistencyStrategy.GlobalStrong is only valid for strong consistency account. Current account consistency: {0}",
                        defaultAccountConsistency));
            }
        }
    }
}
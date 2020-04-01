//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Samples
{
    using System;
    using System.Linq;
    using MongoDB.Driver;

    /// <summary>
    /// Helpers to leverage Cosmos DB multi-master capabilities using the Azure Cosmos DB for MongoDB API
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// Retrieve a MongoClient with a preferred write region set.
        /// When connected to a multi-master enabled Cosmos DB database account,
        /// the preferred write region will be considered when determining which
        /// region to report to the client as the PRIMARY.
        /// </summary>
        /// <param name="mongoClientSettings">The MongoClientSettings used to instantiate the client.</param>
        /// <param name="preferredWriteRegion">The preferred write region</param>
        /// <returns>A MongoClient with the preferred write region set</returns>
        public static MongoClient GetMongoClientWithPreferredWriteRegion(MongoClientSettings mongoClientSettings, string preferredWriteRegion)
        {
            mongoClientSettings = mongoClientSettings.Clone();

            if (mongoClientSettings.Servers.Any(x => x.Port != 10255))
            {
                throw new ArgumentException("For geo-replication scenarios, the initial port in connection string should be 10255.");
            }

            if (!string.IsNullOrEmpty(preferredWriteRegion))
            {
                // Setting the preferred write region is done by appending it to the application name
                // The server will attempt to parse the string after the last '@' as an Azure region
                // If the parse is successful, the writeable region geographically closest to the
                // preferred write region will be presented to the MongoClient as the PRIMARY
                mongoClientSettings.ApplicationName = mongoClientSettings.ApplicationName + $"@{preferredWriteRegion}";
            }

            return new MongoClient(mongoClientSettings);
        }

        /// <summary>
        /// Cosmos DB returns the region for a given endpoint in the TagSet
        /// This can be useful as part of a workflow to switch the PRIMARY to a different region.
        /// </summary>
        public static string TryGetRegionFromTags(TagSet tagSet)
        {
            return tagSet?.Tags?.FirstOrDefault(x => string.Equals("region", x.Name, StringComparison.Ordinal))?.Value;
        }
    }
}

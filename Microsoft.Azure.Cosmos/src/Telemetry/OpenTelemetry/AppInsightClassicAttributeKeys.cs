//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal sealed class AppInsightClassicAttributeKeys
    {
        /// <summary>
        /// Represents the diagnostic namespace for Azure Cosmos.
        /// </summary>
        public const string DbName = "db.name";

        /// <summary>
        /// Represents the name of the database operation.
        /// </summary>
        public const string DbOperation = "db.operation";

        /// <summary>
        /// Represents the server address.
        /// </summary>
        public const string ServerAddress = "net.peer.name";

        /// <summary>
        /// Represents the name of the container in Cosmos DB.
        /// </summary>
        public const string ContainerName = "db.cosmosdb.container";

        /// <summary>
        /// Represents the status code of the response.
        /// </summary>
        public const string StatusCode = "db.cosmosdb.status_code";

        /// <summary>
        /// Represents the user agent
        /// </summary>
        public const string UserAgent = "db.cosmosdb.user_agent";

    }
}

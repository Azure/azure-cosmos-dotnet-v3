//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Metrics received for queries from the backend.
    /// </summary>
    public abstract class ServerSideMetrics
    {
        /// <summary>
        /// Gets the total query time in the Azure Cosmos database service.
        /// </summary>
        public abstract TimeSpan TotalTime { get; }

        /// <summary>
        /// Gets the number of documents retrieved during query in the Azure Cosmos database service.
        /// </summary>
        public abstract long RetrievedDocumentCount { get; }

        /// <summary>
        /// Gets the size of documents retrieved in bytes during query in the Azure Cosmos DB service.
        /// </summary>
        public abstract long RetrievedDocumentSize { get; }

        /// <summary>
        /// Gets the number of documents returned by query in the Azure Cosmos DB service.
        /// </summary>
        public abstract long OutputDocumentCount { get; }

        /// <summary>
        /// Gets the size of documents outputted in bytes during query in the Azure Cosmos database service.
        /// </summary>
        public abstract long OutputDocumentSize { get; }

        /// <summary>
        /// Gets the query preparation time in the Azure Cosmos database service.
        /// </summary>
        public abstract TimeSpan QueryPreparationTime { get; }

        /// <summary>
        /// Gets the query index lookup time in the Azure Cosmos database service.
        /// </summary>
        public abstract TimeSpan IndexLookupTime { get; }

        /// <summary>
        /// Gets the document loading time during query in the Azure Cosmos database service.
        /// </summary>
        public abstract TimeSpan DocumentLoadTime { get; }

        /// <summary>
        /// Gets the query runtime execution time during query in the Azure Cosmos database service.
        /// </summary>
        public abstract TimeSpan RuntimeExecutionTime { get; }

        /// <summary>
        /// Gets the output writing/serializing time during query in the Azure Cosmos database service.
        /// </summary>
        public abstract TimeSpan DocumentWriteTime { get; }

        /// <summary>
        /// Gets the index hit ratio by query in the Azure Cosmos database service. Value is within the range [0,1].
        /// </summary>
        public abstract double IndexHitRatio { get; }

        /// <summary>
        /// Gets the VMExecution Time.
        /// </summary>
        public abstract TimeSpan VMExecutionTime { get; }
    }
}

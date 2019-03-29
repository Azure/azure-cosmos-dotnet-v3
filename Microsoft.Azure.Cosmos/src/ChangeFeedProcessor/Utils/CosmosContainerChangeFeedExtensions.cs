//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Extensions to integrate Change Feed consumption.
    /// </summary>
    public static class CosmosContainerChangeFeedExtensions
    {
        /// <summary>
        /// Initializes a <see cref="ChangeFeedProcessorBuilder{T}"/> for change feed processing.
        /// </summary>
        /// <param name="cosmosContainer">Cosmos Container that is being monitored for changes.</param>
        /// <param name="onChangesDelegate">Delegate to receive changes.</param>
        /// <returns></returns>
        public static ChangeFeedProcessorBuilder<T> CreateChangeFeedProcessorBuilder<T>(this CosmosContainer cosmosContainer, Func<IReadOnlyList<T>, CancellationToken, Task> onChangesDelegate)
        {
            if (cosmosContainer == null) throw new ArgumentNullException(nameof(cosmosContainer));
            if (onChangesDelegate == null) throw new ArgumentNullException(nameof(onChangesDelegate));
            return new ChangeFeedProcessorBuilder<T>(cosmosContainer, onChangesDelegate);
        }

        /// <summary>
        /// Initializes a <see cref="ChangeFeedProcessorBuilder{T}"/> for change feed monitoring.
        /// </summary>
        /// <param name="cosmosContainer">Cosmos Container that is being monitored for changes.</param>
        /// <param name="estimationDelegate">Delegate to receive estimation.</param>
        /// <returns></returns>
        public static ChangeFeedProcessorBuilder<dynamic> CreateChangeFeedProcessorBuilder(this CosmosContainer cosmosContainer, Func<long, CancellationToken, Task> estimationDelegate)
        {
            if (cosmosContainer == null) throw new ArgumentNullException(nameof(cosmosContainer));
            if (estimationDelegate == null) throw new ArgumentNullException(nameof(estimationDelegate));
            return new ChangeFeedProcessorBuilder<dynamic>(cosmosContainer, estimationDelegate);
        }
    }
}

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// BulkItemOperation
    /// </summary>
    public class BulkItemOperation<TContext>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BulkItemOperation{TContext}"/> class.
        /// </summary>
        /// <param name="streamPayload"></param>
        /// <param name="partitionKey"></param>
        /// <param name="requestOptions"></param>
        /// <param name="operationType"></param>
        /// <param name="operationContext"></param>
        internal BulkItemOperation(Stream streamPayload,
                                  PartitionKey partitionKey,
                                  ItemRequestOptions requestOptions,
                                  OperationType operationType,
                                  TContext operationContext)
        {
            this.StreamPayload = streamPayload;
            this.PartitionKey = partitionKey;
            this.RequestOptions = requestOptions;
            this.OperationType = operationType;
            this.OperationContext = operationContext;
        }

        internal Stream StreamPayload { get; }
        internal PartitionKey PartitionKey { get; }
        internal ItemRequestOptions RequestOptions { get; }
        internal OperationType OperationType { get; }
        internal TContext OperationContext { get; }
        internal string Id { get; }

        internal static BulkItemOperation<TContext> GetCreateItemStreamOperation(
                                                        Stream streamPayload,
                                                        PartitionKey partitionKey,
                                                        ItemRequestOptions itemRequestOptions,
                                                        TContext operationContext)
        {
            return new BulkItemOperation<TContext>(streamPayload,
                                                   partitionKey,
                                                   itemRequestOptions,
                                                   OperationType.Create,
                                                   operationContext);
        }
    }

    /// <summary>
    /// Typed BulkItemOperation
    /// </summary>
    public class BulkItemOperation<T, TContext> : BulkItemOperation<TContext>
    {
        internal BulkItemOperation(T resourceBody,
                                  PartitionKey partitionKey,
                                  ItemRequestOptions requestOptions,
                                  OperationType operationType,
                                  TContext operationContext)
            : base(streamPayload: null,
                   partitionKey: partitionKey,
                   requestOptions: requestOptions,
                   operationType: operationType,
                   operationContext: operationContext)
        {
            this.ResourceBody = resourceBody;
        }

        internal T ResourceBody { get; }

        internal static BulkItemOperation<T, TContext> GetCreateItemStreamOperation(
                                                T resourceBody,
                                                PartitionKey partitionKey,
                                                ItemRequestOptions itemRequestOptions,
                                                TContext operationContext)
        {
            return new BulkItemOperation<T, TContext>(resourceBody,
                                                   partitionKey,
                                                   itemRequestOptions,
                                                   OperationType.Create,
                                                   operationContext);
        }
    }
}

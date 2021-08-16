// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal static class ReencryptionItemResponseExtensions
    {
        internal static Task<ReencryptionOperationResponse<T>> CaptureReencryptionOperationResponseAsync<T>(
            this Task<ItemResponse<T>> task,
            T item)
        {
            return task.ContinueWith(itemResponse =>
            {
                if (itemResponse.IsCompleted && itemResponse.Exception == null)
                {
                    return new ReencryptionOperationResponse<T>()
                    {
                        Item = item,
                        IsSuccessful = true,
                        RequestUnitsConsumed = task.Result.RequestCharge,
                    };
                }

                AggregateException innerExceptions = itemResponse.Exception.Flatten();

                if (innerExceptions
                    .InnerExceptions
                    .FirstOrDefault(innerEx => innerEx is CosmosException) is CosmosException cosmosException)
                {
                    return new ReencryptionOperationResponse<T>()
                    {
                        Item = item,
                        RequestUnitsConsumed = cosmosException.RequestCharge,
                        IsSuccessful = false,
                        CosmosException = cosmosException,
                    };
                }

                return new ReencryptionOperationResponse<T>()
                {
                    Item = item,
                    IsSuccessful = false,
                    CosmosException = innerExceptions.InnerExceptions.FirstOrDefault(),
                };
            });
        }
    }
}
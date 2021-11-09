// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Cosmos.Samples.ReEncryption
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal static class ReEncryptionItemResponseExtensions
    {
        internal static async Task<ReEncryptionOperationResponse<T>> CaptureReEncryptionOperationResponseAsync<T>(
            this Task<ItemResponse<T>> task,
            T item)
        {
            try
            {
                await task;
                return new ReEncryptionOperationResponse<T>()
                {
                    Item = item,
                    IsSuccessful = true,
                    RequestUnitsConsumed = task.Result.RequestCharge
                };

            }
            catch(Exception ex)
            {
                if (ex is CosmosException cosmosException)
                {
                    return new ReEncryptionOperationResponse<T>()
                    {
                        Item = item,
                        RequestUnitsConsumed = cosmosException.RequestCharge,
                        IsSuccessful = false,
                        CosmosException = cosmosException
                    };
                }

                return new ReEncryptionOperationResponse<T>()
                {
                    Item = item,
                    IsSuccessful = false,
                    CosmosException = ex
                };
            }
        }
    }
}
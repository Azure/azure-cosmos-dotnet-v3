//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;

    /// <summary>
    /// Internal feed iterator API for casting and mocking purposes.
    /// </summary>
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class FeedIteratorInternal : FeedIterator
    {
        public abstract CosmosElement GetCosmsoElementContinuationToken();

        public virtual async Task<(List<CosmosElement>, List<DecryptionInfo>)> GetDecryptedElementResponseAsync(
            CosmosClientContext clientContext,
            IReadOnlyList<CosmosElement> encryptedCosmosElements,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            List<CosmosElement> decryptedCosmosElements = new List<CosmosElement>();
            List<DecryptionInfo> decryptionInfo = new List<DecryptionInfo>();

            using (diagnosticsContext.CreateScope("Decrypt"))
            {
                foreach (CosmosElement document in encryptedCosmosElements)
                {
                    if (!(document is CosmosObject documentObject))
                    {
                        decryptedCosmosElements.Add(document);
                        decryptionInfo.Add(new DecryptionInfo(false, new List<string>()));
                        continue;
                    }

                    try
                    {
                        (CosmosObject decryptedDocument, List<string> decryptedPaths) = await clientContext.EncryptionProcessor.DecryptAsync(
                            documentObject,
                            clientContext.ClientOptions.Encryptor,
                            diagnosticsContext,
                            cancellationToken);

                        decryptedCosmosElements.Add(decryptedDocument);
                        decryptionInfo.Add(new DecryptionInfo(false, decryptedPaths));
                    }
                    catch (Exception ex)
                    {
                        decryptedCosmosElements.Add(document);
                        decryptionInfo.Add(new DecryptionInfo(true, null, ex.Message));
                    }
                }
            }

            return (decryptedCosmosElements, decryptionInfo);
        }
    }
}
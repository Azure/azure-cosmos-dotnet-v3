//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tracing;

    internal static class ContainerPropertiesExtensions
    {
        private const int DefaultStreamBufferSize = 4096;

        internal static Task<PartitionKey?> EnsureIdGetAppendedToPartitionKeyIfNeededAsync(
            this ContainerInternal container,
            PartitionKey? partitionKey,
            string itemId,
            CancellationToken cancellationToken)
        {
            if (container == null)
            {
                return Task.FromResult(partitionKey);
            }

            // Only use container properties that are already cached in memory.
            // This avoids triggering client initialization (EnsureValidClientAsync)
            // or making HTTP calls (ResolveByNameAsync) before the handler pipeline
            // has had a chance to initialize the client.
            // On subsequent requests, the cache will be populated by the normal
            // request pipeline and this optimization will take effect.
            if (!container.TryGetCachedContainerPropertiesFromMemory(out ContainerProperties containerProperties)
                || containerProperties == null
                || !containerProperties.IsLastPartitionKeyPathId)
            {
                return Task.FromResult(partitionKey);
            }

            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentException("itemId needs to be specified if LastPartitionKeyPath is id");
            }

            IReadOnlyList<Documents.Routing.IPartitionKeyComponent> existingComponents = Array.Empty<Documents.Routing.IPartitionKeyComponent>();
            if (partitionKey.HasValue)
            {
                existingComponents = partitionKey.Value.InternalKey.Components;
                IReadOnlyList<string> partitionKeyPaths = containerProperties.PartitionKey.Paths;

                if (existingComponents.Count != partitionKeyPaths.Count - 1)
                {
                    return Task.FromResult(partitionKey);
                }
            }

            PartitionKeyBuilder builder = new PartitionKeyBuilder();

            if (!partitionKey.HasValue)
            {
                for (int i = 0; i < containerProperties.PartitionKey.Paths.Count - 1; i++)
                {
                    builder.AddNullValue();
                }
            }

            builder.Add(itemId);
            PartitionKey idPath = builder.Build();

            List<Documents.Routing.IPartitionKeyComponent> allComponentsList = new List<Documents.Routing.IPartitionKeyComponent>();
            foreach (Documents.Routing.IPartitionKeyComponent item in existingComponents)
            {
                allComponentsList.Add(item);
            }

            foreach (Documents.Routing.IPartitionKeyComponent item in idPath.InternalKey.Components)
            {
                allComponentsList.Add(item);
            }

            Documents.Routing.PartitionKeyInternal partitionKeyInternal = new Documents.Routing.PartitionKeyInternal(allComponentsList);

            return Task.FromResult<PartitionKey?>(new PartitionKey(partitionKeyInternal));
        }

        internal static async Task<(string, Stream)> GetItemIdFromStreamIfRequiredAsync(
            this ContainerInternal container,
            string itemId,
            Stream streamPayload,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(itemId) || streamPayload == null || container == null)
            {
                return (itemId, streamPayload);
            }

            ContainerProperties containerProperties;
            try
            {
                containerProperties = await container.GetCachedContainerPropertiesAsync(
                    forceRefresh: false,
                    trace: NoOpTrace.Singleton,
                    cancellationToken: cancellationToken);
            }
            catch (Exception)
            {
                // Container may not exist yet (e.g. operations on non-existent containers).
                // Swallow the exception and let the actual operation surface the proper error.
                return (itemId, streamPayload);
            }

            if (containerProperties.IsLastPartitionKeyPathId)
            {
                return await ContainerPropertiesExtensions.GetIdFromStreamPayloadAsync(streamPayload);
            }

            return (itemId, streamPayload);
        }

        private static async Task<(string, Stream)> GetIdFromStreamPayloadAsync(Stream streamPayload)
        {
            if (streamPayload == null)
            {
                return (null, streamPayload);
            }

            long originalPosition = 0;
            if (streamPayload.CanSeek)
            {
                originalPosition = streamPayload.Position;
            }

            ReadOnlyMemory<byte> payload = default;
            byte[] rentedBuffer = null;
            if (streamPayload is MemoryStream memoryStream)
            {
                if (memoryStream.TryGetBuffer(out ArraySegment<byte> existingBuffer))
                {
                    payload = existingBuffer.AsMemory();
                }
            }

            if (payload.IsEmpty)
            {
                int contentLength;
                (rentedBuffer, contentLength) = await ContainerPropertiesExtensions.ReadStreamToPooledBufferAsync(streamPayload);
                payload = new ReadOnlyMemory<byte>(rentedBuffer, 0, contentLength);
            }

            Stream resultStream = streamPayload;
            if (!streamPayload.CanSeek && rentedBuffer != null)
            {
                resultStream = new MemoryStream(payload.ToArray(), writable: false);
            }

            string idValue = null;
            try
            {
                IJsonNavigator jsonNavigator = JsonNavigator.Create(payload);
                IJsonNavigatorNode jsonNavigatorNode = jsonNavigator.GetRootNode();
                CosmosObject cosmosObject = CosmosObject.Create(jsonNavigator, jsonNavigatorNode);

                if (cosmosObject.TryGetValue("id", out CosmosElement idElement) && idElement is CosmosString cosmosString)
                {
                    idValue = cosmosString.Value;
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError($"Failed to extract id from stream payload: {ex.Message}");
            }
            finally
            {
                if (rentedBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }

            if (streamPayload.CanSeek)
            {
                streamPayload.Position = originalPosition;
            }

            return (idValue, resultStream);
        }

        private static async Task<(byte[] Buffer, int Length)> ReadStreamToPooledBufferAsync(Stream stream)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(ContainerPropertiesExtensions.DefaultStreamBufferSize);
            int totalRead = 0;
            try
            {
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead)) > 0)
                {
                    totalRead += bytesRead;
                    if (totalRead == buffer.Length)
                    {
                        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                        System.Buffer.BlockCopy(buffer, 0, newBuffer, 0, totalRead);
                        ArrayPool<byte>.Shared.Return(buffer);
                        buffer = newBuffer;
                    }
                }
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(buffer);
                throw;
            }

            return (buffer, totalRead);
        }
    }
}

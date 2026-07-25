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
    using Microsoft.Azure.Documents;

    internal static class ContainerPropertiesExtensions
    {
        private const int DefaultStreamBufferSize = 4096;

        /// <summary>
        /// Sub-status code returned by the backend on a <see cref="System.Net.HttpStatusCode.BadRequest"/> when the
        /// supplied partition key is missing the trailing "id" component required by a container whose last
        /// partition key path is "/id". When observed, the SDK marks the container as one that requires the item's
        /// "id" to be appended to the partition key and retries the operation.
        /// </summary>
        internal const int AddIdToLastPartitionKeyPathSubStatusCode = 1038;

        internal static bool ShouldValidatePartitionKeyHasId(ResourceType resourceType, OperationType operationType)
        {
            if (resourceType != ResourceType.Document)
            {
                return false;
            }

            if (operationType == OperationType.ReadFeed || operationType == OperationType.Query)
            {
                return false;
            }

            return true;
        }

        internal static async Task<bool> TryMarkLastPartitionKeyPathIsIdAsync(
            this ContainerInternal container,
            CancellationToken cancellationToken)
        {
            if (container == null)
            {
                return false;
            }

            ContainerProperties containerProperties;
            try
            {
                containerProperties = await container.GetCachedContainerPropertiesAsync(
                    forceRefresh: false,
                    trace: NoOpTrace.Singleton,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                DefaultTrace.TraceWarning(
                    "TryMarkLastPartitionKeyPathIsIdAsync: failed to resolve container properties. Exception: {0}",
                    ex.Message);
                return false;
            }

            if (containerProperties == null)
            {
                return false;
            }

            containerProperties.IsLastPartitionKeyPathId = true;
            return true;
        }

        internal static async Task<(PartitionKey?, Stream)> EnsureIdGetsAppendedToPartitionKeyIfNeededAsync(
            this ContainerInternal container,
            PartitionKey? partitionKey,
            string itemId,
            Stream streamPayload,
            CancellationToken cancellationToken)
        {
            if (container == null)
            {
                return (partitionKey, streamPayload);
            }

            ContainerProperties containerProperties;
            try
            {
                containerProperties = await container.GetCachedContainerPropertiesAsync(
                    forceRefresh: false,
                    trace: NoOpTrace.Singleton,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                DefaultTrace.TraceWarning(
                    "EnsureIdGetAppendedToPartitionKeyIfNeededAsync: failed to resolve container properties; this is expected if the container does not exist yet. Exception: {0}",
                    ex.Message);
                return (partitionKey, streamPayload);
            }

            if (containerProperties == null || !containerProperties.IsLastPartitionKeyPathId)
            {
                return (partitionKey, streamPayload);
            }

            (bool isFullySpecified, _) = IsPartitionKeyFullySpecified(partitionKey, containerProperties);
            if (isFullySpecified)
            {
                return (partitionKey, streamPayload);
            }

            (string resolvedItemId, Stream resultStream) = await ContainerPropertiesExtensions.GetItemIdFromStreamIfRequiredAsync(
                containerProperties,
                itemId,
                streamPayload);

            PartitionKey? resolvedPartitionKey = ContainerPropertiesExtensions.EnsureIdGetAppendedToPartitionKeyHelper(
                containerProperties,
                partitionKey,
                resolvedItemId);

            return (resolvedPartitionKey, resultStream);
        }

        private static PartitionKey? EnsureIdGetAppendedToPartitionKeyHelper(
            ContainerProperties containerProperties,
            PartitionKey? partitionKey,
            string itemId)
        {
            if (containerProperties == null || !containerProperties.IsLastPartitionKeyPathId)
            {
                return partitionKey;
            }

            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentException("itemId needs to be specified if LastPartitionKeyPath is id or add it to the partition key paths");
            }

            (_, IReadOnlyList<Documents.Routing.IPartitionKeyComponent> existingComponents) = IsPartitionKeyFullySpecified(partitionKey, containerProperties);
            List<Documents.Routing.IPartitionKeyComponent> allComponentsList = new List<Documents.Routing.IPartitionKeyComponent>();

            foreach (Documents.Routing.IPartitionKeyComponent item in existingComponents)
            {
                allComponentsList.Add(item);
            }

            int prefixComponentCount = GetTargetPartitionKeyComponentCount(containerProperties) - 1;
            if (partitionKey.HasValue && existingComponents.Count != prefixComponentCount)
            {
                return partitionKey;
            }

            PartitionKeyBuilder builder = new PartitionKeyBuilder();

            if (!partitionKey.HasValue)
            {
                for (int i = 0; i < prefixComponentCount; i++)
                {
                    builder.AddNullValue();
                }
            }

            builder.Add(itemId);
            PartitionKey idPath = builder.Build();
            foreach (Documents.Routing.IPartitionKeyComponent item in idPath.InternalKey.Components)
            {
                allComponentsList.Add(item);
            }

            Documents.Routing.PartitionKeyInternal partitionKeyInternal = new Documents.Routing.PartitionKeyInternal(allComponentsList);

            return new PartitionKey(partitionKeyInternal);
        }

        private static async Task<(string, Stream)> GetItemIdFromStreamIfRequiredAsync(
            ContainerProperties containerProperties,
            string itemId,
            Stream streamPayload)
        {
            if (!string.IsNullOrEmpty(itemId) || streamPayload == null || containerProperties == null)
            { 
                return (itemId, streamPayload); 
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
            catch (Exception ex) when (ex is not OperationCanceledException)
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
            else
            {
                streamPayload?.Dispose();
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

        private static (bool, IReadOnlyList<Documents.Routing.IPartitionKeyComponent>) IsPartitionKeyFullySpecified(
            PartitionKey? partitionKey,
            ContainerProperties containerProperties)
        {
            IReadOnlyList<Documents.Routing.IPartitionKeyComponent> existingComponents = Array.Empty<Documents.Routing.IPartitionKeyComponent>();
            if (!partitionKey.HasValue)
            {
                return (false, existingComponents);
            }

            if (partitionKey.Value.IsNone || partitionKey.Value.InternalKey == null)
            {
                return (false, existingComponents);
            }

            existingComponents = partitionKey.Value.InternalKey.Components;

            return (existingComponents.Count == GetTargetPartitionKeyComponentCount(containerProperties), existingComponents);
        }

        /// <summary>
        /// Returns the number of partition key components an item is expected to carry once the item's "id" is
        /// treated as the last partition key path. When the container already declares "/id" as its last path,
        /// this is simply the number of declared paths; otherwise (for example when the backend signals via
        /// sub-status <see cref="AddIdToLastPartitionKeyPathSubStatusCode"/> that "id" must be appended), the
        /// "id" is an additional trailing component beyond the declared paths.
        /// </summary>
        private static int GetTargetPartitionKeyComponentCount(ContainerProperties containerProperties)
        {
            IReadOnlyList<string> partitionKeyPaths = containerProperties.PartitionKey.Paths;
            bool idIsDeclaredPath = partitionKeyPaths.Count > 0
                && string.Equals(partitionKeyPaths[partitionKeyPaths.Count - 1], "/id", StringComparison.Ordinal);

            return idIsDeclaredPath ? partitionKeyPaths.Count : partitionKeyPaths.Count + 1;
        }
    }
}

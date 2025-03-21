//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// A static helper class providing serialization and deserialization 
    /// of requests/responses to and from the RNTBD protocol for the ThinClient scenario.
    /// </summary>
    internal static class ThinClientTransportSerializer
    {
        public sealed class BufferProviderWrapper
        {
            internal BufferProvider Provider { get; set; } = new ();
        }

        /// <summary>
        /// Serialize the Proxy request to the RNTBD protocol format.
        /// Today this takes the HttprequestMessage and reconstructs the DSR.
        /// If the SDK can push properties to the HttpRequestMessage then the handler above having
        /// the DSR can allow us to push that directly to the serialization.
        /// </summary>
        public static async Task<Stream> SerializeProxyRequestAsync(
            BufferProviderWrapper bufferProvider,
            string accountName,
            ClientCollectionCache clientCollectionCache,
            HttpRequestMessage requestMessage)
        {
            // Skip this and use the original DSR.
            OperationType operationType = (OperationType)Enum.Parse(typeof(OperationType), requestMessage.Headers.GetValues(ThinClientConstants.ProxyOperationType).First());
            ResourceType resourceType = (ResourceType)Enum.Parse(typeof(ResourceType), requestMessage.Headers.GetValues(ThinClientConstants.ProxyResourceType).First());

            Guid activityId = Guid.Parse(requestMessage.Headers.GetValues(HttpConstants.HttpHeaders.ActivityId).First());

            Stream requestStream = null;
            if (requestMessage.Content != null)
            {
                requestStream = await requestMessage.Content.ReadAsStreamAsync();
            }

            RequestNameValueCollection dictionaryCollection = new RequestNameValueCollection();
            foreach (KeyValuePair<string, IEnumerable<string>> header in requestMessage.Headers)
            {
                dictionaryCollection.Set(header.Key, string.Join(",", header.Value));
            }

            using DocumentServiceRequest request = new (operationType, resourceType, requestMessage.RequestUri.PathAndQuery,
                                                requestStream, AuthorizationTokenType.PrimaryMasterKey,
                                                dictionaryCollection);

            if (operationType.IsPointOperation())
            {
                string partitionKey = request.Headers.Get(HttpConstants.HttpHeaders.PartitionKey);

                if (string.IsNullOrEmpty(partitionKey))
                {
                    throw new InternalServerErrorException();
                }

                ContainerProperties collection = await clientCollectionCache.ResolveCollectionAsync(
                     request,
                     CancellationToken.None,
                     NoOpTrace.Singleton);
                string epk = GetEffectivePartitionKeyHash(partitionKey, collection.PartitionKey);

                request.Properties = new Dictionary<string, object>
                {
                    { ThinClientConstants.EffectivePartitionKey, HexStringUtility.HexStringToBytes(epk) }
                };
            }
            else if (request.Headers[ThinClientConstants.ProxyStartEpk] != null)
            {
                // Re-add EPK headers removed by RequestInvokerHandler through Properties
                request.Properties = new Dictionary<string, object>
                {
                    { WFConstants.BackendHeaders.StartEpkHash, HexStringUtility.HexStringToBytes(request.Headers[ThinClientConstants.ProxyStartEpk]) },
                    { WFConstants.BackendHeaders.EndEpkHash, HexStringUtility.HexStringToBytes(request.Headers[ThinClientConstants.ProxyEndEpk]) }
                };

                request.Headers.Add(HttpConstants.HttpHeaders.ReadFeedKeyType, RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKeyRange.ToString());
                request.Headers.Add(HttpConstants.HttpHeaders.StartEpk, request.Headers[ThinClientConstants.ProxyStartEpk]);
                request.Headers.Add(HttpConstants.HttpHeaders.EndEpk, request.Headers[ThinClientConstants.ProxyEndEpk]);
            }

            await request.EnsureBufferedBodyAsync();

            using Documents.Rntbd.TransportSerialization.SerializedRequest serializedRequest =
                Documents.Rntbd.TransportSerialization.BuildRequestForProxy(request,
                new ResourceOperation(operationType, resourceType),
                activityId,
                bufferProvider.Provider,
                accountName,
                out _,
                out _);

            // TODO: consider using the SerializedRequest directly.
            MemoryStream memoryStream = new MemoryStream(serializedRequest.RequestSize);
            await serializedRequest.CopyToStreamAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }

        public static string GetEffectivePartitionKeyHash(string partitionJson, PartitionKeyDefinition partitionKeyDefinition)
        {
            return Documents.PartitionKey.FromJsonString(partitionJson).InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition);
        }

        /// <summary>
        /// Deserialize the Proxy Response from the RNTBD protocol format to the Http format needed by the caller.
        /// Today this takes the HttpResponseMessage and reconstructs the modified Http response.
        /// </summary>
        public static async Task<HttpResponseMessage> ConvertProxyResponseAsync(HttpResponseMessage responseMessage)
        {
            using Stream responseStream = await responseMessage.Content.ReadAsStreamAsync();

            (StatusCodes status, byte[] metadata) = await ThinClientTransportSerializer.ReadHeaderAndMetadataAsync(responseStream);

            if (responseMessage.StatusCode != (HttpStatusCode)status)
            {
                throw new InternalServerErrorException($"Status code mismatch: Expected {(int)status}, but received {(int)responseMessage.StatusCode}.");
            }

            Rntbd.BytesDeserializer bytesDeserializer = new Rntbd.BytesDeserializer(metadata, metadata.Length);
            if (!Documents.Rntbd.HeadersTransportSerialization.TryParseMandatoryResponseHeaders(ref bytesDeserializer, out bool payloadPresent, out _))
            {
                throw new InternalServerErrorException("Response metadata length mismatch: Unable to parse mandatory headers.");
            }

            MemoryStream bodyStream = null;
            if (payloadPresent)
            {
                int length = await ThinClientTransportSerializer.ReadBodyLengthAsync(responseStream);
                bodyStream = new MemoryStream(length);
                await responseStream.CopyToAsync(bodyStream);
                bodyStream.Position = 0;
            }

            // TODO(Perf): Clean this up.
            bytesDeserializer = new Rntbd.BytesDeserializer(metadata, metadata.Length);
            StoreResponse storeResponse = Documents.Rntbd.TransportSerialization.MakeStoreResponse(
                status,
                Guid.NewGuid(),
                bodyStream,
                HttpConstants.Versions.CurrentVersion,
                ref bytesDeserializer);

            HttpResponseMessage response = new HttpResponseMessage((HttpStatusCode)storeResponse.StatusCode)
            {
                RequestMessage = responseMessage.RequestMessage
            };

            if (bodyStream != null)
            {
                response.Content = new StreamContent(bodyStream);
            }

            foreach (string header in storeResponse.Headers.Keys())
            {
                if (header == HttpConstants.HttpHeaders.SessionToken)
                {
                    string newSessionToken = storeResponse.PartitionKeyRangeId + ":" + storeResponse.Headers.Get(header);
                    response.Headers.TryAddWithoutValidation(header, newSessionToken);
                }
                else
                {
                    response.Headers.TryAddWithoutValidation(header, storeResponse.Headers.Get(header));
                }
            }

            response.Headers.TryAddWithoutValidation(ThinClientConstants.RoutedViaProxy, "1");
            return response;
        }

        private static async Task<(StatusCodes, byte[] metadata)> ReadHeaderAndMetadataAsync(Stream stream)
        {
            byte[] header = ArrayPool<byte>.Shared.Rent(24);
            const int headerLength = 24;
            try
            {
                int headerRead = 0;
                while (headerRead < headerLength)
                {
                    int read = 0;
                    read = await stream.ReadAsync(header, headerRead, headerLength - headerRead);

                    if (read == 0)
                    {
                        throw new DocumentClientException("Unexpected read empty bytes", HttpStatusCode.Gone, SubStatusCodes.Unknown);
                    }

                    headerRead += read;
                }

                uint totalLength = BitConverter.ToUInt32(header, 0);
                StatusCodes status = (StatusCodes)BitConverter.ToUInt32(header, 4);

                if (totalLength < headerLength)
                {
                    throw new InternalServerErrorException("Header length mismatch: Total length is smaller than expected.");
                }

                int metadataLength = (int)totalLength - headerLength;
                byte[] metadata = new byte[metadataLength];
                int responseMetadataRead = 0;
                while (responseMetadataRead < metadataLength)
                {
                    int read = 0;
                    read = await stream.ReadAsync(metadata, responseMetadataRead, metadataLength - responseMetadataRead);

                    if (read == 0)
                    {
                        throw new DocumentClientException("Unexpected read empty bytes", HttpStatusCode.Gone, SubStatusCodes.Unknown);
                    }

                    responseMetadataRead += read;
                }

                return (status, metadata);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(header);
            }
        }

        private static async Task<int> ReadBodyLengthAsync(Stream stream)
        {
            byte[] header = ArrayPool<byte>.Shared.Rent(4);
            const int headerLength = 4;
            try
            {
                int headerRead = 0;
                while (headerRead < headerLength)
                {
                    int read = 0;
                    read = await stream.ReadAsync(header, headerRead, headerLength - headerRead);

                    if (read == 0)
                    {
                        throw new DocumentClientException("Unexpected read empty bytes", HttpStatusCode.Gone, SubStatusCodes.Unknown);
                    }

                    headerRead += read;
                }

                return BitConverter.ToInt32(header, 0);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(header);
            }

        }
    }
}
//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;


    public static class MockSetupsHelper
    {
        public static void SetupStrongAccountProperties(
            Mock<IHttpHandler> mockHttpClientHandler,
            string accountName,
            string endpoint,
            IList<AccountRegion> writeRegions,
            IList<AccountRegion> readRegions)
        {
            HttpResponseMessage httpResponseMessage = MockSetupsHelper.CreateStrongAccount(
                accountName,
                writeRegions,
                readRegions);

            Uri endpointUri = new Uri(endpoint);
            mockHttpClientHandler.Setup(x => x.SendAsync(
                It.Is<HttpRequestMessage>(x => x.RequestUri == endpointUri),
                It.IsAny<CancellationToken>()))
                .Returns<HttpRequestMessage, CancellationToken>((request, cancellationToken) => Task.FromResult(httpResponseMessage));
        }

        public static Uri SetupSingleRegionAccount(
          string accountName,
          Cosmos.ConsistencyLevel consistencyLevel,
          Mock<IHttpHandler> mockHttpHandler,
          out string primaryRegionEndpoint)
        {
            primaryRegionEndpoint = $"https://{accountName}-eastus.documents.azure.com";
            AccountRegion region = new AccountRegion()
            {
                Name = "East US",
                Endpoint = primaryRegionEndpoint
            };

            AccountProperties accountProperties = new AccountProperties()
            {
                Id = accountName,
                WriteLocationsInternal = new Collection<AccountRegion>()
                {
                    region
                },
                ReadLocationsInternal = new Collection<AccountRegion>()
                {
                    region
                },
                EnableMultipleWriteLocations = false,
                Consistency = new AccountConsistency()
                {
                    DefaultConsistencyLevel = consistencyLevel
                },
                SystemReplicationPolicy = new ReplicationPolicy()
                {
                    MinReplicaSetSize = 3,
                    MaxReplicaSetSize = 4
                },
                ReadPolicy = new ReadPolicy()
                {
                    PrimaryReadCoefficient = 1,
                    SecondaryReadCoefficient = 1
                },
                ReplicationPolicy = new ReplicationPolicy()
                {
                    AsyncReplication = false,
                    MinReplicaSetSize = 3,
                    MaxReplicaSetSize = 4
                }
            };


            Uri endpointUri = new Uri($"https://{accountName}.documents.azure.com");
            mockHttpHandler.Setup(x => x.SendAsync(
                It.Is<HttpRequestMessage>(x => x.RequestUri == endpointUri),
                It.IsAny<CancellationToken>()))
                .Returns<HttpRequestMessage, CancellationToken>((request, cancellationToken) => Task.FromResult(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonConvert.SerializeObject(accountProperties))
                }));
            return endpointUri;
        }

        public static HttpResponseMessage CreateStrongAccount(
            string accountName,
            IList<AccountRegion> writeRegions,
            IList<AccountRegion> readRegions)
        {
            AccountProperties accountProperties = new AccountProperties()
            {
                Id = accountName,
                WriteLocationsInternal = new Collection<AccountRegion>(writeRegions),
                ReadLocationsInternal = new Collection<AccountRegion>(readRegions),
                EnableMultipleWriteLocations = writeRegions.Count > 1,
                Consistency = new AccountConsistency()
                {
                    DefaultConsistencyLevel = Cosmos.ConsistencyLevel.Strong
                },
                SystemReplicationPolicy = new ReplicationPolicy()
                {
                    MinReplicaSetSize = 3,
                    MaxReplicaSetSize = 4
                },
                ReadPolicy = new ReadPolicy()
                {
                    PrimaryReadCoefficient = 1,
                    SecondaryReadCoefficient = 1
                },
                ReplicationPolicy = new ReplicationPolicy()
                {
                    AsyncReplication = false,
                    MinReplicaSetSize = 3,
                    MaxReplicaSetSize = 4
                }
            };

            return new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(accountProperties))
            };
        }

        public static void SetupContainerProperties(
            Mock<IHttpHandler> mockHttpHandler,
            string regionEndpoint,
            string databaseName,
            string containerName,
            string containerRid)
        {
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId(containerRid);
            containerProperties.Id = containerName;
            containerProperties.IndexingPolicy = new Cosmos.IndexingPolicy()
            {
                IndexingMode = Cosmos.IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths = new Collection<Cosmos.IncludedPath>()
                    {
                        new Cosmos.IncludedPath()
                        {
                            Path = @"/*"
                        }
                    },
                ExcludedPaths = new Collection<Cosmos.ExcludedPath>()
                    {
                        new Cosmos.ExcludedPath()
                        {
                            Path = @"/_etag"
                        }
                    }
            };
            containerProperties.PartitionKey = new PartitionKeyDefinition()
            {
                Paths = new Collection<string>()
                {
                    "/pk"
                }
            };

            Uri containerUri = new Uri($"{regionEndpoint}/dbs/{databaseName}/colls/{containerName}");
            mockHttpHandler.Setup(x => x.SendAsync(It.Is<HttpRequestMessage>(x => x.RequestUri == containerUri), It.IsAny<CancellationToken>()))
               .Returns(() => Task.FromResult(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(JsonConvert.SerializeObject(containerProperties))
               }));
        }

        internal static void SetupPartitionKeyRanges(
            Mock<IHttpHandler> mockHttpHandler,
            string regionEndpoint,
            ResourceId containerResourceId,
            out IReadOnlyList<string> partitionKeyRangeIds)
        {
            List<Documents.PartitionKeyRange> partitionKeyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange()
                {
                    MinInclusive = "",
                    MaxExclusive = "05C1DFFFFFFFFC",
                    Id = "0",
                    ResourceId = "ccZ1ANCszwkDAAAAAAAAUA==",
                },
                new Documents.PartitionKeyRange()
                {
                    MinInclusive = "05C1DFFFFFFFFC",
                    MaxExclusive ="FF",
                    Id = "1",
                    ResourceId = "ccZ1ANCszwkDAAAAAAAAUA==",
                }
            };

            partitionKeyRangeIds = partitionKeyRanges.Select(x => x.Id).ToList();
            string containerRidValue = containerResourceId.DocumentCollectionId.ToString();
            JObject jObject = new JObject
            {
                { "_rid",  containerRidValue},
                { "_count", partitionKeyRanges.Count },
                { "PartitionKeyRanges", JArray.FromObject(partitionKeyRanges) }
            };

            Uri partitionKeyUri = new Uri($"{regionEndpoint}/dbs/{containerResourceId.DatabaseId}/colls/{containerRidValue}/pkranges");
            mockHttpHandler.SetupSequence(x => x.SendAsync(It.Is<HttpRequestMessage>(x => x.RequestUri == partitionKeyUri), It.IsAny<CancellationToken>()))
              .Returns(() => Task.FromResult(new HttpResponseMessage()
              {
                  StatusCode = HttpStatusCode.OK,
                  Content = new StringContent(jObject.ToString())
              }))
              .Returns(() => Task.FromResult(new HttpResponseMessage()
              {
                  StatusCode = HttpStatusCode.NotModified,
              }));
        }

        internal static void SetupSinglePartitionKeyRange(
            Mock<IHttpHandler> mockHttpHandler,
            string regionEndpoint,
            ResourceId containerResourceId,
            out IReadOnlyList<string> partitionKeyRangeIds)
        {
            List<Documents.PartitionKeyRange> partitionKeyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange()
                {
                    MinInclusive = "",
                    MaxExclusive = "FF",
                    Id = "0",
                    ResourceId = "ccZ1ANCszwkDAAAAAAAAUA==",
                }
            };

            partitionKeyRangeIds = partitionKeyRanges.Select(x => x.Id).ToList();
            string containerRidValue = containerResourceId.DocumentCollectionId.ToString();
            JObject jObject = new JObject
            {
                { "_rid",  containerRidValue},
                { "_count", partitionKeyRanges.Count },
                { "PartitionKeyRanges", JArray.FromObject(partitionKeyRanges) }
            };

            Uri partitionKeyUri = new Uri($"{regionEndpoint}/dbs/{containerResourceId.DatabaseId}/colls/{containerRidValue}/pkranges");
            mockHttpHandler.SetupSequence(x => x.SendAsync(It.Is<HttpRequestMessage>(x => x.RequestUri == partitionKeyUri), It.IsAny<CancellationToken>()))
              .Returns(() => Task.FromResult(new HttpResponseMessage()
              {
                  StatusCode = HttpStatusCode.OK,
                  Content = new StringContent(jObject.ToString())
              }))
              .Returns(() => Task.FromResult(new HttpResponseMessage()
              {
                  StatusCode = HttpStatusCode.NotModified,
              }));
        }

        internal static HttpResponseMessage CreateAddresses(
            List<string> replicaIds,
            string partitionKeyRangeId,
            string regionName,
            ResourceId containerResourceId)
        {
            int initialPort = 14382;
            int[] ports = new int[replicaIds.Count];
            string basePhysicalUri = "rntbd://cdb-ms-prod-{0}-fd4.documents.azure.com:{1}/apps/9dc0394e-d25f-4c98-baa5-72f1c700bf3e/services/060067c7-a4e9-4465-a412-25cb0104cb58/partitions/2cda760c-f81f-4094-85d0-7bcfb2acc4e6/replicas/{2}";

            for (int i = 0; i < replicaIds.Count; i++)
            {
                ports[i] = initialPort++;
            }

            // Use the partition key range id at the end of each replica id to avoid conflicts when setting up multiple partition key ranges
            List<Address> addresses = new List<Address>();
            for (int i = 0; i < replicaIds.Count; i++)
            {
                string repliaId = replicaIds[i] + (i == 0 ? "p" : "s") + "/";
                addresses.Add(new Address()
                {
                    IsPrimary = i == 0,
                    PartitionKeyRangeId = partitionKeyRangeId,
                    PhysicalUri = string.Format(basePhysicalUri, regionName, ports[i], repliaId),
                    Protocol = "rntbd",
                    PartitionIndex = "7718513@164605136"
                });
            }

            string containerRid = containerResourceId.DocumentCollectionId.ToString();
            JObject jObject = new JObject
            {
                { "_rid", containerRid },
                { "_count", addresses.Count },
                { "Addresss", JArray.FromObject(addresses) }
            };

            return new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jObject.ToString())
            };
        }

        internal static void SetupAddresses(
            Mock<IHttpHandler> mockHttpHandler,
            string partitionKeyRangeId,
            string regionEndpoint,
            string regionName,
            ResourceId containerResourceId,
            out TransportAddressUri primaryReplicaUri)
        {
            string basePhysicalUri = $"rntbd://cdb-ms-prod-{regionName}-fd4.documents.azure.com:14382/apps/9dc0394e-d25f-4c98-baa5-72f1c700bf3e/services/060067c7-a4e9-4465-a412-25cb0104cb58/partitions/2cda760c-f81f-4094-85d0-7bcfb2acc4e6/replicas/";

            // Use the partition key range id at the end of each replica id to avoid conflicts when setting up multiple partition key ranges
            List<Address> addresses = new List<Address>()
            {
                new Address()
                {
                    IsPrimary = true,
                    PartitionKeyRangeId = partitionKeyRangeId,
                    PhysicalUri = basePhysicalUri + $"13260893385949999{partitionKeyRangeId}p/",
                    Protocol = "rntbd",
                    PartitionIndex = "7718513@164605136"
                },
                new Address()
                {
                    IsPrimary = false,
                    PartitionKeyRangeId = partitionKeyRangeId,
                    PhysicalUri = basePhysicalUri + $"13260893385947000{partitionKeyRangeId}s/",
                    Protocol = "rntbd",
                    PartitionIndex = "7718513@164605136"
                },
                new Address()
                {
                    IsPrimary = false,
                    PartitionKeyRangeId = partitionKeyRangeId,
                    PhysicalUri = basePhysicalUri + $"13260893385947111{partitionKeyRangeId}s/",
                    Protocol = "rntbd",
                    PartitionIndex = "7718513@164605136"
                },
                new Address()
                {
                    IsPrimary = false,
                    PartitionKeyRangeId = partitionKeyRangeId,
                    PhysicalUri = basePhysicalUri + $"13260893385947222{partitionKeyRangeId}s/",
                    Protocol = "rntbd",
                    PartitionIndex = "7718513@164605136"
                }
            };

            primaryReplicaUri = new TransportAddressUri(new Uri(addresses.First(x => x.IsPrimary).PhysicalUri));

            string databaseRid = containerResourceId.DatabaseId.ToString();
            string containerRid = containerResourceId.DocumentCollectionId.ToString();
            JObject jObject = new JObject
            {
                { "_rid", containerRid },
                { "_count", addresses.Count },
                { "Addresss", JArray.FromObject(addresses) }
            };

            Uri addressUri = new Uri($"{regionEndpoint}//addresses/?$resolveFor=dbs{HttpUtility.UrlEncode($"/{databaseRid}/colls/{containerRid}/docs")}&$filter=protocol eq rntbd&$partitionKeyRangeIds={partitionKeyRangeId}");
            mockHttpHandler.Setup(x => x.SendAsync(It.Is<HttpRequestMessage>(x => x.RequestUri == addressUri), It.IsAny<CancellationToken>()))
               .Returns(() => Task.FromResult(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(jObject.ToString())
               }));
        }

        internal static void SetupWriteForbiddenException(
            Mock<TransportClient> mockTransportClient,
            TransportAddressUri physicalUri)
        {
            mockTransportClient.Setup(x => x.InvokeResourceOperationAsync(physicalUri, It.IsAny<DocumentServiceRequest>()))
                .Returns(() => throw new ForbiddenException($"Mock write forbidden exception on URI:{physicalUri}", SubStatusCodes.WriteForbidden));
        }

        internal static void SetupServiceUnavailableException(
            Mock<TransportClient> mockTransportClient,
            TransportAddressUri physicalUri)
        {
            mockTransportClient.Setup(x => x.InvokeResourceOperationAsync(physicalUri, It.IsAny<DocumentServiceRequest>()))
                .Returns(() => throw new ServiceUnavailableException($"Mock write forbidden exception on URI:{physicalUri}", SubStatusCodes.Unknown, physicalUri.Uri));
        }

        internal static void SetupRequestTimeoutException(
           Mock<TransportClient> mockTransportClient,
           TransportAddressUri physicalUri)
        {
            mockTransportClient.Setup(x => x.InvokeResourceOperationAsync(physicalUri, It.IsAny<DocumentServiceRequest>()))
                .Returns(() => throw new RequestTimeoutException($"Mock request timeout exception on URI:{physicalUri}", physicalUri.Uri));
        }

        internal static void SetupCreateItemResponse(
            Mock<TransportClient> mockTransportClient,
            TransportAddressUri physicalUri)
        {
            mockTransportClient.Setup(x => x.InvokeResourceOperationAsync(physicalUri, It.IsAny<DocumentServiceRequest>()))
                .Returns<TransportAddressUri, DocumentServiceRequest>(
                (uri, documentServiceRequest) =>
                {
                    Stream createdObject = documentServiceRequest.CloneableBody.Clone();
                    return Task.FromResult(new StoreResponse()
                    {
                        Status = 201,
                        Headers = new StoreResponseNameValueCollection()
                        {
                            ActivityId = Guid.NewGuid().ToString(),
                            LSN = "58593",
                            PartitionKeyRangeId = "1",
                            GlobalCommittedLSN = "58593",
                        },
                        ResponseBody = createdObject
                    });
                });
        }
    }
}
//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosContainerTests
    {
        private CosmosClient cosmosClient = null;
        private Cosmos.Database cosmosDatabase = null;
        private static long ToEpoch(DateTime dateTime) => (long)(dateTime - new DateTime(1970, 1, 1)).TotalSeconds;

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();

            string databaseName = Guid.NewGuid().ToString();
            DatabaseResponse cosmosDatabaseResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            this.cosmosDatabase = cosmosDatabaseResponse;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            if (this.cosmosDatabase != null)
            {
                await this.cosmosDatabase.DeleteStreamAsync();
            }
            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task ReIndexingTest()
        {
            ContainerProperties cp = new ContainerProperties()
            {
                Id = "ReIndexContainer",
                PartitionKeyPath = "/pk",
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    Automatic = false,
                }
            };

            ContainerResponse response = await this.cosmosDatabase.CreateContainerAsync(cp);
            Container container = response;
            ContainerProperties existingContainerProperties = response.Resource;

            // Turn on indexing
            existingContainerProperties.IndexingPolicy.Automatic = true;
            existingContainerProperties.IndexingPolicy.IndexingMode = Cosmos.IndexingMode.Consistent;

            await container.ReplaceContainerAsync(existingContainerProperties);

            // Check progress
            ContainerRequestOptions requestOptions = new ContainerRequestOptions();
            requestOptions.PopulateQuotaInfo = true;

            while (true)
            {
                ContainerResponse readResponse = await container.ReadContainerAsync(requestOptions);
                string indexTransformationStatus = readResponse.Headers["x-ms-documentdb-collection-index-transformation-progress"];
                Assert.IsNotNull(indexTransformationStatus);

                if (int.Parse(indexTransformationStatus) == 100)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(20));
            }
        }

        [TestMethod]
        public async Task ContainerContractTest()
        {
            ContainerResponse response = await this.cosmosDatabase.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");
            this.ValidateCreateContainerResponseContract(response);
        }

        [TestMethod]
        public async Task ContainerBuilderContractTest()
        {
            ContainerResponse response = await this.cosmosDatabase.DefineContainer(Guid.NewGuid().ToString(), "/id").CreateAsync();
            this.ValidateCreateContainerResponseContract(response);

            response = await this.cosmosDatabase.DefineContainer(Guid.NewGuid().ToString(), "/id").CreateIfNotExistsAsync();
            this.ValidateCreateContainerResponseContract(response);

            response = await this.cosmosDatabase.DefineContainer(response.Container.Id, "/id").CreateIfNotExistsAsync();
            this.ValidateCreateContainerResponseContract(response);
        }

        [TestMethod]
        public async Task ContainerBuilderPartitionKeyDefinitionContractTest()
        {
            ContainerResponse response = await this.cosmosDatabase.DefineContainer(Guid.NewGuid().ToString(), "/id")
                .WithPartitionKeyDefinitionVersion(Cosmos.PartitionKeyDefinitionVersion.V2)
                .CreateAsync();

            this.ValidateCreateContainerResponseContract(response);
            Assert.AreEqual(response.Resource.PartitionKeyDefinitionVersion, Cosmos.PartitionKeyDefinitionVersion.V2);

            //response = await this.cosmosDatabase.CreateContainerAsync(new ContainerProperties(new))
            response = await this.cosmosDatabase.DefineContainer(Guid.NewGuid().ToString(), "/id")
                .WithPartitionKeyDefinitionVersion(Cosmos.PartitionKeyDefinitionVersion.V2)
                .CreateIfNotExistsAsync();
            this.ValidateCreateContainerResponseContract(response);
            Assert.AreEqual(response.Resource.PartitionKeyDefinitionVersion, Cosmos.PartitionKeyDefinitionVersion.V2);

            response = await this.cosmosDatabase.DefineContainer(response.Container.Id, "/id")
                .WithPartitionKeyDefinitionVersion(Cosmos.PartitionKeyDefinitionVersion.V2)
                .CreateIfNotExistsAsync();
            this.ValidateCreateContainerResponseContract(response);
            Assert.AreEqual(response.Resource.PartitionKeyDefinitionVersion, Cosmos.PartitionKeyDefinitionVersion.V2);
        }

        [TestMethod]
        public async Task PartitionedCRUDTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerName, partitionKeyPath);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.IsNotNull(containerResponse.Diagnostics);
            string diagnostics = containerResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            SelflinkValidator.ValidateContainerSelfLink(containerResponse.Resource.SelfLink);

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyPath)
            {
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    IndexingMode = Cosmos.IndexingMode.None,
                    Automatic = false
                }
            };

            Container container = containerResponse;
            containerResponse = await container.ReplaceContainerAsync(settings);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.AreEqual(Cosmos.IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);
            Assert.IsNotNull(containerResponse.Diagnostics);
            diagnostics = containerResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            SelflinkValidator.ValidateContainerSelfLink(containerResponse.Resource.SelfLink);

            containerResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            //Assert.AreEqual(Cosmos.PartitionKeyDefinitionVersion.V2, containerResponse.Resource.PartitionKeyDefinitionVersion);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.AreEqual(Cosmos.IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);
            Assert.IsNotNull(containerResponse.Diagnostics);
            diagnostics = containerResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            SelflinkValidator.ValidateContainerSelfLink(containerResponse.Resource.SelfLink);

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task SpatialTest()
        {
            ContainerProperties geographyWithoutBoundingBox = new ContainerProperties()
            {
                Id = "geographyWithoutBoundingBox",
                PartitionKeyPath = "/pk",
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    Automatic = true,
                    IndexingMode = Cosmos.IndexingMode.Consistent,
                    IncludedPaths = new Collection<Cosmos.IncludedPath>()
                    {
                        new Cosmos.IncludedPath()
                        {
                            Path = "/*",
                        }
                    },
                    ExcludedPaths = new Collection<Cosmos.ExcludedPath>(),
                    SpatialIndexes = new Collection<Cosmos.SpatialPath>()
                    {
                        new Cosmos.SpatialPath()
                        {
                            Path = "/location/?",
                            SpatialTypes = new Collection<Cosmos.SpatialType>()
                            {
                                Cosmos.SpatialType.LineString,
                                Cosmos.SpatialType.MultiPolygon,
                                Cosmos.SpatialType.Point,
                                Cosmos.SpatialType.Polygon,
                            }
                        }
                    }
                }
            };

            ContainerProperties geometryWithoutBoundingBox = new ContainerProperties()
            {
                Id = "geometryWithoutBoundingBox",
                PartitionKeyPath = "/pk",
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    Automatic = true,
                    IndexingMode = Cosmos.IndexingMode.Consistent,
                    IncludedPaths = new Collection<Cosmos.IncludedPath>()
                    {
                        new Cosmos.IncludedPath()
                        {
                            Path = "/*",
                        }
                    },
                    ExcludedPaths = new Collection<Cosmos.ExcludedPath>(),
                    SpatialIndexes = new Collection<Cosmos.SpatialPath>()
                    {
                        new Cosmos.SpatialPath()
                        {
                            Path = "/location/?",
                            SpatialTypes = new Collection<Cosmos.SpatialType>()
                            {
                                Cosmos.SpatialType.LineString,
                                Cosmos.SpatialType.MultiPolygon,
                                Cosmos.SpatialType.Point,
                                Cosmos.SpatialType.Polygon,
                            }
                        }
                    }
                },
                GeospatialConfig = new Cosmos.GeospatialConfig()
                {
                    GeospatialType = Cosmos.GeospatialType.Geometry
                }
            };

            ContainerProperties geographyWithBoundingBox = new ContainerProperties()
            {
                Id = "geographyWithBoundingBox",
                PartitionKeyPath = "/pk",
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    Automatic = true,
                    IndexingMode = Cosmos.IndexingMode.Consistent,
                    IncludedPaths = new Collection<Cosmos.IncludedPath>()
                    {
                        new Cosmos.IncludedPath()
                        {
                            Path = "/*",
                        }
                    },
                    ExcludedPaths = new Collection<Cosmos.ExcludedPath>(),
                    SpatialIndexes = new Collection<Cosmos.SpatialPath>()
                    {
                        new Cosmos.SpatialPath()
                        {
                            Path = "/location/?",
                            SpatialTypes = new Collection<Cosmos.SpatialType>()
                            {
                                Cosmos.SpatialType.LineString,
                                Cosmos.SpatialType.MultiPolygon,
                                Cosmos.SpatialType.Point,
                                Cosmos.SpatialType.Polygon,
                            },
                            BoundingBox = new Cosmos.BoundingBoxProperties()
                            {
                                Xmin = 0,
                                Ymin = 0,
                                Xmax = 10,
                                Ymax = 10,
                            }
                        }
                    }
                }
            };

            ContainerProperties geometryWithBoundingBox = new ContainerProperties()
            {
                Id = "geometryWithBoundingBox",
                PartitionKeyPath = "/pk",
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    Automatic = true,
                    IndexingMode = Cosmos.IndexingMode.Consistent,
                    IncludedPaths = new Collection<Cosmos.IncludedPath>()
                    {
                        new Cosmos.IncludedPath()
                        {
                            Path = "/*",
                        }
                    },
                    ExcludedPaths = new Collection<Cosmos.ExcludedPath>(),
                    SpatialIndexes = new Collection<Cosmos.SpatialPath>()
                    {
                        new Cosmos.SpatialPath()
                        {
                            Path = "/location/?",
                            SpatialTypes = new Collection<Cosmos.SpatialType>()
                            {
                                Cosmos.SpatialType.LineString,
                                Cosmos.SpatialType.MultiPolygon,
                                Cosmos.SpatialType.Point,
                                Cosmos.SpatialType.Polygon,
                            },
                            BoundingBox = new Cosmos.BoundingBoxProperties()
                            {
                                Xmin = 0,
                                Ymin = 0,
                                Xmax = 10,
                                Ymax = 10,
                            }
                        }
                    }
                },
                GeospatialConfig = new Cosmos.GeospatialConfig()
                {
                    GeospatialType = Cosmos.GeospatialType.Geometry
                }
            };

            ContainerProperties geometryWithWrongBoundingBox = new ContainerProperties()
            {
                Id = "geometryWithWrongBoundingBox",
                PartitionKeyPath = "/pk",
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    Automatic = true,
                    IndexingMode = Cosmos.IndexingMode.Consistent,
                    IncludedPaths = new Collection<Cosmos.IncludedPath>()
                    {
                        new Cosmos.IncludedPath()
                        {
                            Path = "/*",
                        }
                    },
                    ExcludedPaths = new Collection<Cosmos.ExcludedPath>(),
                    SpatialIndexes = new Collection<Cosmos.SpatialPath>()
                    {
                        new Cosmos.SpatialPath()
                        {
                            Path = "/location/?",
                            SpatialTypes = new Collection<Cosmos.SpatialType>()
                            {
                                Cosmos.SpatialType.LineString,
                                Cosmos.SpatialType.MultiPolygon,
                                Cosmos.SpatialType.Point,
                                Cosmos.SpatialType.Polygon,
                            },
                            BoundingBox = new Cosmos.BoundingBoxProperties()
                            {
                                Xmin = 0,
                                Ymin = 0,
                                Xmax = 0,
                                Ymax = 0,
                            }
                        }
                    }
                },
                GeospatialConfig = new Cosmos.GeospatialConfig()
                {
                    GeospatialType = Cosmos.GeospatialType.Geometry
                }
            };

            //Test 1: try to create a geography collection, with no bounding box
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(geographyWithoutBoundingBox);

            // Test 2: try to create a geometry collection, with no bounding box
            try
            {
                containerResponse = await this.cosmosDatabase.CreateContainerAsync(geometryWithoutBoundingBox);
                Assert.Fail("Expected an exception");
            }
            catch
            {
                //"Required parameter 'boundingBox' for 'Geometry' collection is missing in spatial path '\/location\/?'"
            }

            // Test 3: try to create a geography collection, with bounding box
            try
            {
                containerResponse = await this.cosmosDatabase.CreateContainerAsync(geographyWithBoundingBox);
                Assert.Fail("Expected an exception");
            }
            catch
            {
                //"Incorrect parameter 'boundingBox' specified for 'Geography' collection"
            }

            // Test 4: try to create a geometry collection, with bounding box
            containerResponse = await this.cosmosDatabase.CreateContainerAsync(geometryWithBoundingBox);

            // Test 5: try to create a geometry collection, with wrong bounding box
            try
            {
                containerResponse = await this.cosmosDatabase.CreateContainerAsync(geometryWithWrongBoundingBox);
            }
            catch
            {
                //The value of parameter 'xmax' must be greater than the value of parameter 'xmin' in 'boundingBox' for spatial path '\/location\/?'
            }

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task AnalyticalContainerDefaultsTest()
        {
            string containerId = Guid.NewGuid().ToString();

            ContainerResponse response = await this.cosmosDatabase.CreateContainerAsync(containerId, "/id");
            Assert.IsNull(response.Resource.AnalyticalStoreTimeToLiveInSeconds);

            await response.Container.DeleteContainerAsync();
        }

        [Ignore] // Lack of emulator support
        [TestMethod]
        public async Task AnalyticalContainerCustomTest()
        {
            string containerId = Guid.NewGuid().ToString();
            int analyticalTtlInSec = (int)TimeSpan.FromDays(6 * 30).TotalSeconds; // 6 months
            int defaultTtl = (int)TimeSpan.FromDays(30).TotalSeconds; // 1 month
            ContainerProperties cpInput = new ContainerProperties()
            {
                Id = containerId,
                PartitionKeyPath = "/id",
                DefaultTimeToLive = defaultTtl,
                AnalyticalStoreTimeToLiveInSeconds = analyticalTtlInSec,
            };

            ContainerResponse response = await this.cosmosDatabase.CreateContainerAsync(cpInput);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.IsNotNull(response.Resource.AnalyticalStoreTimeToLiveInSeconds);
            Assert.AreEqual(analyticalTtlInSec, response.Resource.AnalyticalStoreTimeToLiveInSeconds);
            Assert.AreEqual(defaultTtl, response.Resource.DefaultTimeToLive);

            await response.Container.DeleteContainerAsync();
        }

        [TestMethod]
        public async Task CreateHashV1Container()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyPath);
            settings.PartitionKeyDefinitionVersion = Cosmos.PartitionKeyDefinitionVersion.V1;

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(settings);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);

            Assert.AreEqual(Cosmos.PartitionKeyDefinitionVersion.V1, containerResponse.Resource.PartitionKeyDefinitionVersion);
        }

        [TestMethod]
        public async Task PartitionedCreateWithPathDelete()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add(partitionKeyPath);

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyDefinition);
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(settings);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task CreateContainerIfNotExistsPropertiesTestAsync()
        {
            string key = Guid.NewGuid().ToString();
            Dictionary<string, object> properties = new Dictionary<string, object>()
            {
                { key, Guid.NewGuid() }
            };

            // Count is used to validate the handler actually got called.
            int count = 0;
            RequestHandlerHelper requestHandlerHelper = new RequestHandlerHelper
            {
                UpdateRequestMessage = requestMessage =>
                {
                    if (requestMessage.ResourceType == ResourceType.Collection)
                    {
                        count++;
                        Assert.IsNotNull(requestMessage.Properties);
                        Assert.IsTrue(object.ReferenceEquals(properties[key], requestMessage.Properties[key]));
                    }
                }
            };

            CosmosClient client = TestCommon.CreateCosmosClient(x => x.AddCustomHandlers(requestHandlerHelper));
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath1 = "/users";

            RequestOptions requestOptions = new RequestOptions()
            {
                Properties = properties
            };

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyPath1);

            Cosmos.Database database = client.GetDatabase(this.cosmosDatabase.Id);
            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(settings, requestOptions: requestOptions);
            Assert.IsTrue(count > 0);

            count = 0;
            await database.CreateContainerIfNotExistsAsync(settings, requestOptions: requestOptions);
            Assert.IsTrue(count > 0);
        }

        [TestMethod]
        public async Task CreateContainerIfNotExistsAsyncTest()
        {
            RequestChargeHandlerHelper requestChargeHandler = new RequestChargeHandlerHelper();
            RequestHandlerHelper requestHandlerHelper = new RequestHandlerHelper();

            CosmosClient client = TestCommon.CreateCosmosClient(x => x.AddCustomHandlers(requestChargeHandler, requestHandlerHelper));
            Cosmos.Database database = client.GetDatabase(this.cosmosDatabase.Id);

            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath1 = "/users";

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyPath1);
            requestChargeHandler.TotalRequestCharges = 0;
            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(settings);
            Assert.AreEqual(requestChargeHandler.TotalRequestCharges, containerResponse.RequestCharge);

            Assert.IsTrue(containerResponse.RequestCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath1, containerResponse.Resource.PartitionKey.Paths.First());

            //Creating container with same partition key path
            requestChargeHandler.TotalRequestCharges = 0;
            containerResponse = await database.CreateContainerIfNotExistsAsync(settings);
            Assert.AreEqual(requestChargeHandler.TotalRequestCharges, containerResponse.RequestCharge);

            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath1, containerResponse.Resource.PartitionKey.Paths.First());

            //Creating container with different partition key path
            string partitionKeyPath2 = "/users2";
            try
            {
                settings = new ContainerProperties(containerName, partitionKeyPath2);
                containerResponse = await database.CreateContainerIfNotExistsAsync(settings);
                Assert.Fail("Should through ArgumentException on partition key path");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual(nameof(settings.PartitionKey), ex.ParamName);
                Assert.IsTrue(ex.Message.Contains(string.Format(
                    ClientResources.PartitionKeyPathConflict,
                    partitionKeyPath2,
                    containerName,
                    partitionKeyPath1)));
            }
            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);

            // Test the conflict scenario on create.
            bool conflictReturned = false;
            requestHandlerHelper.CallBackOnResponse = (request, response) =>
            {
                if (request.OperationType == Documents.OperationType.Create &&
                    request.ResourceType == Documents.ResourceType.Collection)
                {
                    conflictReturned = true;
                    // Simulate a race condition which results in a 409
                    return CosmosExceptionFactory.Create(
                        statusCode: HttpStatusCode.Conflict,
                        subStatusCode: default,
                        message: "Fake 409 conflict",
                        stackTrace: string.Empty,
                        activityId: Guid.NewGuid().ToString(),
                        requestCharge: response.Headers.RequestCharge,
                        retryAfter: default,
                        headers: response.Headers,
                        error: default,
                        innerException: default,
                        trace: NoOpTrace.Singleton).ToCosmosResponseMessage(request);
                }

                return response;
            };

            requestChargeHandler.TotalRequestCharges = 0;
            ContainerResponse createWithConflictResponse = await database.CreateContainerIfNotExistsAsync(
                Guid.NewGuid().ToString(), 
                "/pk");

            Assert.AreEqual(requestChargeHandler.TotalRequestCharges, createWithConflictResponse.RequestCharge);
            Assert.AreEqual(HttpStatusCode.OK, createWithConflictResponse.StatusCode);
            Assert.IsTrue(conflictReturned);

            await createWithConflictResponse.Container.DeleteContainerAsync();
        }

        [TestMethod]
        public async Task CreateContainerWithSystemKeyTest()
        {
            //Creating existing container with partition key having value for SystemKey
            //https://github.com/Azure/azure-cosmos-dotnet-v3/issues/623
            string v2ContainerName = "V2Container";
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/test");
            partitionKeyDefinition.IsSystemKey = false;
            ContainerProperties containerPropertiesWithSystemKey = new ContainerProperties()
            {
                Id = v2ContainerName,
                PartitionKey = partitionKeyDefinition,
            };
            await this.cosmosDatabase.CreateContainerAsync(containerPropertiesWithSystemKey);

            ContainerProperties containerProperties = new ContainerProperties(v2ContainerName, "/test");
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerProperties);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(v2ContainerName, containerResponse.Resource.Id);
            Assert.AreEqual("/test", containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);

            containerPropertiesWithSystemKey.PartitionKey.IsSystemKey = true;
            await this.cosmosDatabase.CreateContainerAsync(containerPropertiesWithSystemKey);

            containerProperties = new ContainerProperties(v2ContainerName, "/test");
            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerProperties);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(v2ContainerName, containerResponse.Resource.Id);
            Assert.AreEqual("/test", containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task GetFeedRangeOnContainerRecreateScenariosTestAsync()
        {
            try
            {
                await ((ContainerInternal)this.cosmosDatabase.GetContainer(Guid.NewGuid().ToString())).GetFeedRangesAsync();
            }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.NotFound)
            {
                Assert.IsTrue(ce.ToString().Contains("Resource Not Found"));
            }

            for (int i = 0; i < 3; i++) // using a loop to repro because sometimes cache refresh happens providing correct collection rid
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(
                        new ContainerProperties("coll", "/pk"),
                        throughput: 10000);
                await containerResponse.Container.DeleteContainerAsync();

                ContainerResponse recreatedContainer = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(
                        new ContainerProperties("coll", "/pk"),
                        throughput: 10000);
                await ((ContainerInternal)recreatedContainer.Container).GetFeedRangesAsync();
                await recreatedContainer.Container.DeleteContainerAsync();

                try
                {
                    await ((ContainerInternal)recreatedContainer.Container).GetFeedRangesAsync();
                }
                catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.NotFound)
                {
                    Assert.IsTrue(ce.ToString().Contains("Resource Not Found"));
                }
            }
        }

#if INTERNAL || SUBPARTITIONING
        //MultiHash container checks.
        [TestMethod]
        public async Task CreateContainerIfNotExistsAsyncForMultiHashCollectionsTest()
        {
            string containerName = Guid.NewGuid().ToString();
            List<string> partitionKeyPath1 = new List<string>();
            partitionKeyPath1.Add("/users");
            partitionKeyPath1.Add("/sessionId");

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyPath1.AsReadOnly());
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(settings);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);

            //Creating container with same partition key path
            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(settings);

            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);

            //Creating container with different partition key path
            List<string> partitionKeyPath2 = new List<string>();
            partitionKeyPath2.Add("/users2");
            partitionKeyPath2.Add("/sessionId");
            try
            {
                settings = new ContainerProperties(containerName, partitionKeyPath2.AsReadOnly());
                containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(settings);
                Assert.Fail("Should through ArgumentException on partition key path");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual(nameof(settings.PartitionKey), ex.ParamName);
                Assert.IsTrue(ex.Message.Contains(string.Format(
                    ClientResources.PartitionKeyPathConflict,
                    string.Join(",",partitionKeyPath2),
                    containerName,
                    string.Join(",",partitionKeyPath1))));
            }

            // Mismatch in the 2nd path
            List<string> partitionKeyPath3 = new List<string>();
            partitionKeyPath3.Add("/users");
            partitionKeyPath3.Add("/sessionId2");
            try
            {
                settings = new ContainerProperties(containerName, partitionKeyPath3.AsReadOnly());
                containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(settings);
                Assert.Fail("Should through ArgumentException on partition key path");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual(nameof(settings.PartitionKey), ex.ParamName);
                Assert.IsTrue(ex.Message.Contains(string.Format(
                    ClientResources.PartitionKeyPathConflict,
                    string.Join(",", partitionKeyPath3),
                    containerName,
                    string.Join(",", partitionKeyPath1))));
            }

            
            //Create and fetch container with same paths
            List<string> partitionKeyPath4 = new List<string>();
            partitionKeyPath4.Add("/users");
            partitionKeyPath4.Add("/sessionId");

            try
            {
                settings = new ContainerProperties(containerName, partitionKeyPath4.AsReadOnly());
                containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(settings);
            }
            catch (Exception)
            {
                Assert.Fail("The request should have succeeded");
            }

            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);

        }

#endif
        [TestMethod]
        public async Task StreamPartitionedCreateWithPathDelete()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add(partitionKeyPath);

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyDefinition);
            using (ResponseMessage containerResponse = await this.cosmosDatabase.CreateContainerStreamAsync(settings))
            {
                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            }

            using (ResponseMessage containerResponse = await this.cosmosDatabase.GetContainer(containerName).DeleteContainerStreamAsync())
            {
                Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
            }
        }

        [TestMethod]
        public async Task NegativePartitionedCreateDelete()
        {
            string containerName = Guid.NewGuid().ToString();

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/users");
            partitionKeyDefinition.Paths.Add("/test");

            try
            {
                ContainerProperties settings = new ContainerProperties(containerName, partitionKeyDefinition);
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(settings);

                Assert.Fail("Multiple partition keys should have caused an exception.");
            }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.BadRequest)
            {
                string message = ce.ToString();
                Assert.IsNotNull(message);
            }
        }

        [TestMethod]
        public async Task NoPartitionedCreateFail()
        {
            string containerName = Guid.NewGuid().ToString();
            try
            {
                new ContainerProperties(id: containerName, partitionKeyPath: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                new ContainerProperties(id: containerName, partitionKeyDefinition: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            ContainerProperties settings = new ContainerProperties() { Id = containerName };
            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(settings);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(settings);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(id: containerName, partitionKeyPath: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(id: containerName, partitionKeyPath: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }
        }

        [TestMethod]
        public async Task PartitionedCreateDeleteIfNotExists()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task IteratorTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            HashSet<string> containerIds = new HashSet<string>();
            FeedIterator<ContainerProperties> resultSet = this.cosmosDatabase.GetContainerQueryIterator<ContainerProperties>();
            while (resultSet.HasMoreResults)
            {
                foreach (ContainerProperties setting in await resultSet.ReadNextAsync())
                {
                    if (!containerIds.Contains(setting.Id))
                    {
                        containerIds.Add(setting.Id);
                    }
                }
            }

            Assert.IsTrue(containerIds.Count > 0, "The iterator did not find any containers.");
            Assert.IsTrue(containerIds.Contains(containerName), "The iterator did not find the created container");

            resultSet = this.cosmosDatabase.GetContainerQueryIterator<ContainerProperties>($"select * from c where c.id = \"{containerName}\"");
            FeedResponse<ContainerProperties> queryProperties = await resultSet.ReadNextAsync();

            Assert.AreEqual(1, queryProperties.Resource.Count());
            Assert.AreEqual(containerName, queryProperties.First().Id);

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task StreamIteratorTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerName = Guid.NewGuid().ToString();
            containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            using (FeedIterator feedIterator = this.cosmosDatabase.GetContainerQueryStreamIterator(
                "select value c.id From c "))
            {
                while (feedIterator.HasMoreResults)
                {
                    using (ResponseMessage response = await feedIterator.ReadNextAsync())
                    {
                        response.EnsureSuccessStatusCode();
                        using (StreamReader streamReader = new StreamReader(response.Content))
                        using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
                        {
                            // Output will be:
                            // {"_rid":"7p8wAA==","DocumentCollections":["4d310b0d-1716-4bc8-adfa-861a66e4034b","e3eb4ac7-f8a4-47ce-bd71-f65ab43dcb53"],"_count":2}
                            JObject jObject = await JObject.LoadAsync(jsonTextReader);
                            Assert.IsNotNull(jObject["_rid"].ToString());
                            Assert.IsTrue(jObject["DocumentCollections"].ToObject<JArray>().Count > 0);
                            Assert.IsTrue(jObject["_count"].ToObject<int>() > 0);
                        }
                    }
                }
            }

            using (FeedIterator feedIterator = this.cosmosDatabase.GetContainerQueryStreamIterator(
                "select c.id From c "))
            {
                while (feedIterator.HasMoreResults)
                {
                    using (ResponseMessage response = await feedIterator.ReadNextAsync())
                    {
                        response.EnsureSuccessStatusCode();
                        using (StreamReader streamReader = new StreamReader(response.Content))
                        using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
                        {
                            // Output will be:
                            // {"_rid":"FwsdAA==","DocumentCollections":[{"id":"2fdd3591-4ba7-415d-bbe1-c2ca635d409c"},{"id":"3caa5692-3645-4d65-a2aa-a0b67f4dbf52"}],"_count":2}
                            JObject jObject = await JObject.LoadAsync(jsonTextReader);
                            Assert.IsNotNull(jObject["_rid"].ToString());
                            Assert.IsTrue(jObject["DocumentCollections"].ToObject<JArray>().Count > 0);
                            Assert.IsTrue(jObject["_count"].ToObject<int>() > 0);
                        }
                    }
                }
            }

            List<string> ids = new List<string>();
            using (FeedIterator<string> feedIterator = this.cosmosDatabase.GetContainerQueryIterator<string>(
                    "select value c.id From c"))
            {
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<string> iterator = await feedIterator.ReadNextAsync();
                    ids.AddRange(iterator);
                }
            }

            Assert.IsTrue(ids.Count >= 2);

            HashSet<string> containerIds = new HashSet<string>();
            FeedIterator resultSet = this.cosmosDatabase.GetContainerQueryStreamIterator(
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });

            while (resultSet.HasMoreResults)
            {
                using (ResponseMessage message = await resultSet.ReadNextAsync())
                {
                    Assert.AreEqual(HttpStatusCode.OK, message.StatusCode);
                    CosmosJsonDotNetSerializer defaultJsonSerializer = new CosmosJsonDotNetSerializer();
                    dynamic containers = defaultJsonSerializer.FromStream<dynamic>(message.Content).DocumentCollections;
                    foreach (dynamic container in containers)
                    {
                        string id = container.id.ToString();
                        containerIds.Add(id);
                    }
                }
            }

            Assert.IsTrue(containerIds.Count > 0, "The iterator did not find any containers.");
            Assert.IsTrue(containerIds.Contains(containerName), "The iterator did not find the created container");

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task DeleteNonExistingContainer()
        {
            string containerName = Guid.NewGuid().ToString();
            Container container = this.cosmosDatabase.GetContainer(containerName);

            try
            {
                ContainerResponse containerResponse = await container.DeleteContainerAsync();
                Assert.Fail();
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        [TestMethod]
        public async Task DefaultThroughputTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = this.cosmosDatabase.GetContainer(containerName);

            int? readThroughput = await container.ReadThroughputAsync();
            Assert.IsNotNull(readThroughput);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task TimeToLiveTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";
            int timeToLiveInSeconds = 10;
            ContainerProperties setting = new ContainerProperties()
            {
                Id = containerName,
                PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = PartitionKind.Hash },
                DefaultTimeToLive = timeToLiveInSeconds,
            };

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(setting);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = containerResponse;
            ContainerProperties responseSettings = containerResponse;

            Assert.AreEqual(timeToLiveInSeconds, responseSettings.DefaultTimeToLive);

            ContainerResponse readResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(timeToLiveInSeconds, readResponse.Resource.DefaultTimeToLive);

            JObject itemTest = JObject.FromObject(new { id = Guid.NewGuid().ToString(), users = "testUser42" });
            ItemResponse<JObject> createResponse = await container.CreateItemAsync<JObject>(item: itemTest);
            JObject responseItem = createResponse;
            Assert.IsNull(responseItem["ttl"]);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ReplaceThroughputTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = this.cosmosDatabase.GetContainer(containerName);

            int? readThroughput = await container.ReadThroughputAsync();
            Assert.IsNotNull(readThroughput);

            await container.ReplaceThroughputAsync(readThroughput.Value + 1000);
            int? replaceThroughput = await ((ContainerInternal)(ContainerInlineCore)container).ReadThroughputAsync();
            Assert.IsNotNull(replaceThroughput);
            Assert.AreEqual(readThroughput.Value + 1000, replaceThroughput);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ReadReplaceThroughputResponseTests()
        {
            int toStreamCount = 0;
            int fromStreamCount = 0;

            CosmosSerializerHelper mockJsonSerializer = new CosmosSerializerHelper(
                null,
                (x) => fromStreamCount++,
                (x) => toStreamCount++);

            //Create a new cosmos client with the mocked cosmos json serializer
            CosmosClient client = TestCommon.CreateCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.WithCustomSerializer(mockJsonSerializer));

            int databaseThroughput = 10000;
            Cosmos.Database databaseNoThroughput = await client.CreateDatabaseAsync(Guid.NewGuid().ToString(), throughput: null);
            Cosmos.Database databaseWithThroughput = await client.CreateDatabaseAsync(Guid.NewGuid().ToString(), databaseThroughput, null);


            string containerId = Guid.NewGuid().ToString();
            string partitionPath = "/users";
            Container containerNoThroughput = await databaseWithThroughput.CreateContainerAsync(containerId, partitionPath, throughput: null);
            try
            {
                await containerNoThroughput.ReadThroughputAsync(new RequestOptions());
                Assert.Fail("Should through not found exception as throughput is not configured");
            }
            catch (CosmosException exception)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
            }

            try
            {
                await containerNoThroughput.ReplaceThroughputAsync(2000, new RequestOptions());
                Assert.Fail("Should through not found exception as throughput is not configured");
            }
            catch (CosmosException exception)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
            }

            int containerThroughput = 1000;
            Container container = await databaseNoThroughput.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", throughput: containerThroughput);

            int? containerResponseThroughput = await container.ReadThroughputAsync();
            Assert.AreEqual(containerThroughput, containerResponseThroughput);

            ThroughputResponse containerThroughputResponse = await container.ReadThroughputAsync(new RequestOptions());
            Assert.IsNotNull(containerThroughputResponse);
            Assert.IsNotNull(containerThroughputResponse.Resource);
            Assert.IsNotNull(containerThroughputResponse.MinThroughput);
            Assert.IsNotNull(containerThroughputResponse.Resource.Throughput);
            Assert.AreEqual(containerThroughput, containerThroughputResponse.Resource.Throughput.Value);
            SelflinkValidator.ValidateTroughputSelfLink(containerThroughputResponse.Resource.SelfLink);

            containerThroughput += 500;
            containerThroughputResponse = await container.ReplaceThroughputAsync(containerThroughput, new RequestOptions());
            Assert.IsNotNull(containerThroughputResponse);
            Assert.IsNotNull(containerThroughputResponse.Resource);
            Assert.IsNotNull(containerThroughputResponse.Resource.Throughput);
            Assert.AreEqual(containerThroughput, containerThroughputResponse.Resource.Throughput.Value);
            SelflinkValidator.ValidateTroughputSelfLink(containerThroughputResponse.Resource.SelfLink);

            Assert.AreEqual(0, toStreamCount, "Custom serializer to stream should not be used for offer operations");
            Assert.AreEqual(0, fromStreamCount, "Custom serializer from stream should not be used for offer operations");
            await databaseNoThroughput.DeleteAsync();
        }

        [TestMethod]
        public async Task ThroughputNonExistingResourceTest()
        {
            string containerName = Guid.NewGuid().ToString();
            Container container = this.cosmosDatabase.GetContainer(containerName);

            try
            {
                await container.ReadThroughputAsync();
                Assert.Fail("It should throw Resource Not Found exception");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        [TestMethod]
        public async Task ImplicitConversion()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Container container = containerResponse;
            ContainerProperties containerSettings = containerResponse;
            Assert.IsNotNull(container);
            Assert.IsNotNull(containerSettings);

            containerResponse = await container.DeleteContainerAsync();
            container = containerResponse;
            containerSettings = containerResponse;
            Assert.IsNotNull(container);
            Assert.IsNull(containerSettings);
        }

        /// <summary>
        /// This test verifies that we are able to set the ttl property path correctly using SDK.
        /// Also this test will successfully read active item based on its TimeToLivePropertyPath value.
        /// </summary>
        [Obsolete]
        [TestMethod]
        public async Task TimeToLivePropertyPath()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/user";
            int timeToLivetimeToLiveInSeconds = 10;
            ContainerProperties setting = new ContainerProperties()
            {
                Id = containerName,
                PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = PartitionKind.Hash },
                TimeToLivePropertyPath = "/creationDate",
            };

            ContainerResponse containerResponse = null;
            try
            {
                containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(setting);
                Assert.Fail("CreateColleciton with TtlPropertyPath and with no DefaultTimeToLive should have failed.");
            }
            catch (CosmosException exeption)
            {
                // expected because DefaultTimeToLive was not specified
                Assert.AreEqual(HttpStatusCode.BadRequest, exeption.StatusCode);
            }

            // Verify the container content.
            setting.DefaultTimeToLive = timeToLivetimeToLiveInSeconds;
            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(setting);
            Container container = containerResponse;
            Assert.AreEqual(timeToLivetimeToLiveInSeconds, containerResponse.Resource.DefaultTimeToLive);
            Assert.AreEqual("/creationDate", containerResponse.Resource.TimeToLivePropertyPath);

            //verify removing the ttl property path
            setting.TimeToLivePropertyPath = null;
            containerResponse = await container.ReplaceContainerAsync(setting);
            container = containerResponse;
            Assert.AreEqual(timeToLivetimeToLiveInSeconds, containerResponse.Resource.DefaultTimeToLive);
            Assert.IsNull(containerResponse.Resource.TimeToLivePropertyPath);

            //adding back the ttl property path
            setting.TimeToLivePropertyPath = "/creationDate";
            containerResponse = await container.ReplaceContainerAsync(setting);
            container = containerResponse;
            Assert.AreEqual(containerResponse.Resource.TimeToLivePropertyPath, "/creationDate");

            //Creating an item and reading before expiration
            var payload = new { id = "testId", user = "testUser", creationDate = ToEpoch(DateTime.UtcNow) };
            ItemResponse<dynamic> createItemResponse = await container.CreateItemAsync<dynamic>(payload);
            Assert.IsNotNull(createItemResponse.Resource);
            Assert.AreEqual(createItemResponse.StatusCode, HttpStatusCode.Created);
            ItemResponse<dynamic> readItemResponse = await container.ReadItemAsync<dynamic>(payload.id, new Cosmos.PartitionKey(payload.user));
            Assert.IsNotNull(readItemResponse.Resource);
            Assert.AreEqual(readItemResponse.StatusCode, HttpStatusCode.OK);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ClientEncryptionPolicyTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";
            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/path1",
                    ClientEncryptionKeyId = "dekId1",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                    EncryptionType = "Randomized"
                },
                new ClientEncryptionIncludedPath()
                {
                    Path = "/path2",
                    ClientEncryptionKeyId = "dekId2",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                    EncryptionType = "Deterministic"
                }
            };

            ContainerProperties setting = new ContainerProperties()
            {
                Id = containerName,
                PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = PartitionKind.Hash },
                ClientEncryptionPolicy = new ClientEncryptionPolicy(paths)
            };

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(setting);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = containerResponse;
            ContainerProperties responseSettings = containerResponse;

            Assert.AreEqual(2, responseSettings.ClientEncryptionPolicy.IncludedPaths.Count());
            ClientEncryptionIncludedPath includedPath = responseSettings.ClientEncryptionPolicy.IncludedPaths.ElementAt(0);
            Assert.AreEqual("/path1", includedPath.Path);
            Assert.AreEqual("dekId1", includedPath.ClientEncryptionKeyId);
            Assert.AreEqual("AEAD_AES_256_CBC_HMAC_SHA256", includedPath.EncryptionAlgorithm);
            Assert.AreEqual("Randomized", includedPath.EncryptionType);

            includedPath = responseSettings.ClientEncryptionPolicy.IncludedPaths.ElementAt(1);
            Assert.AreEqual("/path2", includedPath.Path);
            Assert.AreEqual("dekId2", includedPath.ClientEncryptionKeyId);
            Assert.AreEqual("AEAD_AES_256_CBC_HMAC_SHA256", includedPath.EncryptionAlgorithm);
            Assert.AreEqual("Deterministic", includedPath.EncryptionType);

            ContainerResponse readResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.ClientEncryptionPolicy);
        }

        [TestMethod]
        public void ClientEncryptionPolicyFailureTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";
            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/path1",
                    ClientEncryptionKeyId = "dekId1",
                    EncryptionAlgorithm = "LegacyAeadAes256CbcHmac256",
                    EncryptionType = "Randomized"
                },
            };

            try
            {
                ContainerProperties setting = new ContainerProperties()
                {
                    Id = containerName,
                    PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = PartitionKind.Hash },
                    ClientEncryptionPolicy = new ClientEncryptionPolicy(paths)
                };

                Assert.Fail("Creating ContainerProperties should have failed.");
            }            
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("EncryptionAlgorithm should be 'AEAD_AES_256_CBC_HMAC_SHA256'."));
            }

            try
            {
                ClientEncryptionIncludedPath path1 = new ClientEncryptionIncludedPath()
                {
                    Path = "/path1",
                    ClientEncryptionKeyId = "dekId2",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                    EncryptionType = "Deterministic"
                };

                Collection<ClientEncryptionIncludedPath> pathsList = new Collection<ClientEncryptionIncludedPath>()
                        {
                            new ClientEncryptionIncludedPath()
                            {
                                Path = "/path1",
                                ClientEncryptionKeyId = "dekId1",
                                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                                EncryptionType = "Randomized"
                            },
                        };
                pathsList.Add(path1);

                ContainerProperties setting = new ContainerProperties()
                {
                    Id = containerName,
                    PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = PartitionKind.Hash },
                    ClientEncryptionPolicy = new ClientEncryptionPolicy(pathsList)                    
                };

                Assert.Fail("Creating ContainerProperties should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Duplicate Path found."));
            }
        }

        private void ValidateCreateContainerResponseContract(ContainerResponse containerResponse)
        {
            Assert.IsNotNull(containerResponse);
            Assert.IsTrue(containerResponse.RequestCharge > 0);
            Assert.IsNotNull(containerResponse.Headers);
            Assert.IsNotNull(containerResponse.Headers.ActivityId);

            ContainerProperties containerSettings = containerResponse.Resource;
            Assert.IsNotNull(containerSettings.Id);
            Assert.IsNotNull(containerSettings.ResourceId);
            Assert.IsNotNull(containerSettings.ETag);
            Assert.IsTrue(containerSettings.LastModified.HasValue);

            Assert.IsNotNull(containerSettings.PartitionKeyPath);
            Assert.IsNotNull(containerSettings.PartitionKeyPathTokens);
            Assert.AreEqual(1, containerSettings.PartitionKeyPathTokens[0].Count);
            Assert.AreEqual("id", containerSettings.PartitionKeyPathTokens[0][0]);

            ContainerInternal containerCore = containerResponse.Container as ContainerInlineCore;
            Assert.IsNotNull(containerCore);
            Assert.IsNotNull(containerCore.LinkUri);
            Assert.IsFalse(containerCore.LinkUri.ToString().StartsWith("/"));

            Assert.IsTrue(containerSettings.LastModified.Value > new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), containerSettings.LastModified.Value.ToString());
        }
    }
}

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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using static Antlr4.Runtime.Atn.SemanticContext;

    [TestClass]
    public class CosmosContainerTests
    {
        private CosmosClient cosmosClient = null;
        private Cosmos.Database cosmosDatabase = null;

        private static long ToEpoch(DateTime dateTime)
        {
            return (long)(dateTime - new DateTime(1970, 1, 1)).TotalSeconds;
        }

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
            ContainerRequestOptions requestOptions = new ContainerRequestOptions
            {
                PopulateQuotaInfo = true
            };

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

            Documents.PartitionKeyDefinition partitionKeyDefinition = new Documents.PartitionKeyDefinition();
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
                    if (requestMessage.ResourceType == Documents.ResourceType.Collection)
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
                        message: "Fake 409 conflict",
                        stackTrace: string.Empty,
                        headers: response.Headers,
                        error: default,
                        innerException: default,
                        trace: request.Trace).ToCosmosResponseMessage(request);
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
            Documents.PartitionKeyDefinition partitionKeyDefinition = new Documents.PartitionKeyDefinition();
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
                    string.Join(",", partitionKeyPath2),
                    containerName,
                    string.Join(",", partitionKeyPath1))));
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

        [TestMethod]
        public async Task StreamPartitionedCreateWithPathDelete()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            Documents.PartitionKeyDefinition partitionKeyDefinition = new Documents.PartitionKeyDefinition();
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

            Documents.PartitionKeyDefinition partitionKeyDefinition = new Documents.PartitionKeyDefinition();
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
                PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = Documents.PartitionKind.Hash },
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
            using CosmosClient client = TestCommon.CreateCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.WithCustomSerializer(mockJsonSerializer));

            int databaseThroughput = 10000;
            Cosmos.Database databaseNoThroughput = await client.CreateDatabaseAsync(Guid.NewGuid().ToString(), throughput: null);
            Cosmos.Database databaseWithThroughput = await client.CreateDatabaseAsync(Guid.NewGuid().ToString(), databaseThroughput, null);

            try
            {
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
            }
            finally
            {
                await databaseNoThroughput.DeleteAsync();
                await databaseWithThroughput.DeleteAsync();
            }
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
                PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = Documents.PartitionKind.Hash },
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
        public async Task ContainerCreationFailsWithUnknownClientEncryptionKey()
        {
            ClientEncryptionIncludedPath unknownKeyConfigured = new Cosmos.ClientEncryptionIncludedPath()
            {
                Path = "/",
                ClientEncryptionKeyId = "unknownKey",
                EncryptionType = "Deterministic",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
            };

            Collection<Cosmos.ClientEncryptionIncludedPath> paths = new Collection<Cosmos.ClientEncryptionIncludedPath> { unknownKeyConfigured };
            Cosmos.ClientEncryptionPolicy clientEncryptionPolicyId = new Cosmos.ClientEncryptionPolicy(paths);

            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicyId };

            try
            {
                await this.cosmosDatabase.CreateContainerAsync(containerProperties, 400);
                Assert.Fail("Expected container creation should fail since client encryption policy is configured with unknown key.");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode);
                Assert.IsTrue(ex.Message.Contains("ClientEncryptionKey with id '[unknownKey]' does not exist."));
            }
        }

        [TestMethod]
        public async Task ClientEncryptionPolicyTest()
        {
            DatabaseInlineCore databaseInlineCore = (DatabaseInlineCore)this.cosmosDatabase;
            await TestCommon.CreateClientEncryptionKey("dekId1", databaseInlineCore);
            await TestCommon.CreateClientEncryptionKey("dekId2", databaseInlineCore);

            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";
            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = partitionKeyPath,
                    ClientEncryptionKeyId = "dekId1",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                    EncryptionType = "Deterministic"
                },
                new ClientEncryptionIncludedPath()
                {
                    Path = "/id",
                    ClientEncryptionKeyId = "dekId2",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                    EncryptionType = "Deterministic"
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
                PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = Documents.PartitionKind.Hash },
                ClientEncryptionPolicy = new ClientEncryptionPolicy(includedPaths: paths, policyFormatVersion: 2)
            };

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(setting);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = containerResponse;
            ContainerProperties responseSettings = containerResponse;

            Assert.AreEqual(3, responseSettings.ClientEncryptionPolicy.IncludedPaths.Count());
            ClientEncryptionIncludedPath includedPath = responseSettings.ClientEncryptionPolicy.IncludedPaths.ElementAt(0);
            Assert.AreEqual(partitionKeyPath, includedPath.Path);
            Assert.AreEqual("dekId1", includedPath.ClientEncryptionKeyId);
            Assert.AreEqual("AEAD_AES_256_CBC_HMAC_SHA256", includedPath.EncryptionAlgorithm);
            Assert.AreEqual("Deterministic", includedPath.EncryptionType);

            includedPath = responseSettings.ClientEncryptionPolicy.IncludedPaths.ElementAt(1);
            Assert.AreEqual("/id", includedPath.Path);
            Assert.AreEqual("dekId2", includedPath.ClientEncryptionKeyId);
            Assert.AreEqual("AEAD_AES_256_CBC_HMAC_SHA256", includedPath.EncryptionAlgorithm);
            Assert.AreEqual("Deterministic", includedPath.EncryptionType);

            includedPath = responseSettings.ClientEncryptionPolicy.IncludedPaths.ElementAt(2);
            Assert.AreEqual("/path2", includedPath.Path);
            Assert.AreEqual("dekId2", includedPath.ClientEncryptionKeyId);
            Assert.AreEqual("AEAD_AES_256_CBC_HMAC_SHA256", includedPath.EncryptionAlgorithm);
            Assert.AreEqual("Deterministic", includedPath.EncryptionType);

            ContainerResponse readResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.ClientEncryptionPolicy);

            // version 1 test.
            containerName = Guid.NewGuid().ToString();
            partitionKeyPath = "/users";
            paths = new Collection<ClientEncryptionIncludedPath>()
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

            setting = new ContainerProperties()
            {
                Id = containerName,
                PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = Documents.PartitionKind.Hash },
                ClientEncryptionPolicy = new ClientEncryptionPolicy(paths)
            };

            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(setting);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            container = containerResponse;
            responseSettings = containerResponse;

            Assert.AreEqual(2, responseSettings.ClientEncryptionPolicy.IncludedPaths.Count());
            includedPath = responseSettings.ClientEncryptionPolicy.IncludedPaths.ElementAt(0);
            Assert.AreEqual("/path1", includedPath.Path);
            Assert.AreEqual("dekId1", includedPath.ClientEncryptionKeyId);
            Assert.AreEqual("AEAD_AES_256_CBC_HMAC_SHA256", includedPath.EncryptionAlgorithm);
            Assert.AreEqual("Randomized", includedPath.EncryptionType);

            includedPath = responseSettings.ClientEncryptionPolicy.IncludedPaths.ElementAt(1);
            Assert.AreEqual("/path2", includedPath.Path);
            Assert.AreEqual("dekId2", includedPath.ClientEncryptionKeyId);
            Assert.AreEqual("AEAD_AES_256_CBC_HMAC_SHA256", includedPath.EncryptionAlgorithm);
            Assert.AreEqual("Deterministic", includedPath.EncryptionType);

            readResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.ClientEncryptionPolicy);

            // replace without updating CEP should be successful
            readResponse.Resource.IndexingPolicy = new Cosmos.IndexingPolicy()
            {
                IndexingMode = Cosmos.IndexingMode.None,
                Automatic = false
            };

            containerResponse = await container.ReplaceContainerAsync(readResponse.Resource);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(Cosmos.IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);

            // update CEP and attempt replace
            readResponse.Resource.ClientEncryptionPolicy = null;
            try
            {
                await container.ReplaceContainerAsync(readResponse.Resource);

                Assert.Fail("ReplaceCollection with update to ClientEncryptionPolicy should have failed.");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode);
                Assert.IsTrue(ex.Message.Contains("'clientEncryptionPolicy' cannot be changed as part of collection replace operation."));
            }
        }

        [TestMethod]
        public async Task ClientEncryptionPolicyFailureTest()
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
                    PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = Documents.PartitionKind.Hash },
                    ClientEncryptionPolicy = new ClientEncryptionPolicy(paths)
                };

                Assert.Fail("Creating ContainerProperties should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("EncryptionAlgorithm should be 'AEAD_AES_256_CBC_HMAC_SHA256'."), ex.Message);
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
                    PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = Documents.PartitionKind.Hash },
                    ClientEncryptionPolicy = new ClientEncryptionPolicy(pathsList)
                };

                Assert.Fail("Creating ContainerProperties should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Duplicate Path found: /path1."), ex.Message);
            }

            try
            {
                Collection<ClientEncryptionIncludedPath> pathsToEncryptWithPartitionKey = new Collection<ClientEncryptionIncludedPath>()
                {
                    new ClientEncryptionIncludedPath()
                    {
                        Path = partitionKeyPath,
                        ClientEncryptionKeyId = "dekId1",
                        EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                        EncryptionType = "Randomized"
                    },
                    new ClientEncryptionIncludedPath()
                    {
                        Path = "/path1",
                        ClientEncryptionKeyId = "dekId1",
                        EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                        EncryptionType = "Deterministic"
                    },
                };

                ContainerProperties setting = new ContainerProperties()
                {
                    Id = containerName,
                    PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = Documents.PartitionKind.Hash },
                    ClientEncryptionPolicy = new ClientEncryptionPolicy(includedPaths: pathsToEncryptWithPartitionKey, policyFormatVersion: 2)
                };

                await this.cosmosDatabase.CreateContainerAsync(setting);
                Assert.Fail("Creating container should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Path: /users which is part of the partition key has to be encrypted with Deterministic type Encryption."), ex.Message);
            }

            try
            {
                Collection<ClientEncryptionIncludedPath> pathsToEncryptWithPartitionKey = new Collection<ClientEncryptionIncludedPath>()
                {
                    new ClientEncryptionIncludedPath()
                    {
                        Path = partitionKeyPath,
                        ClientEncryptionKeyId = "dekId1",
                        EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                        EncryptionType = "Deterministic"
                    },
                    new ClientEncryptionIncludedPath()
                    {
                        Path = "/id",
                        ClientEncryptionKeyId = "dekId1",
                        EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                        EncryptionType = "Randomized"
                    },
                };

                ContainerProperties setting = new ContainerProperties()
                {
                    Id = containerName,
                    PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = Documents.PartitionKind.Hash },
                    ClientEncryptionPolicy = new ClientEncryptionPolicy(includedPaths: pathsToEncryptWithPartitionKey, policyFormatVersion: 2)
                };

                await this.cosmosDatabase.CreateContainerAsync(setting);
                Assert.Fail("Creating container should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Only Deterministic encryption type is supported for path: /id."), ex.Message);
            }

            // failure due to policy format version 1. for Pk and Id
            try
            {
                Collection<ClientEncryptionIncludedPath> pathsToEncryptWithPartitionKey = new Collection<ClientEncryptionIncludedPath>()
                {
                    new ClientEncryptionIncludedPath()
                    {
                        Path = partitionKeyPath,
                        ClientEncryptionKeyId = "dekId1",
                        EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                        EncryptionType = "Deterministic"
                    },
                };

                ContainerProperties setting = new ContainerProperties()
                {
                    Id = containerName,
                    PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = Documents.PartitionKind.Hash },
                    ClientEncryptionPolicy = new ClientEncryptionPolicy(pathsToEncryptWithPartitionKey)
                };

                await this.cosmosDatabase.CreateContainerAsync(setting);
                Assert.Fail("Creating container should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Path: /users which is part of the partition key cannot be encrypted with PolicyFormatVersion: 1. Please use PolicyFormatVersion: 2."), ex.Message);
            }

            try
            {
                Collection<ClientEncryptionIncludedPath> pathsToEncryptWithPartitionKey = new Collection<ClientEncryptionIncludedPath>()
                {
                    new ClientEncryptionIncludedPath()
                    {
                        Path = "/id",
                        ClientEncryptionKeyId = "dekId1",
                        EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                        EncryptionType = "Deterministic"
                    },
                };

                ContainerProperties setting = new ContainerProperties()
                {
                    Id = containerName,
                    PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = Documents.PartitionKind.Hash },
                    ClientEncryptionPolicy = new ClientEncryptionPolicy(pathsToEncryptWithPartitionKey)
                };

                await this.cosmosDatabase.CreateContainerAsync(setting);
                Assert.Fail("Creating container should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Path: /id cannot be encrypted with PolicyFormatVersion: 1. Please use PolicyFormatVersion: 2."), ex.Message);
            }

            // hierarchical partition keys
            try
            {
                Collection<ClientEncryptionIncludedPath> pathsToEncryptWithPartitionKey = new Collection<ClientEncryptionIncludedPath>()
                {
                    new ClientEncryptionIncludedPath()
                    {
                        Path = "/id",
                        ClientEncryptionKeyId = "dekId1",
                        EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                        EncryptionType = "Randomized"
                    },
                };

                ContainerProperties setting = new ContainerProperties()
                {
                    Id = containerName,
                    PartitionKeyPaths = new Collection<string> { "/path1", "/id" },
                    ClientEncryptionPolicy = new ClientEncryptionPolicy(includedPaths: pathsToEncryptWithPartitionKey, policyFormatVersion: 2)
                };

                await this.cosmosDatabase.CreateContainerAsync(setting);
                Assert.Fail("Creating container should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Only Deterministic encryption type is supported for path: /id."), ex.Message);
            }

            // hierarchical partition keys
            try
            {
                Collection<ClientEncryptionIncludedPath> pathsToEncryptWithPartitionKey = new Collection<ClientEncryptionIncludedPath>()
                {
                    new ClientEncryptionIncludedPath()
                    {
                        Path = partitionKeyPath,
                        ClientEncryptionKeyId = "dekId1",
                        EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                        EncryptionType = "Randomized"
                    },
                };

                ContainerProperties setting = new ContainerProperties()
                {
                    Id = containerName,
                    PartitionKeyPaths = new Collection<string> { partitionKeyPath, "/path1" },
                    ClientEncryptionPolicy = new ClientEncryptionPolicy(includedPaths: pathsToEncryptWithPartitionKey, policyFormatVersion: 2)
                };

                await this.cosmosDatabase.CreateContainerAsync(setting);
                Assert.Fail("Creating container should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Path: /users which is part of the partition key has to be encrypted with Deterministic type Encryption."), ex.Message);
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

#if PREVIEW
        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Validate if the child partition key is part of the parent feed range
        ///   Given the parent feed range
        ///   And a child partition key
        ///   When the child partition key is compared to the parent feed range
        ///   Then determine whether the child partition key is part of the parent feed range
        /// ]]>
        /// </summary>
        /// <param name="parentMinimum">The starting value of the parent feed range.</param>
        /// <param name="parentMaximum">The ending value of the parent feed range.</param>
        /// <param name="expectedIsFeedRangePartOfAsync">Indicates whether the child partition key is expected to be part of the parent feed range (true if it is, false if it is not).</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow("", "FFFFFFFFFFFFFFFF", true, DisplayName = "Full range is subset")]
        [DataRow("3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, DisplayName = "Range 3FFFFFFFFFFFFFFF-7FFFFFFFFFFFFFFF is not subset")]
        [Description("Validate if the child partition key is part of the parent feed range.")]
        public async Task GivenFeedRangeChildPartitionKeyIsPartOfParentFeedRange(
            string parentMinimum,
            string parentMaximum,
            bool expectedIsFeedRangePartOfAsync)
        {
            Container container = default;

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk");

                container = containerResponse.Container;

                PartitionKey partitionKey = new("WA");
                FeedRange feedRange = FeedRange.FromPartitionKey(partitionKey);

                bool actualIsFeedRangePartOfAsync = await container.IsFeedRangePartOfAsync(
                    parentFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>(parentMinimum, parentMaximum, true, false)),
                    childFeedRange: feedRange,
                    cancellationToken: CancellationToken.None);

                Assert.AreEqual(expected: expectedIsFeedRangePartOfAsync, actual: actualIsFeedRangePartOfAsync);
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
            finally
            {
                if (container != null)
                {
                    await container.DeleteContainerAsync();
                }
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Validate if the child hierarchical partition key is part of the parent feed range
        ///   Given the parent feed range
        ///   And a child hierarchical partition key
        ///   When the child hierarchical partition key is compared to the parent feed range
        ///   Then determine whether the child hierarchical partition key is part of the parent feed range
        /// ]]>
        /// </summary>
        /// <param name="parentMinimum">The starting value of the parent feed range.</param>
        /// <param name="parentMaximum">The ending value of the parent feed range.</param>
        /// <param name="expectedIsFeedRangePartOfAsync">A boolean value indicating whether the child hierarchical partition key is expected to be part of the parent feed range (true if it is, false if it is not).</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow("", "FFFFFFFFFFFFFFFF", true, DisplayName = "Full range")]
        [DataRow("3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, DisplayName = "Made-up range 3FFFFFFFFFFFFFFF-7FFFFFFFFFFFFFFF")]
        [Description("Validate if the child hierarchical partition key is part of the parent feed range.")]
        public async Task GivenFeedRangeChildHierarchicalPartitionKeyIsPartOfParentFeedRange(
            string parentMinimum,
            string parentMaximum,
            bool expectedIsFeedRangePartOfAsync)
        {
            Container container = default;

            try
            {
                ContainerProperties containerProperties = new ContainerProperties()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKeyPaths = new Collection<string> { "/pk", "/id" }
                };

                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerProperties);

                container = containerResponse.Container;

                PartitionKey partitionKey = new PartitionKeyBuilder()
                    .Add("WA")
                    .Add(Guid.NewGuid().ToString())
                    .Build();

                FeedRange feedRange = FeedRange.FromPartitionKey(partitionKey);

                bool actualIsFeedRangePartOfAsync = await container.IsFeedRangePartOfAsync(
                    parentFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>(parentMinimum, parentMaximum, true, false)),
                    childFeedRange: feedRange,
                    cancellationToken: CancellationToken.None);

                Assert.AreEqual(expected: expectedIsFeedRangePartOfAsync, actual: actualIsFeedRangePartOfAsync);
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
            finally
            {
                if (container != null)
                {
                    await container.DeleteContainerAsync();
                }
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentNullException
        ///
        /// Scenario: Validate that an ArgumentNullException is thrown when the child feed range is null
        ///   Given the parent feed range is defined
        ///   And the child feed range is null
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentNullException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentNullExceptionWhenChildFeedRangeIsNull()
        {
            FeedRange feedRange = default;

            await this.GivenInvalidChildFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentNullException>(
                feedRange: feedRange,
                expectedMessage: $"Argument cannot be null.");
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentNullException
        ///
        /// Scenario: Validate that an ArgumentNullException is thrown when the child feed range has no JSON representation
        ///   Given the parent feed range is defined
        ///   And the child feed range has no JSON representation
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentNullException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentNullExceptionWhenChildFeedRangeHasNoJson()
        {
            FeedRange feedRange = Mock.Of<FeedRange>();

            await this.GivenInvalidChildFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentNullException>(
                feedRange: feedRange,
                expectedMessage: $"Value cannot be null. (Parameter 'value')");
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentException
        ///
        /// Scenario: Validate that an ArgumentException is thrown when the child feed range has invalid JSON representation
        ///   Given the parent feed range is defined
        ///   And the child feed range has an invalid JSON representation
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentExceptionWhenChildFeedRangeHasInvalidJson()
        {
            Mock<FeedRange> mockFeedRange = new Mock<FeedRange>(MockBehavior.Strict);
            mockFeedRange.Setup(feedRange => feedRange.ToJsonString()).Returns("<xml />");
            FeedRange feedRange = mockFeedRange.Object;

            await this.GivenInvalidChildFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentException>(
                feedRange: feedRange,
                expectedMessage: $"The provided string '<xml />' does not represent any known format.");
        }

        private async Task GivenInvalidChildFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<TExceeption>(
            FeedRange feedRange,
            string expectedMessage)
            where TExceeption : Exception
        {
            Container container = default;

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk");

                container = containerResponse.Container;

                TExceeption exception = await Assert.ThrowsExceptionAsync<TExceeption>(
                    async () => await container.IsFeedRangePartOfAsync(
                        parentFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>("", "FFFFFFFFFFFFFFFF", true, false)),
                        childFeedRange: feedRange,
                        cancellationToken: CancellationToken.None));

                Assert.IsNotNull(exception);
                Assert.IsTrue(exception.Message.Contains(expectedMessage));
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
            finally
            {
                if (container != null)
                {
                    await container.DeleteContainerAsync();
                }
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentNullException
        ///
        /// Scenario: Validate that an ArgumentNullException is thrown when the parent feed range is null
        ///   Given the parent feed range is null
        ///   And the child feed range is defined
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentNullException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentNullExceptionWhenParentFeedRangeIsNull()
        {
            FeedRange feedRange = default;

            await this.GivenInvalidParentFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentNullException>(
                feedRange: feedRange,
                expectedMessage: $"Argument cannot be null.");
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentNullException
        ///
        /// Scenario: Validate that an ArgumentNullException is thrown when the parent feed range has no JSON representation
        ///   Given the parent feed range has no JSON representation
        ///   And the child feed range is defined
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentNullException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentNullExceptionWhenParentFeedRangeHasNoJson()
        {
            FeedRange feedRange = Mock.Of<FeedRange>();

            await this.GivenInvalidParentFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentNullException>(
                feedRange: feedRange,
                expectedMessage: $"Value cannot be null. (Parameter 'value')");
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentException
        ///
        /// Scenario: Validate that an ArgumentException is thrown when the parent feed range has an invalid JSON representation
        ///   Given the parent feed range has an invalid JSON representation
        ///   And the child feed range is defined
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentExceptionWhenParentFeedRangeHasInvalidJson()
        {
            Mock<FeedRange> mockFeedRange = new Mock<FeedRange>(MockBehavior.Strict);
            mockFeedRange.Setup(feedRange => feedRange.ToJsonString()).Returns("<xml />");
            FeedRange feedRange = mockFeedRange.Object;

            await this.GivenInvalidParentFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentException>(
                feedRange: feedRange,
                expectedMessage: $"The provided string '<xml />' does not represent any known format.");
        }

        private async Task GivenInvalidParentFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<TException>(FeedRange feedRange, string expectedMessage)
            where TException : Exception
        {
            Container container = default;

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk");

                container = containerResponse.Container;

                TException exception = await Assert.ThrowsExceptionAsync<TException>(
                    async () => await container.IsFeedRangePartOfAsync(
                        parentFeedRange: feedRange,
                        childFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>("", "3FFFFFFFFFFFFFFF", true, false)),
                        cancellationToken: CancellationToken.None));

                Assert.IsNotNull(exception);
                Assert.IsTrue(exception.Message.Contains(expectedMessage));
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
            finally
            {
                if (container != null)
                {
                    await container.DeleteContainerAsync();
                }
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is or is not part of the parent feed range when both child's and parent's isMaxInclusive can be set to true or false
        ///   Given the parent feed range with isMaxInclusive set to true or false
        ///   And the child feed range with isMaxInclusive set to true or false
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is either part of or not part of the parent feed range
        /// ]]>
        /// </summary>
        /// <param name="childMinimum">The starting value of the child feed range.</param>
        /// <param name="childMaximum">The ending value of the child feed range.</param>
        /// <param name="childIsMaxInclusive">Specifies whether the maximum value of the child feed range is inclusive.</param>
        /// <param name="parentMinimum">The starting value of the parent feed range.</param>
        /// <param name="parentMaximum">The ending value of the parent feed range.</param>
        /// <param name="parentIsMaxInclusive">Specifies whether the maximum value of the parent feed range is inclusive.</param>
        /// <param name="expectedIsFeedRangePartOfAsync">Indicates whether the child feed range is expected to be a subset of the parent feed range.</param>
        /// <returns></returns>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DynamicData(nameof(CosmosContainerTests.FeedRangeChildPartOfParentWhenBothChildAndParentIsMaxInclusiveTrue), DynamicDataSourceType.Method)]
        [DynamicData(nameof(CosmosContainerTests.FeedRangeChildNotPartOfParentWhenBothChildAndParentIsMaxInclusiveTrue), DynamicDataSourceType.Method)]
        [DynamicData(nameof(CosmosContainerTests.FeedRangeChildPartOfParentWhenChildIsMaxInclusiveFalseAndParentIsMaxInclusiveTrue), DynamicDataSourceType.Method)]
        [DynamicData(nameof(CosmosContainerTests.FeedRangeChildNotPartOfParentWhenChildIsMaxInclusiveFalseAndParentIsMaxInclusiveTrue), DynamicDataSourceType.Method)]
        [DynamicData(nameof(CosmosContainerTests.FeedRangeChildNotPartOfParentWhenBothIsMaxInclusiveAreFalse), DynamicDataSourceType.Method)]
        [DynamicData(nameof(CosmosContainerTests.FeedRangeChildNotPartOfParentWhenChildAndParentIsMaxInclusiveAreFalse), DynamicDataSourceType.Method)]
        [DynamicData(nameof(CosmosContainerTests.FeedRangeChildPartOfParentWhenChildIsMaxInclusiveTrueAndParentIsMaxInclusiveFalse), DynamicDataSourceType.Method)]
        [DynamicData(nameof(CosmosContainerTests.FeedRangeChildNotPartOfParentWhenChildIsMaxInclusiveTrueAndParentIsMaxInclusiveFalse), DynamicDataSourceType.Method)]
        [Description("Child feed range is or is not part of the parent feed range when both child's and parent's isMaxInclusive can be set to true or false.")]
        public async Task GivenFeedRangeChildPartOfOrNotPartOfParentWhenBothIsMaxInclusiveCanBeTrueOrFalseTestAsync(
            string childMinimum,
            string childMaximum,
            bool childIsMaxInclusive,
            string parentMinimum,
            string parentMaximum,
            bool parentIsMaxInclusive,
            bool expectedIsFeedRangePartOfAsync)
        {
            Container container = default;

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk");

                container = containerResponse.Container;

                bool actualIsFeedRangePartOfAsync = await container.IsFeedRangePartOfAsync(
                    parentFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>(parentMinimum, parentMaximum, true, parentIsMaxInclusive)),
                    childFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>(childMinimum, childMaximum, true, childIsMaxInclusive)),
                    cancellationToken: CancellationToken.None);

                Assert.AreEqual(expected: expectedIsFeedRangePartOfAsync, actual: actualIsFeedRangePartOfAsync);
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
            finally
            {
                if (container != null)
                {
                    await container.DeleteContainerAsync();
                }
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is not part of the parent feed range with both isMaxInclusive set to false
        ///   Given the parent feed range with isMaxInclusive set to false
        ///   And the child feed range with isMaxInclusive set to false
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is part of the parent feed range
        ///   
        /// Arguments: string childMinimum, string childMaximum, bool childIsMaxInclusive, string parentMinimum, string parentMaximum, bool parentIsMaxInclusive, bool expectedIsFeedRangePartOfAsync
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenBothIsMaxInclusiveAreFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // child is subset of the parent
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // child is subset of the parent
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // child is subset of the parent
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // child is subset of the parent
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // child is subset of the parent
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // child is subset of the parent
            yield return new object[] { "5999999999999999", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // child is subset of the parent
            yield return new object[] { "6666666666666666", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // child is subset of the parent
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // child is subset of the parent
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "3FFFFFFFFFFFFFFF", false, true }; // child is same as the parent, which makes it a subset
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is not part of the parent feed range with both child’s and parent’s isMaxInclusive set to false
        ///   Given the parent feed range with isMaxInclusive set to false
        ///   And the child feed range with isMaxInclusive set to false
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is not part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenChildAndParentIsMaxInclusiveAreFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // child is not a subset of parent
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, false }; // child is not a subset of parent
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, false }; // child is not a subset of parent
            yield return new object[] { "", "3333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // child is not a subset of parent
            yield return new object[] { "3333333333333333", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // child is not a subset of parent
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // child is overlap, but not a subset of the parent
            yield return new object[] { "", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // child is overlap, but not a subset of the parent
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is part of the parent feed range with the child’s isMaxInclusive set to true and the parent’s isMaxInclusive set to false
        ///   Given the parent feed range with isMaxInclusive set to false
        ///   And the child feed range with isMaxInclusive set to true
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildPartOfParentWhenChildIsMaxInclusiveTrueAndParentIsMaxInclusiveFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "3FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "5999999999999999", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "6666666666666666", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is not part of the parent feed range with the child’s isMaxInclusive set to true and the parent’s isMaxInclusive set to false
        ///   Given the parent feed range with isMaxInclusive set to false
        ///   And the child feed range with isMaxInclusive set to true
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is not part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenChildIsMaxInclusiveTrueAndParentIsMaxInclusiveFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "", "3333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "3333333333333333", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is part of the parent feed range with the child’s isMaxInclusive set to false and the parent’s isMaxInclusive set to true
        ///   Given the parent feed range with isMaxInclusive set to true
        ///   And the child feed range with isMaxInclusive set to false
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildPartOfParentWhenChildIsMaxInclusiveFalseAndParentIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "3FFFFFFFFFFFFFFF", true, true }; // child is same as the parent, which makes it a subset
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "5999999999999999", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "6666666666666666", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is not part of the parent feed range with the child’s isMaxInclusive set to false and the parent’s isMaxInclusive set to true
        ///   Given the parent feed range with isMaxInclusive set to true
        ///   And the child feed range with isMaxInclusive set to false
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is not part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenChildIsMaxInclusiveFalseAndParentIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // child is not a subset of parent
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, false }; // child is not a subset of parent
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, false }; // child is not a subset of parent
            yield return new object[] { "", "3333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // child is not a subset of parent
            yield return new object[] { "3333333333333333", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // child is not a subset of parent
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // child is overlap, but not a subset of the parent
            yield return new object[] { "", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // child is overlap, but not a subset of the parent
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is part of the parent feed range with both the child’s and parent’s isMaxInclusive set to true
        ///   Given the parent feed range with isMaxInclusive set to true
        ///   And the child feed range with isMaxInclusive set to true
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildPartOfParentWhenBothChildAndParentIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "3FFFFFFFFFFFFFFF", true, true }; // child is same as the parent, which makes it a subset
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "5999999999999999", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "6666666666666666", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // child is subset of the parent
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is not part of the parent feed range with both the child’s and parent’s isMaxInclusive set to true
        ///   Given the parent feed range with isMaxInclusive set to true
        ///   And the child feed range with isMaxInclusive set to true
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is not part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenBothChildAndParentIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // child is not a subset of parent
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, false }; // child is not a subset of parent
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, false }; // child is not a subset of parent
            yield return new object[] { "", "3333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // child is not a subset of parent
            yield return new object[] { "3333333333333333", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // child is not a subset of parent
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // child is overlap, but not a subset of the parent
            yield return new object[] { "", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // child is overlap, but not a subset of the parent
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentOutOfRangeException
        ///
        /// Scenario: Validate if an ArgumentOutOfRangeException is thrown when the child feed range is compared to the parent feed range with the parent's IsMinInclusive set to false
        ///   Given the parent feed range with IsMinInclusive set to false
        ///   And the child feed range with a valid range
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentOutOfRangeException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentOutOfRangeExceptionWhenChildComparedToParentWithParentIsMinInclusiveFalse()
        {
            await this.FeedRangeThrowsArgumentOutOfRangeExceptionWhenIsMinInclusiveFalse(
                parentFeedRange: new Documents.Routing.Range<string>("", "3FFFFFFFFFFFFFFF", false, true),
                childFeedRange: new Documents.Routing.Range<string>("", "FFFFFFFFFFFFFFFF", true, false));
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentOutOfRangeException
        ///
        /// Scenario: Validate if an ArgumentOutOfRangeException is thrown when the child feed range is compared to the parent feed range with the child's IsMinInclusive set to false
        ///   Given the parent feed range with IsMinInclusive set to false
        ///   And the child feed range with a valid range
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentOutOfRangeException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentOutOfRangeExceptionWhenChildComparedToParentWithChildIsMinInclusiveFalse()
        {
            await this.FeedRangeThrowsArgumentOutOfRangeExceptionWhenIsMinInclusiveFalse(
                parentFeedRange: new Documents.Routing.Range<string>("", "3FFFFFFFFFFFFFFF", true, false),
                childFeedRange: new Documents.Routing.Range<string>("", "FFFFFFFFFFFFFFFF", false, true));
        }

        private async Task FeedRangeThrowsArgumentOutOfRangeExceptionWhenIsMinInclusiveFalse(
            Documents.Routing.Range<string> parentFeedRange,
            Documents.Routing.Range<string> childFeedRange)
        {
            Container container = default;

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk");

                container = containerResponse.Container;

                ArgumentOutOfRangeException exception = await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                    async () => await container
                        .IsFeedRangePartOfAsync(
                            parentFeedRange: new FeedRangeEpk(parentFeedRange),
                            childFeedRange: new FeedRangeEpk(childFeedRange),
                            cancellationToken: CancellationToken.None));

                Assert.IsNotNull(exception);
                Assert.IsTrue(exception.Message.Contains("IsMinInclusive must be true."));
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
            finally
            {
                if (container != null)
                {
                    await container.DeleteContainerAsync();
                }
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Subset
        ///
        /// Scenario: Validate whether the child range is a subset of the parent range for various cases.
        ///   Given various parent and child feed ranges
        ///   When the child range is checked if it is a subset of the parent range
        ///   Then the actualIsSubset should either be true or false depending on the ranges
        /// ]]>
        /// </summary>
        /// <param name="parentMinimum">The starting value of the parent range.</param>
        /// <param name="parentMaximum">The ending value of the parent range.</param>
        /// <param name="childMinimum">The starting value of the child range.</param>
        /// <param name="childMaximum">The ending value of the child range.</param>
        /// <param name="expectedIsSubset">The expected actualIsSubset: true if the child is a subset, false otherwise.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow("A", "Z", "B", "Y", true, DisplayName = "Child B-Y is a perfect subset of parent A-Z")]
        [DataRow("A", "Z", "A", "Z", true, DisplayName = "Child A-Z equals parent A-Z")]
        [DataRow("A", "Z", "@", "Y", false, DisplayName = "Child @-Y has min out of parent A-Z")]
        [DataRow("A", "Z", "B", "[", false, DisplayName = "Child B-[ has max out of parent A-Z")]
        [DataRow("A", "Z", "@", "[", false, DisplayName = "Child @-[ is completely outside parent A-Z")]
        [DataRow("A", "Z", "@", "Z", false, DisplayName = "Child @-Z has max equal to parent but min out of range")]
        [DataRow("A", "Z", "A", "[", false, DisplayName = "Child A-[ has min equal to parent but max out of range")]
        [DataRow("A", "Z", "", "", false, DisplayName = "Empty child range")]
        [DataRow("", "", "B", "Y", false, DisplayName = "Empty parent range with non-empty child range")]
        [DataRow("A", "Z", "B", "Y", true, DisplayName = "Parent A-Z encapsulates child B-Y")]
        public void ValidateChildRangeIsSubsetOfParentForVariousCasesTest(string parentMinimum, string parentMaximum, string childMinimum, string childMaximum, bool expectedIsSubset)
        {
            Documents.Routing.Range<string> parentRange = new Documents.Routing.Range<string>(parentMinimum, parentMaximum, true, true);
            Documents.Routing.Range<string> childRange = new Documents.Routing.Range<string>(childMinimum, childMaximum, true, true);

            bool actualIsSubset = ContainerCore.IsSubset(parentRange, childRange);

            Assert.AreEqual(expected: expectedIsSubset, actual: actualIsSubset);
        }

        /// <summary>
        /// Validates if all ranges in the list have consistent inclusivity for both IsMinInclusive and IsMaxInclusive.
        /// Throws InvalidOperationException if any inconsistencies are found.
        ///
        /// <example>
        /// <![CDATA[
        /// Feature: Validate range inclusivity
        ///
        ///   Scenario: All ranges are consistent
        ///     Given a list of ranges where all have the same IsMinInclusive and IsMaxInclusive values
        ///     When the inclusivity is validated
        ///     Then no exception is thrown
        ///
        ///   Scenario: Inconsistent MinInclusive values
        ///     Given a list of ranges where IsMinInclusive values differ
        ///     When the inclusivity is validated
        ///     Then an InvalidOperationException is thrown
        ///
        ///   Scenario: Inconsistent MaxInclusive values
        ///     Given a list of ranges where IsMaxInclusive values differ
        ///     When the inclusivity is validated
        ///     Then an InvalidOperationException is thrown
        /// ]]>
        /// </example>
        /// </summary>
        /// <param name="shouldNotThrow">Indicates if the test should pass without throwing an exception.</param>
        /// <param name="isMin1">IsMinInclusive value for first range.</param>
        /// <param name="isMax1">IsMaxInclusive value for first range.</param>
        /// <param name="isMin2">IsMinInclusive value for second range.</param>
        /// <param name="isMax2">IsMaxInclusive value for second range.</param>
        /// <param name="isMin3">IsMinInclusive value for third range.</param>
        /// <param name="isMax3">IsMaxInclusive value for third range.</param>
        /// <param name="expectedMessage">The expected exception message.></param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow(true, true, false, true, false, true, false, "", DisplayName = "All ranges consistent")]
        [DataRow(false, true, false, false, false, true, false, "Not all 'IsMinInclusive' or 'IsMaxInclusive' values are the same. IsMinInclusive found: True, False, IsMaxInclusive found: False.", DisplayName = "Inconsistent MinInclusive")]
        [DataRow(false, true, false, true, true, true, false, "Not all 'IsMinInclusive' or 'IsMaxInclusive' values are the same. IsMinInclusive found: True, IsMaxInclusive found: False, True.", DisplayName = "Inconsistent MaxInclusive")]
        [DataRow(false, true, false, false, true, true, false, "Not all 'IsMinInclusive' or 'IsMaxInclusive' values are the same. IsMinInclusive found: True, False, IsMaxInclusive found: False, True.", DisplayName = "Inconsistent Min and Max Inclusive")]
        [DataRow(true, null, null, null, null, null, null, "", DisplayName = "Empty range list")]
        public void EnsureConsistentInclusivityValidatesRangesTest(
            bool shouldNotThrow,
            bool? isMin1,
            bool? isMax1,
            bool? isMin2,
            bool? isMax2,
            bool? isMin3,
            bool? isMax3,
            string expectedMessage)
        {
            List<Documents.Routing.Range<string>> ranges = new List<Documents.Routing.Range<string>>();

            if (isMin1.HasValue && isMax1.HasValue)
            {
                ranges.Add(new Documents.Routing.Range<string>(min: "A", max: "B", isMinInclusive: isMin1.Value, isMaxInclusive: isMax1.Value));
            }

            if (isMin2.HasValue && isMax2.HasValue)
            {
                ranges.Add(new Documents.Routing.Range<string>(min: "C", max: "D", isMinInclusive: isMin2.Value, isMaxInclusive: isMax2.Value));
            }

            if (isMin3.HasValue && isMax3.HasValue)
            {
                ranges.Add(new Documents.Routing.Range<string>(min: "E", max: "F", isMinInclusive: isMin3.Value, isMaxInclusive: isMax3.Value));
            }

            InvalidOperationException exception = default;

            if (!shouldNotThrow)
            {
                exception = Assert.ThrowsException<InvalidOperationException>(() => ContainerCore.EnsureConsistentInclusivity(ranges));

                Assert.IsNotNull(exception);
                Assert.AreEqual(expected: expectedMessage, actual: exception.Message);

                return;
            }

            Assert.IsNull(exception);
        }
#endif
    }
}

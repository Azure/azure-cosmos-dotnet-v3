//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Tests for IndexingPolicy.
    /// </summary>
    /// <remarks>
    /// Second answer was very useful: https://stackoverflow.com/questions/14479074/c-sharp-reflection-load-assembly-and-invoke-a-method-if-it-exists
    /// </remarks>
    [TestClass]
    public class IndexingPolicyTests
    {
        /// <summary>
        /// The document client stored for all the tests.
        /// </summary>
        private static readonly CosmosClient cosmosClient = TestCommon.CreateCosmosClient(true);

        /// <summary>
        /// The database used for all the tests.
        /// </summary>
        private static readonly Cosmos.Database database = cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString()).Result;

        private static readonly IndexingPolicyEqualityComparer indexingPolicyEqualityComparer = new IndexingPolicyEqualityComparer();

        private static readonly Dictionary<Version, Assembly> documentClientAssemblyDictionary = new Dictionary<Version, Assembly>();

        /// <summary>
        /// https://docs.microsoft.com/en-us/azure/cosmos-db/sql-api-sdk-dotnet#1.2.0
        /// </summary>
        private static readonly Version IndexingPolicyV2 = new Version(1, 2, 0);

        /// <summary>
        /// https://docs.microsoft.com/en-us/azure/cosmos-db/sql-api-sdk-dotnet#1.3.0
        /// </summary>
        private static readonly Version Spatial = new Version(1, 3, 0);

        /// <summary>
        /// https://docs.microsoft.com/en-us/azure/cosmos-db/sql-api-sdk-dotnet#1.10.0
        /// </summary>
        private static readonly Version GeoFencing = new Version(1, 10, 0);

        private static readonly Version AdditionalIndexes = new Version(2, 4, 0);

        /// <summary>
        /// Reserved version for the latest SDK that got released (note that only the local version can be newer).
        /// </summary>
        private static readonly Version Latest = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue - 1);

        /// <summary>
        /// Reserverd version for the sdk that is locally checked in (note that nothing can be newer than this).
        /// </summary>
        private static readonly Version Local = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);

        private static readonly Version[] SdkVersionsToValidate = new Version[]
        {
            IndexingPolicyV2,
            Spatial,
            GeoFencing,
            AdditionalIndexes,
            Latest,
            Local
        };

        private static readonly string DefaultPath = "/*";


        [TestInitialize]
        public async Task TestInitialize()
        {
            // Put test init code here
            await TestCommon.DeleteAllDatabasesAsync();
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            await TestCommon.DeleteAllDatabasesAsync();
        }

        private static async Task RetryOnNonAssertFailure(Func<Task> function, int retryCount = 5)
        {
            while (retryCount-- > 0)
            {
                try
                {
                    await function();
                    break;
                }
                catch (Exception ex)
                {
                    if (ex.GetType() == typeof(AssertFailedException))
                    {
                        throw;
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        [TestMethod]
        public async Task DefaultIndexingPolicy()
        {
            Cosmos.IndexingPolicy indexingPolicy = IndexingPolicyTests.CreateDefaultIndexingPolicy();
            await IndexingPolicyTests.RoundTripWithLocal(indexingPolicy);
        }

        [TestMethod]
        public async Task RangeHash()
        {
            Cosmos.IndexingPolicy indexingPolicy = IndexingPolicyTests.CreateDefaultIndexingPolicy();
            indexingPolicy.IncludedPaths = new Collection<Cosmos.IncludedPath>()
            {
                new Cosmos.IncludedPath()
                {
                    Path = DefaultPath,
                    Indexes = new Collection<Cosmos.Index>()
                    {
                        new Cosmos.RangeIndex(Cosmos.DataType.Number, -1),
                        new Cosmos.HashIndex(Cosmos.DataType.String, 3),
                    }
                }
            };

            await IndexingPolicyTests.RoundTripWithLocal(indexingPolicy);
        }

        [TestMethod]
        public async Task RangeRange()
        {
            Cosmos.IndexingPolicy indexingPolicy = IndexingPolicyTests.CreateDefaultIndexingPolicy();
            indexingPolicy.IncludedPaths = new Collection<Cosmos.IncludedPath>()
            {
                new Cosmos.IncludedPath()
                {
                    Path = DefaultPath,
                    Indexes = new Collection<Cosmos.Index>()
                    {
                        new Cosmos.RangeIndex(Cosmos.DataType.Number, -1),
                        new Cosmos.RangeIndex(Cosmos.DataType.String, -1),
                    }
                }
            };

            await IndexingPolicyTests.RoundTripWithLocal(indexingPolicy);
        }

        [TestMethod]
        public async Task HashHash()
        {
            Cosmos.IndexingPolicy indexingPolicy = IndexingPolicyTests.CreateDefaultIndexingPolicy();
            indexingPolicy.IncludedPaths = new Collection<Cosmos.IncludedPath>()
            {
                new Cosmos.IncludedPath()
                {
                    Path = DefaultPath,
                    Indexes = new Collection<Cosmos.Index>()
                    {
                        new Cosmos.HashIndex(Cosmos.DataType.Number, -1),
                        new Cosmos.HashIndex(Cosmos.DataType.String, -1),
                    }
                }
            };

            await IndexingPolicyTests.RoundTripWithLocal(indexingPolicy);
        }

        [TestMethod]
        public async Task HashRange()
        {
            Cosmos.IndexingPolicy indexingPolicy = IndexingPolicyTests.CreateDefaultIndexingPolicy();
            indexingPolicy.IncludedPaths = new Collection<Cosmos.IncludedPath>()
            {
                new Cosmos.IncludedPath()
                {
                    Path = DefaultPath,
                    Indexes = new Collection<Cosmos.Index>()
                    {
                        new Cosmos.HashIndex(Cosmos.DataType.Number, 3),
                        new Cosmos.RangeIndex(Cosmos.DataType.String, -1),
                    }
                }
            };

            await IndexingPolicyTests.RoundTripWithLocal(indexingPolicy);
        }

        [TestMethod]
        public async Task SpatialIndex()
        {
            Cosmos.IndexingPolicy indexingPolicy = IndexingPolicyTests.CreateDefaultIndexingPolicy();
            indexingPolicy.IncludedPaths = new Collection<Cosmos.IncludedPath>()
            {
                new Cosmos.IncludedPath()
                {
                    Path = DefaultPath,
                    Indexes = new Collection<Cosmos.Index>()
                    {
                        new Cosmos.SpatialIndex(Cosmos.DataType.Point),
                    }
                }
            };

            await IndexingPolicyTests.RoundTripWithLocal(indexingPolicy);
        }

        [TestMethod]
        public async Task GeoFencingIndex()
        {
            Cosmos.IndexingPolicy indexingPolicy = IndexingPolicyTests.CreateDefaultIndexingPolicy();
            indexingPolicy.IncludedPaths = new Collection<Cosmos.IncludedPath>()
            {
                new  Cosmos.IncludedPath()
                {
                    Path = DefaultPath,
                    Indexes = new Collection<Cosmos.Index>()
                    {
                        new  Cosmos.SpatialIndex(Cosmos.DataType.LineString),
                        new  Cosmos.SpatialIndex(Cosmos.DataType.Point),
                        new  Cosmos.SpatialIndex(Cosmos.DataType.Polygon),
                    }
                }
            };

            await IndexingPolicyTests.RoundTripWithLocal(indexingPolicy);
        }

        [TestMethod]
        public async Task CompositeIndex()
        {
            await RetryOnNonAssertFailure(this.CompositeIndexImplementation);
        }

        private async Task CompositeIndexImplementation()
        {
            Cosmos.IndexingPolicy indexingPolicy = new Cosmos.IndexingPolicy()
            {
                Automatic = true,
                IncludedPaths = new Collection<Cosmos.IncludedPath>()
                {
                    new  Cosmos.IncludedPath()
                    {
                        Path = DefaultPath,
                    }
                },
                ExcludedPaths = new Collection<Cosmos.ExcludedPath>(),
                IndexingMode = Cosmos.IndexingMode.Consistent,
                CompositeIndexes = new Collection<Collection<Cosmos.CompositePath>>()
                {
                    new Collection<Cosmos.CompositePath>()
                    {
                        new  Cosmos.CompositePath()
                        {
                            Path = "/name",
                            Order =  Cosmos.CompositePathSortOrder.Ascending
                        },
                        new  Cosmos.CompositePath()
                        {
                            Path = "/age",
                            Order =  Cosmos.CompositePathSortOrder.Descending
                        }
                    }
                }
            };

            await IndexingPolicyTests.RoundTripWithLocal(indexingPolicy);
        }

        [TestMethod]
        public async Task SpatialBoundingBox()
        {
            await RetryOnNonAssertFailure(this.SpatialBoundingBoxImplementation);
        }

        private async Task SpatialBoundingBoxImplementation()
        {
            Cosmos.IndexingPolicy indexingPolicy = new Cosmos.IndexingPolicy()
            {
                Automatic = true,
                IncludedPaths = new Collection<Cosmos.IncludedPath>()
                {
                    new  Cosmos.IncludedPath()
                    {
                        Path = DefaultPath,
                    }
                },
                ExcludedPaths = new Collection<Cosmos.ExcludedPath>(),
                IndexingMode = Cosmos.IndexingMode.Consistent,
                SpatialIndexes = new Collection<Cosmos.SpatialPath>()
                {
                    new Cosmos.SpatialPath
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
                    },
                     new Cosmos.SpatialPath
                     {
                        Path = "/spatial/*",
                        SpatialTypes = new Collection<Cosmos.SpatialType>()
                        {
                            Cosmos.SpatialType.Point
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
            };

            Cosmos.GeospatialConfig geospatialConfig = new Cosmos.GeospatialConfig
            {
                GeospatialType = Cosmos.GeospatialType.Geometry
            };
            await IndexingPolicyTests.RoundTripWithLocal(indexingPolicy, geospatialConfig);
        }

        [TestMethod]
        public async Task ExcludeAll()
        {
            Cosmos.IndexingPolicy indexingPolicy = IndexingPolicyTests.CreateDefaultIndexingPolicy();
            indexingPolicy.IncludedPaths = new Collection<Cosmos.IncludedPath>();
            indexingPolicy.ExcludedPaths = new Collection<Cosmos.ExcludedPath>()
            {
                new Cosmos.ExcludedPath()
                {
                    Path = "/*",
                }
            };

            await IndexingPolicyTests.RoundTripWithLocal(indexingPolicy);
        }

        [TestMethod]
        public async Task ExcludedPaths()
        {
            Cosmos.IndexingPolicy indexingPolicy = IndexingPolicyTests.CreateDefaultIndexingPolicy();
            indexingPolicy.IncludedPaths = new Collection<Cosmos.IncludedPath>()
            {
                new  Cosmos.IncludedPath()
                {
                    Path = "/*",
                }
            };
            indexingPolicy.ExcludedPaths = new Collection<Cosmos.ExcludedPath>()
            {
                new  Cosmos.ExcludedPath()
                {
                    Path = "/nonIndexedContent/*",
                }
            };

            await IndexingPolicyTests.RoundTripWithLocal(indexingPolicy);
        }

        [TestMethod]
        public async Task DoubleRoundTrip()
        {
            await cosmosClient.CreateDatabaseIfNotExistsAsync(database.Id);
            Cosmos.IndexingPolicy indexingPolicy = IndexingPolicyTests.CreateDefaultIndexingPolicy();
            ContainerProperties cosmosContainerSettings = new ContainerProperties()
            {
                Id = Guid.NewGuid().ToString(),
                IndexingPolicy = indexingPolicy,
            };

            await IndexingPolicyTests.RoundTripWithLocal(indexingPolicy);
        }

        private static async Task RoundTripWithLocal(Cosmos.IndexingPolicy indexingPolicy, Cosmos.GeospatialConfig geospatialConfig = null)
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            ContainerProperties containerSetting = new ContainerProperties()
            {
                Id = Guid.NewGuid().ToString(),
                IndexingPolicy = indexingPolicy,
                PartitionKey = partitionKeyDefinition,
                GeospatialConfig = geospatialConfig
            };

            Cosmos.Database cosmosDatabase = await cosmosClient.CreateDatabaseIfNotExistsAsync(IndexingPolicyTests.database.Id);
            ContainerResponse cosmosContainerResponse = await cosmosDatabase.CreateContainerAsync(containerSetting);

            Assert.IsTrue(IndexingPolicyTests.indexingPolicyEqualityComparer.Equals(indexingPolicy, containerSetting.IndexingPolicy));
        }

        private static IndexingPolicy GetIndexingPolicyFromAssembly(Assembly assembly, object indexingPolicy)
        {
            Type indexingPolicyType = IndexingPolicyTests.GetTypeFromAssembly(assembly, "IndexingPolicy");
            return JsonConvert.DeserializeObject<IndexingPolicy>(JsonConvert.SerializeObject(indexingPolicy, indexingPolicyType, null));
        }

        private static async Task<dynamic> CreateDocumentClientFromAssembly(Assembly assembly)
        {
            // Define parameters for class constructor 'DocumentClient(Uri serviceEndpoint, string authKeyOrResourceToken)'
            object[] documentClientConstructorParameters = new object[]
            {
                new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                ConfigurationManager.AppSettings["MasterKey"],
                null,
                null
            };

            dynamic documentClient = IndexingPolicyTests.CreateTypeFromAssembly<DocumentClient>(assembly, documentClientConstructorParameters);
            await documentClient.OpenAsync();
            return documentClient;
        }

        private static Type GetTypeFromAssembly(Assembly assembly, string typeName)
        {
            string qualifiedName = $"{assembly.GetName().Name}.{typeName}";
            Type type = assembly.GetTypes().Where((candidateType) => candidateType.Name == (typeName)).FirstOrDefault();
            if (type == null)
            {
                Assert.Fail($"Failed to get the type:{typeName} from assembly:{assembly.GetName().Name}");
            }

            return type;
        }

        private static object CreateTypeFromAssembly<T>(Assembly assembly, params object[] parameters)
        {
            Type typeFromAssembly = IndexingPolicyTests.GetTypeFromAssembly(assembly, typeof(T).Name);
            object createdType = Activator.CreateInstance(typeFromAssembly, parameters);
            if (createdType == null)
            {
                Assert.Fail($"Failed to construct type of {nameof(T)} from assembly:{assembly.GetName().Name} using parameters: {parameters}");
            }

            return createdType;
        }

        private static Cosmos.IndexingPolicy CreateDefaultIndexingPolicy()
        {
            return new Cosmos.IndexingPolicy()
            {
                Automatic = true,
                IncludedPaths = new Collection<Cosmos.IncludedPath>()
                {
                    new  Cosmos.IncludedPath()
                    {
                        Path = DefaultPath,
                        Indexes = new Collection<Cosmos.Index>()
                        {
                            new  Cosmos.RangeIndex( Cosmos.DataType.Number, -1),
                            new  Cosmos.RangeIndex( Cosmos.DataType.String, -1),
                        }
                    }
                },
                ExcludedPaths = new Collection<Cosmos.ExcludedPath>(),
                IndexingMode = Cosmos.IndexingMode.Consistent,
            };
        }


        # region EqualityComparers
        private class CompositePathEqualityComparer : IEqualityComparer<CompositePath>
        {
            public bool Equals(CompositePath compositePath1, CompositePath compositePath2)
            {
                if (Object.ReferenceEquals(compositePath1, compositePath2))
                {
                    return true;
                }

                if (compositePath1 == null || compositePath2 == null)
                {
                    return false;
                }

                if (compositePath1.Path == compositePath2.Path && compositePath2.Order == compositePath2.Order)
                {
                    return true;
                }

                return false;
            }

            public int GetHashCode(CompositePath compositePath)
            {
                if (compositePath == null)
                {
                    return 0;
                }

                return compositePath.Path.GetHashCode() ^ compositePath.Order.GetHashCode();
            }
        }

        private class CompositePathsEqualityComparer : IEqualityComparer<HashSet<CompositePath>>
        {
            private static readonly CompositePathEqualityComparer compositePathEqualityComparer = new CompositePathEqualityComparer();
            public bool Equals(HashSet<CompositePath> compositePaths1, HashSet<CompositePath> compositePaths2)
            {
                if (Object.ReferenceEquals(compositePaths1, compositePaths2))
                {
                    return true;
                }

                if (compositePaths1 == null || compositePaths2 == null)
                {
                    return false;
                }

                return compositePaths1.SetEquals(compositePaths2);
            }

            public int GetHashCode(HashSet<CompositePath> obj)
            {
                if (obj == null)
                {
                    return 0;
                }

                int hashCode = 0;
                foreach (CompositePath compositePath in obj)
                {
                    hashCode ^= compositePathEqualityComparer.GetHashCode(compositePath);
                }

                return hashCode;
            }
        }

        private class IndexEqualityComparer : IEqualityComparer<Index>
        {
            public bool Equals(Index index1, Index index2)
            {
                if (Object.ReferenceEquals(index1, index2))
                {
                    return true;
                }

                if (index1 == null || index2 == null)
                {
                    return false;
                }

                if (index1.Kind != index2.Kind)
                {
                    return false;
                }

                switch (index1.Kind)
                {
                    case IndexKind.Hash:
                        if (((HashIndex)index1).Precision != ((HashIndex)index2).Precision)
                        {
                            return false;
                        }

                        if (((HashIndex)index1).DataType != ((HashIndex)index2).DataType)
                        {
                            return false;
                        }
                        break;

                    case IndexKind.Range:
                        if (((RangeIndex)index1).Precision != ((RangeIndex)index2).Precision)
                        {
                            return false;
                        }

                        if (((RangeIndex)index1).DataType != ((RangeIndex)index2).DataType)
                        {
                            return false;
                        }
                        break;

                    case IndexKind.Spatial:
                        if (((SpatialIndex)index1).DataType != ((SpatialIndex)index2).DataType)
                        {
                            return false;
                        }
                        break;

                    default:
                        throw new ArgumentException($"Unexpected Kind: {index1.Kind}");
                }

                return true;
            }

            public int GetHashCode(Index index)
            {
                int hashCode = 0;
                hashCode = hashCode ^ (int)index.Kind;

                switch (index.Kind)
                {
                    case IndexKind.Hash:
                        hashCode = hashCode ^ ((HashIndex)index).Precision ?? 0;
                        hashCode = hashCode ^ ((HashIndex)index).DataType.GetHashCode();
                        break;

                    case IndexKind.Range:
                        hashCode = hashCode ^ ((RangeIndex)index).Precision ?? 0;
                        hashCode = hashCode ^ ((RangeIndex)index).DataType.GetHashCode();
                        break;

                    case IndexKind.Spatial:
                        hashCode = hashCode ^ ((SpatialIndex)index).DataType.GetHashCode();
                        break;

                    default:
                        throw new ArgumentException($"Unexpected Kind: {index.Kind}");
                }

                return hashCode;
            }
        }

        private class IncludedPathEqualityComparer : IEqualityComparer<IncludedPath>
        {
            private static readonly IndexEqualityComparer indexEqualityComparer = new IndexEqualityComparer();
            public bool Equals(IncludedPath includedPath1, IncludedPath includedPath2)
            {
                if (Object.ReferenceEquals(includedPath1, includedPath2))
                {
                    return true;
                }

                if (includedPath1 == null || includedPath2 == null)
                {
                    return false;
                }

                if (includedPath1.Path != includedPath2.Path)
                {
                    return false;
                }

                HashSet<Index> indexes1 = new HashSet<Index>(includedPath1.Indexes, indexEqualityComparer);
                HashSet<Index> indexes2 = new HashSet<Index>(includedPath2.Indexes, indexEqualityComparer);

                return indexes1.SetEquals(indexes2);
            }

            public int GetHashCode(IncludedPath includedPath)
            {
                int hashCode = 0;
                hashCode = hashCode ^ includedPath.Path.GetHashCode();
                foreach (Index index in includedPath.Indexes)
                {
                    hashCode = hashCode ^ indexEqualityComparer.GetHashCode(index);
                }

                return hashCode;
            }
        }

        private class ExcludedPathEqualityComparer : IEqualityComparer<ExcludedPath>
        {
            public bool Equals(ExcludedPath excludedPath1, ExcludedPath excludedPath2)
            {
                if (Object.ReferenceEquals(excludedPath1, excludedPath2))
                {
                    return true;
                }

                if (excludedPath1 == null || excludedPath2 == null)
                {
                    return false;
                }

                return (excludedPath1.Path == excludedPath2.Path);
            }

            public int GetHashCode(ExcludedPath excludedPath1)
            {
                return excludedPath1.Path.GetHashCode();
            }
        }
        #endregion
    }
}
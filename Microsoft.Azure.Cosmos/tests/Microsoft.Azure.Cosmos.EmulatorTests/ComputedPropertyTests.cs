namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using global::Azure;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ComputedPropertyTests
    {
        private static readonly CosmosClient cosmosClient;
        private static readonly Database database;

        private static readonly ComputedProperty LowerName;
        private static readonly ComputedProperty ParentsFullName;
        private static readonly Collection<ComputedProperty> AllComputedProperties;

        private static readonly IndexingPolicy IndexAllComputedProperties_IncludeAll;
        private static readonly IndexingPolicy IndexAllComputedProperties_ExcludeAll;
        private static readonly IndexingPolicy IndexDefault_IncludeAll;
        private static readonly IndexingPolicy IndexDefault_ExcludeAll;

        private static readonly string SelectAllComputedPropertiesQuery;
        private static readonly List<string> AllComputedPropertiesResult;
        private static readonly List<string> EmptyResult;

        static ComputedPropertyTests()
        {
            cosmosClient = TestCommon.CreateCosmosClient();
            database = cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString()).Result;

            LowerName = new ComputedProperty
                {
                    Name = "lowerLastName",
                    Query = "SELECT VALUE LOWER(IS_DEFINED(c.lastName) ? c.lastName : c.parents[0].familyName) FROM c"
                };
            ParentsFullName = new ComputedProperty
                {
                    Name = "parentsFullName",
                    Query = "SELECT VALUE CONCAT(CONCAT(c.parents[0].firstName, ' ', c.lastName), ' & ', CONCAT(c.parents[1].firstName, ' ', c.lastName)) FROM c"
                };
            AllComputedProperties = new Collection<ComputedProperty> { { LowerName }, { ParentsFullName } };

            IndexAllComputedProperties_IncludeAll = new IndexingPolicy
                {
                    IncludedPaths = new Collection<IncludedPath>
                    {
                        { new IncludedPath { Path = $"/{LowerName.Name}/*" } },
                        { new IncludedPath { Path = $"/{ParentsFullName.Name}/*" } },
                        { new IncludedPath { Path = $"/*" } },
                    }
                };
            IndexAllComputedProperties_ExcludeAll = new IndexingPolicy
            {
                IncludedPaths = new Collection<IncludedPath>
                    {
                        { new IncludedPath { Path = $"/{LowerName.Name}/*" } },
                        { new IncludedPath { Path = $"/{ParentsFullName.Name}/*" } },
                    },
                ExcludedPaths = new Collection<ExcludedPath>
                    {
                        { new ExcludedPath { Path = $"/*" } }
                    }
            };
            IndexDefault_IncludeAll = new IndexingPolicy
            {
                IncludedPaths = new Collection<IncludedPath>
                    {
                        { new IncludedPath { Path = $"/*" } },
                    }
            };
            IndexDefault_ExcludeAll = new IndexingPolicy
            {
                ExcludedPaths = new Collection<ExcludedPath>
                    {
                        { new ExcludedPath { Path = $"/*" } }
                    }
            };

            SelectAllComputedPropertiesQuery = @"SELECT c.lowerLastName, c.parentsFullName FROM c";
            AllComputedPropertiesResult = new List<string>
                {
                    $@"{{{Environment.NewLine}  ""lowerLastName"": ""andersen"",{Environment.NewLine}  ""parentsFullName"": ""Thomas Andersen & Mary Kay Andersen""{Environment.NewLine}}}",
                    $@"{{{Environment.NewLine}  ""lowerLastName"": ""wakefield""{Environment.NewLine}}}"
                };
            EmptyResult = new List<string>
                {
                    @"{}",
                    @"{}"
                };
        }

        [ClassCleanup]
        public static async Task Cleanup()
        {
            await database?.DeleteAsync();
        }

        [TestMethod]
        public async Task TestComputedProperties()
        {
            TestVariation[] variations = new TestVariation[]
            {
                /////////////////
                // Create tests
                /////////////////
                new TestVariation
                {
                    Description = "V1: null; V2: Empty",
                    V1 = new ContainerState
                    {
                        ComputedProperties = null,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = EmptyResult
                    },
                    V2 = new ContainerState
                    {
                        ComputedProperties = new Collection<ComputedProperty>(),
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = EmptyResult
                    }
                },
                new TestVariation
                {
                    Description = "V1: default; V2: All computed properties, no indexing",
                    V1 = new ContainerState(),
                    V2 = new ContainerState
                    {
                        ComputedProperties = AllComputedProperties,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = AllComputedPropertiesResult
                    }
                },
                new TestVariation
                {
                    Description = "V1: default; V2: All computed properties, indexed, exclude /*",
                    V1 = new ContainerState(),
                    V2 = new ContainerState
                    {
                        ComputedProperties = AllComputedProperties,
                        IndexingPolicy = IndexAllComputedProperties_ExcludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = AllComputedPropertiesResult
                    }
                },
                new TestVariation
                {
                    Description = "V1: default; V2: All computed properties, indexed, include /*",
                    V1 = new ContainerState(),
                    V2 = new ContainerState
                    {
                        ComputedProperties = AllComputedProperties,
                        IndexingPolicy = IndexAllComputedProperties_IncludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = AllComputedPropertiesResult
                    },
                },
                new TestVariation
                {
                    Description = "V1: default; V2: All computed properties, not indexed, exclude /*",
                    V1 = new ContainerState(),
                    V2 = new ContainerState
                    {
                        ComputedProperties = AllComputedProperties,
                        IndexingPolicy = IndexDefault_ExcludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = AllComputedPropertiesResult
                    },
                },
                new TestVariation
                {
                    Description = "V1: default; V2: All computed properties, not indexed, include /*",
                    V1 = new ContainerState(),
                    V2 = new ContainerState
                    {
                        ComputedProperties = AllComputedProperties,
                        IndexingPolicy = IndexDefault_IncludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = AllComputedPropertiesResult
                    }
                },
                new TestVariation
                {
                    Description = "V1: one computed property; V2: All computed properties, not indexed, include /*",
                    V1 = new ContainerState()
                    {
                        ComputedProperties = new Collection<ComputedProperty>{ LowerName },
                        IndexingPolicy = IndexDefault_IncludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = new List<string>
                            {
                                $@"{{{Environment.NewLine}  ""lowerLastName"": ""andersen""{Environment.NewLine}}}",
                                $@"{{{Environment.NewLine}  ""lowerLastName"": ""wakefield""{Environment.NewLine}}}"
                            }
                    },
                    V2 = new ContainerState
                    {
                        ComputedProperties = AllComputedProperties,
                        IndexingPolicy = IndexDefault_IncludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = AllComputedPropertiesResult
                    }
                },

                /////////////////
                // Replace tests
                /////////////////
                new TestVariation
                {
                    Description = "V1: one computed property; V2: other computed property, not indexed, include /*",
                    V1 = new ContainerState()
                    {
                        ComputedProperties = new Collection<ComputedProperty>{ LowerName },
                        IndexingPolicy = IndexDefault_IncludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = new List<string>
                            {
                                $@"{{{Environment.NewLine}  ""lowerLastName"": ""andersen""{Environment.NewLine}}}",
                                $@"{{{Environment.NewLine}  ""lowerLastName"": ""wakefield""{Environment.NewLine}}}"
                            }
                    },
                    V2 = new ContainerState
                    {
                        ComputedProperties = new Collection<ComputedProperty>{ ParentsFullName },
                        IndexingPolicy = IndexDefault_IncludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = new List<string>
                            {
                                $@"{{{Environment.NewLine}  ""parentsFullName"": ""Thomas Andersen & Mary Kay Andersen""{Environment.NewLine}}}",
                                @"{}"
                            }
                    }
                },
                new TestVariation
                {
                    Description = "V1: one computed property; V2: updated computed property definition, not indexed, include /*",
                    V1 = new ContainerState()
                    {
                        ComputedProperties = new Collection<ComputedProperty>{ LowerName },
                        IndexingPolicy = IndexDefault_IncludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = new List<string>
                            {
                                $@"{{{Environment.NewLine}  ""lowerLastName"": ""andersen""{Environment.NewLine}}}",
                                $@"{{{Environment.NewLine}  ""lowerLastName"": ""wakefield""{Environment.NewLine}}}"
                            }
                    },
                    V2 = new ContainerState
                    {
                        ComputedProperties = new Collection<ComputedProperty>
                            { 
                                new ComputedProperty
                                    {
                                        Name = "lowerLastName",
                                        Query = "SELECT VALUE LOWER(c.lastName) FROM c"
                                    }
                            },
                        IndexingPolicy = IndexDefault_IncludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = new List<string>
                            {
                                $@"{{{Environment.NewLine}  ""lowerLastName"": ""andersen""{Environment.NewLine}}}",
                                @"{}"
                            }
                    }
                },
                
                /////////////////
                // Drop tests
                /////////////////
                new TestVariation
                {
                    Description = "V1: All computed properties; V2: null",
                    V1 = new ContainerState
                    {
                        ComputedProperties = AllComputedProperties,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = AllComputedPropertiesResult
                    },
                    V2 = new ContainerState
                    {
                        ComputedProperties = null,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = EmptyResult
                    }
                },
                new TestVariation
                {
                    Description = "V1: All computed properties; V2: only 1 computed property",
                    V1 = new ContainerState
                    {
                        ComputedProperties = AllComputedProperties,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = AllComputedPropertiesResult
                    },
                    V2 = new ContainerState
                    {
                        ComputedProperties = new Collection<ComputedProperty>{ LowerName },
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = new List<string>
                            {
                                $@"{{{Environment.NewLine}  ""lowerLastName"": ""andersen""{Environment.NewLine}}}",
                                $@"{{{Environment.NewLine}  ""lowerLastName"": ""wakefield""{Environment.NewLine}}}"
                            }
                    }
                },
                new TestVariation
                {
                    Description = "V1: All computed properties, indexed, exclude /*; V2: null",
                    V1 = new ContainerState
                    {
                        ComputedProperties = AllComputedProperties,
                        IndexingPolicy = IndexAllComputedProperties_ExcludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = AllComputedPropertiesResult
                    },
                    V2 = new ContainerState
                    {
                        ComputedProperties = null,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = EmptyResult
                    }
                },
                new TestVariation
                {
                    Description = "V1: All computed properties, indexed, include /*; V2: null",
                    V1 = new ContainerState
                    {
                        ComputedProperties = AllComputedProperties,
                        IndexingPolicy = IndexAllComputedProperties_IncludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = AllComputedPropertiesResult
                    },
                    V2 = new ContainerState
                    {
                        ComputedProperties = null,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = EmptyResult
                    }
                },
                new TestVariation
                {
                    Description = "V1: All computed properties, not indexed, exclude /*; V2: null",
                    V1 = new ContainerState
                    {
                        ComputedProperties = AllComputedProperties,
                        IndexingPolicy = IndexDefault_ExcludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = AllComputedPropertiesResult
                    },
                    V2 = new ContainerState
                    {
                        ComputedProperties = null,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = EmptyResult
                    }
                },
                new TestVariation
                {
                    Description = "V1: All computed properties, not indexed, include /*; V2: null",
                    V1 = new ContainerState
                    {
                        ComputedProperties = AllComputedProperties,
                        IndexingPolicy = IndexDefault_IncludeAll,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = AllComputedPropertiesResult
                    },
                    V2 = new ContainerState
                    {
                        ComputedProperties = null,
                        Query = SelectAllComputedPropertiesQuery,
                        ExpectedDocuments = EmptyResult
                    }
                }
            };

            int i = 0;
            foreach (TestVariation variation in variations)
            {
                Console.WriteLine($"Variation {i++} : {variation.Description}");
                await this.RunTest(variation);
            }
        }

        private async Task<Container> RunTest(TestVariation variation)
        {
            Container container = await this.CreateOrReplace(container: null, containerState: variation.V1);
            return await this.CreateOrReplace(container, containerState: variation.V2);
        }

        private async Task<Container> CreateOrReplace(Container container, ContainerState containerState)
        {
            ContainerProperties containerProperties = new ContainerProperties(container?.Id ?? Guid.NewGuid().ToString(), "/id")
            {
                IndexingPolicy = containerState.IndexingPolicy ?? new IndexingPolicy(),
                ComputedProperties = containerState.ComputedProperties,
            };

            ContainerResponse response = container == null ?
                await database.CreateContainerAsync(containerProperties) :
                await container.ReplaceContainerAsync(containerProperties);

            this.ValidateComputedProperties(containerState.ComputedProperties, response.Resource.ComputedProperties);

            if (container == null)
            {
                await this.InsertDocuments(response.Container);
            }
            else
            {
                // Sometimes the container changes are not immediately reflected in the query.
                // We force a insert-delete after container replacement, which seems to help with this problem.
                // If this still doesn't help with the flakiness, we need to find other ways of running the query scenario.
                // One alternative is to wait for V2 for all test variations to take effect
                //   and then run queries separately on each container.
                await this.DeleteReinsertDocuments(response.Container);
            }

            if (!string.IsNullOrEmpty(containerState.Query))
            {
                List<dynamic> results = await this.QueryItems(response.Container, containerState.Query);

                Assert.AreEqual(containerState.ExpectedDocuments.Count, results.Count);
                for (int i = 0; i < containerState.ExpectedDocuments.Count; i++)
                {
                    Assert.AreEqual(containerState.ExpectedDocuments[i], results[i].ToString());
                }
            }

            return response.Container;
        }

        private async Task DeleteReinsertDocuments(Container container)
        {
            foreach (CosmosObject document in Documents)
            {
                string id = ((CosmosString)document["id"]).Value;
                await container.DeleteItemAsync<dynamic>(id, new PartitionKey(id));
            }

            await this.InsertDocuments(container);
        }

        private async Task InsertDocuments(Container container)
        {
            foreach(CosmosObject document in Documents)
            {
                await container.CreateItemAsync<dynamic>(document);
            }
        }

        private async Task<List<dynamic>> QueryItems(Container container, string query)
        {
            List<dynamic> results = new List<dynamic>();
            FeedIterator<dynamic> iterator = container.GetItemQueryIterator<dynamic>(query);
            do
            {
                FeedResponse<dynamic> page = await iterator.ReadNextAsync();
                results.AddRange(page);
            } while (iterator.HasMoreResults);

            return results;
        }

        private void ValidateComputedProperties(Collection<ComputedProperty> expected, Collection<ComputedProperty> actual)
        {
            ComputedPropertiesComparer.AssertAreEqual(expected, actual);
        }

        private class TestVariation
        {
            public string Description { get; set; }
            public ContainerState V1 { get; set; }
            public ContainerState V2 { get; set; }
        }

        private class ContainerState
        {
            public Collection<ComputedProperty> ComputedProperties { get; set; }
            public IndexingPolicy IndexingPolicy { get; set; }
            public List<string> ExpectedDocuments { get; set; }
            public string Query { get; set; }
        }

        private static readonly CosmosObject AndersenFamily = CosmosObject.Parse(@"
        {
          ""id"": ""AndersenFamily"",
          ""lastName"": ""Andersen"",
          ""parents"": [
             { ""firstName"": ""Thomas"" },
             { ""firstName"": ""Mary Kay""}
          ],
          ""children"": [
             {
                 ""firstName"": ""Henriette Thaulow"",
                 ""gender"": ""female"",
                 ""grade"": 5,
                 ""pets"": [{ ""givenName"": ""Fluffy"" }]
             }
          ],
          ""address"": { ""state"": ""WA"", ""county"": ""King"", ""city"": ""seattle"" },
          ""creationDate"": 1431620472,
          ""isRegistered"": true,
          ""_rid"": ""0fomAIxnukU1AQAAAAAAAA==""
        }");

        private static readonly CosmosObject WakefieldFamily = CosmosObject.Parse(@"
        {
          ""id"": ""WakefieldFamily"",
          ""parents"": [
              { ""familyName"": ""Wakefield"", ""givenName"": ""Robin"" },
              { ""familyName"": ""Miller"", ""givenName"": ""Ben"" }
          ],
          ""children"": [
              {
                ""familyName"": ""Merriam"",
                ""givenName"": ""Jesse"",
                ""gender"": ""female"", ""grade"": 1,
                ""pets"": [
                    { ""givenName"": ""Goofy"" },
                    { ""givenName"": ""Shadow"" }
                ]
              },
              { 
                ""familyName"": ""Miller"", 
                 ""givenName"": ""Lisa"", 
                 ""gender"": ""female"", 
                 ""grade"": 8 }
          ],
          ""address"": { ""state"": ""NY"", ""county"": ""Manhattan"", ""city"": ""NY"" },
          ""creationDate"": 1431620462,
          ""isRegistered"": false,
          ""_rid"": ""0fomAIxnukU1AQAAAAAAAB==""
        }");

        private static readonly CosmosElement[] Documents = new CosmosElement[]
        {
            AndersenFamily,
            WakefieldFamily,
        };
    }
}

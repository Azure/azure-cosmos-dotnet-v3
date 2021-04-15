namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public sealed class OrderByQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestQueryCrossPartitionTopOrderByDifferentDimensionAsync()
        {
            string[] documents = new[]
            {
                @"{""id"":""documentId1"",""key"":""A""}",
                @"{""id"":""documentId2"",""key"":""A"",""prop"":3}",
                @"{""id"":""documentId3"",""key"":""A""}",
                @"{""id"":""documentId4"",""key"":5}",
                @"{""id"":""documentId5"",""key"":5,""prop"":2}",
                @"{""id"":""documentId6"",""key"":5}",
                @"{""id"":""documentId7"",""key"":2}",
                @"{""id"":""documentId8"",""key"":2,""prop"":1}",
                @"{""id"":""documentId9"",""key"":2}",
            };

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                this.TestQueryCrossPartitionTopOrderByDifferentDimensionHelper,
                "/key");
        }

        private async Task TestQueryCrossPartitionTopOrderByDifferentDimensionHelper(
            Container container,
            IReadOnlyList<CosmosObject> documents)
        {
            await QueryTestsBase.NoOp();

            string[] expected = new[] { "documentId2", "documentId5", "documentId8" };
            List<CosmosElement> query = await QueryTestsBase.RunQueryAsync(
                container,
                "SELECT r.id FROM r ORDER BY r.prop DESC",
                new QueryRequestOptions()
                {
                    MaxItemCount = 1,
                    MaxConcurrency = 1,
                });

            Assert.AreEqual(
                string.Join(", ", expected),
                string.Join(", ", query.Select(doc => ((CosmosString)(doc as CosmosObject)["id"]).Value)));
        }

        [TestMethod]
        public async Task TestQueryCrossPartitionTopOrderByAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 1000;
            string partitionKey = "field_0";

            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> documents = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync<string>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                this.TestQueryCrossPartitionTopOrderByHelper,
                partitionKey,
                "/" + partitionKey);
        }

        private async Task TestQueryCrossPartitionTopOrderByHelper(Container container, IReadOnlyList<CosmosObject> documents, string testArg)
        {
            string partitionKey = testArg;
            IDictionary<string, string> idToRangeMinKeyMap = new Dictionary<string, string>();
            IRoutingMapProvider routingMapProvider = await this.Client.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);

            ContainerProperties containerSettings = await container.ReadContainerAsync();
            foreach (CosmosObject document in documents)
            {
                IReadOnlyList<PartitionKeyRange> targetRanges = await routingMapProvider
                    .TryGetOverlappingRangesAsync(
                        containerSettings.ResourceId,
                        Range<string>.GetPointRange(
                            PartitionKeyInternal.FromObjectArray(
                                new object[]
                                {
                                    Number64.ToLong((document[partitionKey] as CosmosNumber).Value)
                                },
                                strict: true).GetEffectivePartitionKeyString(containerSettings.PartitionKey)),
                        NoOpTrace.Singleton);
                Debug.Assert(targetRanges.Count == 1);
                idToRangeMinKeyMap.Add(((CosmosString)document["id"]).Value, targetRanges[0].MinInclusive);
            }

            IList<int> partitionKeyValues = new HashSet<int>(documents.Select(doc => (int)Number64.ToLong((doc[partitionKey] as CosmosNumber).Value))).ToList();

            // Test Empty Results
            List<string> expectedResults = new List<string> { };
            List<string> computedResults = new List<string>();

            string emptyQueryText = @"
                SELECT TOP 5 *
                FROM Root r
                WHERE
                    r.partitionKey = 9991123 OR
                    r.partitionKey = 9991124 OR
                    r.partitionKey = 99991125";
            List<CosmosElement> queryEmptyResult = await QueryTestsBase.RunQueryAsync(
                container,
                emptyQueryText);

            computedResults = queryEmptyResult.Select(doc => (doc as CosmosObject)["id"].ToString()).ToList();
            computedResults.Sort();
            expectedResults.Sort();

            Random rand = new Random();
            Assert.AreEqual(string.Join(",", expectedResults), string.Join(",", computedResults));
            List<Task> tasks = new List<Task>();
            for (int trial = 0; trial < 1; ++trial)
            {
                foreach (bool fanOut in new[] { true, false })
                {
                    foreach (bool isParametrized in new[] { true, false })
                    {
                        foreach (bool hasTop in new[] { false, true })
                        {
                            foreach (bool hasOrderBy in new[] { false, true })
                            {
                                foreach (string sortOrder in new[] { string.Empty, "ASC", "DESC" })
                                {
                                    #region Expected Documents
                                    string topValueName = "@topValue";
                                    int top = rand.Next(4) * rand.Next(partitionKeyValues.Count);
                                    string queryText;
                                    string orderByField = "field_" + rand.Next(10);
                                    IEnumerable<CosmosObject> filteredDocuments;

                                    string getTop() =>
                                        hasTop ? string.Format(CultureInfo.InvariantCulture, "TOP {0} ", isParametrized ? topValueName : top.ToString()) : string.Empty;

                                    string getOrderBy() =>
                                        hasOrderBy ? string.Format(CultureInfo.InvariantCulture, " ORDER BY r.{0} {1}", orderByField, sortOrder) : string.Empty;

                                    if (fanOut)
                                    {
                                        queryText = string.Format(
                                            CultureInfo.InvariantCulture,
                                            "SELECT {0}r.id, r.{1} FROM r{2}",
                                            getTop(),
                                            partitionKey,
                                            getOrderBy());

                                        filteredDocuments = documents;
                                    }
                                    else
                                    {
                                        HashSet<int> selectedPartitionKeyValues = new HashSet<int>(partitionKeyValues
                                            .OrderBy(x => rand.Next())
                                            .ThenBy(x => x)
                                            .Take(rand.Next(1, Math.Min(100, partitionKeyValues.Count) + 1)));

                                        queryText = string.Format(
                                            CultureInfo.InvariantCulture,
                                            "SELECT {0}r.id, r.{1} FROM r WHERE r.{2} IN ({3}){4}",
                                            getTop(),
                                            partitionKey,
                                            partitionKey,
                                            string.Join(", ", selectedPartitionKeyValues),
                                            getOrderBy());

                                        filteredDocuments = documents
                                            .Where(doc => selectedPartitionKeyValues.Contains((int)Number64.ToLong((doc[partitionKey] as CosmosNumber).Value)));
                                    }

                                    if (hasOrderBy)
                                    {
                                        switch (sortOrder)
                                        {
                                            case "":
                                            case "ASC":
                                                filteredDocuments = filteredDocuments
                                                    .AsParallel()
                                                    .OrderBy(doc => (int)Number64.ToLong((doc[orderByField] as CosmosNumber).Value))
                                                    .ThenBy(doc => idToRangeMinKeyMap[((CosmosString)doc["id"]).Value])
                                                    .ThenBy(doc => int.Parse(((CosmosString)doc["id"]).Value, CultureInfo.InvariantCulture));
                                                break;
                                            case "DESC":
                                                filteredDocuments = filteredDocuments
                                                    .AsParallel()
                                                    .OrderByDescending(doc => (int)Number64.ToLong((doc[orderByField] as CosmosNumber).Value))
                                                    .ThenBy(doc => idToRangeMinKeyMap[((CosmosString)doc["id"]).Value])
                                                    .ThenByDescending(doc => int.Parse(((CosmosString)doc["id"]).Value, CultureInfo.InvariantCulture));
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        filteredDocuments = filteredDocuments
                                            .AsParallel()
                                            .OrderBy(doc => idToRangeMinKeyMap[((CosmosString)doc["id"]).Value])
                                            .ThenBy(doc => int.Parse(((CosmosString)doc["id"]).Value, CultureInfo.InvariantCulture));
                                    }

                                    if (hasTop)
                                    {
                                        filteredDocuments = filteredDocuments.Take(top);
                                    }
                                    #endregion
                                    #region Actual Documents
                                    IEnumerable<CosmosObject> actualDocuments;

                                    int maxDegreeOfParallelism = hasTop ? rand.Next(4) : (rand.Next(2) == 0 ? -1 : (1 + rand.Next(0, 10)));
                                    int? maxItemCount = rand.Next(2) == 0 ? -1 : rand.Next(1, documents.Count());
                                    QueryRequestOptions feedOptions = new QueryRequestOptions
                                    {
                                        MaxBufferedItemCount = rand.Next(2) == 0 ? -1 : rand.Next(Math.Min(100, documents.Count()), documents.Count() + 1),
                                        MaxConcurrency = maxDegreeOfParallelism
                                    };

                                    if (rand.Next(3) == 0)
                                    {
                                        maxItemCount = null;
                                    }

                                    QueryDefinition querySpec = new QueryDefinition(queryText);
                                    SqlParameterCollection parameters = new SqlParameterCollection();
                                    if (isParametrized)
                                    {
                                        if (hasTop)
                                        {
                                            querySpec.WithParameter(topValueName, top);
                                        }
                                    }

                                    DateTime startTime = DateTime.Now;
                                    List<CosmosObject> result = new List<CosmosObject>();
                                    FeedIterator<CosmosObject> query = container.GetItemQueryIterator<CosmosObject>(
                                        querySpec,
                                        requestOptions: feedOptions);

                                    while (query.HasMoreResults)
                                    {
                                        FeedResponse<CosmosObject> response = await query.ReadNextAsync();
                                        result.AddRange(response);
                                    }

                                    actualDocuments = result;

                                    #endregion

                                    double time = (DateTime.Now - startTime).TotalMilliseconds;

                                    System.Diagnostics.Trace.TraceInformation("<Query>: {0}, <Document Count>: {1}, <MaxItemCount>: {2}, <MaxDegreeOfParallelism>: {3}, <MaxBufferedItemCount>: {4}, <Time>: {5} ms",
                                        JsonConvert.SerializeObject(querySpec),
                                        actualDocuments.Count(),
                                        maxItemCount,
                                        maxDegreeOfParallelism,
                                        feedOptions.MaxBufferedItemCount,
                                        time);

                                    string allDocs = JsonConvert.SerializeObject(documents);

                                    string expectedResultDocs = JsonConvert.SerializeObject(filteredDocuments);
                                    IEnumerable<string> expectedResult = filteredDocuments.Select(doc => ((CosmosString)doc["id"]).Value.ToString());

                                    string actualResultDocs = JsonConvert.SerializeObject(actualDocuments);
                                    IEnumerable<string> actualResult = actualDocuments.Select(doc => ((CosmosString)doc["id"]).Value.ToString());

                                    Assert.AreEqual(
                                        string.Join(", ", expectedResult),
                                        string.Join(", ", actualResult),
                                        $"query: {querySpec}, trial: {trial}, fanOut: {fanOut}, hasTop: {hasTop}, hasOrderBy: {hasOrderBy}, sortOrder: {sortOrder}");
                                }
                            }
                        }
                    }
                }
            }
        }

        private struct CrossPartitionWithContinuationsArgs
        {
            public int NumberOfDocuments;
            public string PartitionKey;
            public string NumberField;
            public string BoolField;
            public string StringField;
            public string NullField;
            public string Children;
        }

        [TestMethod]
        public async Task TestQueryCrossPartitionWithContinuationsAsync()
        {
            int numberOfDocuments = 1 << 2;
            string partitionKey = "key";
            string numberField = "numberField";
            string boolField = "boolField";
            string stringField = "stringField";
            string nullField = "nullField";
            string children = "children";

            List<string> documents = new List<string>(numberOfDocuments);
            for (int i = 0; i < numberOfDocuments; ++i)
            {
                Document doc = new Document();
                doc.SetPropertyValue(partitionKey, i);
                doc.SetPropertyValue(numberField, i % 8);
                doc.SetPropertyValue(boolField, (i % 2) == 0 ? bool.TrueString : bool.FalseString);
                doc.SetPropertyValue(stringField, (i % 8).ToString());
                doc.SetPropertyValue(nullField, null);
                doc.SetPropertyValue(children, new[] { i % 2, i % 2, i % 3, i % 3, i });
                documents.Add(doc.ToString());
            }

            CrossPartitionWithContinuationsArgs args = new CrossPartitionWithContinuationsArgs()
            {
                NumberOfDocuments = numberOfDocuments,
                PartitionKey = partitionKey,
                NumberField = numberField,
                BoolField = boolField,
                StringField = stringField,
                NullField = nullField,
                Children = children,
            };

            await this.CreateIngestQueryDeleteAsync<CrossPartitionWithContinuationsArgs>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                this.TestQueryCrossPartitionWithContinuationsHelper,
                args,
                "/" + partitionKey);
        }

        private async Task TestQueryCrossPartitionWithContinuationsHelper(
            Container container,
            IReadOnlyList<CosmosObject> documents,
            CrossPartitionWithContinuationsArgs args)
        {
            int documentCount = args.NumberOfDocuments;
            string partitionKey = args.PartitionKey;
            string numberField = args.NumberField;
            string boolField = args.BoolField;
            string stringField = args.StringField;
            string nullField = args.NullField;
            string children = args.Children;

            // Try resuming from bad continuation token
            #region BadContinuations
            try
            {
                await container.GetItemQueryIterator<Document>(
                    "SELECT * FROM t",
                    continuationToken: Guid.NewGuid().ToString(),
                    requestOptions: new QueryRequestOptions() { MaxConcurrency = 1 }).ReadNextAsync();

                Assert.Fail("Expect exception");
            }
            catch (CosmosException dce)
            {
                Assert.IsTrue(dce.StatusCode == HttpStatusCode.BadRequest);
            }
            catch (AggregateException aggrEx)
            {
                Assert.Fail(aggrEx.ToString());
            }

            try
            {
                await container.GetItemQueryIterator<Document>(
                    "SELECT TOP 10 * FROM r",
                    continuationToken: "{'top':11}",
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 10, MaxConcurrency = -1 }).ReadNextAsync();

                Assert.Fail("Expect exception");
            }
            catch (CosmosException dce)
            {
                Assert.IsTrue(dce.StatusCode == HttpStatusCode.BadRequest);
            }

            try
            {
                await container.GetItemQueryIterator<Document>(
                    "SELECT * FROM r ORDER BY r.field1",
                    continuationToken: "{'compositeToken':{'range':{'min':'05C1E9CD673398','max':'FF'}}, 'orderByItems':[{'item':2}, {'item':1}]}",
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 10, MaxConcurrency = -1 }).ReadNextAsync();

                Assert.Fail("Expect exception");
            }
            catch (CosmosException dce)
            {
                Assert.IsTrue(dce.StatusCode == HttpStatusCode.BadRequest);
            }

            try
            {
                await container.GetItemQueryIterator<Document>(
                   "SELECT * FROM r ORDER BY r.field1, r.field2",
                   continuationToken: "{'compositeToken':{'range':{'min':'05C1E9CD673398','max':'FF'}}, 'orderByItems':[{'item':2}, {'item':1}]}",
                   requestOptions: new QueryRequestOptions() { MaxItemCount = 10, MaxConcurrency = -1 }).ReadNextAsync();

                Assert.Fail("Expect exception");
            }
            catch (CosmosException dce)
            {
                Assert.IsTrue(dce.StatusCode == HttpStatusCode.BadRequest);
            }
            #endregion

            FeedResponse<Document> responseWithEmptyContinuationExpected = await container.GetItemQueryIterator<Document>(
                $"SELECT TOP 0 * FROM r",
                requestOptions: new QueryRequestOptions()
                {
                    MaxConcurrency = 10,
                    MaxItemCount = -1
                }).ReadNextAsync();

            Assert.AreEqual(null, responseWithEmptyContinuationExpected.ContinuationToken);

            string[] queries = new[]
            {
                $"SELECT * FROM r",
                $"SELECT * FROM r WHERE r.{partitionKey} BETWEEN 0 AND {documentCount}",
                $"SELECT r.{partitionKey} FROM r JOIN c in r.{children}",
                $"SELECT * FROM r ORDER BY r.{partitionKey}",
                $"SELECT * FROM r WHERE r.{partitionKey} BETWEEN 0 AND {documentCount} ORDER BY r.{numberField} DESC",
                $"SELECT r.{partitionKey} FROM r JOIN c in r.{children} ORDER BY r.{numberField}",
                $"SELECT TOP 10 * FROM r",
                $"SELECT TOP 10 * FROM r WHERE r.{partitionKey} BETWEEN 0 AND {documentCount} ORDER BY r.{partitionKey} DESC",
                $"SELECT TOP 10 * FROM r ORDER BY r.{numberField}",
                $"SELECT TOP 40 r.{partitionKey} FROM r JOIN c in r.{children} ORDER BY r.{numberField} DESC",
                $"SELECT * FROM r WHERE r.{partitionKey} BETWEEN 0 AND {documentCount} ORDER BY r.{boolField} DESC",
                $"SELECT * FROM r WHERE r.{partitionKey} BETWEEN 0 AND {documentCount} ORDER BY r.{stringField} DESC",
                $"SELECT * FROM r WHERE r.{partitionKey} BETWEEN 0 AND {documentCount} ORDER BY r.{nullField} DESC",
            };

            foreach (string query in queries)
            {
                foreach (int pageSize in new int[] { 1, documentCount / 2, documentCount })
                {
                    await RunQueryAsync(
                        container,
                        query,
                        new QueryRequestOptions()
                        {
                            MaxItemCount = pageSize,
                        });
                }
            }
        }

        [TestMethod]
        public async Task TestMultiOrderByQueriesAsync()
        {
            int numberOfDocuments = 4;

            List<string> documents = new List<string>(numberOfDocuments);
            Random random = new Random(1234);
            for (int i = 0; i < numberOfDocuments; ++i)
            {
                MultiOrderByDocument multiOrderByDocument = OrderByQueryTests.GenerateMultiOrderByDocument(random);
                int numberOfDuplicates = 5;

                for (int j = 0; j < numberOfDuplicates; j++)
                {
                    // Add the document itself for exact duplicates
                    documents.Add(JsonConvert.SerializeObject(multiOrderByDocument));

                    // Permute all the fields so that there are duplicates with tie breaks
                    MultiOrderByDocument numberClone = MultiOrderByDocument.GetClone(multiOrderByDocument);
                    numberClone.NumberField = random.Next(0, 5);
                    documents.Add(JsonConvert.SerializeObject(numberClone));

                    MultiOrderByDocument stringClone = MultiOrderByDocument.GetClone(multiOrderByDocument);
                    stringClone.StringField = random.Next(0, 5).ToString();
                    documents.Add(JsonConvert.SerializeObject(stringClone));

                    MultiOrderByDocument boolClone = MultiOrderByDocument.GetClone(multiOrderByDocument);
                    boolClone.BoolField = random.Next(0, 2) % 2 == 0;
                    documents.Add(JsonConvert.SerializeObject(boolClone));

                    // Also fuzz what partition it goes to
                    MultiOrderByDocument partitionClone = MultiOrderByDocument.GetClone(multiOrderByDocument);
                    partitionClone.PartitionKey = random.Next(0, 5);
                    documents.Add(JsonConvert.SerializeObject(partitionClone));
                }
            }

            // Shuffle the documents so they end up in different pages
            documents = documents.OrderBy((person) => Guid.NewGuid()).ToList();

            Cosmos.IndexingPolicy indexingPolicy = new Cosmos.IndexingPolicy()
            {
                CompositeIndexes = new Collection<Collection<Cosmos.CompositePath>>()
                {
                    // Simple
                    new Collection<Cosmos.CompositePath>()
                    {
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.NumberField),
                            Order = Cosmos.CompositePathSortOrder.Ascending,
                        },
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.StringField),
                            Order = Cosmos.CompositePathSortOrder.Descending,
                        }
                    },

                    // Max Columns
                    new Collection<Cosmos.CompositePath>()
                    {
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.NumberField),
                            Order = Cosmos.CompositePathSortOrder.Descending,
                        },
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.StringField),
                            Order = Cosmos.CompositePathSortOrder.Ascending,
                        },
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.NumberField2),
                            Order = Cosmos.CompositePathSortOrder.Descending,
                        },
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.StringField2),
                            Order = Cosmos.CompositePathSortOrder.Ascending,
                        }
                    },

                    // All primitive values
                    new Collection<Cosmos.CompositePath>()
                    {
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.NumberField),
                            Order = Cosmos.CompositePathSortOrder.Descending,
                        },
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.StringField),
                            Order = Cosmos.CompositePathSortOrder.Ascending,
                        },
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.BoolField),
                            Order = Cosmos.CompositePathSortOrder.Descending,
                        },
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.NullField),
                            Order = Cosmos.CompositePathSortOrder.Ascending,
                        }
                    },

                    // Primitive and Non Primitive (waiting for composite on objects and arrays)
                    //new Collection<Cosmos.CompositePath>()
                    //{
                    //    new Cosmos.CompositePath()
                    //    {
                    //        Path = "/" + nameof(MultiOrderByDocument.NumberField),
                    //    },
                    //    new Cosmos.CompositePath()
                    //    {
                    //        Path = "/" + nameof(MultiOrderByDocument.ObjectField),
                    //    },
                    //    new Cosmos.CompositePath()
                    //    {
                    //        Path = "/" + nameof(MultiOrderByDocument.StringField),
                    //    },
                    //    new Cosmos.CompositePath()
                    //    {
                    //        Path = "/" + nameof(MultiOrderByDocument.ArrayField),
                    //    },
                    //},

                    // Long strings
                    new Collection<Cosmos.CompositePath>()
                    {
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.StringField),
                        },
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.ShortStringField),
                        },
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.MediumStringField),
                        },
                        new Cosmos.CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.LongStringField),
                        }
                    },

                    // System Properties 
                    //new Collection<Cosmos.CompositePath>()
                    //{
                    //    new Cosmos.CompositePath()
                    //    {
                    //        Path = "/id",
                    //    },
                    //    new Cosmos.CompositePath()
                    //    {
                    //        Path = "/_ts",
                    //    },
                    //    new Cosmos.CompositePath()
                    //    {
                    //        Path = "/_etag",
                    //    },

                    //    // _rid is not allowed
                    //    //new Cosmos.CompositePath()
                    //    //{
                    //    //    Path = "/_rid",
                    //    //},
                    //},
                }
            };

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                this.TestMultiOrderByQueriesHelper,
                "/" + nameof(MultiOrderByDocument.PartitionKey),
                indexingPolicy,
                this.CreateNewCosmosClient);
        }

        private sealed class MultiOrderByDocument
        {
            public double NumberField { get; set; }
            public double NumberField2 { get; set; }
            public bool BoolField { get; set; }
            public string StringField { get; set; }
            public string StringField2 { get; set; }
            public object NullField { get; set; }
            public object ObjectField { get; set; }
            public List<object> ArrayField { get; set; }
            public string ShortStringField { get; set; }
            public string MediumStringField { get; set; }
            public string LongStringField { get; set; }
            public int PartitionKey { get; set; }

            public static MultiOrderByDocument GetClone(MultiOrderByDocument other)
            {
                return JsonConvert.DeserializeObject<MultiOrderByDocument>(JsonConvert.SerializeObject(other));
            }
        }

        private static MultiOrderByDocument GenerateMultiOrderByDocument(Random random)
        {
            return new MultiOrderByDocument()
            {
                NumberField = random.Next(0, 5),
                NumberField2 = random.Next(0, 5),
                BoolField = (random.Next() % 2) == 0,
                StringField = random.Next(0, 5).ToString(),
                StringField2 = random.Next(0, 5).ToString(),
                NullField = null,
                ObjectField = new object(),
                ArrayField = new List<object>(),
                ShortStringField = new string('a', random.Next(0, 100)),
                MediumStringField = new string('a', random.Next(100, 128)),
                //Max precisions is 2kb / number of terms
                LongStringField = new string('a', random.Next(128, 255)),
                PartitionKey = random.Next(0, 5),
            };
        }

        private async Task TestMultiOrderByQueriesHelper(
            Container container,
            IReadOnlyList<CosmosObject> documents)
        {
            ContainerProperties containerSettings = await container.ReadContainerAsync();
            // For every composite index
            foreach (Collection<Cosmos.CompositePath> compositeIndex in containerSettings.IndexingPolicy.CompositeIndexes)
            {
                // for every order
                foreach (bool invert in new bool[] { false, true })
                {
                    foreach (bool hasTop in new bool[] { false, true })
                    {
                        foreach (bool hasFilter in new bool[] { false, true })
                        {
                            // Generate a multi order by from that index
                            List<string> orderByItems = new List<string>();
                            List<string> selectItems = new List<string>();
                            bool isDesc;
                            foreach (Cosmos.CompositePath compositePath in compositeIndex)
                            {
                                isDesc = compositePath.Order == Cosmos.CompositePathSortOrder.Descending;
                                if (invert)
                                {
                                    isDesc = !isDesc;
                                }

                                string isDescString = isDesc ? "DESC" : "ASC";
                                orderByItems.Add($"root.{compositePath.Path.Replace("/", "")} { isDescString }");
                                selectItems.Add($"root.{compositePath.Path.Replace("/", "")}");
                            }

                            const int topCount = 10;
                            string topString = hasTop ? $"TOP {topCount}" : string.Empty;
                            string whereString = hasFilter ? $"WHERE root.{nameof(MultiOrderByDocument.NumberField)} % 2 = 0" : string.Empty;
                            string query = $@"
                                SELECT { topString } VALUE [{string.Join(", ", selectItems)}] 
                                FROM root { whereString }
                                ORDER BY {string.Join(", ", orderByItems)}";
#if false
                            // Used for debugging which partitions have which documents
                            IReadOnlyList<PartitionKeyRange> pkranges = GetPartitionKeyRanges(container);
                            foreach (PartitionKeyRange pkrange in pkranges)
                            {
                                List<dynamic> documentsWithinPartition = cosmosClient.CreateDocumentQuery(
                                    container,
                                    query,
                                    new FeedOptions()
                                    {
                                        EnableScanInQuery = true,
                                        PartitionKeyRangeId = pkrange.Id
                                    }).ToList();
                            }
#endif
                            #region ExpectedUsingLinq
                            List<MultiOrderByDocument> castedDocuments = documents
                                .Select(x => JsonConvert.DeserializeObject<MultiOrderByDocument>(JsonConvert.SerializeObject(x)))
                                .ToList();

                            if (hasFilter)
                            {
                                castedDocuments = castedDocuments.Where(document => document.NumberField % 2 == 0).ToList();
                            }

                            IOrderedEnumerable<MultiOrderByDocument> oracle;
                            Cosmos.CompositePath firstCompositeIndex = compositeIndex.First();

                            isDesc = firstCompositeIndex.Order == Cosmos.CompositePathSortOrder.Descending ? true : false;
                            if (invert)
                            {
                                isDesc = !isDesc;
                            }

                            if (isDesc)
                            {
                                oracle = castedDocuments.OrderByDescending(x => x.GetType().GetProperty(firstCompositeIndex.Path.Replace("/", "")).GetValue(x, null));
                            }
                            else
                            {
                                oracle = castedDocuments.OrderBy(x => x.GetType().GetProperty(firstCompositeIndex.Path.Replace("/", "")).GetValue(x, null));
                            }

                            foreach (Cosmos.CompositePath compositePath in compositeIndex.Skip(1))
                            {
                                isDesc = compositePath.Order == Cosmos.CompositePathSortOrder.Descending ? true : false;
                                if (invert)
                                {
                                    isDesc = !isDesc;
                                }

                                if (isDesc)
                                {
                                    oracle = oracle.ThenByDescending(x => x.GetType().GetProperty(compositePath.Path.Replace("/", "")).GetValue(x, null));
                                }
                                else
                                {
                                    oracle = oracle.ThenBy(x => x.GetType().GetProperty(compositePath.Path.Replace("/", "")).GetValue(x, null));
                                }
                            }

                            List<CosmosArray> expected = new List<CosmosArray>();
                            foreach (MultiOrderByDocument document in oracle)
                            {
                                List<object> projectedItems = new List<object>();
                                foreach (Cosmos.CompositePath compositePath in compositeIndex)
                                {
                                    projectedItems.Add(
                                        typeof(MultiOrderByDocument)
                                        .GetProperty(compositePath.Path.Replace("/", ""))
                                        .GetValue(document, null));
                                }

                                List<CosmosElement> cosmosProjectedItems = projectedItems
                                    .Select(x => x == null ? CosmosNull.Create() : CosmosElement.Parse(JsonConvert.SerializeObject(x)))
                                    .ToList();

                                expected.Add(CosmosArray.Create(cosmosProjectedItems));
                            }

                            if (hasTop)
                            {
                                expected = expected.Take(topCount).ToList();
                            }

                            #endregion

                            QueryRequestOptions feedOptions = new QueryRequestOptions()
                            {
                                MaxBufferedItemCount = 1000,
                                MaxItemCount = 3,
                                MaxConcurrency = 10,
                            };

                            List<CosmosElement> actual = await QueryTestsBase.RunQueryAsync(
                                container,
                                query,
                                queryRequestOptions: feedOptions);
                            List<CosmosArray> actualCasted = actual.Select(x => (CosmosArray)x).ToList();

                            this.AssertMultiOrderByResults(expected, actualCasted, query);
                        }
                    }
                }
            }
        }

        private void AssertMultiOrderByResults(
            IReadOnlyList<CosmosArray> expected,
            IReadOnlyList<CosmosArray> actual,
            string query)
        {
            IEnumerable<(CosmosArray, CosmosArray)> expectedZippedWithActual = expected
                .Zip(actual, (first, second) => (first, second));

            foreach ((CosmosArray first, CosmosArray second) in expectedZippedWithActual)
            {
                Assert.AreEqual(
                    expected: first,
                    actual: second,
                    message: $@"
                        query: {query}: 
                        first: {first}
                        second: {second}
                        expected: {JsonConvert.SerializeObject(expected).Replace(".0", "")}
                        actual: {JsonConvert.SerializeObject(actual).Replace(".0", "")}");
            }
        }

        [TestMethod]
        public async Task TestOrderByWithUndefinedFieldsAsync()
        {
            const string possiblyUndefinedFieldName = "possiblyUndefinedField";
            const string alwaysDefinedFieldName = "alwaysDefinedFieldName";
            List<string> inputDocuments = new List<string>();
            Random random = new Random();
            for (int i = 0; i < 100; i++)
            {
                Dictionary<string, CosmosElement> keyValuePairs = new Dictionary<string, CosmosElement>();
                bool shouldHaveDefinedField = (random.Next() % 2) == 0;
                if (shouldHaveDefinedField)
                {
                    keyValuePairs[possiblyUndefinedFieldName] = CosmosNumber64.Create(random.Next(0, 100));
                }

                keyValuePairs[alwaysDefinedFieldName] = CosmosNumber64.Create(random.Next(0, 100));

                CosmosObject document = CosmosObject.Create(keyValuePairs);
                inputDocuments.Add(document.ToString());
            }

            Cosmos.IndexingPolicy indexingPolicy = new Cosmos.IndexingPolicy()
            {
                IncludedPaths = new Collection<Cosmos.IncludedPath>()
                {
                    new Cosmos.IncludedPath()
                    {
                        Path = "/*",
                    },
                    new Cosmos.IncludedPath()
                    {
                        Path = $"/{possiblyUndefinedFieldName}/?",
                    },
                    new Cosmos.IncludedPath()
                    {
                        Path = $"/{alwaysDefinedFieldName}/?",
                    }
                },

                CompositeIndexes = new Collection<Collection<Cosmos.CompositePath>>()
            };

            foreach (bool switchColumns in new bool[] { true, false })
            {
                foreach (bool firstColumnAscending in new bool[] { true, false })
                {
                    foreach (bool secondColumnAscending in new bool[] { true, false })
                    {
                        Collection<Cosmos.CompositePath> compositeIndex = new Collection<Cosmos.CompositePath>()
                        {
                            new Cosmos.CompositePath()
                            {
                                Path = "/" + (switchColumns ? possiblyUndefinedFieldName : alwaysDefinedFieldName),
                                Order = firstColumnAscending ? Cosmos.CompositePathSortOrder.Ascending : Cosmos.CompositePathSortOrder.Descending,
                            },
                            new Cosmos.CompositePath()
                            {
                                Path = "/" + (switchColumns ? alwaysDefinedFieldName : possiblyUndefinedFieldName),
                                Order = secondColumnAscending ? Cosmos.CompositePathSortOrder.Ascending : Cosmos.CompositePathSortOrder.Descending,
                            },
                        };

                        indexingPolicy.CompositeIndexes.Add(compositeIndex);
                    }
                }
            }

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                const string possiblyUndefinedFieldName = "possiblyUndefinedField";
                const string alwaysDefinedFieldName = "alwaysDefinedFieldName";

                // Handle all the single order by cases
                foreach (bool ascending in new bool[] { true, false })
                {
                    List<CosmosElement> queryResults = await QueryTestsBase.RunQueryAsync(
                        container,
                        $"SELECT c.{alwaysDefinedFieldName}, c.{possiblyUndefinedFieldName} FROM c ORDER BY c.{possiblyUndefinedFieldName} {(ascending ? "ASC" : "DESC")}",
                        new QueryRequestOptions()
                        {
                            MaxItemCount = 1,
                        });

                    Assert.AreEqual(
                        documents.Count(),
                        queryResults.Count);

                    IEnumerable<CosmosElement> actual = queryResults
                        .Select(x =>
                        {
                            if (!((CosmosObject)x).TryGetValue(possiblyUndefinedFieldName, out CosmosElement cosmosElement))
                            {
                                cosmosElement = null;
                            }

                            return cosmosElement;
                        });

                    IEnumerable<CosmosElement> expected = documents
                        .Select(x =>
                        {
                            if (!x.TryGetValue(possiblyUndefinedFieldName, out CosmosElement cosmosElement))
                            {
                                cosmosElement = null;
                            }

                            return cosmosElement;
                        });

                    if (ascending)
                    {
                        expected = expected.OrderBy(x => x, MockOrderByComparer.Value);
                    }
                    else
                    {
                        expected = expected.OrderByDescending(x => x, MockOrderByComparer.Value);
                    }

                    Assert.IsTrue(
                        expected.SequenceEqual(actual),
                        $"Expected: {JsonConvert.SerializeObject(expected)}" +
                        $"Actual: {JsonConvert.SerializeObject(actual)}");
                }

                // Handle all the multi order by cases
                foreach (bool switchColumns in new bool[] { true, false })
                {
                    foreach (bool firstColumnAscending in new bool[] { true, false })
                    {
                        foreach (bool secondColumnAscending in new bool[] { true, false })
                        {
                            string query = $"" +
                                $"SELECT c.{(switchColumns ? possiblyUndefinedFieldName : alwaysDefinedFieldName)}, c.{(switchColumns ? alwaysDefinedFieldName : possiblyUndefinedFieldName)} " +
                                $"FROM c " +
                                $"ORDER BY " +
                                $"  c.{(switchColumns ? possiblyUndefinedFieldName : alwaysDefinedFieldName)} {(firstColumnAscending ? "ASC" : "DESC")}, " +
                                $"  c.{(switchColumns ? alwaysDefinedFieldName : possiblyUndefinedFieldName)} {(secondColumnAscending ? "ASC" : "DESC")}";

                            List<CosmosElement> queryResults = await QueryTestsBase.RunQueryAsync(
                                container,
                                query,
                                new QueryRequestOptions()
                                {
                                    MaxItemCount = 1,
                                });

                            Assert.AreEqual(
                                documents.Count(),
                                queryResults.Count);

                            IEnumerable<CosmosElement> actual = queryResults;

                            IOrderedEnumerable<CosmosElement> expected;
                            expected = firstColumnAscending
                                ? documents.OrderBy(
                                    x =>
                                    {
                                        if (!((CosmosObject)x).TryGetValue(switchColumns ? possiblyUndefinedFieldName : alwaysDefinedFieldName, out CosmosElement cosmosElement))
                                        {
                                            cosmosElement = null;
                                        }

                                        return cosmosElement;
                                    }, MockOrderByComparer.Value)
                                : documents.OrderByDescending(
                                    x =>
                                    {
                                        if (!((CosmosObject)x).TryGetValue(switchColumns ? possiblyUndefinedFieldName : alwaysDefinedFieldName, out CosmosElement cosmosElement))
                                        {
                                            cosmosElement = null;
                                        }

                                        return cosmosElement;
                                    }, MockOrderByComparer.Value);

                            expected = secondColumnAscending
                                ? expected.ThenBy(
                                    x =>
                                    {
                                        if (!((CosmosObject)x).TryGetValue(switchColumns ? alwaysDefinedFieldName : possiblyUndefinedFieldName, out CosmosElement cosmosElement))
                                        {
                                            cosmosElement = null;
                                        }

                                        return cosmosElement;
                                    }, MockOrderByComparer.Value)
                                : expected.ThenByDescending(
                                    x =>
                                    {
                                        if (!((CosmosObject)x).TryGetValue(switchColumns ? alwaysDefinedFieldName : possiblyUndefinedFieldName, out CosmosElement cosmosElement))
                                        {
                                            cosmosElement = null;
                                        }

                                        return cosmosElement;
                                    }, MockOrderByComparer.Value);

                            IEnumerable<CosmosElement> expectedFinal = expected.Select(
                                x =>
                                {
                                    Dictionary<string, CosmosElement> keyValuePairs = new Dictionary<string, CosmosElement>
                                    {
                                        [alwaysDefinedFieldName] = ((CosmosObject)x)[alwaysDefinedFieldName]
                                    };

                                    if (((CosmosObject)x).TryGetValue(possiblyUndefinedFieldName, out CosmosElement cosmosElement))
                                    {
                                        keyValuePairs[possiblyUndefinedFieldName] = cosmosElement;
                                    }

                                    return (CosmosElement)CosmosObject.Create(keyValuePairs);
                                });

                            Assert.IsTrue(
                                expectedFinal.SequenceEqual(actual),
                                $"Query: {query}" +
                                $"Expected: {JsonConvert.SerializeObject(expectedFinal)}" +
                                $"Actual: {JsonConvert.SerializeObject(actual)}");
                        }
                    }
                }
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync,
                indexingPolicy: indexingPolicy);
        }

        [TestMethod]
        public async Task TestMixedTypeOrderByAsync()
        {
            int numberOfDocuments = 1 << 4;
            int numberOfDuplicates = 1 << 2;

            List<string> documents = new List<string>(numberOfDocuments * numberOfDuplicates);
            Random random = new Random(1234);
            for (int i = 0; i < numberOfDocuments; ++i)
            {
                MixedTypedDocument mixedTypeDocument = OrderByQueryTests.GenerateMixedTypeDocument(random);
                for (int j = 0; j < numberOfDuplicates; j++)
                {
                    if (mixedTypeDocument.MixedTypeField != null)
                    {
                        documents.Add(JsonConvert.SerializeObject(mixedTypeDocument));
                    }
                    else
                    {
                        documents.Add("{}");
                    }
                }
            }

            // Add a composite index to force an index v2 container to be made.
            Cosmos.IndexingPolicy indexV2Policy = new Cosmos.IndexingPolicy()
            {
                IncludedPaths = new Collection<Cosmos.IncludedPath>()
                {
                    new Cosmos.IncludedPath()
                    {
                        Path = "/*",
                    },
                    new Cosmos.IncludedPath()
                    {
                        Path = $"/{nameof(MixedTypedDocument.MixedTypeField)}/?",
                    }
                },

                CompositeIndexes = new Collection<Collection<Cosmos.CompositePath>>()
                {
                    // Simple
                    new Collection<Cosmos.CompositePath>()
                    {
                        new Cosmos.CompositePath()
                        {
                            Path = "/_ts",
                        },
                        new Cosmos.CompositePath()
                        {
                            Path = "/_etag",
                        }
                    }
                }
            };

            OrderByTypes primitives = OrderByTypes.Bool | OrderByTypes.Null | OrderByTypes.Number | OrderByTypes.String;
            OrderByTypes nonPrimitives = OrderByTypes.Array | OrderByTypes.Object;
            OrderByTypes all = primitives | nonPrimitives | OrderByTypes.Undefined;

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                this.TestMixedTypeOrderByHelper,
                new OrderByTypes[]
                {
                    OrderByTypes.Array,
                    OrderByTypes.Bool,
                    OrderByTypes.Null,
                    OrderByTypes.Number,
                    OrderByTypes.Object,
                    OrderByTypes.String,
                    OrderByTypes.Undefined,
                    primitives,
                    nonPrimitives,
                    all,
                },
                "/id",
                indexV2Policy);
        }

        private sealed class MixedTypedDocument
        {
            public CosmosElement MixedTypeField { get; set; }
        }

        private static MixedTypedDocument GenerateMixedTypeDocument(Random random)
        {
            return new MixedTypedDocument()
            {
                MixedTypeField = GenerateRandomJsonValue(random),
            };
        }

        private static CosmosElement GenerateRandomJsonValue(Random random)
        {
            return (random.Next(0, 7)) switch
            {
                // Number
                0 => CosmosNumber64.Create(random.Next()),
                // String
                1 => CosmosString.Create(new string('a', random.Next(0, 100))),
                // Null
                2 => CosmosNull.Create(),
                // Bool
                3 => CosmosBoolean.Create((random.Next() % 2) == 0),
                // Object
                4 => CosmosObject.Create(new Dictionary<string, CosmosElement>()),
                // Array
                5 => CosmosArray.Create(new List<CosmosElement>()),
                // Undefined
                6 => null,
                _ => throw new ArgumentException(),
            };
        }

        private sealed class MockOrderByComparer : IComparer<CosmosElement>
        {
            public static readonly MockOrderByComparer Value = new MockOrderByComparer();

            public int Compare(CosmosElement element1, CosmosElement element2)
            {
                return ItemComparer.Instance.Compare(element1, element2);
            }

        }

        [Flags]
        private enum OrderByTypes
        {
            Number = 1 << 0,
            String = 1 << 1,
            Null = 1 << 2,
            Bool = 1 << 3,
            Object = 1 << 4,
            Array = 1 << 5,
            Undefined = 1 << 6,
        };

        private async Task TestMixedTypeOrderByHelper(
            Container container,
            IReadOnlyList<CosmosObject> documents,
            OrderByTypes[] args)
        {
            OrderByTypes[] orderByTypesList = args;
            foreach (bool isDesc in new bool[] { true, false })
            {
                foreach (OrderByTypes orderByTypes in orderByTypesList)
                {
                    string orderString = isDesc ? "DESC" : "ASC";
                    List<string> mixedTypeFilters = new List<string>();
                    if (orderByTypes.HasFlag(OrderByTypes.Array))
                    {
                        mixedTypeFilters.Add($"IS_ARRAY(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                    }

                    if (orderByTypes.HasFlag(OrderByTypes.Bool))
                    {
                        mixedTypeFilters.Add($"IS_BOOL(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                    }

                    if (orderByTypes.HasFlag(OrderByTypes.Null))
                    {
                        mixedTypeFilters.Add($"IS_NULL(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                    }

                    if (orderByTypes.HasFlag(OrderByTypes.Number))
                    {
                        mixedTypeFilters.Add($"IS_NUMBER(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                    }

                    if (orderByTypes.HasFlag(OrderByTypes.Object))
                    {
                        mixedTypeFilters.Add($"IS_OBJECT(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                    }

                    if (orderByTypes.HasFlag(OrderByTypes.String))
                    {
                        mixedTypeFilters.Add($"IS_STRING(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                    }

                    if (orderByTypes.HasFlag(OrderByTypes.Undefined))
                    {
                        mixedTypeFilters.Add($"not IS_DEFINED(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                    }

                    string filter = mixedTypeFilters.Count() == 0 ? "true" : string.Join(" OR ", mixedTypeFilters);

                    string query = $@"
                            SELECT c.{nameof(MixedTypedDocument.MixedTypeField)}
                            FROM c
                            WHERE {filter}
                            ORDER BY c.{nameof(MixedTypedDocument.MixedTypeField)} {orderString}";

                    QueryRequestOptions feedOptions = new QueryRequestOptions()
                    {
                        MaxBufferedItemCount = 1000,
                        MaxItemCount = 16,
                        MaxConcurrency = 10,
                    };

#if false
                        For now we can not serve the query through continuation tokens correctly.
                        This is because we allow order by on mixed types but not comparisions across types
                        For example suppose the following query:
                            SELECT c.MixedTypeField FROM c ORDER BY c.MixedTypeField
                        returns:
                        [
                            {"MixedTypeField":[]},
                            {"MixedTypeField":[1, 2, 3]},
                            {"MixedTypeField":{}},
                        ]
                        and we left off on [1, 2, 3] then at some point the cross partition code resumes the query by running the following:
                            SELECT c.MixedTypeField FROM c WHERE c.MixedTypeField > [1, 2, 3] ORDER BY c.MixedTypeField
                        And comparison on arrays and objects is undefined.
#endif

                    List<CosmosElement> actual = await QueryTestsBase.QueryWithoutContinuationTokensAsync<CosmosElement>(
                        container,
                        query,
                        queryRequestOptions: feedOptions);

                    IEnumerable<CosmosObject> insertedDocs = documents
                        .Select(document => CosmosElement.CreateFromBuffer<CosmosObject>(Encoding.UTF8.GetBytes(document.ToString())))
                        .Select(document =>
                        {
                            Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>();
                            if (document.TryGetValue(nameof(MixedTypedDocument.MixedTypeField), out CosmosElement value))
                            {
                                dictionary.Add(nameof(MixedTypedDocument.MixedTypeField), value);
                            }

                            return CosmosObject.Create(dictionary);
                        });

                    // Build the expected results using LINQ
                    IEnumerable<CosmosObject> expected = new List<CosmosObject>();

                    // Filter based on the mixedOrderByType enum

                    if (orderByTypes.HasFlag(OrderByTypes.Undefined))
                    {
                        expected = expected.Concat(insertedDocs.Where(x => !x.TryGetValue(nameof(MixedTypedDocument.MixedTypeField), out CosmosElement value)));
                    }

                    if (orderByTypes.HasFlag(OrderByTypes.Null))
                    {
                        expected = expected.Concat(insertedDocs.Where(x => x.TryGetValue(nameof(MixedTypedDocument.MixedTypeField), out CosmosNull value)));
                    }

                    if (orderByTypes.HasFlag(OrderByTypes.Bool))
                    {
                        expected = expected.Concat(insertedDocs.Where(x => x.TryGetValue(nameof(MixedTypedDocument.MixedTypeField), out CosmosBoolean value)));
                    }

                    if (orderByTypes.HasFlag(OrderByTypes.Number))
                    {
                        expected = expected.Concat(insertedDocs.Where(x => x.TryGetValue(nameof(MixedTypedDocument.MixedTypeField), out CosmosNumber value)));
                    }

                    if (orderByTypes.HasFlag(OrderByTypes.String))
                    {
                        expected = expected.Concat(insertedDocs.Where(x => x.TryGetValue(nameof(MixedTypedDocument.MixedTypeField), out CosmosString value)));
                    }

                    if (orderByTypes.HasFlag(OrderByTypes.Array))
                    {
                        expected = expected.Concat(insertedDocs.Where(x => x.TryGetValue(nameof(MixedTypedDocument.MixedTypeField), out CosmosArray value)));
                    }

                    if (orderByTypes.HasFlag(OrderByTypes.Object))
                    {
                        expected = expected.Concat(insertedDocs.Where(x => x.TryGetValue(nameof(MixedTypedDocument.MixedTypeField), out CosmosObject value)));
                    }

                    // Order using the mock order by comparer
                    if (isDesc)
                    {
                        expected = expected.OrderByDescending(x =>
                        {
                            if (!x.TryGetValue(nameof(MixedTypedDocument.MixedTypeField), out CosmosElement cosmosElement))
                            {
                                cosmosElement = null;
                            }

                            return cosmosElement;
                        }, MockOrderByComparer.Value);
                    }
                    else
                    {
                        expected = expected.OrderBy(x =>
                        {
                            if (!x.TryGetValue(nameof(MixedTypedDocument.MixedTypeField), out CosmosElement cosmosElement))
                            {
                                cosmosElement = null;
                            }

                            return cosmosElement;
                        }, MockOrderByComparer.Value);
                    }

                    Assert.IsTrue(
                        expected.SequenceEqual(actual),
                        $@" queryWithoutContinuations: {query},
                            expected:{JsonConvert.SerializeObject(expected)},
                            actual: {JsonConvert.SerializeObject(actual)}");
                }
            }
        }

        class OrderByRequestChargeArgs
        {
            public string Query { get; set; }
        }

        [TestMethod]
        public async Task TestQueryCrossPartitionRequestChargesAsync()
        {
            string[] documents = new[]
            {
                @"{""id"":""documentId1"",""key"":""A""}",
                @"{""id"":""documentId2"",""key"":""A"",""prop"":3}",
                @"{""id"":""documentId3"",""key"":""A""}",
                @"{""id"":""documentId4"",""key"":5}",
                @"{""id"":""documentId5"",""key"":5,""prop"":2}",
                @"{""id"":""documentId6"",""key"":5}",
                @"{""id"":""documentId7"",""key"":2}",
                @"{""id"":""documentId8"",""key"":2,""prop"":1}",
                @"{""id"":""documentId9"",""key"":2}",
            };

            // Matches no documents
            await this.CreateIngestQueryDeleteAsync<OrderByRequestChargeArgs>(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                documents,
                this.TestQueryCrossPartitionRequestChargesHelper,
                new OrderByRequestChargeArgs
                {
                    Query = "SELECT r.id FROM r WHERE r.prop = 'A' ORDER BY r.prop DESC",
                },
                "/key");

            // Matches some documents
            await this.CreateIngestQueryDeleteAsync<OrderByRequestChargeArgs>(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                documents,
                this.TestQueryCrossPartitionRequestChargesHelper,
                new OrderByRequestChargeArgs
                {
                    Query = "SELECT r.id FROM r ORDER BY r.prop DESC",
                },
                "/key");

            // Matches some documents, skipped with OFFSET LIMIT
            await this.CreateIngestQueryDeleteAsync<OrderByRequestChargeArgs>(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                documents,
                this.TestQueryCrossPartitionRequestChargesHelper,
                new OrderByRequestChargeArgs
                {
                    Query = "SELECT r.id FROM r ORDER BY r.prop DESC OFFSET 10 LIMIT 1",
                },
                "/key");
        }

        private async Task TestQueryCrossPartitionRequestChargesHelper(
            Container container,
            IReadOnlyList<CosmosObject> documents,
            OrderByRequestChargeArgs args)
        {
            base.DirectRequestChargeHandler.StartTracking();

            double totalRUs = 0;
            await foreach (FeedResponse<CosmosElement> query in QueryTestsBase.RunSimpleQueryAsync<CosmosElement>(
                container,
                args.Query,
                new QueryRequestOptions()
                {
                    MaxItemCount = 1,
                    MaxConcurrency = 1,
                }))
            {
                totalRUs += query.RequestCharge;
            }

            double expectedRequestCharge = base.DirectRequestChargeHandler.StopTracking();
            Assert.AreEqual(expectedRequestCharge, totalRUs, 0.01);
        }

    }
}

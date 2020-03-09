

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [TestClass]
    public sealed class PartitioningCrossPartitionQueryTests : CrossPartitionQueryTestsBase
    {
        [TestMethod]
        public async Task TestQueryWithPartitionKeyAsync()
        {
            string[] inputDocs = new[]
            {
                @"{""id"":""documentId1"",""key"":""A"",""prop"":3,""shortArray"":[{""a"":5}]}",
                @"{""id"":""documentId2"",""key"":""A"",""prop"":2,""shortArray"":[{""a"":6}]}",
                @"{""id"":""documentId3"",""key"":""A"",""prop"":1,""shortArray"":[{""a"":7}]}",
                @"{""id"":""documentId4"",""key"":5,""prop"":3,""shortArray"":[{""a"":5}]}",
                @"{""id"":""documentId5"",""key"":5,""prop"":2,""shortArray"":[{""a"":6}]}",
                @"{""id"":""documentId6"",""key"":5,""prop"":1,""shortArray"":[{""a"":7}]}",
                @"{""id"":""documentId10"",""prop"":3,""shortArray"":[{""a"":5}]}",
                @"{""id"":""documentId11"",""prop"":2,""shortArray"":[{""a"":6}]}",
                @"{""id"":""documentId12"",""prop"":1,""shortArray"":[{""a"":7}]}",
            };

            async Task ImplementationAsync(Container container, IEnumerable<Document> documents)
            {
                Assert.AreEqual(0, (await CrossPartitionQueryTestsBase.RunQueryAsync<Document>(
                container,
                @"SELECT * FROM Root r WHERE false",
                new QueryRequestOptions()
                {
                    MaxConcurrency = 1,
                })).Count);

                object[] keys = new object[] { "A", 5, Undefined.Value };
                for (int i = 0; i < keys.Length; ++i)
                {
                    List<string> expected = documents.Skip(i * 3).Take(3).Select(doc => doc.Id).ToList();
                    string expectedResult = string.Join(",", expected);
                    // Order-by
                    expected.Reverse();
                    string expectedOrderByResult = string.Join(",", expected);

                    List<(string, string)> queries = new List<(string, string)>()
                {
                    ($@"SELECT * FROM Root r WHERE r.id IN (""{expected[0]}"", ""{expected[1]}"", ""{expected[2]}"")", expectedResult),
                    (@"SELECT * FROM Root r WHERE r.prop BETWEEN 1 AND 3", expectedResult),
                    (@"SELECT VALUE r FROM Root r JOIN c IN r.shortArray WHERE c.a BETWEEN 5 and 7", expectedResult),
                    ($@"SELECT TOP 10 * FROM Root r WHERE r.id IN (""{expected[0]}"", ""{expected[1]}"", ""{expected[2]}"")", expectedResult),
                    (@"SELECT TOP 10 * FROM Root r WHERE r.prop BETWEEN 1 AND 3", expectedResult),
                    (@"SELECT TOP 10 VALUE r FROM Root r JOIN c IN r.shortArray WHERE c.a BETWEEN 5 and 7", expectedResult),
                    ($@"SELECT * FROM Root r WHERE r.id IN (""{expected[0]}"", ""{expected[1]}"", ""{expected[2]}"") ORDER BY r.prop", expectedOrderByResult),
                    (@"SELECT * FROM Root r WHERE r.prop BETWEEN 1 AND 3 ORDER BY r.prop", expectedOrderByResult),
                    (@"SELECT VALUE r FROM Root r JOIN c IN r.shortArray WHERE c.a BETWEEN 5 and 7 ORDER BY r.prop", expectedOrderByResult),
                };



                    if (i < keys.Length - 1)
                    {
                        string key;
                        if (keys[i] is string)
                        {
                            key = "'" + keys[i].ToString() + "'";
                        }
                        else
                        {
                            key = keys[i].ToString();
                        }

                        queries.Add((string.Format(CultureInfo.InvariantCulture, @"SELECT * FROM Root r WHERE r.key = {0} ORDER BY r.prop", key), expectedOrderByResult));
                    }

                    foreach ((string, string) queryAndExpectedResult in queries)
                    {
                        FeedIterator<Document> resultSetIterator = container.GetItemQueryIterator<Document>(
                            queryText: queryAndExpectedResult.Item1,
                            requestOptions: new QueryRequestOptions()
                            {
                                MaxItemCount = 1,
                                PartitionKey = new Cosmos.PartitionKey(keys[i]),
                            });

                        List<Document> result = new List<Document>();
                        while (resultSetIterator.HasMoreResults)
                        {
                            result.AddRange(await resultSetIterator.ReadNextAsync());
                        }

                        string resultDocIds = string.Join(",", result.Select(doc => doc.Id));
                        Assert.AreEqual(queryAndExpectedResult.Item2, resultDocIds);
                    }
                }
            }

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocs,
                ImplementationAsync,
                "/key");
        }

        [TestMethod]
        public async Task TestQuerySinglePartitionKey()
        {
            string[] inputDocs = new[]
            {
                @"{""pk"":""doc1""}",
                @"{""pk"":""doc2""}",
                @"{""pk"":""doc3""}",
                @"{""pk"":""doc4""}",
                @"{""pk"":""doc5""}",
                @"{""pk"":""doc6""}",
            };

            async Task ImplementationAsync(Container container, IEnumerable<Document> documents)
            {
                // Query with partition key should be done in one round trip.
                FeedIterator<dynamic> resultSetIterator = container.GetItemQueryIterator<dynamic>(
                    "SELECT * FROM c WHERE c.pk = 'doc5'");

                FeedResponse<dynamic> response = await resultSetIterator.ReadNextAsync();
                Assert.AreEqual(1, response.Count());
                Assert.IsNull(response.ContinuationToken);

                resultSetIterator = container.GetItemQueryIterator<dynamic>(
                   "SELECT * FROM c WHERE c.pk = 'doc10'");

                response = await resultSetIterator.ReadNextAsync();
                Assert.AreEqual(0, response.Count());
                Assert.IsNull(response.ContinuationToken);
            }

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocs,
                ImplementationAsync,
                partitionKey: "/pk");
        }

        private struct QueryWithSpecialPartitionKeysArgs
        {
            public string Name;
            public object Value;
            public Func<object, object> ValueToPartitionKey;
        }

        // V3 only supports Numeric, string, bool, null, undefined
        [TestMethod]
        [Ignore]
        public async Task TestQueryWithSpecialPartitionKeysAsync()
        {
            async Task ImplementationAsync(
                Container container,
                IEnumerable<Document> documents,
                QueryWithSpecialPartitionKeysArgs testArgs)
            {
                QueryWithSpecialPartitionKeysArgs args = testArgs;

                SpecialPropertyDocument specialPropertyDocument = new SpecialPropertyDocument
                {
                    Id = Guid.NewGuid().ToString()
                };

                specialPropertyDocument.GetType().GetProperty(args.Name).SetValue(specialPropertyDocument, args.Value);
                object getPropertyValueFunction(SpecialPropertyDocument d) => d.GetType().GetProperty(args.Name).GetValue(d);

                ItemResponse<SpecialPropertyDocument> response = await container.CreateItemAsync<SpecialPropertyDocument>(specialPropertyDocument);
                dynamic returnedDoc = response.Resource;
                Assert.AreEqual(args.Value, getPropertyValueFunction((SpecialPropertyDocument)returnedDoc));

                PartitionKey key = new PartitionKey(args.ValueToPartitionKey(args.Value));
                response = await container.ReadItemAsync<SpecialPropertyDocument>(response.Resource.Id, new Cosmos.PartitionKey(key));
                returnedDoc = response.Resource;
                Assert.AreEqual(args.Value, getPropertyValueFunction((SpecialPropertyDocument)returnedDoc));

                returnedDoc = (await this.RunSinglePartitionQuery<SpecialPropertyDocument>(
                    container,
                    "SELECT * FROM t")).Single();

                Assert.AreEqual(args.Value, getPropertyValueFunction(returnedDoc));

                string query;
                switch (args.Name)
                {
                    case "Guid":
                        query = $"SELECT * FROM T WHERE T.Guid = '{(Guid)args.Value}'";
                        break;
                    case "Enum":
                        query = $"SELECT * FROM T WHERE T.Enum = '{(HttpStatusCode)args.Value}'";
                        break;
                    case "DateTime":
                        query = $"SELECT * FROM T WHERE T.DateTime = '{(DateTime)args.Value}'";
                        break;
                    case "CustomEnum":
                        query = $"SELECT * FROM T WHERE T.CustomEnum = '{(HttpStatusCode)args.Value}'";
                        break;
                    case "ResourceId":
                        query = $"SELECT * FROM T WHERE T.ResourceId = '{(string)args.Value}'";
                        break;
                    case "CustomDateTime":
                        query = $"SELECT * FROM T WHERE T.CustomDateTime = '{(DateTime)args.Value}'";
                        break;
                    default:
                        query = null;
                        break;
                }

                returnedDoc = (await container.GetItemQueryIterator<SpecialPropertyDocument>(
                    query,
                    requestOptions: new QueryRequestOptions()
                    {
                        MaxItemCount = 1,
                        PartitionKey = new Cosmos.PartitionKey(args.ValueToPartitionKey),
                    }).ReadNextAsync()).First();

                Assert.AreEqual(args.Value, getPropertyValueFunction(returnedDoc));
            }
            QueryWithSpecialPartitionKeysArgs[] queryWithSpecialPartitionKeyArgsList = new QueryWithSpecialPartitionKeysArgs[]
            {
                new QueryWithSpecialPartitionKeysArgs()
                {
                    Name = "Guid",
                    Value = Guid.NewGuid(),
                    ValueToPartitionKey = val => val.ToString(),
                },
                //new QueryWithSpecialPartitionKeysArgs()
                //{
                //    Name = "DateTime",
                //    Value = DateTime.Now,
                //    ValueToPartitionKey = val =>
                //    {
                //        string str = JsonConvert.SerializeObject(
                //            val,
                //            new JsonSerializerSettings()
                //            {
                //                Converters = new List<JsonConverter> { new IsoDateTimeConverter() }
                //            });
                //        return str.Substring(1, str.Length - 2);
                //    },
                //},
                new QueryWithSpecialPartitionKeysArgs()
                {
                    Name = "Enum",
                    Value = HttpStatusCode.OK,
                    ValueToPartitionKey = val => (int)val,
                },
                new QueryWithSpecialPartitionKeysArgs()
                {
                    Name = "CustomEnum",
                    Value = HttpStatusCode.OK,
                    ValueToPartitionKey = val => val.ToString(),
                },
                new QueryWithSpecialPartitionKeysArgs()
                {
                    Name = "ResourceId",
                    Value = "testid",
                    ValueToPartitionKey = val => val,
                },
                new QueryWithSpecialPartitionKeysArgs()
                {
                    Name = "CustomDateTime",
                    Value = new DateTime(2016, 11, 12),
                    ValueToPartitionKey = val => EpochDateTimeConverter.DateTimeToEpoch((DateTime)val),
                },
            };

            foreach (QueryWithSpecialPartitionKeysArgs testArg in queryWithSpecialPartitionKeyArgsList)
            {
                // For this test we need to split direct and gateway runs into separate collections,
                // since the query callback inserts some documents (thus has side effects).
                await this.CreateIngestQueryDelete<QueryWithSpecialPartitionKeysArgs>(
                    ConnectionModes.Direct,
                    CollectionTypes.SinglePartition,
                    CrossPartitionQueryTestsBase.NoDocuments,
                    ImplementationAsync,
                    testArg,
                    "/" + testArg.Name);

                await this.CreateIngestQueryDelete<QueryWithSpecialPartitionKeysArgs>(
                    ConnectionModes.Direct,
                    CollectionTypes.MultiPartition,
                    CrossPartitionQueryTestsBase.NoDocuments,
                    ImplementationAsync,
                    testArg,
                    "/" + testArg.Name);

                await this.CreateIngestQueryDelete<QueryWithSpecialPartitionKeysArgs>(
                    ConnectionModes.Gateway,
                    CollectionTypes.SinglePartition,
                    CrossPartitionQueryTestsBase.NoDocuments,
                    ImplementationAsync,
                    testArg,
                    "/" + testArg.Name);

                await this.CreateIngestQueryDelete<QueryWithSpecialPartitionKeysArgs>(
                    ConnectionModes.Gateway,
                    CollectionTypes.MultiPartition,
                    CrossPartitionQueryTestsBase.NoDocuments,
                    ImplementationAsync,
                    testArg,
                    "/" + testArg.Name);
            }
        }

        private sealed class SpecialPropertyDocument
        {
            public string Id
            {
                get;
                set;
            }

            public Guid Guid
            {
                get;
                set;
            }

            [JsonConverter(typeof(IsoDateTimeConverter))]
            public DateTime DateTime
            {
                get;
                set;
            }

            [JsonConverter(typeof(EpochDateTimeConverter))]
            public DateTime CustomDateTime
            {
                get;
                set;
            }


            public HttpStatusCode Enum
            {
                get;
                set;
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public HttpStatusCode CustomEnum
            {
                get;
                set;
            }

            public string ResourceId
            {
                get;
                set;
            }
        }

        private sealed class EpochDateTimeConverter : JsonConverter
        {
            public static int DateTimeToEpoch(DateTime dt)
            {
                if (!dt.Equals(DateTime.MinValue))
                {
                    DateTime epoch = new DateTime(1970, 1, 1);
                    TimeSpan epochTimeSpan = dt - epoch;
                    return (int)epochTimeSpan.TotalSeconds;
                }
                else
                {
                    return int.MinValue;
                }
            }

            public override bool CanConvert(Type objectType)
            {
                return true;
            }

            public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.None || reader.TokenType == JsonToken.Null)
                {
                    return null;
                }


                if (reader.TokenType != JsonToken.Integer)
                {
                    throw new Exception(
                        string.Format(
                        CultureInfo.InvariantCulture,
                        "Unexpected token parsing date. Expected Integer, got {0}.",
                        reader.TokenType));
                }

                int seconds = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                return new DateTime(1970, 1, 1).AddSeconds(seconds);
            }

            public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, JsonSerializer serializer)
            {
                int seconds;
                if (value is DateTime)
                {
                    seconds = DateTimeToEpoch((DateTime)value);
                }
                else
                {
                    throw new Exception("Expected date object value.");
                }

                writer.WriteValue(seconds);
            }
        }

        private struct QueryCrossPartitionWithLargeNumberOfKeysArgs
        {
            public int NumberOfDocuments;
            public string PartitionKey;
            public HashSet<int> ExpectedPartitionKeyValues;
        }

        [TestMethod]
        public async Task TestQueryCrossPartitionWithLargeNumberOfKeys()
        {
            int numberOfDocuments = 1000;
            string partitionKey = "key";
            HashSet<int> expectedPartitionKeyValues = new HashSet<int>();
            List<string> documents = new List<string>();
            for (int i = 0; i < numberOfDocuments; i++)
            {
                Document doc = new Document();
                doc.SetPropertyValue(partitionKey, i);
                documents.Add(doc.ToString());

                expectedPartitionKeyValues.Add(i);
            }

            Assert.AreEqual(numberOfDocuments, expectedPartitionKeyValues.Count);

            QueryCrossPartitionWithLargeNumberOfKeysArgs args = new QueryCrossPartitionWithLargeNumberOfKeysArgs()
            {
                NumberOfDocuments = numberOfDocuments,
                PartitionKey = partitionKey,
                ExpectedPartitionKeyValues = expectedPartitionKeyValues,
            };

            async Task ImplementationAsync(
                Container container,
                IEnumerable<Document> inputDocs,
                QueryCrossPartitionWithLargeNumberOfKeysArgs testArgs)
            {
                QueryDefinition query = new QueryDefinition(
                $"SELECT VALUE r.{args.PartitionKey} FROM r WHERE ARRAY_CONTAINS(@keys, r.{testArgs.PartitionKey})").WithParameter("@keys", testArgs.ExpectedPartitionKeyValues);

                HashSet<int> actualPartitionKeyValues = new HashSet<int>();
                FeedIterator<int> documentQuery = container.GetItemQueryIterator<int>(
                        queryDefinition: query,
                        requestOptions: new QueryRequestOptions() { MaxItemCount = -1, MaxConcurrency = 100 });

                while (documentQuery.HasMoreResults)
                {
                    FeedResponse<int> response = await documentQuery.ReadNextAsync();
                    foreach (int item in response)
                    {
                        actualPartitionKeyValues.Add(item);
                    }
                }

                Assert.IsTrue(actualPartitionKeyValues.SetEquals(args.ExpectedPartitionKeyValues));
            }

            await this.CreateIngestQueryDelete<QueryCrossPartitionWithLargeNumberOfKeysArgs>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                ImplementationAsync,
                args,
                "/" + partitionKey);
        }
    }
}

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.CFP.AllVersionsAndDeletes
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("ChangeFeedProcessor")]
    public class BuilderWithCustomSerializerTests
    {
        [TestMethod]
        [Owner("philipthomas")]
        [Description("Validating to deserization of ChangeFeedItem with a Delete payload with TimeToLiveExpired set to true.")]
        [DataRow(true)]
        [DataRow(false)]
        public void ValidateNSJAndSTJSerializationOfChangeFeedItemDeleteTimeToLiveExpiredIsTrueTest(bool propertyNameCaseInsensitive)
        {
            string json = @"[
             {
              ""current"": {},
              ""metadata"": {
               ""lsn"": 17,
               ""crts"": 1722511591,
               ""operationType"": ""delete"",
               ""timeToLiveExpired"": true,
               ""previousImageLSN"": 16
              },
              ""previous"": {
               ""id"": ""1"",
               ""pk"": ""1"",
               ""description"": ""Testing TTL on CFP."",
               ""ttl"": 5,
               ""_rid"": ""SnxPAOM2VfMBAAAAAAAAAA=="",
               ""_self"": ""dbs/SnxPAA==/colls/SnxPAOM2VfM=/docs/SnxPAOM2VfMBAAAAAAAAAA==/"",
               ""_etag"": ""\""00000000-0000-0000-e405-5632b83c01da\"""",
               ""_attachments"": ""attachments/"",
               ""_ts"": 1722511453
              }
             }
            ]";

            ValidateSystemTextJsonDeserialization(json, propertyNameCaseInsensitive);
            ValidateNewtonsoftJsonDeserialization(json);

            static void ValidateSystemTextJsonDeserialization(string json, bool propertyNameCaseInsensitive)
            {
                ValidateDeserialization(
                    System.Text.Json.JsonSerializer.Deserialize<List<ChangeFeedItem<ToDoActivity>>>(
                        json: json,
                        options: new JsonSerializerOptions()
                        {
                            PropertyNameCaseInsensitive = propertyNameCaseInsensitive,
                        }));
            }

            static void ValidateNewtonsoftJsonDeserialization(string json)
            {
                ValidateDeserialization(JsonConvert.DeserializeObject<List<ChangeFeedItem<ToDoActivity>>>(json));
            }

            static void ValidateDeserialization(List<ChangeFeedItem<ToDoActivity>> activities)
            {
                Assert.IsNotNull(activities);

                ChangeFeedItem<ToDoActivity> deletedChange = activities.ElementAt(0);
                Assert.IsNotNull(deletedChange);
                Assert.IsNotNull(deletedChange.Current); // Current is not null, but not data.
                Assert.AreEqual(expected: default, actual: deletedChange.Current.description); // No current description for Delete
                Assert.AreEqual(expected: default, actual: deletedChange.Current.id); // No current id for Delete
                Assert.AreEqual(expected: default, actual: deletedChange.Current.ttl); // No current ttl for Delete
                Assert.IsNotNull(deletedChange.Metadata);
                Assert.AreEqual(expected: DateTime.Parse("8/1/2024 11:26:31 AM"), actual: deletedChange.Metadata.ConflictResolutionTimestamp);
                Assert.AreEqual(expected: 17, actual: deletedChange.Metadata.Lsn);
                Assert.AreEqual(expected: ChangeFeedOperationType.Delete, actual: deletedChange.Metadata.OperationType);
                Assert.AreEqual(expected: 16, actual: deletedChange.Metadata.PreviousLsn);
                Assert.IsTrue(deletedChange.Metadata.IsTimeToLiveExpired);
                Assert.IsNotNull(deletedChange.Previous);
                Assert.AreEqual(expected: "Testing TTL on CFP.", actual: deletedChange.Previous.description);
                Assert.AreEqual(expected: "1", actual: deletedChange.Previous.id);
                Assert.AreEqual(expected: 5, actual: deletedChange.Previous.ttl);
            }
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Validating to deserization using NSJ and STJ of ChangeFeedItem with a Create payload with TTL set to a non-default value.")]
        [DataRow(true)]
        [DataRow(false)]
        public void ValidateNSJAndSTJSerializationOfChangeFeedItemCreateTTLTest(bool propertyNameCaseInsensitive)
        {
            string json = @"[
             {
              ""current"": {
               ""id"": ""1"",
               ""pk"": ""1"",
               ""description"": ""Testing TTL on CFP."",
               ""ttl"": 5,
               ""_rid"": ""SnxPAOM2VfMBAAAAAAAAAA=="",
               ""_self"": ""dbs/SnxPAA==/colls/SnxPAOM2VfM=/docs/SnxPAOM2VfMBAAAAAAAAAA==/"",
               ""_etag"": ""\""00000000-0000-0000-e405-5632b83c01da\"""",
               ""_attachments"": ""attachments/"",
               ""_ts"": 1722511453
              },
              ""metadata"": {
               ""lsn"": 16,
               ""crts"": 1722511453,
               ""operationType"": ""create""
              }
             }
            ]";

            ValidateSystemTextJsonDeserialization(json, propertyNameCaseInsensitive);
            ValidateNewtonsoftJsonDeserialization(json);

            static void ValidateSystemTextJsonDeserialization(string json, bool propertyNameCaseInsensitive)
            {
                ValidateDeserialization(System.Text.Json.JsonSerializer.Deserialize<List<ChangeFeedItem<ToDoActivity>>>(
                    json: json,
                    options: new JsonSerializerOptions()
                    {
                        PropertyNameCaseInsensitive = propertyNameCaseInsensitive,
                    }));
            }

            static void ValidateNewtonsoftJsonDeserialization(string json)
            {
                ValidateDeserialization(JsonConvert.DeserializeObject<List<ChangeFeedItem<ToDoActivity>>>(json));
            }

            static void ValidateDeserialization(List<ChangeFeedItem<ToDoActivity>> activities)
            {
                Assert.IsNotNull(activities);

                ChangeFeedItem<ToDoActivity> createdUpdate = activities.ElementAt(0);
                Assert.IsNotNull(createdUpdate);
                Assert.IsNotNull(createdUpdate.Current);
                Assert.AreEqual(expected: "Testing TTL on CFP.", actual: createdUpdate.Current.description);
                Assert.AreEqual(expected: "1", actual: createdUpdate.Current.id);
                Assert.AreEqual(expected: 5, actual: createdUpdate.Current.ttl);
                Assert.IsNotNull(createdUpdate.Metadata);
                Assert.AreEqual(expected: DateTime.Parse("8/1/2024 11:24:13 AM"), actual: createdUpdate.Metadata.ConflictResolutionTimestamp);
                Assert.AreEqual(expected: 16, actual: createdUpdate.Metadata.Lsn);
                Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: createdUpdate.Metadata.OperationType);
                Assert.AreEqual(expected: 0, actual: createdUpdate.Metadata.PreviousLsn);
                Assert.IsFalse(createdUpdate.Metadata.IsTimeToLiveExpired);
                Assert.IsNull(createdUpdate.Previous); // No Previous for a Create change.
            }
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Validating to deserization using NSJ and STJ of ChangeFeedItem with a Create, Replace, and Delete payload.")]
        [DataRow(true)]
        [DataRow(false)]
        public void ValidateNSJAndSTJSerializationOfChangeFeedItemTest(bool propertyNameCaseInsensitive)
        {
            string json = @"[
             {
              ""current"": {
               ""id"": ""1"",
               ""pk"": ""1"",
               ""description"": ""original test"",
               ""_rid"": ""HpxDAL+dzLQBAAAAAAAAAA=="",
               ""_self"": ""dbs/HpxDAA==/colls/HpxDAL+dzLQ=/docs/HpxDAL+dzLQBAAAAAAAAAA==/"",
               ""_etag"": ""\""00000000-0000-0000-e384-28095c1a01da\"""",
               ""_attachments"": ""attachments/"",
               ""_ts"": 1722455970
              },
              ""metadata"": {
               ""crts"": 1722455970,
               ""lsn"": 374,
               ""operationType"": ""create"",
               ""previousImageLSN"": 0,
               ""timeToLiveExpired"": false
              }
             },
             {
              ""current"": {
               ""id"": ""1"",
               ""pk"": ""1"",
               ""description"": ""test after replace"",
               ""_rid"": ""HpxDAL+dzLQBAAAAAAAAAA=="",
               ""_self"": ""dbs/HpxDAA==/colls/HpxDAL+dzLQ=/docs/HpxDAL+dzLQBAAAAAAAAAA==/"",
               ""_etag"": ""\""00000000-0000-0000-e384-28a5abdd01da\"""",
               ""_attachments"": ""attachments/"",
               ""_ts"": 1722455971
              },
              ""metadata"": {
               ""crts"": 1722455971,
               ""lsn"": 375,
               ""operationType"": ""replace"",
               ""previousImageLSN"": 374,
               ""timeToLiveExpired"": false
              }
             },
             {
              ""current"": {},
              ""metadata"": {
               ""crts"": 1722455972,
               ""lsn"": 376,
               ""operationType"": ""delete"",
               ""previousImageLSN"": 375,
               ""timeToLiveExpired"": false
              },
              ""previous"": {
               ""id"": ""1"",
               ""pk"": ""1"",
               ""description"": ""test after replace"",
               ""_rid"": ""HpxDAL+dzLQBAAAAAAAAAA=="",
               ""_self"": ""dbs/HpxDAA==/colls/HpxDAL+dzLQ=/docs/HpxDAL+dzLQBAAAAAAAAAA==/"",
               ""_etag"": ""\""00000000-0000-0000-e384-28a5abdd01da\"""",
               ""_attachments"": ""attachments/"",
               ""_ts"": 1722455971
              }
             }
            ]";

            ValidateSystemTextJsonDeserialization(json, propertyNameCaseInsensitive);
            ValidateNewtonsoftJsonDeserialization(json);

            static void ValidateNewtonsoftJsonDeserialization(string json)
            {
                ValidateDeserialization(JsonConvert.DeserializeObject<List<ChangeFeedItem<ToDoActivity>>>(json));
            }

            static void ValidateSystemTextJsonDeserialization(string json, bool propertyNameCaseInsensitive)
            {
                ValidateDeserialization(System.Text.Json.JsonSerializer.Deserialize<List<ChangeFeedItem<ToDoActivity>>>(
                    json: json,
                    options: new JsonSerializerOptions()
                    {
                        PropertyNameCaseInsensitive = propertyNameCaseInsensitive
                    }));
            }

            static void ValidateDeserialization(List<ChangeFeedItem<ToDoActivity>> activities)
            {
                Assert.IsNotNull(activities);

                ChangeFeedItem<ToDoActivity> createdUpdate = activities.ElementAt(0);
                Assert.IsNotNull(createdUpdate);
                Assert.IsNotNull(createdUpdate.Current);
                Assert.AreEqual(expected: "original test", actual: createdUpdate.Current.description);
                Assert.AreEqual(expected: "1", actual: createdUpdate.Current.id);
                Assert.AreEqual(expected: 0, actual: createdUpdate.Current.ttl);
                Assert.IsNotNull(createdUpdate.Metadata);
                Assert.AreEqual(expected: DateTime.Parse("7/31/2024 7:59:30 PM"), actual: createdUpdate.Metadata.ConflictResolutionTimestamp);
                Assert.AreEqual(expected: 374, actual: createdUpdate.Metadata.Lsn);
                Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: createdUpdate.Metadata.OperationType);
                Assert.AreEqual(expected: 0, actual: createdUpdate.Metadata.PreviousLsn);
                Assert.IsFalse(createdUpdate.Metadata.IsTimeToLiveExpired);
                Assert.IsNull(createdUpdate.Previous); // No Previous for a Create change.

                ChangeFeedItem<ToDoActivity> replacedChange = activities.ElementAt(1);
                Assert.IsNotNull(replacedChange);
                Assert.IsNotNull(replacedChange.Current);
                Assert.AreEqual(expected: "test after replace", actual: replacedChange.Current.description);
                Assert.AreEqual(expected: "1", actual: replacedChange.Current.id);
                Assert.AreEqual(expected: 0, actual: replacedChange.Current.ttl);
                Assert.IsNotNull(replacedChange.Metadata);
                Assert.AreEqual(expected: DateTime.Parse("7/31/2024 7:59:31 PM"), actual: replacedChange.Metadata.ConflictResolutionTimestamp);
                Assert.AreEqual(expected: 375, actual: replacedChange.Metadata.Lsn);
                Assert.AreEqual(expected: ChangeFeedOperationType.Replace, actual: replacedChange.Metadata.OperationType);
                Assert.AreEqual(expected: 374, actual: replacedChange.Metadata.PreviousLsn);
                Assert.IsFalse(replacedChange.Metadata.IsTimeToLiveExpired);
                Assert.IsNull(replacedChange.Previous); // No Previous for a Replace change.

                ChangeFeedItem<ToDoActivity> deletedChange = activities.ElementAt(2);
                Assert.IsNotNull(deletedChange);
                Assert.IsNotNull(deletedChange.Current); // Current is not null, but not data.
                Assert.AreEqual(expected: default, actual: deletedChange.Current.description); // No current description for Delete
                Assert.AreEqual(expected: default, actual: deletedChange.Current.id); // No current id for Delete
                Assert.AreEqual(expected: default, actual: deletedChange.Current.ttl); // No current ttl for Delete
                Assert.IsNotNull(deletedChange.Metadata);
                Assert.AreEqual(expected: DateTime.Parse("7/31/2024 7:59:32 PM"), actual: deletedChange.Metadata.ConflictResolutionTimestamp);
                Assert.AreEqual(expected: 376, actual: deletedChange.Metadata.Lsn);
                Assert.AreEqual(expected: ChangeFeedOperationType.Delete, actual: deletedChange.Metadata.OperationType);
                Assert.AreEqual(expected: 375, actual: deletedChange.Metadata.PreviousLsn);
                Assert.IsFalse(deletedChange.Metadata.IsTimeToLiveExpired);
                Assert.IsNotNull(deletedChange.Previous);
                Assert.AreEqual(expected: "test after replace", actual: deletedChange.Previous.description);
                Assert.AreEqual(expected: "1", actual: deletedChange.Previous.id);
                Assert.AreEqual(expected: 0, actual: deletedChange.Previous.ttl);
            }
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Replace and Deletes have full ChangeFeedMetadata.")]
        [DataRow(true)]
        [DataRow(false)]
        public void ValidateChangeFeedMetadataSerializationReplaceAnDeleteWriteTest(bool propertyNameCaseInsensitive)
        {
            ChangeFeedMetadata metadata = new()
            {
                PreviousLsn = 15,
                Lsn = 374,
                OperationType = ChangeFeedOperationType.Create,
                IsTimeToLiveExpired = true,
                ConflictResolutionTimestamp = DateTime.Parse("7/31/2024 7:59:30 PM")
            };

            string json = System.Text.Json.JsonSerializer.Serialize<ChangeFeedMetadata>(
                value: metadata,
                options: new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = propertyNameCaseInsensitive
                });

            Assert.AreEqual(
                expected: @"{""crts"":1722455970,""timeToLiveExpired"":true,""lsn"":374,""operationType"":""Create"",""previousImageLSN"":15}",
                actual: json);
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Creates have partial ChangeFeedMetadata.")]
        [DataRow(true)]
        [DataRow(false)]
        public void ValidateChangeFeedMetadataSerializationCreateWriteTest(bool propertyNameCaseInsensitive)
        {
            ChangeFeedMetadata metadata = new()
            {
                Lsn = 374,
                OperationType = ChangeFeedOperationType.Create,
                ConflictResolutionTimestamp = DateTime.Parse("7/31/2024 7:59:30 PM")
            };

            string json = System.Text.Json.JsonSerializer.Serialize<ChangeFeedMetadata>(
                value: metadata,
                options: new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = propertyNameCaseInsensitive
                });

            Assert.AreEqual(
                expected: @"{""crts"":1722455970,""timeToLiveExpired"":false,""lsn"":374,""operationType"":""Create"",""previousImageLSN"":0}",
                actual: json);
        }

        [TestMethod]
        [Timeout(300000)]
        [TestCategory("LongRunning")]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When a document is created with ttl set, there should be 1 create and 1 delete that will appear for that " +
            "document when using ChangeFeedProcessor with AllVersionsAndDeletes set as the ChangeFeedMode.")]
        [DataRow(true)]
        [DataRow(false)]
        public async Task WhenADocumentIsCreatedWithTtlSetThenTheDocumentIsDeletedTestsAsync(bool propertyNameCaseInsensitive)
        {
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient((cosmosClientBuilder) =>
                cosmosClientBuilder.WithSystemTextJsonSerializerOptions(
                    new JsonSerializerOptions()
                    {
                        PropertyNameCaseInsensitive = propertyNameCaseInsensitive,
                    }),
                    useCustomSeralizer: false);

            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(id: Guid.NewGuid().ToString());
            Container leaseContainer = await database.CreateContainerIfNotExistsAsync(containerProperties: new ContainerProperties(id: "leases", partitionKeyPath: "/id"));
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes, database);
            Exception exception = default;
            int ttlInSeconds = 5;
            Stopwatch stopwatch = new();
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            ChangeFeedProcessor processor = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(processorName: "processor", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<ToDoActivity>> docs, CancellationToken token) =>
                {
                    // NOTE(philipthomas-MSFT): Please allow these Logger.LogLine because TTL on items will purge at random times so I am using this to test when ran locally using emulator.

                    Logger.LogLine($"@ {DateTime.Now}, {nameof(stopwatch)} -> CFP AVAD took '{stopwatch.ElapsedMilliseconds}' to read document CRUD in feed.");

                    foreach (ChangeFeedItem<ToDoActivity> change in docs)
                    {
                        if (change.Metadata.OperationType == ChangeFeedOperationType.Create)
                        {
                            // current
                            Assert.AreEqual(expected: "1", actual: change.Current.id.ToString());
                            Assert.AreEqual(expected: "1", actual: change.Current.pk.ToString());
                            Assert.AreEqual(expected: "Testing TTL on CFP.", actual: change.Current.description.ToString());
                            Assert.AreEqual(expected: ttlInSeconds, actual: change.Current.ttl);

                            // metadata
                            Assert.IsTrue(DateTime.TryParse(s: change.Metadata.ConflictResolutionTimestamp.ToString(), out _), message: "Invalid csrt must be a datetime value.");
                            Assert.IsTrue(change.Metadata.Lsn > 0, message: "Invalid lsn must be a long value.");
                            Assert.IsFalse(change.Metadata.IsTimeToLiveExpired);

                            // previous
                            Assert.IsNull(change.Previous);
                        }
                        else if (change.Metadata.OperationType == ChangeFeedOperationType.Delete)
                        {
                            // current
                            Assert.IsNull(change.Current.id);

                            // metadata
                            Assert.IsTrue(DateTime.TryParse(s: change.Metadata.ConflictResolutionTimestamp.ToString(), out _), message: "Invalid csrt must be a datetime value.");
                            Assert.IsTrue(change.Metadata.Lsn > 0, message: "Invalid lsn must be a long value.");
                            Assert.IsTrue(change.Metadata.IsTimeToLiveExpired);

                            // previous
                            Assert.AreEqual(expected: "1", actual: change.Previous.id.ToString());
                            Assert.AreEqual(expected: "1", actual: change.Previous.pk.ToString());
                            Assert.AreEqual(expected: "Testing TTL on CFP.", actual: change.Previous.description.ToString());
                            Assert.AreEqual(expected: ttlInSeconds, actual: change.Previous.ttl);

                            // stop after reading delete since it is the last document in feed.
                            stopwatch.Stop();
                            allDocsProcessed.Set();
                        }
                        else
                        {
                            Assert.Fail("Invalid operation.");
                        }
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName(Guid.NewGuid().ToString())
                .WithLeaseContainer(leaseContainer)
                .WithErrorNotification((leaseToken, error) =>
                {
                    exception = error.InnerException;

                    return Task.CompletedTask;
                })
                .Build();

            stopwatch.Start();

            // NOTE(philipthomas-MSFT): Please allow these Logger.LogLine because TTL on items will purge at random times so I am using this to test when ran locally using emulator.

            Logger.LogLine($"@ {DateTime.Now}, CFProcessor starting...");

            await processor.StartAsync();

            try
            {
                await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
                await monitoredContainer.CreateItemAsync<ToDoActivity>(new ToDoActivity { id = "1", pk = "1", description = "Testing TTL on CFP.", ttl = ttlInSeconds }, partitionKey: new PartitionKey("1"));

                // NOTE(philipthomas-MSFT): Please allow these Logger.LogLine because TTL on items will purge at random times so I am using this to test when ran locally using emulator.

                Logger.LogLine($"@ {DateTime.Now}, Document created.");

                bool receivedDelete = allDocsProcessed.WaitOne(250000);
                Assert.IsTrue(receivedDelete, "Timed out waiting for docs to process");

                if (exception != default)
                {
                    Assert.Fail(exception.ToString());
                }
            }
            finally
            {
                await processor.StopAsync();
            }

            if (database != null)
            {
                await database.DeleteAsync();
            }

            cosmosClient?.Dispose();
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When a document is created, then updated, and finally deleted, there should be 3 changes that will appear for that " +
            "document when using ChangeFeedProcessor with AllVersionsAndDeletes set as the ChangeFeedMode.")]
        [DataRow(true)]
        [DataRow(false)]
        public async Task WhenADocumentIsCreatedThenUpdatedThenDeletedTestsAsync(bool propertyNameCaseInsensitive)
        {
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient((cosmosClientBuilder) =>
                cosmosClientBuilder.WithSystemTextJsonSerializerOptions(
                    new JsonSerializerOptions()
                    {
                        PropertyNameCaseInsensitive = propertyNameCaseInsensitive
                    }),
                    useCustomSeralizer: false);

            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(id: Guid.NewGuid().ToString());
            Container leaseContainer = await database.CreateContainerIfNotExistsAsync(containerProperties: new ContainerProperties(id: "leases", partitionKeyPath: "/id"));
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes, database);
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);
            Exception exception = default;

            ChangeFeedProcessor processor = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(processorName: "processor", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<ToDoActivity>> docs, CancellationToken token) =>
                {
                    Logger.LogLine($"@ {DateTime.Now}, {nameof(docs)} -> {System.Text.Json.JsonSerializer.Serialize(docs)}");

                    string id = default;
                    string pk = default;
                    string description = default;

                    foreach (ChangeFeedItem<ToDoActivity> change in docs)
                    {
                        if (change.Metadata.OperationType != ChangeFeedOperationType.Delete)
                        {
                            id = change.Current.id.ToString();
                            pk = change.Current.pk.ToString();
                            description = change.Current.description.ToString();
                        }
                        else
                        {
                            id = change.Previous.id.ToString();
                            pk = change.Previous.pk.ToString();
                            description = change.Previous.description.ToString();
                        }

                        ChangeFeedOperationType operationType = change.Metadata.OperationType;
                        long previousLsn = change.Metadata.PreviousLsn;
                        DateTime m = change.Metadata.ConflictResolutionTimestamp;
                        long lsn = change.Metadata.Lsn;
                        bool isTimeToLiveExpired = change.Metadata.IsTimeToLiveExpired;
                    }

                    Assert.IsNotNull(context.LeaseToken);
                    Assert.IsNotNull(context.Diagnostics);
                    Assert.IsNotNull(context.Headers);
                    Assert.IsNotNull(context.Headers.Session);
                    Assert.IsTrue(context.Headers.RequestCharge > 0);
                    Assert.IsTrue(context.Diagnostics.ToString().Contains("Change Feed Processor Read Next Async"));
                    Assert.AreEqual(expected: 3, actual: docs.Count);

                    ChangeFeedItem<ToDoActivity> createChange = docs.ElementAt(0);
                    Assert.IsNotNull(createChange.Current);
                    Assert.AreEqual(expected: "1", actual: createChange.Current.id.ToString());
                    Assert.AreEqual(expected: "1", actual: createChange.Current.pk.ToString());
                    Assert.AreEqual(expected: "original test", actual: createChange.Current.description.ToString());
                    Assert.AreEqual(expected: createChange.Metadata.OperationType, actual: ChangeFeedOperationType.Create);
                    Assert.AreEqual(expected: createChange.Metadata.PreviousLsn, actual: 0);
                    Assert.IsNull(createChange.Previous);

                    ChangeFeedItem<ToDoActivity> replaceChange = docs.ElementAt(1);
                    Assert.IsNotNull(replaceChange.Current);
                    Assert.AreEqual(expected: "1", actual: replaceChange.Current.id.ToString());
                    Assert.AreEqual(expected: "1", actual: replaceChange.Current.pk.ToString());
                    Assert.AreEqual(expected: "test after replace", actual: replaceChange.Current.description.ToString());
                    Assert.AreEqual(expected: replaceChange.Metadata.OperationType, actual: ChangeFeedOperationType.Replace);
                    Assert.AreEqual(expected: createChange.Metadata.Lsn, actual: replaceChange.Metadata.PreviousLsn);
                    Assert.IsNull(replaceChange.Previous);

                    ChangeFeedItem<ToDoActivity> deleteChange = docs.ElementAt(2);
                    Assert.IsNull(deleteChange.Current.id);
                    Assert.AreEqual(expected: deleteChange.Metadata.OperationType, actual: ChangeFeedOperationType.Delete);
                    Assert.AreEqual(expected: replaceChange.Metadata.Lsn, actual: deleteChange.Metadata.PreviousLsn);
                    Assert.IsNotNull(deleteChange.Previous);
                    Assert.AreEqual(expected: "1", actual: deleteChange.Previous.id.ToString());
                    Assert.AreEqual(expected: "1", actual: deleteChange.Previous.pk.ToString());
                    Assert.AreEqual(expected: "test after replace", actual: deleteChange.Previous.description.ToString());

                    Assert.IsTrue(condition: createChange.Metadata.ConflictResolutionTimestamp < replaceChange.Metadata.ConflictResolutionTimestamp, message: "The create operation must happen before the replace operation.");
                    Assert.IsTrue(condition: replaceChange.Metadata.ConflictResolutionTimestamp < deleteChange.Metadata.ConflictResolutionTimestamp, message: "The replace operation must happen before the delete operation.");
                    Assert.IsTrue(condition: createChange.Metadata.Lsn < replaceChange.Metadata.Lsn, message: "The create operation must happen before the replace operation.");
                    Assert.IsTrue(condition: createChange.Metadata.Lsn < replaceChange.Metadata.Lsn, message: "The replace operation must happen before the delete operation.");

                    return Task.CompletedTask;
                })
                .WithInstanceName(Guid.NewGuid().ToString())
                .WithLeaseContainer(leaseContainer)
                .WithErrorNotification((leaseToken, error) =>
                {
                    exception = error.InnerException;

                    return Task.CompletedTask;
                })
                .Build();

            // Start the processor, insert 1 document to generate a checkpoint, modify it, and then delete it.
            // 1 second delay between operations to get different timestamps.

            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            await monitoredContainer.CreateItemAsync<ToDoActivity>(new ToDoActivity { id = "1", pk = "1", description = "original test", ttl = -1 }, partitionKey: new PartitionKey("1"));
            await Task.Delay(1000);

            await monitoredContainer.UpsertItemAsync<ToDoActivity>(new ToDoActivity { id = "1", pk = "1", description = "test after replace", ttl = -1 }, partitionKey: new PartitionKey("1"));
            await Task.Delay(1000);

            await monitoredContainer.DeleteItemAsync<ToDoActivity>(id: "1", partitionKey: new PartitionKey("1"));

            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            await processor.StopAsync();

            if (exception != default)
            {
                Assert.Fail(exception.ToString());
            }

            if (database != null)
            {
                await database.DeleteAsync();
            }

            cosmosClient?.Dispose();
        }

        private static async Task RevertLeaseDocumentsToLegacyWithNoMode(
            Container leaseContainer,
            int leaseDocumentCount)
        {
            FeedIterator iterator = leaseContainer.GetItemQueryStreamIterator(
                queryText: "SELECT * FROM c",
                continuationToken: null);

            List<JObject> leases = new List<JObject>();
            while (iterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage = await iterator.ReadNextAsync().ConfigureAwait(false))
                {
                    responseMessage.EnsureSuccessStatusCode();
                    leases.AddRange(CosmosFeedResponseSerializer.FromFeedResponseStream<JObject>(
                        serializerCore: CosmosContainerExtensions.DefaultJsonSerializer,
                        streamWithServiceEnvelope: responseMessage.Content));
                }
            }

            int counter = 0;

            foreach (JObject lease in leases)
            {
                if (!lease.ContainsKey("Mode"))
                {
                    continue;
                }

                counter++;
                lease.Remove("Mode");

                _ = await leaseContainer.UpsertItemAsync(item: lease);
            }

            Assert.AreEqual(expected: leaseDocumentCount, actual: counter);
        }

        private static async Task BuildChangeFeedProcessorWithLatestVersionAsync(
            ContainerInternal monitoredContainer,
            Container leaseContainer,
            ManualResetEvent allDocsProcessed,
            bool withStartFromBeginning)
        {
            Exception exception = default;
            ChangeFeedProcessor latestVersionProcessorAtomic = null;

            ChangeFeedProcessorBuilder processorBuilder = monitoredContainer
                .GetChangeFeedProcessorBuilder(processorName: $"processorName", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ToDoActivity> documents, CancellationToken token) => Task.CompletedTask)
                .WithInstanceName(Guid.NewGuid().ToString())
                .WithLeaseContainer(leaseContainer)
                .WithErrorNotification((leaseToken, error) =>
                {
                    exception = error.InnerException;

                    return Task.CompletedTask;
                });

            if (withStartFromBeginning)
            {
                processorBuilder.WithStartFromBeginning();
            }

            ChangeFeedProcessor processor = processorBuilder.Build();
            Interlocked.Exchange(ref latestVersionProcessorAtomic, processor);

            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            if (exception != default)
            {
                Assert.Fail(exception.ToString());
            }
        }

        private static async Task BuildChangeFeedProcessorWithAllVersionsAndDeletesAsync(
            ContainerInternal monitoredContainer,
            Container leaseContainer,
            ManualResetEvent allDocsProcessed)
        {
            Exception exception = default;
            ChangeFeedProcessor allVersionsAndDeletesProcessorAtomic = null;

            ChangeFeedProcessorBuilder allVersionsAndDeletesProcessorBuilder = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(processorName: $"processorName", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<ToDoActivity>> documents, CancellationToken token) => Task.CompletedTask)
                .WithInstanceName(Guid.NewGuid().ToString())
                .WithMaxItems(1)
                .WithLeaseContainer(leaseContainer)
                .WithErrorNotification((leaseToken, error) =>
                {
                    exception = error.InnerException;

                    return Task.FromResult(exception);
                });

            ChangeFeedProcessor processor = allVersionsAndDeletesProcessorBuilder.Build();
            Interlocked.Exchange(ref allVersionsAndDeletesProcessorAtomic, processor);

            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            if (exception != default)
            {
                Assert.Fail(exception.ToString());
            }
        }

        private async Task<ContainerInternal> CreateMonitoredContainer(
            ChangeFeedMode changeFeedMode,
            Database database)
        {
            string PartitionKey = "/pk";
            ContainerProperties properties = new ContainerProperties(id: Guid.NewGuid().ToString(),
                partitionKeyPath: PartitionKey);

            if (changeFeedMode == ChangeFeedMode.AllVersionsAndDeletes)
            {
                properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
                properties.DefaultTimeToLive = -1;
            }

            ContainerResponse response = await database.CreateContainerAsync(properties,
                throughput: 10000,
                cancellationToken: CancellationToken.None);

            return (ContainerInternal)response;
        }
    }
}

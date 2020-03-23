//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class ScriptSampleTests
    {
        private DocumentClient client;
        private Database database;
        private DocumentCollection collection;
        private string triggerName;

        [TestInitialize]
        public void TestInitialize()
        {
            this.client = TestCommon.CreateClient(true);
            this.database = TestCommon.CreateOrGetDatabase(this.client);
            PartitionKeyDefinition defaultPartitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
            this.collection = new DocumentCollection() { Id = Guid.NewGuid().ToString(), PartitionKey = defaultPartitionKeyDefinition };
            this.collection.IndexingPolicy.IndexingMode = IndexingMode.Lazy;

            try
            {
                this.collection = this.client.CreateDocumentCollectionAsync(this.database.SelfLink, this.collection).Result;
            }
            catch (DocumentClientException exception)
            {
                Assert.Fail(exception.InnerException.Message);
            }

            this.triggerName = "uniqueConstraint_" + Guid.NewGuid().ToString("N");
            string triggerContent = File.ReadAllText("ScriptSampleTests_UniqueConstraint.js");
            Trigger triggerResource = new Trigger
            {
                Id = this.triggerName,
                Body = triggerContent,
                TriggerOperation = TriggerOperation.All,
                TriggerType = TriggerType.Pre
            };
            Trigger trigger = this.client.CreateTriggerAsync(this.collection.SelfLink, triggerResource).Result;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.client.DeleteDatabaseAsync(this.database.SelfLink).Wait();
        }

        [TestMethod]
        public void TestUniqueConstraintSample()
        {


            // 1. Create.
            UidDocument doc = new UidDocument { Id = "TestUniqueConstraintSample_1", Uid = "mic" };
            doc.SetPropertyValue("pk", "test");
            PartitionKey partitionKey = new PartitionKey("test");
            RequestOptions triggerRequestOptions = new RequestOptions { PreTriggerInclude = new List<string> { this.triggerName }, PartitionKey = partitionKey };
            doc = (dynamic)this.client.CreateDocumentAsync(this.collection.SelfLink, doc, triggerRequestOptions).Result.Resource;

            // 2. Create with same uid -- conflict.
            this.AssertThrows(
                () => this.client.CreateDocumentAsync(this.collection.SelfLink, doc, triggerRequestOptions).Wait(),
                typeof(DocumentClientException),
                "Create with same email didn't throw");

            // 3. Replace, change uid.
            doc.Uid = "nik";
            this.client.ReplaceDocumentAsync(doc, triggerRequestOptions).Wait();
            this.ValidateReadMetadoc("nik");

            // 4. Replace, change other property (id). Metadoc stays the same as unique property is not changed.
            doc.Id = "TestUniqueConstraintSample_2";
            this.client.ReplaceDocumentAsync(doc, triggerRequestOptions).Wait();
            this.ValidateReadMetadoc("nik");

            // 5. Upsert, change uid.
            doc.Uid = "cam";
            this.client.UpsertDocumentAsync(this.collection.SelfLink, doc, triggerRequestOptions).Wait();
            this.ValidateReadMetadoc("cam");

            // 6. Upsert, change other.
            doc.Other = "cam";
            this.client.UpsertDocumentAsync(this.collection.SelfLink, doc, triggerRequestOptions).Wait();
            this.ValidateReadMetadoc("cam");

            // 7. Upsert, new id causes insert which would cause another doc with same value of unique property.
            doc.Id = "TestUniqueConstraintSample_3";
            this.AssertThrows(
                () => this.client.UpsertDocumentAsync(this.collection.SelfLink, doc, triggerRequestOptions).Wait(),
                typeof(DocumentClientException),
                "Create with same email didn't throw");

            // 8. Upsert, new id, different value of unique property.
            doc.Uid = "lion";
            this.client.UpsertDocumentAsync(this.collection.SelfLink, doc, triggerRequestOptions).Wait();

            // 9. Delete 1st doc (uid = cam).
            doc.Id = "TestUniqueConstraintSample_2";
            this.client.DeleteDocumentAsync(doc.SelfLink, triggerRequestOptions).Wait();

            this.ValidateReadMetadoc("lion");
        }

        private void ValidateReadMetadoc(string expectedUid)
        {
            System.Linq.IQueryable<Document> query = this.client.CreateDocumentQuery<Document>(this.collection.SelfLink, "SELECT * FROM root r WHERE r.isMetadata = true", feedOptions: new FeedOptions { EnableCrossPartitionQuery = true });
            foreach (Document metaDoc in query)
            {
                string id = metaDoc.GetPropertyValue<string>("id");
                StringAssert.Contains(id, expectedUid);
            }
        }

        private void AssertThrows(Action action, Type expectedExceptionType, string errorOnNoThrow = null)
        {
            try
            {
                action();
                string message = errorOnNoThrow ?? "The action didn't throw.";
                Assert.Fail(errorOnNoThrow);
            }
            catch (Exception ex)
            {
                while (ex != null && ex is AggregateException)
                {
                    ex = ex.InnerException;
                }
                if (ex.GetType() != expectedExceptionType)
                {
                    throw;
                }
            }
        }

        private sealed class UidDocument : Document
        {
            [JsonProperty("uid")]
            public string Uid { get; set; }

            [JsonProperty("other")]
            public string Other { get; set; }
        }
    }
}

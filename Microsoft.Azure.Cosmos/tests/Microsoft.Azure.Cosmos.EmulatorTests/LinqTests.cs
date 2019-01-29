//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [Ignore]
    [TestClass]
    public class LinqTests
    {
        private readonly DocumentClient client;

        public LinqTests()
        {
            this.client = TestCommon.CreateClient(true, defaultConsistencyLevel: ConsistencyLevel.Session);

            this.CleanUp();
        }

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            DocumentClientSwitchLinkExtension.Reset("LinqTests");
        }

        private class BaseDocument
        {
            [JsonProperty(PropertyName = Constants.Properties.Id)]
            public string Id { get; set; }
            public string TypeName { get; set; }
        }

        private class DataObject : BaseDocument
        {
            public int Number { get; set; }
        }

        private class QueryHelper
        {
            private readonly DocumentClient client;
            private readonly CosmosContainerSettings docCollection;

            public QueryHelper(DocumentClient client, CosmosContainerSettings docCollection)
            {
                this.client = client;
                this.docCollection = docCollection;
            }

            public IQueryable<T> Query<T>() where T : BaseDocument
            {
                var query = this.client.CreateDocumentQuery<T>(this.docCollection.DocumentsLink)
                                       .Where(d => d.TypeName == "Hello");
                var queryString = query.ToString();
                return query;
            }
        }

        public class GreatGreatFamily
        {
            [JsonProperty(PropertyName = "id")]
            public string GreatFamilyId;

            public GreatFamily GreatFamily;
        }

        public class GreatFamily
        {
            public Family Family;
        }

        public class Family
        {
            [JsonProperty(PropertyName = "id")]
            public string FamilyId;
            public Parent[] Parents;
            public Child[] Children;
            public bool IsRegistered;
            public object NullObject;
            public int Int;
            public int? NullableInt;
        }

        public class Parent
        {
            public string FamilyName;
            public string GivenName;
        }

        public class Child
        {
            public string FamilyName;
            public string GivenName;
            public string Gender;
            public int Grade;
            public List<Pet> Pets;
            public Dictionary<string, string> Things;
        }

        public class Pet
        {
            public string GivenName;
        }

        public class Address
        {
            public string State;
            public string County;
            public string City;
        }

        public class GuidClass
        {
            [JsonProperty(PropertyName = "id")]
            public Guid Id;
        }

        public class ListArrayClass
        {
            [JsonProperty(PropertyName = "id")]
            public string Id;

            public int[] ArrayField;
            public List<int> ListField;
        }

        [DataContract]
        public class Sport
        {
            [DataMember(Name = "id")]
            public string SportName;

            [JsonProperty(PropertyName = "json")]
            [DataMember(Name = "data")]
            public string SportType;
        }

        public class Sport2
        {
            [DataMember(Name = "data")]
            public string id;
        }

        [TestMethod]
        public void TestNestedSelectMany()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            IOrderedQueryable<Family> families = new DocumentQuery<Family>(client, ResourceType.Document, typeof(Document), null, null);

            IQueryable query = families.SelectMany(family => family.Children
                .SelectMany(child => child.Pets
                    .Where(pet => pet.GivenName == "Fluffy")
                    .Select(pet => pet
                    )));

            this.VerifyQueryTranslation(query.ToString(), "SELECT VALUE pet0 FROM root JOIN child0 IN root[\"Children\"] JOIN pet0 IN child0[\"Pets\"] WHERE (pet0[\"GivenName\"] = \"Fluffy\") ");

            query = families.SelectMany(family => family.Children
                .SelectMany(child => child.Pets
                    .Where(pet => pet.GivenName == "Fluffy")
                    .Select(pet => new
                    {
                        family = family.FamilyId,
                        child = child.GivenName,
                        pet = pet.GivenName
                    }
                    )));

            this.VerifyQueryTranslation(query.ToString(), "SELECT VALUE {\"family\": root[\"id\"], \"child\": child[\"GivenName\"], \"pet\": pet[\"GivenName\"]} FROM root JOIN child IN root[\"Children\"] JOIN pet IN child[\"Pets\"] WHERE (pet[\"GivenName\"] = \"Fluffy\") ");
        }

        [TestMethod]
        [TestCategory("Ignore")]
        public void TestOrderByTranslation()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            IOrderedQueryable<Family> families = new DocumentQuery<Family>(client, ResourceType.Document, typeof(Document), "//dbs/", null);

            // Ascending
            IQueryable query = from f in families
                               where f.Int == 5 && f.NullableInt != null
                               orderby f.IsRegistered
                               select f.FamilyId;

            this.VerifyQueryTranslation(query.ToString(), "SELECT VALUE root[\"id\"] FROM root WHERE ((root[\"Int\"] = 5) AND (root[\"NullableInt\"] != null)) ORDER BY root[\"IsRegistered\"] ASC ");

            query = families.Where(f => f.Int == 5 && f.NullableInt != null).OrderBy(f => f.IsRegistered).Select(f => f.FamilyId);
            this.VerifyQueryTranslation(query.ToString(), "SELECT VALUE root[\"id\"] FROM root WHERE ((root[\"Int\"] = 5) AND (root[\"NullableInt\"] != null)) ORDER BY root[\"IsRegistered\"] ASC ");

            query = from f in families
                    orderby f.FamilyId
                    select f;
            this.VerifyQueryTranslation(query.ToString(), "SELECT * FROM root ORDER BY root[\"id\"] ASC ");

            query = families.OrderBy(f => f.FamilyId);
            this.VerifyQueryTranslation(query.ToString(), "SELECT * FROM root ORDER BY root[\"id\"] ASC ");

            query = from f in families
                    orderby f.FamilyId
                    select f.FamilyId;
            this.VerifyQueryTranslation(query.ToString(), "SELECT VALUE root[\"id\"] FROM root ORDER BY root[\"id\"] ASC ");

            query = families.OrderBy(f => f.FamilyId).Select(f => f.FamilyId);
            this.VerifyQueryTranslation(query.ToString(), "SELECT VALUE root[\"id\"] FROM root ORDER BY root[\"id\"] ASC ");

            // Descending
            query = from f in families
                    where f.Int == 5 && f.NullableInt != null
                    orderby f.IsRegistered descending
                    select f.FamilyId;
            this.VerifyQueryTranslation(query.ToString(), "SELECT VALUE root[\"id\"] FROM root WHERE ((root[\"Int\"] = 5) AND (root[\"NullableInt\"] != null)) ORDER BY root[\"IsRegistered\"] DESC ");

            query = families.Where(f => f.Int == 5 && f.NullableInt != null).OrderByDescending(f => f.IsRegistered).Select(f => f.FamilyId);
            this.VerifyQueryTranslation(query.ToString(), "SELECT VALUE root[\"id\"] FROM root WHERE ((root[\"Int\"] = 5) AND (root[\"NullableInt\"] != null)) ORDER BY root[\"IsRegistered\"] DESC ");

            query = from f in families
                    orderby f.FamilyId descending
                    select f;
            this.VerifyQueryTranslation(query.ToString(), "SELECT * FROM root ORDER BY root[\"id\"] DESC ");

            query = families.OrderByDescending(f => f.FamilyId);
            this.VerifyQueryTranslation(query.ToString(), "SELECT * FROM root ORDER BY root[\"id\"] DESC ");

            query = from f in families
                    orderby f.FamilyId descending
                    select f.FamilyId;
            this.VerifyQueryTranslation(query.ToString(), "SELECT VALUE root[\"id\"] FROM root ORDER BY root[\"id\"] DESC ");

            query = families.OrderByDescending(f => f.FamilyId).Select(f => f.FamilyId);
            this.VerifyQueryTranslation(query.ToString(), "SELECT VALUE root[\"id\"] FROM root ORDER BY root[\"id\"] DESC ");

            // orderby multiple expression is not supported yet
            query = from f in families
                    orderby f.FamilyId, f.Int
                    select f.FamilyId;
            try
            {
                query.ToString();
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message.Contains("Method 'ThenBy' is not supported."));
            }
        }

        [TestMethod]
        public void ValidateLinqQueries()
        {
            CosmosDatabaseSettings database = this.client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = Guid.NewGuid().ToString("N") }).Result.Resource;
            CosmosContainerSettings collection = this.client.CreateDocumentCollectionAsync(
                database.SelfLink, new CosmosContainerSettings { Id = Guid.NewGuid().ToString("N") }).Result.Resource;

            DataObject doc = new DataObject() { Id = Guid.NewGuid().ToString("N"), Number = 0, TypeName = "Hello" };
            this.client.CreateDocumentAsync(collection, doc).Wait();

            QueryHelper queryHelper = new QueryHelper(client, collection);
            IEnumerable<BaseDocument> result = queryHelper.Query<BaseDocument>();
            Assert.AreEqual(1, result.Count());

            BaseDocument baseDocument = result.FirstOrDefault<BaseDocument>();
            Assert.AreEqual(doc.Id, baseDocument.Id);

            BaseDocument iDocument = doc;
            IOrderedQueryable<DataObject> q = this.client.CreateDocumentQuery<DataObject>(collection.DocumentsLink);

            IEnumerable<DataObject> iresult = from f in q
                                              where f.Id == iDocument.Id
                                              select f;
            DataObject id = iresult.FirstOrDefault<DataObject>();
            Assert.AreEqual(doc.Id, id.Id);

            Parent mother = new Parent { FamilyName = "Wakefield", GivenName = "Robin" };
            Parent father = new Parent { FamilyName = "Miller", GivenName = "Ben" };
            Pet pet = new Pet { GivenName = "Fluffy" };
            Child child = new Child
            {
                FamilyName = "Merriam",
                GivenName = "Jesse",
                Gender = "female",
                Grade = 1,
                Pets = new List<Pet>() { pet, new Pet() { GivenName = "koko" } },
                Things = new Dictionary<string, string>() { { "A", "B" }, { "C", "D" } }
            };

            Address address = new Address { State = "NY", County = "Manhattan", City = "NY" };
            Family family = new Family { FamilyId = "WakefieldFamily", Parents = new Parent[] { mother, father }, Children = new Child[] { child }, IsRegistered = false, Int = 3, NullableInt = 5 };

            List<Family> fList = new List<Family>();
            fList.Add(family);

            this.client.CreateDocumentAsync(collection.SelfLink, family).Wait();

            IOrderedQueryable<Family> query = this.client.CreateDocumentQuery<Family>(collection.DocumentsLink);

            IEnumerable<string> q1 = query.Select(f => f.Parents[0].FamilyName);
            Assert.AreEqual(q1.FirstOrDefault(), family.Parents[0].FamilyName);

            IEnumerable<int> q2 = query.Select(f => f.Children[0].Grade + 13);
            Assert.AreEqual(q2.FirstOrDefault(), family.Children[0].Grade + 13);

            IEnumerable<Family> q3 = query.Where(f => f.Children[0].Pets[0].GivenName == "Fluffy");
            Assert.AreEqual(q3.FirstOrDefault().FamilyId, family.FamilyId);

            IEnumerable<Family> q4 = query.Where(f => f.Children[0].Things["A"] == "B");
            Assert.AreEqual(q4.FirstOrDefault().FamilyId, family.FamilyId);

            for (int index = 0; index < 2; index++)
            {
                IEnumerable<Pet> q5 = query.Where(f => f.Children[0].Gender == "female").Select(f => f.Children[0].Pets[index]);
                Assert.AreEqual(q5.FirstOrDefault().GivenName, family.Children[0].Pets[index].GivenName);
            }

            IEnumerable<dynamic> q6 = query.SelectMany(f => f.Children.Select(c => new { Id = f.FamilyId }));
            Assert.AreEqual(q6.FirstOrDefault().Id, family.FamilyId);

            string nullString = null;
            IEnumerable<Family> q7 = query.Where(f => nullString == f.FamilyId);
            Assert.IsNull(q7.FirstOrDefault());

            object nullObject = null;
            q7 = query.Where(f => f.NullObject == nullObject);
            Assert.AreEqual(q7.FirstOrDefault().FamilyId, family.FamilyId);

            q7 = query.Where(f => f.FamilyId == nullString);
            Assert.IsNull(q7.FirstOrDefault());

            IEnumerable<Family> q8 = query.Where(f => null == f.FamilyId);
            Assert.IsNull(q8.FirstOrDefault());

            IEnumerable<Family> q9 = query.Where(f => f.IsRegistered == false);
            Assert.AreEqual(q9.FirstOrDefault().FamilyId, family.FamilyId);

            dynamic q10 = System.Linq.Dynamic.Core.DynamicQueryableExtensions.AsEnumerable(query.Where(f => f.FamilyId.Equals("WakefieldFamily"))).FirstOrDefault();
            Assert.AreEqual(q10.FamilyId, family.FamilyId);

            GuidClass guidObject = new GuidClass() { Id = Guid.NewGuid() };
            this.client.CreateDocumentAsync(collection.SelfLink, guidObject).Wait();

            IEnumerable<GuidClass> q11 = this.client.CreateDocumentQuery<GuidClass>(collection.DocumentsLink).Where(g => g.Id == guidObject.Id);
            Assert.AreEqual(q11.FirstOrDefault().Id, guidObject.Id);

            IEnumerable<GuidClass> q12 = this.client.CreateDocumentQuery<GuidClass>(collection.DocumentsLink).Where(g => g.Id.ToString() == guidObject.Id.ToString());
            Assert.AreEqual(q12.FirstOrDefault().Id, guidObject.Id);

            ListArrayClass arrayObject = new ListArrayClass() { Id = "arrayObject", ArrayField = new int[] { 1, 2, 3 } };
            this.client.CreateDocumentAsync(collection.SelfLink, arrayObject).Wait();

            IEnumerable<dynamic> q13 = this.client.CreateDocumentQuery<ListArrayClass>(collection.DocumentsLink).Where(a => a.ArrayField == arrayObject.ArrayField);
            Assert.AreEqual(q13.FirstOrDefault().Id, arrayObject.Id);

            int[] nullArray = null;
            q13 = this.client.CreateDocumentQuery<ListArrayClass>(collection.DocumentsLink).Where(a => a.ArrayField == nullArray);
            Assert.IsNull(q13.FirstOrDefault());

            ListArrayClass listObject = new ListArrayClass() { Id = "listObject", ListField = new List<int> { 1, 2, 3 } };
            this.client.CreateDocumentAsync(collection.SelfLink, listObject).Wait();

            IEnumerable<dynamic> q14 = this.client.CreateDocumentQuery<ListArrayClass>(collection.DocumentsLink).Where(a => a.ListField == listObject.ListField);
            Assert.AreEqual(q14.FirstOrDefault().Id, listObject.Id);

            IEnumerable<dynamic> q15 = query.Where(f => f.NullableInt == null);
            Assert.AreEqual(q15.ToList().Count, 0);

            int? nullInt = null;
            q15 = query.Where(f => f.NullableInt == nullInt);
            Assert.AreEqual(q15.ToList().Count, 0);

            q15 = query.Where(f => f.NullableInt == 5);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            nullInt = 5;
            q15 = query.Where(f => f.NullableInt == nullInt);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            q15 = query.Where(f => f.NullableInt == nullInt.Value);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            nullInt = 3;
            q15 = query.Where(f => f.Int == nullInt);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            q15 = query.Where(f => f.Int == nullInt.Value);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            nullInt = null;
            q15 = query.Where(f => f.Int == nullInt);
            Assert.AreEqual(q15.ToList().Count, 0);

            var v = fList.Where(f => f.Int > nullInt).ToList();

            q15 = query.Where(f => f.Int < nullInt);

            string doc1Id = "document1:x:'!@TT){}\"";
            Document doubleQoutesDocument = new Document() { Id = doc1Id };
            this.client.CreateDocumentAsync(collection.DocumentsLink, doubleQoutesDocument).Wait();

            var docQuery = from book in client.CreateDocumentQuery<Document>(collection.DocumentsLink)
                           where book.Id == doc1Id
                           select book;

            Assert.AreEqual(System.Linq.Dynamic.Core.DynamicQueryableExtensions.AsEnumerable(docQuery).Single().Id, doc1Id);

            GreatFamily greatFamily = new GreatFamily() { Family = family };
            GreatGreatFamily greatGreatFamily = new GreatGreatFamily() { GreatFamilyId = Guid.NewGuid().ToString(), GreatFamily = greatFamily };
            this.client.CreateDocumentAsync(collection.DocumentsLink, greatGreatFamily).Wait();

            IOrderedQueryable<GreatGreatFamily> queryable = this.client.CreateDocumentQuery<GreatGreatFamily>(collection.DocumentsLink);

            IEnumerable<GreatGreatFamily> q16 = queryable.SelectMany(gf => gf.GreatFamily.Family.Children.Where(c => c.GivenName == "Jesse").Select(c => gf));

            Assert.AreEqual(q16.FirstOrDefault().GreatFamilyId, greatGreatFamily.GreatFamilyId);

            Sport sport = new Sport() { SportName = "Tennis", SportType = "Racquet" };
            this.client.CreateDocumentAsync(collection.DocumentsLink, sport).Wait();

            IEnumerable<Sport> q17 = this.client.CreateDocumentQuery<Sport>(collection.DocumentsLink)
                .Where(s => s.SportName == "Tennis");

            Assert.AreEqual(sport.SportName, q17.FirstOrDefault().SportName);

            q17 = this.client.CreateDocumentQuery<Sport>(collection.DocumentsLink)
                .Where(s => s.SportType == "Racquet");

            this.VerifyQueryTranslation(q17.ToString(), "SELECT * FROM root WHERE (root[\"json\"] = \"Racquet\") ");

            Sport2 sport2 = new Sport2() { id = "json" };
            this.client.CreateDocumentAsync(collection.DocumentsLink, sport2).Wait();

            IEnumerable<Sport2> q18 = this.client.CreateDocumentQuery<Sport2>(collection.DocumentsLink)
                .Where(s => s.id == "json");

            this.VerifyQueryTranslation(q18.ToString(), "SELECT * FROM root WHERE (root[\"id\"] = \"json\") ");
        }

        [TestMethod]
        public void ValidateServerSideQueryEvalWithPagination()
        {
            this.ValidateServerSideQueryEvalWithPaginationScenario().Wait();
        }

        private async Task ValidateServerSideQueryEvalWithPaginationScenario()
        {
            DocumentClient client = TestCommon.CreateClient(false, defaultConsistencyLevel: ConsistencyLevel.Session);

            CosmosDatabaseSettings database = TestCommon.CreateOrGetDatabase(client);
            CosmosContainerSettings collection = new CosmosContainerSettings
            {
                Id = "ConsistentCollection"
            };
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

            collection = client.Create<CosmosContainerSettings>(
                database.ResourceId,
                collection);

            //Do script post to insert as many document as we could in a tight loop.
            string script = @"function() {
                var output = 0;
                var client = getContext().getCollection();
                function callback(err, docCreated) {
                    if(err) throw 'Error while creating document';
                    output++;
                    getContext().getResponse().setBody(output);
                    if(output < 50) 
                        client.createDocument(client.getSelfLink(), { id: 'testDoc' + output, title : 'My Book'}, {}, callback);                       
                };
                client.createDocument(client.getSelfLink(), { id: 'testDoc' + output, title : 'My Book'}, {}, callback); }";


            StoredProcedureResponse<int> scriptResponse = null;
            int totalNumberOfDocuments = GatewayTests.CreateExecuteAndDeleteProcedure(client, collection, script, out scriptResponse);

            int pageSize = 5;
            int totalHit = 0;
            IDocumentQuery<Book> documentQuery =
                (from book in client.CreateDocumentQuery<Book>(
                    collection.SelfLink, new FeedOptions { MaxItemCount = pageSize })
                 where book.Title == "My Book"
                 select book).AsDocumentQuery();

            while (documentQuery.HasMoreResults)
            {
                FeedResponse<dynamic> pagedResult = await documentQuery.ExecuteNextAsync();
                string isUnfiltered = pagedResult.ResponseHeaders[HttpConstants.HttpHeaders.IsFeedUnfiltered];
                Assert.IsTrue(string.IsNullOrEmpty(isUnfiltered), "Query is evaulated in client");
                Assert.IsTrue(pagedResult.Count <= pageSize, "Page size is not honored in client site eval");

                if (totalHit != 0 && documentQuery.HasMoreResults)
                {
                    //Except first page and last page we should have seen client continuation token.
                    Assert.IsFalse(pagedResult.ResponseHeaders[HttpConstants.HttpHeaders.Continuation].Contains(HttpConstants.Delimiters.ClientContinuationDelimiter),
                        "Client continuation is missing from the response continuation");
                }
                totalHit += pagedResult.Count;
            }
            Assert.AreEqual(totalHit, totalNumberOfDocuments, "Didnt get all the documents");

            //Do with default pagination.
            documentQuery =
                (from book in client.CreateDocumentQuery<Book>(
                    collection.SelfLink)
                 where book.Title == "My Book"
                 select book).AsDocumentQuery();

            totalHit = 0;

            while (documentQuery.HasMoreResults)
            {
                FeedResponse<dynamic> pagedResult = await documentQuery.ExecuteNextAsync();
                string isUnfiltered = pagedResult.ResponseHeaders[HttpConstants.HttpHeaders.IsFeedUnfiltered];
                Assert.IsTrue(string.IsNullOrEmpty(isUnfiltered), "Query is evaulated in client");
                Assert.IsTrue(pagedResult.Count == totalNumberOfDocuments, "Page size is not honored in client site eval");
                totalHit += pagedResult.Count;
            }
            Assert.AreEqual(totalHit, totalNumberOfDocuments, "Didnt get all the documents");
        }

        [TestMethod]
        public void ValidateBasicQuery()
        {
            this.ValidateBasicQueryAsync().Wait();
        }

        private async Task ValidateBasicQueryAsync()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            string databaseName = Guid.NewGuid().ToString("n"); //Make sure local site variable are evaluated inline.
            CosmosDatabaseSettings database = await client.CreateDatabaseAsync(
                new CosmosDatabaseSettings { Id = databaseName });

            List<CosmosDatabaseSettings> queryResults = new List<CosmosDatabaseSettings>();
            //Simple Equality
            IQueryable<CosmosDatabaseSettings> dbQuery = from db in client.CreateDatabaseQuery()
                                           where db.Id == databaseName
                                           select db;
            IDocumentQuery<CosmosDatabaseSettings> documentQuery = dbQuery.AsDocumentQuery();

            while (documentQuery.HasMoreResults)
            {
                FeedResponse<CosmosDatabaseSettings> pagedResponse = await documentQuery.ExecuteNextAsync<CosmosDatabaseSettings>();
                Assert.IsNotNull(pagedResponse.ResponseHeaders, "ResponseHeaders is null");
                Assert.IsNotNull(pagedResponse.ActivityId, "Query ActivityId is null");
                queryResults.AddRange(pagedResponse);
            }

            Assert.AreEqual(1, queryResults.Count);
            Assert.AreEqual(databaseName, queryResults[0].Id);

            //Logical Or 
            dbQuery = from db in client.CreateDatabaseQuery()
                      where db.Id == databaseName || db.ResourceId == database.ResourceId
                      select db;
            documentQuery = dbQuery.AsDocumentQuery();

            while (documentQuery.HasMoreResults)
            {
                queryResults.AddRange(await documentQuery.ExecuteNextAsync<CosmosDatabaseSettings>());
            }

            Assert.AreEqual(2, queryResults.Count);
            Assert.AreEqual(databaseName, queryResults[0].Id);

            //Select Property
            IQueryable<string> idQuery = from db in client.CreateDatabaseQuery()
                                         where db.Id == databaseName
                                         select db.ResourceId;
            IDocumentQuery<string> documentIdQuery = idQuery.AsDocumentQuery();

            List<string> idResults = new List<string>();
            while (documentIdQuery.HasMoreResults)
            {
                idResults.AddRange(await documentIdQuery.ExecuteNextAsync<string>());
            }

            Assert.AreEqual(1, idResults.Count);
            Assert.AreEqual(database.ResourceId, idResults[0]);
        }

        [TestMethod]
        public void ValidateTransformQuery()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            IQueryable<dynamic> dbQuery = client.CreateDatabaseQuery(@"select * from root r where r.id=""db123""").AsQueryable();
            foreach (CosmosDatabaseSettings db in dbQuery)
            {
                TestCommon.Delete<CosmosDatabaseSettings>(client, db.ResourceId);
            }

            CosmosDatabaseSettings database = client.Create<CosmosDatabaseSettings>(null,
                new CosmosDatabaseSettings { Id = "db123" });

            dbQuery = client.CreateDatabaseQuery(@"select * from root r where r.id=""db123""").AsQueryable();
            foreach (CosmosDatabaseSettings db in dbQuery)
            {
                Assert.AreEqual(db.Id, "db123");
            }
            Assert.AreNotEqual(0, System.Linq.Dynamic.Core.DynamicQueryableExtensions.AsEnumerable(dbQuery).Count());

            IQueryable<dynamic> dbIdQuery = client.CreateDatabaseQuery(@"select r._rid from root r where r.id=""db123""").AsQueryable();
            Assert.AreNotEqual(0, System.Linq.Dynamic.Core.DynamicQueryableExtensions.AsEnumerable(dbIdQuery).Count());

            CosmosContainerSettings collection = new CosmosContainerSettings
            {
                Id = Guid.NewGuid().ToString("N")
            };
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

            collection = client.Create<CosmosContainerSettings>(database.ResourceId, collection);
            int documentsToCreate = 100;
            for (int i = 0; i < documentsToCreate; i++)
            {
                dynamic myDocument = new Document();
                myDocument.Id = "doc" + i;
                myDocument.Title = "MyBook"; //Simple Property.
                myDocument.Languages = new Language[] { new Language { Name = "English", Copyright = "London Publication" }, new Language { Name = "French", Copyright = "Paris Publication" } }; //Array Property
                myDocument.Author = new Author { Name = "Don", Location = "France" }; //Complex Property
                myDocument.Price = 9.99;
                myDocument = client.CreateDocumentAsync(collection.DocumentsLink, myDocument).Result;
            }

            //Read response as dynamic.
            IQueryable<dynamic> docQuery = client.CreateDocumentQuery(collection.DocumentsLink, @"select * from root r where r.Title=""MyBook""", null);

            IDocumentQuery<dynamic> DocumentQuery = docQuery.AsDocumentQuery();
            FeedResponse<dynamic> queryResponse = DocumentQuery.ExecuteNextAsync().Result;

            Assert.IsNotNull(queryResponse.ResponseHeaders, "ResponseHeaders is null");
            Assert.IsNotNull(queryResponse.ActivityId, "ActivityId is null");
            Assert.AreEqual(documentsToCreate, queryResponse.Count);

            foreach (dynamic myBook in queryResponse)
            {
                Assert.AreEqual(myBook.Title, "MyBook");
            }

            client.DeleteDocumentCollectionAsync(collection.SelfLink).Wait();
        }

        [TestMethod]
        public void ValidateDynamicDocumentQuery() //Ensure query on custom property of document.
        {
            DocumentClient client = TestCommon.CreateClient(true);

            Book myDocument = new Book();
            myDocument.Id = Guid.NewGuid().ToString();
            myDocument.Title = "My Book"; //Simple Property.
            myDocument.Languages = new Language[] { new Language { Name = "English", Copyright = "London Publication" }, new Language { Name = "French", Copyright = "Paris Publication" } }; //Array Property
            myDocument.Author = new Author { Name = "Don", Location = "France" }; //Complex Property
            myDocument.Price = 9.99;
            myDocument.Editions = new List<Edition>() { new Edition() { Name = "First", Year = 2001 }, new Edition() { Name = "Second", Year = 2005 } };

            //Create second document to make sure we have atleast one document which are filtered out of query.
            Book secondDocument = new Book
            {
                Id = Guid.NewGuid().ToString(),
                Title = "My Second Book",
                Languages = new Language[] { new Language { Name = "Spanish", Copyright = "Mexico Publication" } },
                Author = new Author { Name = "Carlos", Location = "Cancun" },
                Price = 25,
                Editions = new List<Edition>() { new Edition() { Name = "First", Year = 1970 } }
            };

            //Unfiltered execution.
            DocumentQuery<Book> bookDocQuery = new DocumentQuery<Book>(client, ResourceType.Document, typeof(Document), null, null);

            //Simple Equality on custom property.
            IQueryable<dynamic> docQuery = from book in bookDocQuery
                                           where book.Title == "My Book"
                                           select book;
            this.VerifyQueryTranslation(docQuery, "SELECT * FROM root WHERE (root[\"title\"] = \"My Book\") ");

            //Nested Property access
            docQuery = from book in bookDocQuery
                       where book.Author.Name == "Don"
                       select book;
            this.VerifyQueryTranslation(docQuery, "SELECT * FROM root WHERE (root[\"Author\"][\"id\"] = \"Don\") ");

            //Array references & Project Author out..
            docQuery = from book in bookDocQuery
                       where book.Languages[0].Name == "English"
                       select book.Author;
            this.VerifyQueryTranslation(docQuery, "SELECT VALUE root[\"Author\"] FROM root WHERE (root[\"Languages\"][0][\"Name\"] = \"English\") ");

            //SelectMany
            docQuery = bookDocQuery.SelectMany(
                       book => book.Languages).Where(lang => lang.Name == "French").Select(lang => lang.Copyright);
            this.VerifyQueryTranslation(docQuery, "SELECT VALUE tmp[\"Copyright\"] FROM root JOIN tmp IN root[\"Languages\"] WHERE (tmp[\"Name\"] = \"French\") ");

            //NumericRange query
            docQuery = from book in bookDocQuery
                       where book.Price < 10
                       select book.Author;
            this.VerifyQueryTranslation(docQuery, "SELECT VALUE root[\"Author\"] FROM root WHERE (root[\"Price\"] < 10.0) ");

            //Or query
            docQuery = from book in bookDocQuery
                       where book.Title == "My Book" || book.Author.Name == "Don"
                       select book;
            this.VerifyQueryTranslation(docQuery, "SELECT * FROM root WHERE ((root[\"title\"] = \"My Book\") OR (root[\"Author\"][\"id\"] = \"Don\")) ");

            //SelectMany query on a List type.
            docQuery = bookDocQuery
                .SelectMany(book => book.Editions)
                .Select(ed => ed.Name);

            this.VerifyQueryTranslation(docQuery, "SELECT VALUE tmp[\"Name\"] FROM root JOIN tmp IN root[\"Editions\"] ");

            // Below samples are strictly speaking not Any equivalent. But they join and filter "all"
            // subchildren which match predicate. When SQL BE supports ANY, we can replace these with Any Flavor.
            docQuery = bookDocQuery
                       .SelectMany(book =>
                           book.Languages
                           .Where(lng => lng.Name == "English")
                           .Select(lng => book.Author));
            this.VerifyQueryTranslation(docQuery, "SELECT VALUE root[\"Author\"] FROM root JOIN lng IN root[\"Languages\"] WHERE (lng[\"Name\"] = \"English\") ");

            //Any query on a List type.
            docQuery = bookDocQuery
                           .SelectMany(book =>
                               book.Editions
                               .Where(edition => edition.Year == 2001)
                               .Select(lng => book.Author));
            this.VerifyQueryTranslation(docQuery, "SELECT VALUE root[\"Author\"] FROM root JOIN edition IN root[\"Editions\"] WHERE (edition[\"Year\"] = 2001) ");
        }

        [TestMethod]
        public void ValidateDynamicAttachmentQuery() //Ensure query on custom property of attachment.
        {
            DocumentClient client = TestCommon.CreateClient(true);

            var myDocument = new Document();

            IOrderedQueryable<SpecialAttachment2> attachmentQuery = new DocumentQuery<SpecialAttachment2>(client, ResourceType.Attachment, typeof(Attachment), null, null);

            //Simple Equality on custom property.
            IQueryable<dynamic> docQuery = from attachment in attachmentQuery
                                           where attachment.Title == "My Book Title2"
                                           select attachment;
            this.VerifyQueryTranslation(docQuery, "SELECT * FROM root WHERE (root[\"Title\"] = \"My Book Title2\") ");

            docQuery = from attachment in attachmentQuery
                       where attachment.Title == "My Book Title"
                       select attachment;
            this.VerifyQueryTranslation(docQuery, "SELECT * FROM root WHERE (root[\"Title\"] = \"My Book Title\") ");
        }

        [TestMethod]
        public void TestLinqTypeSystem()
        {
            Assert.AreEqual(null, TypeSystem.GetElementType(typeof(Book)));
            Assert.AreEqual(null, TypeSystem.GetElementType(typeof(Author)));

            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(Language[])));
            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(List<Language>)));
            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(IList<Language>)));
            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(IEnumerable<Language>)));
            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(ICollection<Language>)));

            Assert.AreEqual(typeof(DerivedFooItem), TypeSystem.GetElementType(typeof(DerivedFooItem[])));
            Assert.AreEqual(typeof(FooItem), TypeSystem.GetElementType(typeof(List<FooItem>)));
            Assert.AreEqual(typeof(string), TypeSystem.GetElementType(typeof(MyList<string>)));
            Assert.AreEqual(typeof(Tuple<string, string>), TypeSystem.GetElementType(typeof(MyTupleList<string>)));

            Assert.AreEqual(typeof(DerivedFooItem), TypeSystem.GetElementType(typeof(DerivedFooCollection)));
            Assert.AreEqual(typeof(string), TypeSystem.GetElementType(typeof(FooStringCollection)));

            Assert.AreEqual(typeof(FooItem), TypeSystem.GetElementType(typeof(FooTCollection<object>)));
            Assert.AreEqual(typeof(FooItem), TypeSystem.GetElementType(typeof(FooTCollection<IFooItem>)));
            Assert.AreEqual(typeof(FooItem), TypeSystem.GetElementType(typeof(FooTCollection<FooItem>)));
            Assert.AreEqual(typeof(DerivedFooItem), TypeSystem.GetElementType(typeof(FooTCollection<DerivedFooItem>)));
        }

        private void VerifyQueryTranslation(IQueryable query, string expectedSQLQuery)
        {
            VerifyQueryTranslation(query.ToString(), expectedSQLQuery);
        }

        private void VerifyQueryTranslation(string queryString, string expectedSQLQuery)
        {
            VerifyQueryTranslation(queryString, new SqlQuerySpec(expectedSQLQuery));
        }

        private void VerifyQueryTranslation(object queryString, SqlQuerySpec expectedQuerySpec)
        {
            string expectedString = JsonConvert.SerializeObject(expectedQuerySpec);

            Assert.AreEqual(expectedString, queryString);
        }


        private void CleanUp()
        {
            IEnumerable<CosmosDatabaseSettings> allDatabases = from database in this.client.CreateDatabaseQuery()
                                                 select database;
            foreach (CosmosDatabaseSettings database in allDatabases)
            {
                this.client.DeleteDatabaseAsync(database.SelfLink).Wait();
            }
        }

        public class Author
        {
            [JsonProperty(PropertyName = Constants.Properties.Id)]
            public string Name { get; set; }
            public string Location { get; set; }
        }

        public class Language
        {
            public string Name { get; set; }
            public string Copyright { get; set; }
        }

        public class Edition
        {
            public string Name { get; set; }
            public int Year { get; set; }
        }

        public class Book
        {
            //Verify that we can override the propertyName but still can query them using .NET Property names.
            [JsonProperty(PropertyName = "title")]
            public string Title { get; set; }
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }
            public Language[] Languages { get; set; }
            public Author Author { get; set; }
            public double Price { get; set; }
            [JsonProperty(PropertyName = Constants.Properties.Id)]
            public string Id { get; set; }
            public List<Edition> Editions { get; set; }
        }

        public class SpecialAttachment2 //Non attachemnt derived.
        {
            [JsonProperty(PropertyName = Constants.Properties.Id)]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "contentType")]
            public string ContentType { get; set; }

            [JsonProperty(PropertyName = Constants.Properties.MediaLink)]
            public string Media { get; set; }

            public string Author { get; set; }
            public string Title { get; set; }
        }

        #region TypeSystem test reference classes
        public interface IFooItem { }

        public class FooItem : IFooItem { }

        public class DerivedFooItem : FooItem { }

        public class MyList<T> : List<T> { }

        public class MyTupleList<T> : List<Tuple<T, T>> { }

        public class DerivedFooCollection : IList<IFooItem>, IEnumerable<DerivedFooItem>
        {
            public int IndexOf(IFooItem item)
            {
                throw new NotImplementedException();
            }

            public void Insert(int index, IFooItem item)
            {
                throw new NotImplementedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            public IFooItem this[int index]
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public void Add(IFooItem item)
            {
                throw new NotImplementedException();
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(IFooItem item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(IFooItem[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { throw new NotImplementedException(); }
            }

            public bool IsReadOnly
            {
                get { throw new NotImplementedException(); }
            }

            public bool Remove(IFooItem item)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<IFooItem> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator<DerivedFooItem> IEnumerable<DerivedFooItem>.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }

        public class FooStringCollection : IList<string>, IEnumerable<FooItem>
        {
            public int IndexOf(string item)
            {
                throw new NotImplementedException();
            }

            public void Insert(int index, string item)
            {
                throw new NotImplementedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            public string this[int index]
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public void Add(string item)
            {
                throw new NotImplementedException();
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(string item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(string[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { throw new NotImplementedException(); }
            }

            public bool IsReadOnly
            {
                get { throw new NotImplementedException(); }
            }

            public bool Remove(string item)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<string> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator<FooItem> IEnumerable<FooItem>.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }

        public class FooTCollection<T> : List<FooItem>, IEnumerable<T>
        {
            public new IEnumerator<T> GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }
        #endregion
    }
}

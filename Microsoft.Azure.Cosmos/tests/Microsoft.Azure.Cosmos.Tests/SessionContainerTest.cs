//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Common;
    using Collections;
    using Client;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Tests for <see cref="SessionContainer"/> class.
    /// </summary>
    [TestClass]
    public class SessionContainerTest
    {
        /// <summary>
        /// Simple test for <see cref="SessionContainer"/> class.
        /// </summary>
        [TestMethod]
        public void TestSessionContainer()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            int numCollections = 2;
            int numPartitionKeyRangeIds = 5;

            for (uint i = 0; i < numCollections; i++)
            {
                var collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129 + i).DocumentCollectionId.ToString();
                string collectionFullname = "dbs/db1/colls/collName_" + i;

                for (int j = 0; j < numPartitionKeyRangeIds; j++)
                {
                    string partitionKeyRangeId = "range_" + j;
                    string lsn = "1#" + j + "#4=90#5=2";

                    sessionContainer.SetSessionToken(
                        collectionResourceId,
                        collectionFullname,
                        new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, $"{partitionKeyRangeId}:{lsn}" } });
                }
            }

            using (DocumentServiceRequest request =
                 DocumentServiceRequest.Create(
                     OperationType.ReadFeed,
                     ResourceType.Collection,
                     new Uri("https://foo.com/dbs/db1/colls/collName_1", UriKind.Absolute),
                     new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                     AuthorizationTokenType.PrimaryMasterKey,
                     null))
            {
                ISessionToken sessionToken = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_1");
                Assert.IsTrue(sessionToken.LSN == 1);

                DocumentServiceRequestContext dsrContext = new DocumentServiceRequestContext();
                PartitionKeyRange resolvedPKRange = new PartitionKeyRange();
                resolvedPKRange.Id = "range_" + (numPartitionKeyRangeIds + 10);
                resolvedPKRange.Parents = new Collection<string>(new List<string> { "range_2", "range_x" });
                dsrContext.ResolvedPartitionKeyRange = resolvedPKRange;
                request.RequestContext = dsrContext;

                sessionToken = sessionContainer.ResolvePartitionLocalSessionToken(request, resolvedPKRange.Id);
                Assert.IsTrue(sessionToken.LSN == 2);
            }
        }

        [TestMethod]
        public void TestResolveGlobalSessionTokenReturnsEmptyStringOnEmptyCache()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");
            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                Assert.AreEqual(string.Empty, sessionContainer.ResolveGlobalSessionToken(request));
            }
        }

        [TestMethod]
        public void TestResolveGlobalSessionTokenReturnsEmptyStringOnCacheMiss()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls1/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=0" } }
            );

            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName2/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                Assert.AreEqual(string.Empty, sessionContainer.ResolveGlobalSessionToken(request));
            }
        }

        [TestMethod]
        public void TestResolveGlobalSessionTokenReturnsSerializedPartitionTokenMapUsingName()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
            );


            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                string token = sessionContainer.ResolveGlobalSessionToken(request);

                HashSet<string> map = new HashSet<string>(token.Split(','));

                Assert.AreEqual(2, map.Count);
                Assert.IsTrue(map.Contains("range_0:1#100#4=90#5=1"));
                Assert.IsTrue(map.Contains("range_1:1#101#4=90#5=1"));
            }
        }

        [TestMethod]
        public void TestResolveGlobalSessionTokenReturnsSerializedPartitionTokenMapUsingResourceId()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
            );


            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                string token = sessionContainer.ResolveGlobalSessionToken(request);

                HashSet<string> map = new HashSet<string>(token.Split(','));

                Assert.AreEqual(2, map.Count);
                Assert.IsTrue(map.Contains("range_0:1#100#4=90#5=1"));
                Assert.IsTrue(map.Contains("range_1:1#101#4=90#5=1"));
            }
        }

        [TestMethod]
        public void TestResolvePartitionLocalSessionTokenReturnsTokenMapUsingName()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
            );


            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(100, token.LSN);
            }
        }

        [TestMethod]
        public void TestResolvePartitionLocalSessionTokenReturnsTokenMapUsingResourceId()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
            );


            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_1");

                Assert.AreEqual(101, token.LSN);
            }
        }

        [TestMethod]
        public void TestResolvePartitionLocalSessionTokenReturnsNullOnPartitionMiss()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
            );

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange();
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_2");

                Assert.AreEqual(null, token);
            }
        }

        [TestMethod]
        public void TestResolvePartitionLocalSessionTokenReturnsNullOnCollectionMiss()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId1,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId1,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
            );

            var collectionResourceId2 = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId2, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_1");

                Assert.AreEqual(null, token);
            }
        }

        [TestMethod]
        public void TestResolvePartitionLocalSessionTokenReturnsTokenOnParentMatch()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
            );

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange();
                request.RequestContext.ResolvedPartitionKeyRange.Parents = new Collection<string>() { "range_1" };

                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_2");

                Assert.AreEqual(101, token.LSN);
            }
        }

        [TestMethod]
        public void TestClearTokenByCollectionFullnameRemovesToken()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            // check that can read from cache based on resource-id
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");
                Assert.AreEqual(100, token.LSN);
            }

            // check that can read from cache based on name
            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");
                Assert.AreEqual(100, token.LSN);
            }

            sessionContainer.ClearTokenByCollectionFullname(collectionFullname);

            // check that can't read from cache based on resource-id
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");
                Assert.AreEqual(null, token);
            }

            // check that can't read from cache based on name
            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");
                Assert.AreEqual(null, token);
            }
        }

        [TestMethod]
        public void TestClearTokenByResourceIdRemovesToken()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            // check that can read from cache based on resource-id
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");
                Assert.AreEqual(100, token.LSN);
            }

            // check that can read from cache based on name
            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");
                Assert.AreEqual(100, token.LSN);
            }

            sessionContainer.ClearTokenByResourceId(collectionResourceId);

            // check that can't read from cache based on resource-id
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");
                Assert.AreEqual(null, token);
            }

            // check that can't read from cache based on name
            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");
                Assert.AreEqual(null, token);
            }
        }

        [TestMethod]
        public void TestClearTokenKeepsUnmatchedCollection()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            sessionContainer.SetSessionToken(
                collectionResourceId1,
                collectionFullname1,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            var collectionResourceId2 = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();
            string collectionFullname2 = "dbs/db1/colls/collName2";

            sessionContainer.SetSessionToken(
                collectionResourceId2,
                collectionFullname2,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId1, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");
                Assert.AreEqual(100, token.LSN);
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId2, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");
                Assert.AreEqual(100, token.LSN);
            }

            sessionContainer.ClearTokenByResourceId(collectionResourceId1);

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId1, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");
                Assert.AreEqual(null, token);
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId2, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");
                Assert.AreEqual(100, token.LSN);
            }
        }

        [TestMethod]
        public void TestSetSessionTokenDoesntFailOnEmptySessionTokenHeader()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            sessionContainer.SetSessionToken(null, new StoreRequestNameValueCollection());
        }

        [TestMethod]
        public void TestSetSessionTokenSetsTokenWhenRequestIsntNameBased()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                Assert.IsFalse(request.IsNameBased);

                sessionContainer.SetSessionToken(request, new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } });
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId1, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(100, token.LSN);
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(100, token.LSN);
            }
        }

        [TestMethod]
        public void TestSetSessionTokenGivesPriorityToOwnerFullNameOverResourceAddress()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";
            string collectionFullname2 = "dbs/db1/colls/collName2";

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                sessionContainer.SetSessionToken(
                    request,
                    new StoreRequestNameValueCollection() {
                        { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" },
                        { HttpConstants.HttpHeaders.OwnerFullName, collectionFullname2 }
                    }
                );
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(null, token);
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, collectionFullname2 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(100, token.LSN);
            }
        }

        [TestMethod]
        public void TestSetSessionTokenIgnoresOwnerIdWhenRequestIsntNameBased()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            var collectionResourceId2 = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                Assert.IsFalse(request.IsNameBased);

                sessionContainer.SetSessionToken(
                    request,
                    new StoreRequestNameValueCollection() {
                        { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" },
                        { HttpConstants.HttpHeaders.OwnerId, collectionResourceId2 }
                    }
                );
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId1, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(100, token.LSN);
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId2, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(null, token);
            }
        }

        [TestMethod]
        public void TestSetSessionTokenSetsTokenWhenRequestIsNameBased()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                Assert.IsTrue(request.IsNameBased);

                sessionContainer.SetSessionToken(request, new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } });
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId1, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(100, token.LSN);
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(100, token.LSN);
            }
        }

        [TestMethod]
        public void TestSetSessionTokenGivesPriorityToOwnerIdOverResourceIdWhenRequestIsNameBased()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            var collectionResourceId2 = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                Assert.IsTrue(request.IsNameBased);

                sessionContainer.SetSessionToken(
                    request,
                    new StoreRequestNameValueCollection() {
                        { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" },
                        { HttpConstants.HttpHeaders.OwnerId, collectionResourceId2 }
                    }
                );
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId1, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(null, token);
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId2, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(100, token.LSN);
            }
        }

        [TestMethod]
        public void TestSetSessionTokenDoesntWorkForMasterQueries()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.ReadFeed, collectionFullname1 + "/docs/42", ResourceType.Collection, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                sessionContainer.SetSessionToken(request, new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1" } });
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId1, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(null, token);
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(null, token);
            }
        }

        [TestMethod]
        public void TestSetSessionTokenDoesntOverwriteHigherLSN()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                sessionContainer.SetSessionToken(request, new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#105#4=90#5=1" } });
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                sessionContainer.SetSessionToken(request, new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } });
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId1, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(105, token.LSN);
            }
        }

        [TestMethod]
        public void TestSetSessionTokenOverwritesLowerLSN()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                sessionContainer.SetSessionToken(request, new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } });
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                sessionContainer.SetSessionToken(request, new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#105#4=90#5=1" } });
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId1, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_0");

                Assert.AreEqual(105, token.LSN);
            }
        }

        [TestMethod]
        public void TestSetSessionTokenDoesNothingOnEmptySessionTokenHeader()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            var collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname + "/docs/42",
                new StoreRequestNameValueCollection()
                {
                    { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" }
                }
            );

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                string token = sessionContainer.ResolveGlobalSessionToken(request);

                HashSet<string> map = new HashSet<string>(token.Split(','));

                Assert.AreEqual(1, map.Count);
                Assert.IsTrue(map.Contains("range_0:1#100#4=90#5=1"));
            }

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname + "/docs/42",
                new StoreRequestNameValueCollection()
            );

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                string token = sessionContainer.ResolveGlobalSessionToken(request);

                HashSet<string> map = new HashSet<string>(token.Split(','));

                Assert.AreEqual(1, map.Count);
                Assert.IsTrue(map.Contains("range_0:1#100#4=90#5=1"));
            }
        }

        [TestMethod]
        public void TestNewCollectionResourceIdInvalidatesOldCollectionResourceId()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            ResourceId resourceId = ResourceId.NewDocumentCollectionId(42, 129);
            string dbResourceId = resourceId.DatabaseId.ToString();
            string oldCollectionResourceId = resourceId.DocumentCollectionId.ToString();
            string newCollectionResourceId = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();

            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                oldCollectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                newCollectionResourceId,
                collectionFullname,
                new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#101#4=90#5=1" } }
            );

            Assert.IsTrue(string.IsNullOrEmpty(sessionContainer.GetSessionToken(string.Format("dbs/{0}/colls/{1}", dbResourceId, oldCollectionResourceId))));
            Assert.IsFalse(string.IsNullOrEmpty(sessionContainer.GetSessionToken(string.Format("dbs/{0}/colls/{1}", dbResourceId, newCollectionResourceId))));
        }
    }
}

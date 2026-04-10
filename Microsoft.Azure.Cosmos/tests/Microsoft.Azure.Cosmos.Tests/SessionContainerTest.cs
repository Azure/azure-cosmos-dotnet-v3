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
    using System.Threading.Tasks;
    using Client;
    using Collections;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129 + i).DocumentCollectionId.ToString();
                string collectionFullname = "dbs/db1/colls/collName_" + i;

                for (int j = 0; j < numPartitionKeyRangeIds; j++)
                {
                    string partitionKeyRangeId = "range_" + j;
                    string lsn = "1#" + j + "#4=90#5=2";

                    sessionContainer.SetSessionToken(
                        collectionResourceId,
                        collectionFullname,
                        new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, $"{partitionKeyRangeId}:{lsn}" } });
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
                PartitionKeyRange resolvedPKRange = new PartitionKeyRange
                {
                    Id = "range_" + (numPartitionKeyRangeIds + 10),
                    Parents = new Collection<string>(new List<string> { "range_2", "range_x" })
                };
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

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls1/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=0" } }
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

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
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

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
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

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
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

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
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

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
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

            string collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId1,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId1,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
            );

            string collectionResourceId2 = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();
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

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#101#4=90#5=1" } }
            );

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionResourceId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange
                {
                    Parents = new Collection<string>() { "range_1" }
                };

                ISessionToken token = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_2");

                Assert.AreEqual(101, token.LSN);
            }
        }

        [TestMethod]
        public void TestClearTokenByCollectionFullnameRemovesToken()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
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

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
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

            string collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            sessionContainer.SetSessionToken(
                collectionResourceId1,
                collectionFullname1,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            string collectionResourceId2 = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();
            string collectionFullname2 = "dbs/db1/colls/collName2";

            sessionContainer.SetSessionToken(
                collectionResourceId2,
                collectionFullname2,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
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

            sessionContainer.SetSessionToken(null, new RequestNameValueCollection());
        }

        [TestMethod]
        public void TestSetSessionTokenSetsTokenWhenRequestIsntNameBased()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            string collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                Assert.IsFalse(request.IsNameBased);

                sessionContainer.SetSessionToken(request, new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } });
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

            string collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";
            string collectionFullname2 = "dbs/db1/colls/collName2";

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                sessionContainer.SetSessionToken(
                    request,
                    new RequestNameValueCollection() {
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

            string collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionResourceId2 = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                Assert.IsFalse(request.IsNameBased);

                sessionContainer.SetSessionToken(
                    request,
                    new RequestNameValueCollection() {
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

            string collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                Assert.IsTrue(request.IsNameBased);

                sessionContainer.SetSessionToken(request, new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } });
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

            string collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionResourceId2 = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                Assert.IsTrue(request.IsNameBased);

                sessionContainer.SetSessionToken(
                    request,
                    new RequestNameValueCollection() {
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

            string collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.ReadFeed, collectionFullname1 + "/docs/42", ResourceType.Collection, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                sessionContainer.SetSessionToken(request, new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1" } });
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

            string collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                sessionContainer.SetSessionToken(request, new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#105#4=90#5=1" } });
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                sessionContainer.SetSessionToken(request, new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } });
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

            string collectionResourceId1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname1 = "dbs/db1/colls/collName1";

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                sessionContainer.SetSessionToken(request, new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } });
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, collectionFullname1 + "/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null))
            {
                request.ResourceId = collectionResourceId1;

                sessionContainer.SetSessionToken(request, new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#105#4=90#5=1" } });
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

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname + "/docs/42",
                new RequestNameValueCollection()
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
                new RequestNameValueCollection()
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
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#100#4=90#5=1" } }
            );

            sessionContainer.SetSessionToken(
                newCollectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#101#4=90#5=1" } }
            );

            Assert.IsTrue(string.IsNullOrEmpty(sessionContainer.GetSessionToken(string.Format("dbs/{0}/colls/{1}", dbResourceId, oldCollectionResourceId))));
            Assert.IsFalse(string.IsNullOrEmpty(sessionContainer.GetSessionToken(string.Format("dbs/{0}/colls/{1}", dbResourceId, newCollectionResourceId))));
        }

        /// <summary>
        /// Use the session token of the parent if request comes for a child
        /// </summary>
        [TestMethod]
        public void TestResolveSessionTokenFromParent_Gateway_AfterSplit()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            // Set token for the parent
            string parentPKRangeId = "0";
            string parentSession = "1#100#4=90#5=1";
            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, $"{parentPKRangeId}:{parentSession}" } }
            );

            // We send requests for the children

            string childPKRangeId = "1";
            string childPKRangeId2 = "1";

            DocumentServiceRequest documentServiceRequestToChild1 = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null);

            documentServiceRequestToChild1.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange()
            {
                Id = childPKRangeId,
                MinInclusive = "",
                MaxExclusive = "AA",
                Parents = new Collection<string>() { parentPKRangeId } // PartitionKeyRange says who is the parent
            };

            string resolvedToken = sessionContainer.ResolvePartitionLocalSessionTokenForGateway(
                documentServiceRequestToChild1,
                childPKRangeId);// For one of the children

            Assert.AreEqual($"{childPKRangeId}:{parentSession}", resolvedToken);

            DocumentServiceRequest documentServiceRequestToChild2 = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null);

            documentServiceRequestToChild2.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange()
            {
                Id = childPKRangeId2,
                MinInclusive = "AA",
                MaxExclusive = "FF",
                Parents = new Collection<string>() { parentPKRangeId } // PartitionKeyRange says who is the parent
            };

            resolvedToken = sessionContainer.ResolvePartitionLocalSessionTokenForGateway(
                documentServiceRequestToChild2,
                childPKRangeId2);// For the other child

            Assert.AreEqual($"{childPKRangeId2}:{parentSession}", resolvedToken);
        }

        // <summary>
        /// Use the session token of the parent if request comes for a child when 2 parents are present
        /// </summary>
        [TestMethod]
        public void TestResolveSessionTokenFromParent_Gateway_AfterMerge()
        {
            SessionContainer sessionContainer = new SessionContainer("127.0.0.1");

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            // Set tokens for the parents
            string parentPKRangeId = "0";
            int maxGlobalLsn = 100;
            int maxLsnRegion1 = 200;
            int maxLsnRegion2 = 300;
            int maxLsnRegion3 = 400;

            // Generate 2 tokens, one has max global but lower regional, the other lower global but higher regional
            // Expect the merge to contain all the maxes
            string parentSession = $"1#{maxGlobalLsn}#1={maxLsnRegion1 - 1}#2={maxLsnRegion2}#3={maxLsnRegion3 - 1}";
            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, $"{parentPKRangeId}:{parentSession}" } }
            );

            string parent2PKRangeId = "1";
            string parent2Session = $"1#{maxGlobalLsn - 1}#1={maxLsnRegion1}#2={maxLsnRegion2 - 1}#3={maxLsnRegion3}";
            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, $"{parent2PKRangeId}:{parent2Session}" } }
            );

            string tokenWithAllMax = $"1#{maxGlobalLsn}#1={maxLsnRegion1}#2={maxLsnRegion2}#3={maxLsnRegion3}";

            // Request for a child from both parents
            string childPKRangeId = "2";

            DocumentServiceRequest documentServiceRequestToChild1 = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null);

            documentServiceRequestToChild1.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange()
            {
                Id = childPKRangeId,
                MinInclusive = "",
                MaxExclusive = "FF",
                Parents = new Collection<string>() { parentPKRangeId, parent2PKRangeId } // PartitionKeyRange says who are the parents
            };

            string resolvedToken = sessionContainer.ResolvePartitionLocalSessionTokenForGateway(
                documentServiceRequestToChild1,
                childPKRangeId);// For one of the children

            // Expect the resulting token is for the child partition but containing all maxes of the lsn of the parents
            Assert.AreEqual($"{childPKRangeId}:{tokenWithAllMax}", resolvedToken);
        }

    }
}
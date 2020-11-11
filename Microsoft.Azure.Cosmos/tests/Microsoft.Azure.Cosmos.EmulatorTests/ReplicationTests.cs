//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class ReplicationTests
    {
        private readonly int serverStalenessIntervalInSeconds;
        private readonly int masterStalenessIntervalInSeconds;

        internal static readonly int ConfigurationRefreshIntervalInSec = 40;
        internal static readonly int BackendConfigurationRefreshIntervalInSec = 240;

        public ReplicationTests()
        {
            this.serverStalenessIntervalInSeconds = int.Parse(ConfigurationManager.AppSettings["ServerStalenessIntervalInSeconds"], CultureInfo.InvariantCulture);
            this.masterStalenessIntervalInSeconds = int.Parse(ConfigurationManager.AppSettings["MasterStalenessIntervalInSeconds"], CultureInfo.InvariantCulture);
        }


        private void WaitForMasterReplication()
        {
            if (this.masterStalenessIntervalInSeconds != 0)
            {
                Task.Delay(this.masterStalenessIntervalInSeconds * 1000);
            }
        }

        private void WaitForServerReplication()
        {
            if (this.serverStalenessIntervalInSeconds != 0)
            {
                Task.Delay(this.serverStalenessIntervalInSeconds * 1000);
            }
        }
#region Replication Validation Helpers
        internal static bool ResourceDoesnotExists<T>(string resourceId,
            DocumentClient client,
            string ownerId = null) where T : Resource, new()
        {
            T nonExistingResource;
            try
            {
                if (typeof(T) != typeof(Attachment))
                {
                    INameValueCollection responseHeaders;
                    nonExistingResource = TestCommon.ReadWithRetry<T>(client, resourceId, out responseHeaders);
                }
                else
                {
                    Attachment nonExisitingAttachment = client.Read<Attachment>(resourceId);
                }
                return false;
            }
            catch (DocumentClientException clientException)
            {
                TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                return true;
            }
        }

        internal static void ValidateResourceProperties<T>(List<T> resources) where T : Resource, new()
        {
            for (int i = 0; i < resources.Count - 1; i++)
            {
                for (int j = i + 1; j < resources.Count; j++)
                {
                    // First validate resource properties
                    Assert.AreEqual(resources[i].ResourceId, resources[j].ResourceId, "RID mismatched");
                    Assert.AreEqual(resources[i].ETag, resources[j].ETag, "ETag mismatched");
                    Assert.AreEqual(resources[i].Id, resources[j].Id, "Name mismatched");
                    Assert.AreEqual(resources[i].SelfLink, resources[j].SelfLink, "SelfLink mismatched");
                    Assert.AreEqual(resources[i].Timestamp, resources[j].Timestamp, "Timestamp mismatched");
                }
            }

            if (typeof(T) == typeof(Database))
            {
                Database[] databases = resources.Cast<Database>().ToArray();

                for (int i = 0; i < resources.Count - 1; i++)
                {
                    for (int j = i + 1; j < resources.Count; j++)
                    {
                        Assert.AreEqual(databases[i].CollectionsLink, databases[j].CollectionsLink,
                            "Database CollectionLink don't match");
                    }
                }
            }
            else if (typeof(T) == typeof(DocumentCollection))
            {
                DocumentCollection[] documentCollections = resources.Cast<DocumentCollection>().ToArray();

                for (int i = 0; i < documentCollections.Length - 1; i++)
                {
                    for (int j = i + 1; j < documentCollections.Length; j++)
                    {
                        Assert.AreEqual(documentCollections[i].DocumentsLink,
                            documentCollections[j].DocumentsLink,
                            "DocumentCollection DocumentsLink mismatch");
                        Assert.AreEqual(documentCollections[i].IndexingPolicy.Automatic,
                            documentCollections[j].IndexingPolicy.Automatic,
                            "DocumentCollection IndexingPolicy.Automatic mismatch");
                        Assert.AreEqual(documentCollections[i].IndexingPolicy.IndexingMode,
                            documentCollections[j].IndexingPolicy.IndexingMode,
                            "DocumentCollection IndexingPolicy.IndexingMode mismatch");
                        // TODO: nemanjam, add other collection properties
                    }
                }
            }
            else if (typeof(T) == typeof(User))
            {
                User[] users = resources.Cast<User>().ToArray();

                for (int i = 0; i < users.Length - 1; i++)
                {
                    for (int j = i + 1; j < users.Length; j++)
                    {
                        Assert.AreEqual(users[i].PermissionsLink, users[j].PermissionsLink,
                            "User PermissionsLink mismatch");
                    }
                }
            }
            else if (typeof(T) == typeof(Permission))
            {
                Permission[] permissions = resources.Cast<Permission>().ToArray();

                for (int i = 0; i < permissions.Length - 1; i++)
                {
                    for (int j = i + 1; j < permissions.Length; j++)
                    {
                        Assert.AreEqual(permissions[i].PermissionMode, permissions[j].PermissionMode,
                            "Permission PermissionMode mismatch");
                        Assert.AreEqual(permissions[i].ResourceLink, permissions[j].ResourceLink,
                            "Permission ResourceLink mismatch");
                    }
                }
            }
            else if (typeof(T) == typeof(Document))
            {
                Document[] documents = resources.Cast<Document>().ToArray();

                for (int i = 0; i < documents.Length - 1; i++)
                {
                    for (int j = i + 1; j < documents.Length; j++)
                    {
                        Assert.AreEqual(documents[i].AttachmentsLink, documents[j].AttachmentsLink,
                            "Document AttachmentsLink mismatch");
                    }
                }

                //TODO, KRAMAN, ADD validation for document content.
            }
            else if (typeof(T) == typeof(Attachment))
            {
                Attachment[] attachments = resources.Cast<Attachment>().ToArray();

                for (int i = 0; i < attachments.Length - 1; i++)
                {
                    for (int j = i + 1; j < attachments.Length; j++)
                    {
                        Assert.AreEqual(attachments[i].ContentType, attachments[j].ContentType,
                            "Attachment ContentType mismatch");
                        Assert.AreEqual(attachments[i].MediaLink, attachments[j].MediaLink,
                            "Attachment MediaLink mismatch");
                    }
                }
            }
            else if (typeof(T) == typeof(StoredProcedure))
            {
                StoredProcedure[] storedProcedures = resources.Cast<StoredProcedure>().ToArray();

                for (int i = 0; i < storedProcedures.Length - 1; i++)
                {
                    for (int j = i + 1; j < storedProcedures.Length; j++)
                    {
                        Assert.AreEqual(storedProcedures[i].Body, storedProcedures[j].Body,
                            "StoredProcedure Body mismatch");
                    }
                }
            }
            else if (typeof(T) == typeof(Trigger))
            {
                Trigger[] triggers = resources.Cast<Trigger>().ToArray();

                for (int i = 0; i < triggers.Length - 1; i++)
                {
                    for (int j = i + 1; j < triggers.Length; j++)
                    {
                        Assert.AreEqual(triggers[i].Body, triggers[j].Body, "Trigger Body mismatch");
                    }
                }
            }
            else if (typeof(T) == typeof(UserDefinedFunction))
            {
                UserDefinedFunction[] userDefinedFunctions = resources.Cast<UserDefinedFunction>().ToArray();

                for (int i = 0; i < userDefinedFunctions.Length - 1; i++)
                {
                    for (int j = i + 1; j < userDefinedFunctions.Length; j++)
                    {
                        Assert.AreEqual(userDefinedFunctions[i].Body, userDefinedFunctions[j].Body,
                            "UserDefinedFunction Body mismatch");
                    }
                }
            }
            else if (typeof(T) == typeof(Conflict))
            {
                Conflict[] conflicts = resources.Cast<Conflict>().ToArray();

                for (int i = 0; i < conflicts.Length - 1; i++)
                {
                    for (int j = i + 1; j < conflicts.Length; j++)
                    {
                        Assert.AreEqual(conflicts[i].ResourceType, conflicts[j].ResourceType,
                            "Conflict ResourceType mismatch");
                        Assert.AreEqual(conflicts[i].OperationKind, conflicts[j].OperationKind,
                            "Conflict OperationKind mismatch");
                        Assert.AreEqual(conflicts[i].ResourceId, conflicts[j].ResourceId,
                            "Conflict ResourceId mismatch");
                        Assert.AreEqual(conflicts[i].OperationKind, conflicts[j].OperationKind,
                            "Conflict OperationKind mismatch");

                        if (conflicts[i].OperationKind == Documents.OperationKind.Delete)
                        {
                            continue;
                        }

                        if (conflicts[i].ResourceType == typeof(Attachment))
                        {
                            ReplicationTests.ValidateResourceProperties<Attachment>(
                                conflicts.Select(x => x.GetResource<Attachment>()).ToList());
                        }
                        else if (conflicts[i].ResourceType == typeof(Document))
                        {
                            ReplicationTests.ValidateResourceProperties<Document>(
                                conflicts.Select(x => x.GetResource<Document>()).ToList());
                        }
                        else if (conflicts[i].ResourceType == typeof(StoredProcedure))
                        {
                            ReplicationTests.ValidateResourceProperties<StoredProcedure>(
                                conflicts.Select(x => x.GetResource<StoredProcedure>()).ToList());
                        }
                        else if (conflicts[i].ResourceType == typeof(Trigger))
                        {
                            ReplicationTests.ValidateResourceProperties<Trigger>(
                                conflicts.Select(x => x.GetResource<Trigger>()).ToList());
                        }
                        else if (conflicts[i].ResourceType == typeof(UserDefinedFunction))
                        {
                            ReplicationTests.ValidateResourceProperties<UserDefinedFunction>(
                                conflicts.Select(x => x.GetResource<UserDefinedFunction>()).ToList());
                        }
                        else
                        {
                            Assert.Fail("Invalid resource type {0}", conflicts[i].ResourceType);
                        }
                    }
                }
            }
        }

        internal static void ValidateQuery<T>(DocumentClient client, string collectionLink, string queryProperty, string queryPropertyValue, int expectedCount, INameValueCollection headers = null)
            where T : Resource, new()
        {
            INameValueCollection inputHeaders = headers;
            headers = new StoreRequestNameValueCollection();
            if (inputHeaders != null)
            {
                headers.Add(inputHeaders); // dont mess with the input headers
            }

            int maxTries = 5;
            const int minIndexInterval = 5000; // 5 seconds
            while (maxTries-- > 0)
            {
                DocumentFeedResponse<dynamic> resourceFeed = null;
                IDocumentQuery<dynamic> queryService = null;
                string queryString = @"select * from root r where r." + queryProperty + @"=""" + queryPropertyValue + @"""";
                if (typeof(T) == typeof(Database))
                {
                    queryService = client.CreateDatabaseQuery(queryString).AsDocumentQuery();
                }
                else if (typeof(T) == typeof(DocumentCollection))
                {
                    queryService = client.CreateDocumentCollectionQuery(collectionLink, queryString).AsDocumentQuery();
                }
                else if (typeof(T) == typeof(Document))
                {
                    queryService = client.CreateDocumentQuery(collectionLink, queryString).AsDocumentQuery();
                }
                else
                {
                    Assert.Fail("Unexpected type");
                }

                while (queryService.HasMoreResults)
                {
                    resourceFeed = queryService.ExecuteNextAsync().Result;

                    if (resourceFeed.Count > 0)
                    {
                        Assert.IsNotNull(resourceFeed, "Query result is null");
                        Assert.AreNotEqual(0, resourceFeed.Count, "Query result is invalid");

                        foreach (T resource in resourceFeed)
                        {
                            if (queryProperty.Equals("name", StringComparison.CurrentCultureIgnoreCase))
                                Assert.AreEqual(resource.Id, queryPropertyValue, "Result contain invalid result");
                        }
                        return;
                    }
                }

                Task.Delay(minIndexInterval);
            }

            Assert.Fail("Query did not return result after max tries");
        }

        ///// <summary>
        ///// Changes the replication mode to sync for server.
        ///// </summary>
        ///// <param name="fabricClient"></param>
        //internal static void SwitchServerToSyncReplication(COMMONNAMESPACE.IFabricClient fabricClient, string callingComponent)
        //{
        //    if (!IsCurrentReplicationModeAsync(fabricClient)["Server"])
        //    {
        //        return;
        //    }

        //    NamingServiceConfig.NamingServiceConfigurationWriter namingServiceWriter =
        //            new NamingServiceConfig.NamingServiceConfigurationWriter(fabricClient);

        //    NamingServiceConfig.DocumentServiceConfiguration config = new NamingServiceConfig.DocumentServiceConfiguration();
        //    config.DocumentServiceName = ConfigurationManager.AppSettings["DatabaseAccountId"];
        //    config.IsServerReplicationAsync = false;

        //    namingServiceWriter.UpdateDatabaseAccountConfigurationAsync(config).Wait();

        //    Task.Delay(TimeSpan.FromSeconds(ReplicationTests.ConfigurationRefreshIntervalInSec)).Wait();
        //    TestCommon.ForceRefreshNamingServiceConfigs(callingComponent, FabricServiceType.ServerService).Wait();
        //}

        ///// <summary>
        ///// Changes the replication mode to async for server.
        ///// </summary>
        ///// <param name="fabricClient"></param>
        //internal static void SwitchServerToAsyncReplication(COMMONNAMESPACE.IFabricClient fabricClient, string callingComponent)
        //{
        //    if (IsCurrentReplicationModeAsync(fabricClient)["Server"])
        //    {
        //        return;
        //    }

        //    NamingServiceConfig.NamingServiceConfigurationWriter namingServiceWriter =
        //        new NamingServiceConfig.NamingServiceConfigurationWriter(fabricClient);

        //    NamingServiceConfig.DocumentServiceConfiguration config = new NamingServiceConfig.DocumentServiceConfiguration();
        //    config.DocumentServiceName = ConfigurationManager.AppSettings["DatabaseAccountId"];
        //    config.IsServerReplicationAsync = true;

        //    namingServiceWriter.UpdateDatabaseAccountConfigurationAsync(config).Wait();

        //    Task.Delay(TimeSpan.FromSeconds(ReplicationTests.ConfigurationRefreshIntervalInSec)).Wait();
        //    TestCommon.ForceRefreshNamingServiceConfigs(callingComponent, FabricServiceType.ServerService).Wait();
        //}

#endregion

#region Environment Configuration Helpers

        internal static DocumentClient[] GetClientsLocked(bool useGateway = false, Protocol protocol = Protocol.Tcp, int timeoutInSeconds = 10, ConsistencyLevel? defaultConsistencyLevel = null, AuthorizationTokenType tokenType = AuthorizationTokenType.PrimaryMasterKey)
        {
            DocumentClient[] toReturn = new DocumentClient[TestCommon.ReplicationFactor];
            for (uint i = 0; i < toReturn.Length; i++)
            {
                toReturn[i] = TestCommon.CreateClient(useGateway, protocol, timeoutInSeconds, defaultConsistencyLevel, tokenType);
                toReturn[i].LockClient(i);
            }

            return toReturn;
        }
#endregion
    }
}

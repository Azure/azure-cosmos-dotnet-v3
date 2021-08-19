//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.EmulatorTests
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption;
    using Microsoft.Data.Encryption.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Encryption.EmulatorTests.MdeEncryptionTests;
    using EncryptionKeyWrapMetadata = Cosmos.EncryptionKeyWrapMetadata;

    [TestClass]
    public class MdeReencryptionTests
    {
        private static EncryptionKeyWrapMetadata metadata1_v1;
        private static EncryptionKeyWrapMetadata metadata2_v1;
        private static EncryptionKeyWrapMetadata metadata1_v2;
        private static EncryptionKeyWrapMetadata metadata2_v2;

        private static CosmosClient client;
        private static CosmosClient encryptionCosmosClient;
        private static Database database;
        private static Container encryptionContainer;
        private static TestEncryptionKeyStoreProvider testEncryptionKeyStoreProvider;

        [ClassInitialize]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The ClassInitialize method takes a single parameter of type TestContext.")]
        public static async Task ClassInitialize(TestContext context)
        {
            MdeReencryptionTests.client = TestCommon.CreateCosmosClient(builder => builder
                .WithBulkExecution(true)
                .Build());

            testEncryptionKeyStoreProvider = new TestEncryptionKeyStoreProvider
            {
                DataEncryptionKeyCacheTimeToLive = null
            };

            metadata1_v1 = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, "key1_v1", "tempmetadata1");
            metadata2_v1 = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, "key2_v1", "tempmetadata2");

            // key rotation
            metadata1_v2 = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, "key1_v2", "tempmetadata2");
            metadata2_v2 = new EncryptionKeyWrapMetadata(testEncryptionKeyStoreProvider.ProviderName, "key2_v2", "tempmetadata1");

            MdeReencryptionTests.encryptionCosmosClient = MdeReencryptionTests.client.WithEncryption(testEncryptionKeyStoreProvider);
            MdeReencryptionTests.database = await MdeReencryptionTests.encryptionCosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());

            await MdeReencryptionTests.CreateClientEncryptionKeyAsync(
               "key1_v1",
               metadata1_v1);

            await MdeReencryptionTests.CreateClientEncryptionKeyAsync(
                "key2_v1",
                metadata2_v1);

            await MdeReencryptionTests.CreateClientEncryptionKeyAsync(
               "key1_v2",
               metadata1_v2);

            await MdeReencryptionTests.CreateClientEncryptionKeyAsync(
                "key2_v2",
                metadata2_v2);

            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
            {            
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ArrayFormat",
                    ClientEncryptionKeyId = "key1_v1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_NestedObjectFormatL1",
                    ClientEncryptionKeyId = "key1_v1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntArray",
                    ClientEncryptionKeyId = "key2_v1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_BoolFormat",
                    ClientEncryptionKeyId = "key1_v1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_DecimalFormat",
                    ClientEncryptionKeyId = "key2_v1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },               

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_DateFormat",
                    ClientEncryptionKeyId = "key2_v1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },               
                
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ArrayMultiTypes",
                    ClientEncryptionKeyId = "key1_v1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
               
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntMultiDimArray",
                    ClientEncryptionKeyId = "key2_v1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
            };

            ClientEncryptionPolicy clientEncryptionPolicy = new ClientEncryptionPolicy(paths);        
            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/PK") { ClientEncryptionPolicy = clientEncryptionPolicy };
            encryptionContainer = await database.CreateContainerAsync(containerProperties, 400);
            await encryptionContainer.InitializeEncryptionAsync();
        }

        [TestMethod]
        public async Task ValidateChangeEncryptionPolicy()
        {
            int totalDocsToPopulate = 10;
            (Container encryptionContainerWithNewPolicy, Collection<ClientEncryptionIncludedPath> paths) = await PopulateExistingContainerAndGetContainerWithNewPolicy(
                totalDocsToPopulate);

            await encryptionContainerWithNewPolicy.InitializeEncryptionAsync();

            ChangeFeedRequestOptions changeFeedRequestOptions = new ChangeFeedRequestOptions
            {
                PageSizeHint = 5
            };

            ReencryptionIterator reencryptionIterator =  await MdeReencryptionTests.encryptionContainer.GetReencryptionIteratorAsync(
                encryptionContainerWithNewPolicy.Id,
                () => true,
                changeFeedRequestOptions: changeFeedRequestOptions);

            ReencryptionResponseMessage reencryptionResponseMessage = await reencryptionIterator.EncryptNextAsync();           

            // pass the continuation token
            reencryptionIterator = await MdeReencryptionTests.encryptionContainer.GetReencryptionIteratorAsync(
                encryptionContainerWithNewPolicy.Id,
                () => true,
                continuationToken: reencryptionResponseMessage.ContinuationToken);

            while (reencryptionIterator.HasMoreResults)
            {
                reencryptionResponseMessage = await reencryptionIterator.EncryptNextAsync();
                if (reencryptionResponseMessage.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }
            }

            // add 10 more
            await MdeReencryptionTests.PopulateContainerWithDataAsync(MdeReencryptionTests.encryptionContainer, totalDocsToPopulate);

            // pass the continuation token
            reencryptionIterator = await MdeReencryptionTests.encryptionContainer.GetReencryptionIteratorAsync(
                encryptionContainerWithNewPolicy.Id,
                () => true,
                continuationToken: reencryptionResponseMessage.ContinuationToken);

            while (reencryptionIterator.HasMoreResults)
            {
                reencryptionResponseMessage = await reencryptionIterator.EncryptNextAsync();
                if (reencryptionResponseMessage.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }
            }

            await ValidatePathChangesInPolicyAsync(MdeReencryptionTests.encryptionContainer,encryptionContainerWithNewPolicy, totalDocsToPopulate * 2, paths);
        }

        [TestMethod]
        public async Task ValidateClientEncryptionKeyRotation()
        {
            int totalDocsToPopulate = 10;
            (Container encryptionContainerWithRotatedKeys, Collection<ClientEncryptionIncludedPath> paths) = await PopulateExistingContainerAndRotatePolicyKeys(totalDocsToPopulate);

            //await encryptionContainerWithRotatedKeys.InitializeEncryptionAsync();

            ReencryptionIterator reencryptionIterator = await MdeReencryptionTests.encryptionContainer.GetReencryptionIteratorAsync(encryptionContainerWithRotatedKeys.Id, () => true, null, null);

            while (reencryptionIterator.HasMoreResults)
            {
                ReencryptionResponseMessage reencryptionResponseMessage = await reencryptionIterator.EncryptNextAsync();

                if(reencryptionResponseMessage.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }
            }

            await ValidateKeyChangesInPolicyAsync(MdeReencryptionTests.encryptionContainer, encryptionContainerWithRotatedKeys, totalDocsToPopulate, paths);
        }


        private static async Task<(Container, Collection<ClientEncryptionIncludedPath>)> PopulateExistingContainerAndGetContainerWithNewPolicy(int totalDocsToPopulate)
        {
            await MdeReencryptionTests.PopulateContainerWithDataAsync(MdeReencryptionTests.encryptionContainer, totalDocsToPopulate);

            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
            {
                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ArrayMultiTypes",
                    ClientEncryptionKeyId = "key1_v1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ArrayFormat",
                    ClientEncryptionKeyId = "key2_v1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_FloatFormat",
                    ClientEncryptionKeyId = "key1_v1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_NestedObjectFormatL1",
                    ClientEncryptionKeyId = "key2_v1",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
            };

            ClientEncryptionPolicy changedPolicy = new ClientEncryptionPolicy(paths);
            ContainerProperties containerProperties = new ContainerProperties("dstContainerWithNewPolicy", "/PK") { ClientEncryptionPolicy = changedPolicy };
            Container encryptionContainerWithNewPolicy = await database.CreateContainerAsync(containerProperties, 400);
            return (await encryptionContainerWithNewPolicy.InitializeEncryptionAsync(), paths);
        }

        private static async Task<(Container, Collection<ClientEncryptionIncludedPath>)> PopulateExistingContainerAndRotatePolicyKeys(int totalDocsToPopulate)
        {
            await MdeReencryptionTests.PopulateContainerWithDataAsync(MdeReencryptionTests.encryptionContainer, totalDocsToPopulate);

            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
            {
               new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ArrayFormat",
                    ClientEncryptionKeyId = "key1_v2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_NestedObjectFormatL1",
                    ClientEncryptionKeyId = "key1_v2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntArray",
                    ClientEncryptionKeyId = "key2_v2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_BoolFormat",
                    ClientEncryptionKeyId = "key1_v2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_DecimalFormat",
                    ClientEncryptionKeyId = "key2_v2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_DateFormat",
                    ClientEncryptionKeyId = "key2_v2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_ArrayMultiTypes",
                    ClientEncryptionKeyId = "key1_v2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },

                new ClientEncryptionIncludedPath()
                {
                    Path = "/Sensitive_IntMultiDimArray",
                    ClientEncryptionKeyId = "key2_v2",
                    EncryptionType = "Deterministic",
                    EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                },
            };

            ClientEncryptionPolicy changedPolicy = new ClientEncryptionPolicy(paths);
            ContainerProperties containerProperties = new ContainerProperties("dstContainerWithKeyRotated", "/PK") { ClientEncryptionPolicy = changedPolicy };
            Container encryptionContainerWithNewPolicy = await database.CreateContainerAsync(containerProperties, 400);
            return (await encryptionContainerWithNewPolicy.InitializeEncryptionAsync(), paths);
        }

        private static async Task PopulateContainerWithDataAsync(Container container, int totalDocumentCount)
        {
            for(int i=0; i< totalDocumentCount; i++)
            {
                await MdeReencryptionTests.MdeCreateItemAsync(container);
            }
        }

        private static async Task<ClientEncryptionKeyResponse> CreateClientEncryptionKeyAsync(string cekId, Cosmos.EncryptionKeyWrapMetadata encryptionKeyWrapMetadata)
        {
            ClientEncryptionKeyResponse clientEncrytionKeyResponse = await database.CreateClientEncryptionKeyAsync(
                   cekId,
                   DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256,
                   encryptionKeyWrapMetadata);

            Assert.AreEqual(HttpStatusCode.Created, clientEncrytionKeyResponse.StatusCode);
            Assert.IsTrue(clientEncrytionKeyResponse.RequestCharge > 0);
            Assert.IsNotNull(clientEncrytionKeyResponse.ETag);
            return clientEncrytionKeyResponse;
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (MdeReencryptionTests.database != null)
            {
                using (await MdeReencryptionTests.database.DeleteStreamAsync()) { }
            }

            if (MdeReencryptionTests.client != null)
            {
                MdeReencryptionTests.client.Dispose();
            }
        }      

        private static async Task ValidatePathChangesInPolicyAsync(
            Container srcContainer,
            Container dstContainer,
            int count,
            Collection<ClientEncryptionIncludedPath> paths)
        {
            CosmosClient client = TestCommon.CreateCosmosClient();
            Container containerWithoutEncryptionSupport = client.GetContainer(dstContainer.Database.Id, dstContainer.Id);

            // read the document in raw format, document is not decrypted
            FeedIterator<JObject> feedIterator = containerWithoutEncryptionSupport.GetItemQueryIterator<JObject>();
            FeedResponse<JObject>  feedResponseWithoutDecryption = await feedIterator.ReadNextAsync();

            // make sure we have migrated all the documents.
            Assert.AreEqual(feedResponseWithoutDecryption.Count, count);

            TestDoc toGetType = new TestDoc();

            // for each non decrypted document
            foreach (JObject doc in feedResponseWithoutDecryption)
            {
                // first try to convert the Jobject to TestDoc, and check for the failure in the Exception Path, indicating the path was an encrypted string.
                // and remove that property from document.At the end all the encrypted policies paths are removed. 
                // If the reencryption went through properly then we would be able to convert the JObject back to TestDoc.

                int totalPathsProcessed = 0;
                foreach (ClientEncryptionIncludedPath path in paths)
                {
                    try
                    {
                        TestDoc tt = doc.ToObject<TestDoc>();
                        Assert.Fail("Should have failed while creating an instance of TestDoc type");
                    }
                    catch (Exception ex)
                    {
                        if (ex is JsonReaderException jsonReaderException)
                        {
                            bool pathFound = false;
                            foreach (ClientEncryptionIncludedPath pathToVerify in paths)
                            {
                                if (jsonReaderException.Path.Contains(pathToVerify.Path.Substring(1)))
                                {
                                    totalPathsProcessed++;
                                    pathFound = true;
                                    string policyPath = pathToVerify.Path.Substring(1);
                                    doc.Remove(policyPath);
                                    break;
                                }
                            }

                            Assert.AreEqual(true, pathFound);
                        }
                        else
                        {
                            Console.WriteLine(ex);
                            Assert.Fail("Incorrect exception caught");
                        }
                    }                    
                }

                if(totalPathsProcessed != paths.Count)
                {
                    Assert.Fail("Mismatch in total paths encrypted");
                }

                // the conversion would succeed since the encryptedFields are removed. The values are set to default which are ignored while verifying doc.
                TestDoc modifiedDocToCompare = doc.ToObject<TestDoc>();
                
                // get the corresponding decrypted document fromt he source container.
                IOrderedQueryable<TestDoc>  linqQueryable = (IOrderedQueryable<TestDoc>)srcContainer.GetItemLinqQueryable<TestDoc>().Where(d => d.Id == doc.GetValue("id").ToString());
                FeedIterator<TestDoc>  queryResponseIterator = srcContainer.ToEncryptionFeedIterator<TestDoc>(linqQueryable);
                FeedResponse<TestDoc> sourceContainerDoc = await queryResponseIterator.ReadNextAsync();
                VerifyExpectedDocResponse(modifiedDocToCompare, sourceContainerDoc.Resource.First());
            }        
        }

        private static async Task ValidateKeyChangesInPolicyAsync(
            Container srcContainer,
            Container dstContainer,
            int count,
            Collection<ClientEncryptionIncludedPath> paths)
        {
            CosmosClient client = TestCommon.CreateCosmosClient();
            Container containerWithoutEncryptionSupport = client.GetContainer(dstContainer.Database.Id, dstContainer.Id);
            Container srcContainerWithoutEncryptionSupport = client.GetContainer(dstContainer.Database.Id, srcContainer.Id);

            // read the document in raw format, document is not decrypted
            FeedIterator<JObject> feedIterator = containerWithoutEncryptionSupport.GetItemQueryIterator<JObject>();
            FeedResponse<JObject> feedResponseWithoutDecryption = await feedIterator.ReadNextAsync();

            // for each non decrypted document
            foreach (JObject doc in feedResponseWithoutDecryption)
            {
                ItemResponse<JObject> sourceContainerDoc = await srcContainerWithoutEncryptionSupport.ReadItemAsync<JObject>(
                    doc.GetValue("id").ToString(),
                    new PartitionKey(doc.GetValue("PK").ToString()));

                int totalPathsProcessed = 0;
                foreach (ClientEncryptionIncludedPath path in paths)
                {
                    try
                    {
                        TestDoc tt = doc.ToObject<TestDoc>();
                        Assert.Fail("Should have failed while creating an instance of TestDoc type");
                    }
                    catch (Exception ex)
                    {
                        if (ex is JsonReaderException jsonReaderException)
                        {                          
                            bool pathFound = false;
                            foreach (ClientEncryptionIncludedPath pathToVerify in paths)
                            {
                                if (jsonReaderException.Path.Contains(pathToVerify.Path.Substring(1)))
                                {
                                    totalPathsProcessed++;
                                    pathFound = true;
                                    string policyPath = pathToVerify.Path.Substring(1);

                                    JToken srcProperty = sourceContainerDoc.Resource.GetValue(policyPath).Value<JToken>();
                                    JToken dstProperty = doc.GetValue(policyPath).Value<JToken>();

                                    // validate if both the cipher texts are different.Since the keys have been changed.
                                    await ValidateCipherText(srcProperty, dstProperty);

                                    doc.Remove(policyPath);
                                    sourceContainerDoc.Resource.Remove(policyPath);
                                    break;
                                }
                            }

                            Assert.AreEqual(true, pathFound);
                        }
                        else
                        {
                            Console.WriteLine(ex);
                            Assert.Fail("Incorrect exception caught");
                        }
                    }
                }

                if (totalPathsProcessed != paths.Count)
                {
                    Assert.Fail("Mismatch in total paths encrypted");
                }

                // the conversion would succeed since the encryptedFields are removed. The values are set to default which are ignored while verifying doc.
                TestDoc modifiedDocToCompare = doc.ToObject<TestDoc>();
                TestDoc modifiedSourceDocToCompare = sourceContainerDoc.Resource.ToObject<TestDoc>();
                VerifyExpectedDocResponse(modifiedDocToCompare, modifiedSourceDocToCompare);
            }
        }

        private static async Task ValidateCipherText(
            JToken expectedJToken,
            JToken verifyJToken)
        {
            if (expectedJToken.Type == JTokenType.Object && verifyJToken.Type == JTokenType.Object)
            {
                int count = expectedJToken.Children<JProperty>().Count();

                Assert.AreEqual(count, verifyJToken.Children<JProperty>().Count());

                for(int i =0; i< count;i++)
                {
                    JProperty expectedjProperty = expectedJToken.Children<JProperty>().ElementAt(i);
                    JProperty verifyjProperty = verifyJToken.Children<JProperty>().ElementAt(i);
                    await ValidateCipherText(
                        expectedjProperty.Value,
                        verifyjProperty.Value);
                }
            }
            else if (expectedJToken.Type == JTokenType.Array && verifyJToken.Type == JTokenType.Array)
            {
                if (expectedJToken.Children().Any() && verifyJToken.Children().Any())
                {
                    int count = expectedJToken.Count();
                    Assert.AreEqual(count, verifyJToken.Count());
                    for (int i = 0; i < count; i++)
                    {
                        await ValidateCipherText(
                            expectedJToken[i],
                            verifyJToken[i]);
                    }
                }
            }
            else
            {
                if(expectedJToken.ToString().Equals(verifyJToken.ToString()) && expectedJToken.Type != JTokenType.Null && verifyJToken.Type != JTokenType.Null)
                {
                    Assert.Fail("The cipher text should be different.");
                }
            }
        }

        private static async Task<ItemResponse<TestDoc>> MdeCreateItemAsync(
            Container container,
            string partitionKey = null)
        {
            TestDoc testDoc = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> createResponse = await container.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));           

            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            VerifyExpectedDocResponse(testDoc, createResponse.Resource);
            return createResponse;
        }

        private static void VerifyExpectedDocResponse(TestDoc verifyDoc, TestDoc expectedDoc)
        {
            Assert.AreEqual(verifyDoc.Id, expectedDoc.Id);
            if (verifyDoc.Sensitive_StringFormat != null)
            {
                Assert.AreEqual(verifyDoc.Sensitive_StringFormat, expectedDoc.Sensitive_StringFormat);
            }

            if (verifyDoc.Sensitive_ArrayFormat != null)
            {
                Assert.AreEqual(verifyDoc.Sensitive_ArrayFormat[0].Sensitive_ArrayDecimalFormat, expectedDoc.Sensitive_ArrayFormat[0].Sensitive_ArrayDecimalFormat);
                Assert.AreEqual(verifyDoc.Sensitive_ArrayFormat[0].Sensitive_ArrayIntFormat, expectedDoc.Sensitive_ArrayFormat[0].Sensitive_ArrayIntFormat);               
            }            

            if (verifyDoc.Sensitive_IntArray != null)
            {
                for(int i = 0; i< verifyDoc.Sensitive_IntArray.Length; i++ )
                Assert.AreEqual(verifyDoc.Sensitive_IntArray[i], expectedDoc.Sensitive_IntArray[i]);
            }            

            if (verifyDoc.Sensitive_IntMultiDimArray != null)
            {
                for (int i = 0; i < verifyDoc.Sensitive_IntMultiDimArray.GetLength(0); i++)
                {
                    for (int j = 0; j < verifyDoc.Sensitive_IntMultiDimArray.GetLength(1); j++)
                    {
                        Assert.AreEqual(verifyDoc.Sensitive_IntMultiDimArray[i, j], expectedDoc.Sensitive_IntMultiDimArray[i, j]);
                    }
                }
            }

            if(verifyDoc.Sensitive_ObjectArrayType != null)
            {
                TestDoc.Sensitive_ArrayData expectedValue = verifyDoc.Sensitive_ObjectArrayType[0] is JObject jObjectValue
                    ? jObjectValue.ToObject<TestDoc.Sensitive_ArrayData>()
                    : (TestDoc.Sensitive_ArrayData)verifyDoc.Sensitive_ObjectArrayType[0];
                jObjectValue = (JObject)expectedDoc.Sensitive_ObjectArrayType[0];
                TestDoc.Sensitive_ArrayData test = jObjectValue.ToObject<TestDoc.Sensitive_ArrayData>();

                Assert.AreEqual(expectedValue.Sensitive_ArrayDecimalFormat, test.Sensitive_ArrayDecimalFormat);
                Assert.AreEqual(expectedValue.Sensitive_ArrayIntFormat, test.Sensitive_ArrayIntFormat);

                Assert.AreEqual(verifyDoc.Sensitive_ObjectArrayType[1], expectedDoc.Sensitive_ObjectArrayType[1]);
            }

            if (verifyDoc.Sensitive_ArrayMultiTypes != null)
            {
                for (int i = 0; i < verifyDoc.Sensitive_ArrayMultiTypes.GetLength(0); i++)
                {
                    for (int j = 0; j < verifyDoc.Sensitive_ArrayMultiTypes.GetLength(1); j++)
                    {
                        Assert.AreEqual(
                        verifyDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_NestedObjectFormatL0.Sensitive_DecimalFormatL0,
                        expectedDoc.Sensitive_ArrayMultiTypes[i, j].Sensitive_NestedObjectFormatL0.Sensitive_DecimalFormatL0);
                        Assert.AreEqual(
                            verifyDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_NestedObjectFormatL0.Sensitive_IntFormatL0,
                            expectedDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_NestedObjectFormatL0.Sensitive_IntFormatL0);

                        for (int l = 0; l < verifyDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_StringArrayMultiType.Length; l++)
                        {
                            Assert.AreEqual(verifyDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_StringArrayMultiType[l],
                                expectedDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_StringArrayMultiType[l]);
                        }

                        Assert.AreEqual(verifyDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_ArrayMultiTypeDecimalFormat,
                            expectedDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_ArrayMultiTypeDecimalFormat);

                        for (int k = 0; k < verifyDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_IntArrayMultiType.Length; k++)
                        {
                            Assert.AreEqual(verifyDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_IntArrayMultiType[k],
                                expectedDoc.Sensitive_ArrayMultiTypes[i,j].Sensitive_IntArrayMultiType[k]);
                        }
                    }
                }
            }

            if (verifyDoc.Sensitive_NestedObjectFormatL1 != null)
            {
                Assert.AreEqual(verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_IntFormatL1, expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_IntFormatL1);

                if (verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2 == null)
                {
                    Assert.IsNull(expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2);
                }
                else
                {
                    Assert.AreEqual(
                        verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_StringFormatL2,
                        expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_StringFormatL2);

                    Assert.AreEqual(
                        verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_IntFormatL3,
                        expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_IntFormatL3);

                    Assert.AreEqual(
                       verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_DecimalFormatL3,
                       expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_DecimalFormatL3);

                    Assert.AreEqual(
                       verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayFormatL3[0].Sensitive_ArrayIntFormat,
                       expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayFormatL3[0].Sensitive_ArrayIntFormat);

                    Assert.AreEqual(
                       verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayFormatL3[0].Sensitive_ArrayDecimalFormat,
                       expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayFormatL3[0].Sensitive_ArrayDecimalFormat);

                    Assert.AreEqual(
                       verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_ArrayDecimalFormat,
                       expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_ArrayDecimalFormat);

                    Assert.AreEqual(
                       verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_ArrayIntFormat,
                       expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_ArrayIntFormat);

                    Assert.AreEqual(
                      verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_NestedObjectFormatL0.Sensitive_IntFormatL0,
                      expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_NestedObjectFormatL0.Sensitive_IntFormatL0);

                    Assert.AreEqual(
                      verifyDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_NestedObjectFormatL0.Sensitive_DecimalFormatL0,
                      expectedDoc.Sensitive_NestedObjectFormatL1.Sensitive_NestedObjectFormatL2.Sensitive_NestedObjectFormatL3.Sensitive_ArrayWithObjectFormat[0].Sensitive_NestedObjectFormatL0.Sensitive_DecimalFormatL0);
                }
            }

            if (verifyDoc.Sensitive_DateFormat != new DateTime())
            {
                Assert.AreEqual(verifyDoc.Sensitive_DateFormat, expectedDoc.Sensitive_DateFormat);
            }

            if (verifyDoc.Sensitive_DecimalFormat != 0)
            {
                Assert.AreEqual(verifyDoc.Sensitive_DecimalFormat, expectedDoc.Sensitive_DecimalFormat);
            }

            if (verifyDoc.Sensitive_IntFormat != 0)
            {
                Assert.AreEqual(verifyDoc.Sensitive_IntFormat, expectedDoc.Sensitive_IntFormat);
            }

            if (verifyDoc.Sensitive_FloatFormat != 0)
            {
                Assert.AreEqual(verifyDoc.Sensitive_FloatFormat, expectedDoc.Sensitive_FloatFormat);
            }

            Assert.AreEqual(verifyDoc.NonSensitive, expectedDoc.NonSensitive);
            Assert.AreEqual(verifyDoc.NonSensitiveInt, expectedDoc.NonSensitiveInt);
        }        
    }
}

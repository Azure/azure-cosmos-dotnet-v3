//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.ComparableTask;
    using Microsoft.Azure.Cosmos.Rntbd;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;


    [TestClass]
    public class ComparableTaskSchedulerTests
    {
        [TestMethod]
        public void TransportSerializationTest()
        {
            List<string> notSupportedHeaders = new List<string>();
            // These properties are excluded because they come from DocumentServiceRequest fields that are not in Headers or Properties bag
            HashSet<string> excluededRntdbProperties = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                nameof(RntbdConstants.Request.resourceId),
                nameof(RntbdConstants.Request.payloadPresent),
                nameof(RntbdConstants.Request.entityId),
                nameof(RntbdConstants.Request.replicaPath),
                nameof(RntbdConstants.Request.databaseName),
                nameof(RntbdConstants.Request.snapshotName),
                nameof(RntbdConstants.Request.roleDefinitionName),
                nameof(RntbdConstants.Request.roleAssignmentName),
                nameof(RntbdConstants.Request.collectionName),
                nameof(RntbdConstants.Request.documentName),
                nameof(RntbdConstants.Request.attachmentName),
                nameof(RntbdConstants.Request.userName),
                nameof(RntbdConstants.Request.userDefinedFunctionName),
                nameof(RntbdConstants.Request.storedProcedureName),
                nameof(RntbdConstants.Request.triggerName),
                nameof(RntbdConstants.Request.conflictName),
                nameof(RntbdConstants.Request.permissionName),
                nameof(RntbdConstants.Request.clientEncryptionKeyName),
                nameof(RntbdConstants.Request.partitionKeyRangeName),
                nameof(RntbdConstants.Request.mergeCheckpointGlsnKeyName),
                nameof(RntbdConstants.Request.schemaName),
                nameof(RntbdConstants.Request.systemDocumentName),
                nameof(RntbdConstants.Request.userDefinedTypeName),
                nameof(RntbdConstants.Request.isAutoScaleRequest),
                nameof(RntbdConstants.Request.schemaHash), // need to investigate. FillTokens doesn't handle bytes
                nameof(RntbdConstants.Request.collectionChildResourceNameLimitInBytes), // Value set to long but Rntbd token type is bytes
                nameof(RntbdConstants.Request.collectionChildResourceContentLengthLimitInKB), // Value set to long but Rntbd token type is bytes
            };

            FieldInfo[] allProperties = typeof(RntbdConstants.Request).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(info => !excluededRntdbProperties.Contains(info.Name)).ToArray();

            Assert.IsTrue(allProperties.Length > 20);

            FieldInfo[] allHeaderConstants = typeof(HttpConstants.HttpHeaders).GetFields(BindingFlags.Public | BindingFlags.Static);
            Assert.IsTrue(allHeaderConstants.Length > 20);
            Dictionary<string, string> mapRntbdFieldToHeaderKey = allHeaderConstants.ToDictionary(item => item.Name, item => (string)item.GetValue(null), StringComparer.OrdinalIgnoreCase);
            // Match custom rntbd field names to the http headers.
            mapRntbdFieldToHeaderKey["AuthorizationToken"] = HttpConstants.HttpHeaders.Authorization;
            mapRntbdFieldToHeaderKey["Date"] = HttpConstants.HttpHeaders.XDate;
            mapRntbdFieldToHeaderKey["ContinuationToken"] = HttpConstants.HttpHeaders.Continuation;
            mapRntbdFieldToHeaderKey["Match"] = HttpConstants.HttpHeaders.IfNoneMatch;
            mapRntbdFieldToHeaderKey["IsFanout"] = WFConstants.BackendHeaders.IsFanoutRequest;
            mapRntbdFieldToHeaderKey["ClientVersion"] = HttpConstants.HttpHeaders.Version;
            mapRntbdFieldToHeaderKey["filterBySchemaRid"] = HttpConstants.HttpHeaders.FilterBySchemaResourceId;
            mapRntbdFieldToHeaderKey["collectionChildResourceContentLengthLimitInKB"] = WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB;
            mapRntbdFieldToHeaderKey["returnPreference"] = HttpConstants.HttpHeaders.Prefer;

            FieldInfo[] allBackendHeaderConstants = typeof(WFConstants.BackendHeaders).GetFields(BindingFlags.Public | BindingFlags.Static);
            Assert.IsTrue(allBackendHeaderConstants.Length > 20);
            Dictionary<string, string> backendHeaderPropertyNameToValue = allBackendHeaderConstants.ToDictionary(item => item.Name, item => (string)item.GetValue(null), StringComparer.OrdinalIgnoreCase);

            RntbdConstants.Request request = new RntbdConstants.Request();
            StoreResponseNameValueCollection dsrHeaders = new StoreResponseNameValueCollection();
            Dictionary<string, object> dsrProperties = new Dictionary<string, object>();
            DocumentServiceRequest documentServiceRequest = new DocumentServiceRequest(
               operationType: OperationType.Read,
               resourceIdOrFullName: @"dbs\test\colls\a1\docs\notFound",
               resourceType: ResourceType.Document,
               body: null,
               headers: dsrHeaders,
               isNameBased: true,
               authorizationTokenType: Documents.AuthorizationTokenType.PrimaryMasterKey)
            {
                Properties = dsrProperties,
            };

            Random random = new Random();

            Dictionary<string, Action<DocumentServiceRequest, RntbdConstants.Request, RntbdToken>> customHeaderHandling = new Dictionary<string, Action<DocumentServiceRequest, RntbdConstants.Request, RntbdToken>>();
            customHeaderHandling[HttpConstants.HttpHeaders.SystemDocumentType] = (dsr, rntbdRequest, token) =>
            {
                this.EnumToByteHelper(
                    HttpConstants.HttpHeaders.SystemDocumentType,
                    ((SystemDocumentType)0x01).ToString(),
                     0x01,
                     dsr,
                     rntbdRequest,
                     token);
            };

            customHeaderHandling[WFConstants.BackendHeaders.UniqueIndexReIndexingState] = (dsr, rntbdRequest, token) =>
            {
                this.EnumToByteHelper(
                     WFConstants.BackendHeaders.UniqueIndexReIndexingState,
                     "1",
                     0x01,
                     dsr,
                     rntbdRequest,
                     token);
            };

            customHeaderHandling[WFConstants.BackendHeaders.UniqueIndexNameEncodingMode] = (dsr, rntbdRequest, token) =>
            {
                this.EnumToByteHelper(
                     WFConstants.BackendHeaders.UniqueIndexNameEncodingMode,
                     "1",
                     0x01,
                     dsr,
                     rntbdRequest,
                     token);
            };

            customHeaderHandling[HttpConstants.HttpHeaders.Prefer] = (dsr, rntbdRequest, token) =>
            {
                this.EnumToByteHelper(
                    HttpConstants.HttpHeaders.Prefer,
                    HttpConstants.HttpHeaderValues.PreferReturnMinimal,
                     0x01,
                     dsr,
                     rntbdRequest,
                     token);
            };

            customHeaderHandling[HttpConstants.HttpHeaders.ContentSerializationFormat] = (dsr, rntbdRequest, token) =>
            {
                this.EnumToByteHelper(
                    HttpConstants.HttpHeaders.ContentSerializationFormat,
                    ((ContentSerializationFormat)0x01).ToString(),
                     0x01,
                     dsr,
                     rntbdRequest,
                     token);
            };

            customHeaderHandling[HttpConstants.HttpHeaders.ConsistencyLevel] = (dsr, rntbdRequest, token) =>
            {
                this.EnumToByteHelper(
                    HttpConstants.HttpHeaders.ConsistencyLevel,
                    ((ConsistencyLevel)0x01).ToString(),
                     0x01,
                     dsr,
                     rntbdRequest,
                     token);
            };

            customHeaderHandling[HttpConstants.HttpHeaders.MigrateCollectionDirective] = (dsr, rntbdRequest, token) =>
            {
                this.EnumToByteHelper(
                    HttpConstants.HttpHeaders.MigrateCollectionDirective,
                    ((MigrateCollectionDirective)0x01).ToString(),
                     0x01,
                     dsr,
                     rntbdRequest,
                     token);
            };

            customHeaderHandling[WFConstants.BackendHeaders.RemoteStorageType] = (dsr, rntbdRequest, token) =>
            {
                this.EnumToByteHelper(
                    WFConstants.BackendHeaders.RemoteStorageType,
                    ((RemoteStorageType)0x01).ToString(),
                     0x02,
                     dsr,
                     rntbdRequest,
                     token);
            };

            customHeaderHandling[WFConstants.BackendHeaders.FanoutOperationState] = (dsr, rntbdRequest, token) =>
            {
                this.EnumToByteHelper(
                    WFConstants.BackendHeaders.FanoutOperationState,
                    ((FanoutOperationState)0x01).ToString(),
                     0x02,
                     dsr,
                     rntbdRequest,
                     token);
            };

            customHeaderHandling[HttpConstants.HttpHeaders.IndexingDirective] = (dsr, rntbdRequest, token) =>
            {
                this.EnumToByteHelper(
                    HttpConstants.HttpHeaders.IndexingDirective,
                    ((IndexingDirective)0x01).ToString(),
                     0x01,
                     dsr,
                     rntbdRequest,
                     token);
            };

            customHeaderHandling[HttpConstants.HttpHeaders.EnumerationDirection] = (dsr, rntbdRequest, token) =>
            {
                dsr.Headers[HttpConstants.HttpHeaders.EnumerationDirection] = ((EnumerationDirection)0x01).ToString();
                dsr.Properties[HttpConstants.HttpHeaders.EnumerationDirection] = (byte)0x02;
                this.ValidateRntbdProperty(
                    HttpConstants.HttpHeaders.EnumerationDirection,
                    documentServiceRequest,
                    request,
                    token,
                    () => Assert.AreEqual((byte)0x02, token.value.valueByte));
            };

            customHeaderHandling[HttpConstants.HttpHeaders.StartId] = (dsr, rntbdRequest, token) => { };
            customHeaderHandling[HttpConstants.HttpHeaders.EndId] = (dsr, rntbdRequest, token) => { };
            customHeaderHandling[HttpConstants.HttpHeaders.StartEpk] = (dsr, rntbdRequest, token) => { };
            customHeaderHandling[HttpConstants.HttpHeaders.EndEpk] = (dsr, rntbdRequest, token) => { };
            customHeaderHandling[HttpConstants.HttpHeaders.ReadFeedKeyType] = (dsr, rntbdRequest, token) =>
            {
                ReadOnlyMemory<byte> startEpk = new byte[] { (byte)0x03 };
                ReadOnlyMemory<byte> endEpk = new byte[] { (byte)0x04 };
                string startEpkString = Convert.ToBase64String(startEpk.Span);
                string endEpkString = Convert.ToBase64String(endEpk.Span);

                // check the headers
                dsr.Headers[HttpConstants.HttpHeaders.ReadFeedKeyType] = ((ReadFeedKeyType)0x01).ToString();
                dsr.Headers[HttpConstants.HttpHeaders.StartEpk] = startEpkString;
                dsr.Headers[HttpConstants.HttpHeaders.EndEpk] = endEpkString;
                Assert.IsTrue(TransportSerialization.AddHeaders.ContainsKey(HttpConstants.HttpHeaders.ReadFeedKeyType), HttpConstants.HttpHeaders.ReadFeedKeyType);
                TransportSerialization.AddHeaders[HttpConstants.HttpHeaders.ReadFeedKeyType](documentServiceRequest.Headers[HttpConstants.HttpHeaders.ReadFeedKeyType], documentServiceRequest, request);

                Assert.IsTrue(rntbdRequest.readFeedKeyType.isPresent, HttpConstants.HttpHeaders.ReadFeedKeyType);
                Assert.AreEqual((byte)0x02, token.value.valueByte);

                Assert.IsTrue(rntbdRequest.StartEpk.isPresent, HttpConstants.HttpHeaders.StartEpk);
                Assert.IsTrue(startEpk.Span.SequenceEqual(rntbdRequest.StartEpk.value.valueBytes.Span));

                Assert.IsTrue(rntbdRequest.EndEpk.isPresent, HttpConstants.HttpHeaders.EndEpk);
                Assert.IsTrue(endEpk.Span.SequenceEqual(rntbdRequest.EndEpk.value.valueBytes.Span));

                // Reset the rntbd value and remove the header to verify property value works
                rntbdRequest.readFeedKeyType.isPresent = false;
                rntbdRequest.readFeedKeyType.value = new RntbdTokenValue();
                documentServiceRequest.Headers.Remove(HttpConstants.HttpHeaders.ReadFeedKeyType);

                rntbdRequest.StartEpk.isPresent = false;
                rntbdRequest.StartEpk.value = new RntbdTokenValue();
                documentServiceRequest.Headers.Remove(HttpConstants.HttpHeaders.StartEpk);

                rntbdRequest.EndEpk.isPresent = false;
                rntbdRequest.EndEpk.value = new RntbdTokenValue();
                documentServiceRequest.Headers.Remove(HttpConstants.HttpHeaders.EndEpk);

                // check the properties
                dsr.Properties[HttpConstants.HttpHeaders.ReadFeedKeyType] = (byte)0x02;
                dsr.Properties[HttpConstants.HttpHeaders.StartEpk] = startEpk.ToArray();
                dsr.Properties[HttpConstants.HttpHeaders.EndEpk] = endEpk.ToArray();

                TransportSerialization.AddHeaders[HttpConstants.HttpHeaders.ReadFeedKeyType](documentServiceRequest.Properties[HttpConstants.HttpHeaders.ReadFeedKeyType], documentServiceRequest, request);

                Assert.IsTrue(rntbdRequest.readFeedKeyType.isPresent, HttpConstants.HttpHeaders.ReadFeedKeyType);
                Assert.AreEqual((byte)0x02, token.value.valueByte);

                Assert.IsTrue(rntbdRequest.StartEpk.isPresent, HttpConstants.HttpHeaders.StartEpk);
                Assert.IsTrue(startEpk.Span.SequenceEqual(rntbdRequest.StartEpk.value.valueBytes.Span));

                Assert.IsTrue(rntbdRequest.EndEpk.isPresent, HttpConstants.HttpHeaders.EndEpk);
                Assert.IsTrue(endEpk.Span.SequenceEqual(rntbdRequest.EndEpk.value.valueBytes.Span));
            };

            customHeaderHandling[WFConstants.BackendHeaders.BinaryId] = (dsr, rntbdRequest, token) =>
            {
               this.PropertiesBytesHelper(WFConstants.BackendHeaders.BinaryId, dsr, rntbdRequest, token);
            };

            customHeaderHandling[WFConstants.BackendHeaders.MergeStaticId] = (dsr, rntbdRequest, token) =>
            {
               this.PropertiesBytesHelper(WFConstants.BackendHeaders.MergeStaticId, dsr, rntbdRequest, token);
            };

            customHeaderHandling[WFConstants.BackendHeaders.EffectivePartitionKey] = (dsr, rntbdRequest, token) =>
            {
               this.PropertiesBytesHelper(WFConstants.BackendHeaders.EffectivePartitionKey, dsr, rntbdRequest, token);
            };

            customHeaderHandling[WFConstants.BackendHeaders.TransactionFirstRequest] = (dsr, rntbdRequest, token) => { };
            customHeaderHandling[WFConstants.BackendHeaders.TransactionId] = (dsr, rntbdRequest, token) =>
            {
               
                documentServiceRequest.Properties[WFConstants.BackendHeaders.TransactionFirstRequest] = true;

                this.PropertiesBytesHelper(WFConstants.BackendHeaders.TransactionId, dsr, rntbdRequest, token);

                Assert.IsTrue(rntbdRequest.transactionFirstRequest.isPresent);
                Assert.AreEqual((byte)0x01, rntbdRequest.transactionFirstRequest.value.valueByte);
            };

            customHeaderHandling[WFConstants.BackendHeaders.TransactionCommit] = (dsr, rntbdRequest, token) =>
            {
                string headerKey = WFConstants.BackendHeaders.TransactionCommit;
                documentServiceRequest.Properties[headerKey] = true;

                TransportSerialization.AddHeaders[headerKey](documentServiceRequest.Properties[headerKey], documentServiceRequest, request);

                Assert.IsTrue(rntbdRequest.transactionCommit.isPresent);
                Assert.AreEqual((byte)0x01, rntbdRequest.transactionCommit.value.valueByte);
            };

            customHeaderHandling[WFConstants.BackendHeaders.RetriableWriteRequestId] = (dsr, rntbdRequest, token) =>
            {
                this.PropertiesBytesHelper(WFConstants.BackendHeaders.RetriableWriteRequestId, dsr, rntbdRequest, token);
            };

            customHeaderHandling[WFConstants.BackendHeaders.RetriableWriteRequestStartTimestamp] = (dsr, rntbdRequest, token) =>
            {
                string headerKey = WFConstants.BackendHeaders.RetriableWriteRequestStartTimestamp;
                UInt64 value = 1234859822589;
                documentServiceRequest.Properties[headerKey] = value;

                TransportSerialization.AddHeaders[headerKey](documentServiceRequest.Properties[headerKey], documentServiceRequest, request);
                Assert.IsTrue(rntbdRequest.retriableWriteRequestStartTimestamp.isPresent);
                Assert.AreEqual(value, rntbdRequest.retriableWriteRequestStartTimestamp.value.valueULongLong);
            };


            foreach (FieldInfo propertyInfo in allProperties)
            {
                if (!mapRntbdFieldToHeaderKey.TryGetValue(propertyInfo.Name, out string headerKey) &&
                    !backendHeaderPropertyNameToValue.TryGetValue(propertyInfo.Name, out headerKey))
                {
                    Assert.Fail($"{propertyInfo.Name}; could not find a matching header constant");
                }

                RntbdToken rntdbToken = (RntbdToken)propertyInfo.GetValue(request);

                if (customHeaderHandling.ContainsKey(headerKey))
                {
                    customHeaderHandling[headerKey](documentServiceRequest, request, rntdbToken);
                    continue;
                }

                switch (rntdbToken.GetTokenType())
                {
                    case RntbdTokenTypes.SmallString:
                    case RntbdTokenTypes.String:
                    case RntbdTokenTypes.ULongString:
                        string headerValue = propertyInfo.Name;
                        dsrHeaders[headerKey] = headerValue;
                        dsrProperties[headerKey] = dsrHeaders[headerKey];
                        this.ValidateRntbdProperty(
                            headerKey,
                            documentServiceRequest,
                            request,
                            rntdbToken,
                            () => Assert.AreEqual(headerValue, BytesSerializer.GetStringFromBytes(rntdbToken.value.valueBytes)));

                        break;
                    case RntbdTokenTypes.ULong:
                        uint ulongHeaderValue = (uint)random.Next(1, 99999999);
                        dsrHeaders[headerKey] = ulongHeaderValue.ToString(CultureInfo.InvariantCulture);
                        dsrProperties[headerKey] = dsrHeaders[headerKey];
                        this.ValidateRntbdProperty(
                            headerKey,
                            documentServiceRequest,
                            request,
                            rntdbToken,
                            () => Assert.AreEqual(ulongHeaderValue, rntdbToken.value.valueULong));
                        break;
                    case RntbdTokenTypes.Long:
                        long longHeaderValue = random.Next(1, 99999999);
                        dsrHeaders[headerKey] = longHeaderValue.ToString(CultureInfo.InvariantCulture);
                        dsrProperties[headerKey] = dsrHeaders[headerKey];
                        this.ValidateRntbdProperty(
                            headerKey,
                            documentServiceRequest,
                            request,
                            rntdbToken,
                            () => Assert.AreEqual(longHeaderValue, rntdbToken.value.valueLong));
                        break;
                    case RntbdTokenTypes.LongLong:
                        long longlongHeaderValue = random.Next(1, 99999999);
                        dsrHeaders[headerKey] = longlongHeaderValue.ToString(CultureInfo.InvariantCulture);
                        dsrProperties[headerKey] = dsrHeaders[headerKey];
                        this.ValidateRntbdProperty(
                            headerKey,
                            documentServiceRequest,
                            request,
                            rntdbToken,
                            () => Assert.AreEqual(longlongHeaderValue, rntdbToken.value.valueLongLong));
                        break;
                    case RntbdTokenTypes.Double:
                        double doubleHeaderValue = random.NextDouble();
                        dsrHeaders[headerKey] = doubleHeaderValue.ToString(CultureInfo.InvariantCulture);
                        dsrProperties[headerKey] = dsrHeaders[headerKey];
                        this.ValidateRntbdProperty(
                            headerKey,
                            documentServiceRequest,
                            request,
                            rntdbToken,
                            () => Assert.AreEqual(doubleHeaderValue, rntdbToken.value.valueDouble));
                        break;
                    case RntbdTokenTypes.Byte:
                        string byteHeaderValue = bool.TrueString;
                        byte expectedValue = 0x01;

                        dsrHeaders[headerKey] = byteHeaderValue;
                        dsrProperties[headerKey] = dsrHeaders[headerKey];

                        this.ValidateRntbdProperty(
                            headerKey,
                            documentServiceRequest,
                            request,
                            rntdbToken,
                            () => Assert.AreEqual(expectedValue, rntdbToken.value.valueByte));
                        break;
                    case RntbdTokenTypes.Bytes:
                        string originalString = propertyInfo.Name.ToString(CultureInfo.InvariantCulture);
                        byte[] byteArray = Encoding.UTF8.GetBytes(originalString);
                        string base64String = Convert.ToBase64String(byteArray);
                        dsrHeaders[headerKey] = base64String;
                        dsrProperties[headerKey] = dsrHeaders[headerKey];

                        this.ValidateRntbdProperty(
                            headerKey,
                            documentServiceRequest,
                            request,
                            rntdbToken,
                            () => Assert.AreEqual(originalString, Encoding.UTF8.GetString(rntdbToken.value.valueBytes.Span)));
                        break;
                    default:
                        throw new Exception($"{headerKey} Token type not expected");
                }
            }
        }

        private void ValidateRntbdProperty(
            string headerKey,
            DocumentServiceRequest documentServiceRequest,
            RntbdConstants.Request request,
            RntbdToken rntbdToken,
            Action validateValues)
        {
            Assert.IsTrue(TransportSerialization.AddHeaders.ContainsKey(headerKey), headerKey);
            TransportSerialization.AddHeaders[headerKey](documentServiceRequest.Headers[headerKey], documentServiceRequest, request);
            Assert.IsTrue(rntbdToken.isPresent, headerKey);
            validateValues();

            // Reset the rntbd value and remove the header to verify property value works
            rntbdToken.isPresent = false;
            rntbdToken.value = new RntbdTokenValue();
            documentServiceRequest.Headers.Remove(headerKey);

            TransportSerialization.AddHeaders[headerKey](documentServiceRequest.Properties[headerKey], documentServiceRequest, request);
            Assert.IsTrue(rntbdToken.isPresent, headerKey);
            validateValues();
        }

        private void PropertiesBytesHelper(
            string headerKey,
            DocumentServiceRequest documentServiceRequest,
            RntbdConstants.Request request,
            RntbdToken rntbdToken)
        {
            Random random = new Random();
            // Only dictionary is handled
            byte[] binaryValue = new byte[5];
            random.NextBytes(binaryValue);

            documentServiceRequest.Properties[headerKey] = binaryValue;
            TransportSerialization.AddHeaders[headerKey](documentServiceRequest.Properties[headerKey], documentServiceRequest, request);

            Assert.IsTrue(rntbdToken.isPresent, headerKey);
            Assert.IsTrue(((ReadOnlySpan<byte>)binaryValue).SequenceEqual(rntbdToken.value.valueBytes.Span));
        }

        private void EnumToByteHelper(
            string headerKey,
            string headerValue,
            byte expectedValue,
            DocumentServiceRequest documentServiceRequest,
            RntbdConstants.Request request,
            RntbdToken rntbdToken)
        {
            documentServiceRequest.Headers[headerKey] = headerValue;
            documentServiceRequest.Properties[headerKey] = headerValue;

            this.ValidateRntbdProperty(
               headerKey,
               documentServiceRequest,
               request,
               rntbdToken,
               () => Assert.AreEqual(expectedValue, rntbdToken.value.valueByte));
        }

        [TestMethod]
        public async Task SimpleTestAsync()
        {
            foreach (bool useConstructorToAddTasks in new[] { true, false })
            {
                List<Task> tasks = new List<Task>();
                int maximumConcurrencyLevel = 10;

                for (int i = 0; i < maximumConcurrencyLevel; ++i)
                {
                    tasks.Add(new Task(() => { }));
                }

                await Task.Delay(1);

                foreach (Task task in tasks)
                {
                    Assert.AreEqual(false, task.IsCompleted);
                }

                ComparableTaskScheduler scheduler;
                if (useConstructorToAddTasks)
                {
                    scheduler = new ComparableTaskScheduler(
                        tasks.Select(task => new TestComparableTask(tasks.IndexOf(task), task)),
                        maximumConcurrencyLevel);
                }
                else
                {
                    scheduler = new ComparableTaskScheduler(maximumConcurrencyLevel);
                    for (int i = 0; i < maximumConcurrencyLevel; ++i)
                    {
                        Assert.AreEqual(true, scheduler.TryQueueTask(new TestComparableTask(i, tasks[i])));
                    }
                }

                bool completionStatus = Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(10));
                Assert.IsTrue(completionStatus);

                foreach (Task task in tasks)
                {
                    Assert.AreEqual(true, task.IsCompleted, $"Is overloaded constructor {useConstructorToAddTasks} and status {task.Status.ToString()}");
                }
            }
        }

        [TestMethod]
        public void TestMaximumConcurrencyLevel()
        {
            ComparableTaskScheduler scheduler = new ComparableTaskScheduler(10);
            Assert.AreEqual(10, scheduler.MaximumConcurrencyLevel);

            scheduler = new ComparableTaskScheduler();
            Assert.AreEqual(Environment.ProcessorCount, scheduler.MaximumConcurrencyLevel);

            scheduler.IncreaseMaximumConcurrencyLevel(1);
            Assert.AreEqual(Environment.ProcessorCount + 1, scheduler.MaximumConcurrencyLevel);

            try
            {
                scheduler.IncreaseMaximumConcurrencyLevel(-1);
                Assert.Fail("Expect ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        [TestMethod]
        public void TestStop()
        {
            ComparableTaskScheduler scheduler = new ComparableTaskScheduler();
            Assert.AreEqual(true, scheduler.TryQueueTask(new TestComparableTask(0, Task.FromResult(false))));
            scheduler.Stop();
            Assert.AreEqual(false, scheduler.TryQueueTask(new TestComparableTask(0, Task.FromResult(false))));
        }


        [TestMethod]
        public async Task TestDelayedQueueTaskAsync()
        {
            ComparableTaskScheduler scheduler = new ComparableTaskScheduler();

            Task task = new Task(() =>
            {
                Assert.AreEqual(1, scheduler.CurrentRunningTaskCount);
            });

            Task delayedTask = new Task(() =>
            {
                Assert.AreEqual(1, scheduler.CurrentRunningTaskCount);
            });

            Assert.AreEqual(
                true,
                scheduler.TryQueueTask(new TestComparableTask(schedulePriority: 0, delayedTask), TimeSpan.FromMilliseconds(200)));
            Assert.AreEqual(
                false,
                scheduler.TryQueueTask(new TestComparableTask(schedulePriority: 0, delayedTask), TimeSpan.FromMilliseconds(200)));
            Assert.AreEqual(
                false,
                scheduler.TryQueueTask(new TestComparableTask(schedulePriority: 0, task)));
            Assert.AreEqual(
                true,
                scheduler.TryQueueTask(new TestComparableTask(schedulePriority: 1, task)));

            await Task.Delay(150);

            Assert.AreEqual(true, task.IsCompleted);
            Assert.AreEqual(false, delayedTask.IsCompleted);
            Assert.AreEqual(0, scheduler.CurrentRunningTaskCount);

            await Task.Delay(400);

            Assert.AreEqual(true, delayedTask.IsCompleted);
        }

        private sealed class TestComparableTask : ComparableTask
        {
            private readonly Task task;
            public TestComparableTask(int schedulePriority, Task task) :
                base(schedulePriority)
            {
                this.task = task;
            }

            public override Task StartAsync(CancellationToken token)
            {
                try
                {
                    this.task.Start();
                }
                catch (InvalidOperationException)
                {
                }

                return this.task;
            }

            public override int GetHashCode()
            {
                return this.schedulePriority;
            }

            public override bool Equals(IComparableTask other)
            {
                return this.CompareTo(other) == 0;
            }
        }
    }
}
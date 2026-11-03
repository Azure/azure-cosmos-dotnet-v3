//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Rntbd;
    using Microsoft.Azure.Cosmos.Test;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Documents.RntbdConstants;

    [TestClass]
    public class TransportSerializationTests
    {
        private readonly BufferProvider bufferProvider = new BufferProvider();

        [TestMethod]
        [Owner("kirankk")]
        public void SerializeAccountNameDefault()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsFalse(rntbdRequest.globalDatabaseAccountName.isPresent, $"Unexpected {nameof(RntbdConstants.Request.globalDatabaseAccountName)} is present");
        }

        [TestMethod]
        [Owner("cosmosdbsecurity")]
        public void MutualTlsAuthModeIsSent()
        {
            // serialize
            byte[] contextRequestSerialized = TransportSerialization.BuildContextRequest(
                Guid.NewGuid(),
                new UserAgentContainer(),
                CallerId.Gateway,
                enableChannelMultiplexing: true,
                mutualTlsAuthMode: RntbdMutualTlsAuthMode.System);

            byte[] contextRequestWithoutAuthModeSerialized = TransportSerialization.BuildContextRequest(
                Guid.NewGuid(),
                new UserAgentContainer(),
                CallerId.Gateway,
                enableChannelMultiplexing: true,
                mutualTlsAuthMode: null);

            CollectionAssert.AreNotEquivalent(contextRequestSerialized, contextRequestWithoutAuthModeSerialized);

            const int headerSize = 24;

            // deserialize
            RntbdConstants.ConnectionContextRequest contextRequest = new();
            BytesDeserializer reader = new BytesDeserializer(
                contextRequestSerialized.Skip(headerSize).ToArray(),
                contextRequestSerialized.Length - headerSize);
            contextRequest.ParseFrom(ref reader);

            Assert.IsTrue(contextRequest.mutualTlsAuthMode.isPresent);
            Assert.AreEqual((byte)RntbdMutualTlsAuthMode.System, contextRequest.mutualTlsAuthMode.value.valueByte);

            RntbdConstants.ConnectionContextRequest contextRequestWithoutAuthMode = new();
            reader = new BytesDeserializer(
                contextRequestWithoutAuthModeSerialized.Skip(headerSize).ToArray(),
                contextRequestWithoutAuthModeSerialized.Length - headerSize);
            contextRequestWithoutAuthMode.ParseFrom(ref reader);

            Assert.IsFalse(contextRequestWithoutAuthMode.mutualTlsAuthMode.isPresent);
        }

        [TestMethod]
        [Owner("kirankk")]
        public void SerializeAccountNameForProxy()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();

            string accountName = Guid.NewGuid().ToString();

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequestForProxy(
                request,
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                accountName,
                accountName, // same as globalDatabaseAccountName for test
                out _,
                out _);

            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
            reader.ReadBytes(24);
            try
            {
                rntbdRequest.ParseFrom(ref reader);
                Assert.Fail("Should have thrown InternalServerErrorException due to ReplicaPath missing");
            }
            catch (InternalServerErrorException)
            {
                // Expect ReplicaPath missing check for Proxy, but because globalDatabaseAccountName is higher than replicaPath, the globalDatabaseAccountName is already parsed
            }

            Assert.IsTrue(rntbdRequest.globalDatabaseAccountName.isPresent, $"Expected {nameof(RntbdConstants.Request.globalDatabaseAccountName)} is absent");
            Assert.AreEqual(accountName, Encoding.ASCII.GetString(rntbdRequest.globalDatabaseAccountName.value.valueBytes.ToArray()), $"Unexpected {nameof(RntbdConstants.Request.globalDatabaseAccountName)}");
        }

        [TestMethod]
        [Owner("paulchen")]
        public void SerializeRequestWithTransactionIdTest()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            request.Properties = new Dictionary<string, object>()
            {
                { WFConstants.BackendHeaders.TransactionId, new byte[24] },
                { WFConstants.BackendHeaders.TransactionFirstRequest, true },
            };

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count); reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(rntbdRequest.transactionId.isPresent, $"Expected {nameof(RntbdConstants.Request.transactionId)} is absent");
            Assert.IsTrue(rntbdRequest.transactionFirstRequest.isPresent, $"Expected {nameof(RntbdConstants.Request.transactionFirstRequest)} is absent");
            Assert.IsFalse(rntbdRequest.transactionCommit.isPresent, $"Unexpected {nameof(RntbdConstants.Request.transactionCommit)}");
        }

        [TestMethod]
        [Owner("cosmosdbquery")] // dev maintainer: dmmelnik
        public void SerializeRequestWithTransportRequestIdTest()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            request.Properties = new Dictionary<string, object>();

            // serialize
            int transportRequestId = Environment.TickCount;

            // Set TransportRequestID on headers to a different value to validate it being overriden with TransportRequestID passed as parameter
            request.Headers.Add(HttpConstants.HttpHeaders.TransportRequestID, (transportRequestId - 1).ToString());
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                transportRequestId.ToString(),
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count); reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(rntbdRequest.transportRequestID.isPresent, $"Expected {nameof(RntbdConstants.Request.transportRequestID)} is absent");
            Assert.AreEqual(transportRequestId, (int)rntbdRequest.transportRequestID.value.valueLong, $"Unexpected {nameof(RntbdConstants.Request.transportRequestID)}");
        }

        [TestMethod]
        [Owner("harshitdirect")]
        public void SerializeRequestWithDistributedTracingHeadersTest()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            request.Properties = new Dictionary<string, object>();

            string traceParent = "00-000000000-0000-00";
            string traceState = "000000";

            request.Headers.Add(HttpConstants.HttpHeaders.TraceParent, traceParent);
            request.Headers.Add(HttpConstants.HttpHeaders.TraceState, traceState);
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(rntbdRequest.traceParent.isPresent, $"Expected {nameof(RntbdConstants.Request.traceParent)} is absent");
            Assert.IsTrue(rntbdRequest.traceState.isPresent, $"Expected {nameof(RntbdConstants.Request.traceState)} is absent");

            Assert.AreEqual(traceParent, Encoding.ASCII.GetString(rntbdRequest.traceParent.value.valueBytes.ToArray()), $"Unexpected {nameof(RntbdConstants.Request.traceParent)}");
            Assert.AreEqual(traceState, Encoding.ASCII.GetString(rntbdRequest.traceState.value.valueBytes.ToArray()), $"Unexpected {nameof(RntbdConstants.Request.traceParent)}");
        }

        [TestMethod]
        [Owner("vivekra")]
        public async Task SerializeRequestWithTransactionIdTestValidForMultipleRequestsAsync()
        {
            // initialize
            DocumentServiceRequest request = new DocumentServiceRequest(
                OperationType.Read,
                ResourceType.Document,
                "dbs/wo1ZAA==/colls/wo1ZAP7zFQA=/",
                null,
                AuthorizationTokenType.PrimaryMasterKey,
                new RequestNameValueCollection())
            {
                Properties = new Dictionary<string, object>()
            };
            RequestNameValueCollection headers = request.Headers as RequestNameValueCollection;

            // serialize
            int transportRequestId1 = Environment.TickCount;
            int transportRequestId2 = Environment.TickCount + 10;

            using TransportSerialization.SerializedRequest contextMessage1 = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Read, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                transportRequestId1.ToString(),
                out _,
                out _);
            using TransportSerialization.SerializedRequest contextMessage2 = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Read, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                transportRequestId2.ToString(),
                out _,
                out _);

            ArraySegment<byte> buffer1 = new ArraySegment<byte>(new byte[contextMessage1.RequestSize]);
            contextMessage1.CopyTo(buffer1);
            ArraySegment<byte> buffer2 = new ArraySegment<byte>(new byte[contextMessage2.RequestSize]);
            contextMessage2.CopyTo(buffer2);

            MemoryStream stream1 = new MemoryStream();
            await contextMessage1.CopyToStreamAsync(stream1);
            CollectionAssert.AreEqual(buffer1.Array, stream1.ToArray());

            MemoryStream stream2 = new MemoryStream();
            await contextMessage2.CopyToStreamAsync(stream2);
            CollectionAssert.AreEqual(buffer2.Array, stream2.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest1 = new RntbdConstants.Request();
            BytesDeserializer reader1 = new BytesDeserializer(buffer1.Array, buffer1.Count); reader1.ReadBytes(24);
            rntbdRequest1.ParseFrom(ref reader1);

            RntbdConstants.Request rntbdRequest2 = new RntbdConstants.Request();
            BytesDeserializer reader2 = new BytesDeserializer(buffer2.Array, buffer2.Count); reader2.ReadBytes(24);
            rntbdRequest2.ParseFrom(ref reader2);

            Assert.IsTrue(rntbdRequest1.transportRequestID.isPresent, $"Expected {nameof(RntbdConstants.Request.transportRequestID)} is absent");
            Assert.AreEqual(transportRequestId1, (int)rntbdRequest1.transportRequestID.value.valueLong, $"Unexpected {nameof(RntbdConstants.Request.transportRequestID)}");
            Assert.IsNull(headers.TransportRequestID);
            Assert.IsTrue(rntbdRequest2.transportRequestID.isPresent, $"Expected {nameof(RntbdConstants.Request.transportRequestID)} is absent");
            Assert.AreEqual(transportRequestId2, (int)rntbdRequest2.transportRequestID.value.valueLong, $"Unexpected {nameof(RntbdConstants.Request.transportRequestID)}");
            Assert.IsNull(headers.TransportRequestID);
        }

        [TestMethod]
        [Owner("paulchen")]
        public void SerializeRequestWithTransactionCommitTest()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            request.Properties = new Dictionary<string, object>()
            {
                { WFConstants.BackendHeaders.TransactionId, new byte[24] },
                { WFConstants.BackendHeaders.TransactionFirstRequest, false },
                { WFConstants.BackendHeaders.TransactionCommit, true },
            };

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.CompleteUserTransaction, ResourceType.Transaction),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(rntbdRequest.transactionId.isPresent, $"Expected {nameof(RntbdConstants.Request.transactionId)} is absent");
            Assert.IsTrue(rntbdRequest.transactionFirstRequest.isPresent, $"Expected {nameof(RntbdConstants.Request.transactionFirstRequest)} is absent");
            Assert.IsTrue(rntbdRequest.transactionCommit.isPresent, $"Expected {nameof(RntbdConstants.Request.transactionCommit)} is absent");
        }

        [TestMethod]
        [Owner("cdbcpint")]
        public void SerializeRequestWithRetriableWriteMetadataTest()
        {
            string retriableWriteRequestId = Guid.NewGuid().ToString();

            byte[] retriableWriteRequestIdBytes = Encoding.ASCII.GetBytes(retriableWriteRequestId);

            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            request.Properties = new Dictionary<string, object>()
            {
                { WFConstants.BackendHeaders.RetriableWriteRequestId, retriableWriteRequestIdBytes },
                { WFConstants.BackendHeaders.IsRetriedWriteRequest, true },
                { WFConstants.BackendHeaders.RetriableWriteRequestStartTimestamp, (UInt64)1000 },
            };

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);

            // Seek the reader by 24 bytes to cover the request headers
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);
            Assert.IsTrue(
                rntbdRequest.retriableWriteRequestId.isPresent,
                $"Expected {nameof(RntbdConstants.Request.retriableWriteRequestId)} is absent");

            Assert.AreEqual(
                Encoding.ASCII.GetString(rntbdRequest.retriableWriteRequestId.value.valueBytes.ToArray()),
                retriableWriteRequestId,
                $"Unexpected value for {nameof(RntbdConstants.Request.retriableWriteRequestId)}");


            Assert.IsTrue(rntbdRequest.isRetriedWriteRequest.isPresent, $"Expected {nameof(RntbdConstants.Request.isRetriedWriteRequest)} is absent");

            Assert.IsTrue(
                rntbdRequest.isRetriedWriteRequest.value.valueByte == (byte)0x01,
                $"Unexpected value for {nameof(RntbdConstants.Request.isRetriedWriteRequest)}");

            Assert.IsTrue(
                rntbdRequest.retriableWriteRequestStartTimestamp.isPresent,
                $"Expected {nameof(RntbdConstants.Request.retriableWriteRequestStartTimestamp)} is absent");

            Assert.AreEqual(
                (UInt64)1000,
                rntbdRequest.retriableWriteRequestStartTimestamp.value.valueULongLong,
                $"Unexpected value for {nameof(RntbdConstants.Request.retriableWriteRequestStartTimestamp)}");
        }

        [TestMethod]
        [Owner("swvya")]
        public void SerializeRequestWithSkipForceRefreshDatabaseAccountConfigsTest()
        {
            bool skipForceRefreshDatabaseAccountConfigs = true;

            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            request.Properties = new Dictionary<string, object>()
            {
                { WFConstants.BackendHeaders.SkipRefreshDatabaseAccountConfigs, skipForceRefreshDatabaseAccountConfigs }
            };

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);

            // Seek the reader by 24 bytes to cover the request headers
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(
                rntbdRequest.skipRefreshDatabaseAccountConfigs.isPresent,
                $"Expected {nameof(RntbdConstants.Request.skipRefreshDatabaseAccountConfigs)} is absent");

            Assert.IsTrue(
                rntbdRequest.skipRefreshDatabaseAccountConfigs.value.valueByte == (byte)0x01,
                $"Unexpected value for {nameof(RntbdConstants.Request.skipRefreshDatabaseAccountConfigs)}");
        }

        [TestMethod]
        [Owner("dikshibahl")]
        public void TestStoreRequestNameValueHeaders()
        {
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest(true);

            request.Headers.Add(HttpConstants.HttpHeaders.SessionToken, "3#002");

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);

            // Seek the reader by 24 bytes to cover the request headers
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(
                rntbdRequest.sessionToken.isPresent,
                $"Expected {nameof(RntbdConstants.Request.sessionToken)} is present");

        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        [Owner("kirankk")]
        public void AddStartEndEpk_NoProperties(bool useStoreRequestNameValueCollection)
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest(useStoreRequestNameValueCollection);
            request.Headers.Add(HttpConstants.HttpHeaders.ReadFeedKeyType, ReadFeedKeyType.EffectivePartitionKeyRange.ToString());
            request.Headers.Add(HttpConstants.HttpHeaders.StartEpk, "AA");
            request.Headers.Add(HttpConstants.HttpHeaders.EndEpk, "BB");

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);

            // Seek the reader by 24 bytes to cover the request headers
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(
                rntbdRequest.startEpk.isPresent,
                $"Expected {nameof(RntbdConstants.Request.startEpk)} is present");

            Assert.IsTrue(
                rntbdRequest.endEpk.isPresent,
                $"Expected {nameof(RntbdConstants.Request.endEpk)} is present");

            byte[] expectedStart = BytesSerializer.GetBytesForString("AA", rntbdRequest).ToArray();
            byte[] expectedEnd = BytesSerializer.GetBytesForString("BB", rntbdRequest).ToArray();

            CollectionAssert.AreEqual(
                rntbdRequest.startEpk.value.valueBytes.ToArray(), expectedStart,
                $"Unexpected value for {nameof(RntbdConstants.Request.startEpk)}");

            CollectionAssert.AreEqual(
                rntbdRequest.endEpk.value.valueBytes.ToArray(), expectedEnd,
                $"Unexpected value for {nameof(RntbdConstants.Request.endEpk)}");
        }

        [TestMethod]
        [Owner("kirankk")]
        public void AddStartEndEpk_WithPropertiesAndHeaders()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            // Set some random property unrelated
            request.Properties = new Dictionary<string, object>()
            {
                { WFConstants.BackendHeaders.TransactionId, new byte[24] }
            };

            request.Headers.Add(HttpConstants.HttpHeaders.ReadFeedKeyType, ReadFeedKeyType.EffectivePartitionKeyRange.ToString());
            request.Headers.Add(HttpConstants.HttpHeaders.StartEpk, "AA");
            request.Headers.Add(HttpConstants.HttpHeaders.EndEpk, "BB");

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);

            // Seek the reader by 24 bytes to cover the request headers
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(
                rntbdRequest.startEpk.isPresent,
                $"Expected {nameof(RntbdConstants.Request.startEpk)} is present");

            Assert.IsTrue(
                rntbdRequest.endEpk.isPresent,
                $"Expected {nameof(RntbdConstants.Request.endEpk)} is present");

            byte[] expectedStart = BytesSerializer.GetBytesForString("AA", rntbdRequest).ToArray();
            byte[] expectedEnd = BytesSerializer.GetBytesForString("BB", rntbdRequest).ToArray();

            CollectionAssert.AreEqual(
                rntbdRequest.startEpk.value.valueBytes.ToArray(), expectedStart,
                $"Unexpected value for {nameof(RntbdConstants.Request.startEpk)}");

            CollectionAssert.AreEqual(
                rntbdRequest.endEpk.value.valueBytes.ToArray(), expectedEnd,
                $"Unexpected value for {nameof(RntbdConstants.Request.endEpk)}");
        }

        [TestMethod]
        [Owner("kirankk")]
        public void AddStartEndEpk_WithProperties()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            // Set some random property unrelated
            request.Properties = new Dictionary<string, object>()
            {
                { HttpConstants.HttpHeaders.ReadFeedKeyType, (byte)ReadFeedKeyType.EffectivePartitionKeyRange },
                { HttpConstants.HttpHeaders.StartEpk, new byte[1] },
                { HttpConstants.HttpHeaders.EndEpk, new byte[1] }
            };

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);

            // Seek the reader by 24 bytes to cover the request headers
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(
                rntbdRequest.startEpk.isPresent,
                $"Expected {nameof(RntbdConstants.Request.startEpk)} is present");

            Assert.IsTrue(
                rntbdRequest.endEpk.isPresent,
                $"Expected {nameof(RntbdConstants.Request.endEpk)} is present");
        }

        [TestMethod]
        [Owner("kirankk")]
        public void AddStartEndEpkHash()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();

            // Set some random property unrelated
            request.Properties = new Dictionary<string, object>()
            {
                { WFConstants.BackendHeaders.StartEpkHash, new byte[1] },
                { WFConstants.BackendHeaders.EndEpkHash, new byte[1] }
            };

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);

            // Seek the reader by 24 bytes to cover the request headers
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(
                rntbdRequest.startEpkHash.isPresent,
                $"Expected {nameof(RntbdConstants.Request.startEpkHash)} is present");

            Assert.IsTrue(
                rntbdRequest.endEpkHash.isPresent,
                $"Expected {nameof(RntbdConstants.Request.endEpkHash)} is present");
        }

        [TestMethod]
        [Owner("dikshibahl")]
        public async Task TestUpdatesDuringTransportSerializationOnDictionaryNameValueCollectionAsync()
        {
            await this.TestUpdatesDuringTransportSerializationTestsAsync(new DictionaryNameValueCollection());
        }

        [TestMethod]
        [Owner("dikshibahl")]
        public async Task TestUpdatesDuringTransportSerializationOnRequestNameValueCollectionAsync()
        {
            await this.TestUpdatesDuringTransportSerializationTestsAsync(new RequestNameValueCollection());
        }

        [TestMethod]
        [Owner("cosmosdbquery")] // dev maintainer: dmmelnik
        [Ignore]
        public void SerializeResponseTest()
        {
            byte[] byteArray = new byte[200];
            BytesDeserializer deserializer;
            foreach (KeyValuePair<ResponseIdentifiers, RntbdTokenTypes> mapping in ResponseRntbdHeaders.ResponseIdentifierTypes)
            {
                BytesSerializer serializer = new(byteArray, byteArray.Length);
                serializer.Write((ushort)mapping.Key);
                switch (mapping.Value)
                {
                    case RntbdTokenTypes.Guid:
                        Guid guid = Guid.NewGuid();
                        serializer.Write((byte)RntbdTokenTypes.Guid);
                        serializer.Write(guid);
                        deserializer = new(byteArray, length: serializer.GetPosition());
                        this.ValidateStoreResponseHeader(deserializer, mapping.Key, guid.ToString());
                        break;
                    case RntbdTokenTypes.LongLong:
                        serializer.Write((byte)RntbdTokenTypes.LongLong);
                        serializer.Write(long.MaxValue);
                        deserializer = new(byteArray, length: serializer.GetPosition());
                        this.ValidateStoreResponseHeader(deserializer, mapping.Key, long.MaxValue.ToString(CultureInfo.InvariantCulture));
                        break;
                    case RntbdTokenTypes.Double:
                        serializer.Write((byte)RntbdTokenTypes.Double);
                        serializer.Write(double.MaxValue);
                        deserializer = new(byteArray, length: serializer.GetPosition());
                        if (mapping.Key == RntbdConstants.ResponseIdentifiers.RequestCharge)
                        {
                            this.ValidateStoreResponseHeader(deserializer, mapping.Key, string.Format(CultureInfo.InvariantCulture, "{0:0.##}", double.MaxValue));
                        }
                        else
                        {
                            this.ValidateStoreResponseHeader(deserializer, mapping.Key, double.MaxValue.ToString(CultureInfo.InvariantCulture));
                        }

                        break;
                    case RntbdTokenTypes.ULong:
                        serializer.Write((byte)RntbdTokenTypes.ULong);
                        serializer.Write(uint.MaxValue);
                        deserializer = new(byteArray, length: serializer.GetPosition());
                        this.ValidateStoreResponseHeader(deserializer, mapping.Key, uint.MaxValue.ToString(CultureInfo.InvariantCulture));
                        break;
                    case RntbdTokenTypes.Long:
                        serializer.Write((byte)RntbdTokenTypes.Long);
                        serializer.Write((long)int.MaxValue);
                        deserializer = new(byteArray, length: serializer.GetPosition());
                        this.ValidateStoreResponseHeader(deserializer, mapping.Key, int.MaxValue.ToString(CultureInfo.InvariantCulture));
                        break;
                    case RntbdTokenTypes.ULongLong:
                        serializer.Write((byte)RntbdTokenTypes.ULongLong);
                        serializer.Write(ulong.MaxValue);
                        deserializer = new(byteArray, length: serializer.GetPosition());
                        this.ValidateStoreResponseHeader(deserializer, mapping.Key, ulong.MaxValue.ToString(CultureInfo.InvariantCulture));
                        break;
                    case RntbdTokenTypes.Byte:
                        serializer.Write((byte)RntbdTokenTypes.Byte);
                        serializer.Write((byte)0x01);
                        deserializer = new(byteArray, length: serializer.GetPosition());
                        if (mapping.Key == RntbdConstants.ResponseIdentifiers.IndexingDirective)
                        {
                            this.ValidateStoreResponseHeader(deserializer, mapping.Key, IndexingDirectiveStrings.Include);
                        }
                        else if (mapping.Key == ResponseIdentifiers.RequestValidationFailure
                            || mapping.Key == ResponseIdentifiers.IsRUPerMinuteUsed)
                        {
                            this.ValidateStoreResponseHeader(deserializer, mapping.Key, "1");
                        }
                        else
                        {
                            this.ValidateStoreResponseHeader(deserializer, mapping.Key, "true");
                        }

                        break;
                    case RntbdTokenTypes.String:
                        Guid guidForString = Guid.NewGuid();
                        byte[] payload = Encoding.UTF8.GetBytes(guidForString.ToString());
                        serializer.Write((byte)mapping.Value);
                        serializer.Write((ushort)payload.Length);
                        serializer.Write(payload);
                        deserializer = new(byteArray, length: serializer.GetPosition());
                        this.ValidateStoreResponseHeader(deserializer, mapping.Key, guidForString.ToString());
                        break;
                    case RntbdTokenTypes.SmallString:
                        Guid guidForSmallString = Guid.NewGuid();
                        byte[] smallPayload = Encoding.UTF8.GetBytes(guidForSmallString.ToString());
                        serializer.Write((byte)mapping.Value);
                        serializer.Write((byte)smallPayload.Length);
                        serializer.Write(smallPayload);
                        deserializer = new(byteArray, length: serializer.GetPosition());
                        this.ValidateStoreResponseHeader(deserializer, mapping.Key, guidForSmallString.ToString());
                        break;
                    default:
                        // Ignore header we do not map
                        Assert.Fail($"Non tested mapping {mapping}");
                        break;
                }
            }
        }

        [DataRow(true, (uint)100)]
        [DataRow(false, (uint)200)]
        [DataRow(null, (uint)100)]
        [DataRow(false, null)]
        [TestMethod]
        [Owner("cosmosdbquery")] // dev maintainer: dmmelnik
        public void TestIsPayloadPresentOnResponse(bool? payloadPresent, uint? transportRequestId)
        {
            byte[] byteArray = new byte[200];
            BytesSerializer serializer = new(byteArray, byteArray.Length);

            if (payloadPresent.HasValue)
            {
                serializer.Write((ushort)ResponseIdentifiers.PayloadPresent);
                serializer.Write((byte)RntbdTokenTypes.Byte);
                serializer.Write((byte)(payloadPresent.Value ? 1 : 0));
            }

            if (transportRequestId.HasValue)
            {
                serializer.Write((ushort)ResponseIdentifiers.TransportRequestID);
                serializer.Write((byte)RntbdTokenTypes.ULong);
                serializer.Write(transportRequestId.Value);
            }

            BytesDeserializer deserializer = new(byteArray, serializer.GetPosition());
            bool result = HeadersTransportSerialization.TryParseMandatoryResponseHeaders(ref deserializer, out bool payloadPresentOut, out uint transportRequestIdOut);

            Assert.AreEqual(payloadPresent.HasValue && transportRequestId.HasValue, result, "Unexpected call result");
            if (result)
            {
                Assert.AreEqual(payloadPresent.Value, payloadPresentOut, "Unexpected payloadPresent");
                Assert.AreEqual(transportRequestId.Value, transportRequestIdOut, "Unexpected transportRequestId");
            }
        }

        [TestMethod]
        [Owner("arjanx")]
        public void TestGlobalNRegionSynchronousCommittedResponseHeader()
        {
            byte[] byteArray = new byte[200];
            BytesSerializer serializer = new(byteArray, byteArray.Length);

            long expectedValue = 101;
            serializer.Write((ushort)ResponseIdentifiers.GlobalNRegionCommittedGLSN);
            serializer.Write((byte)RntbdTokenTypes.LongLong);
            serializer.Write(expectedValue);

            BytesDeserializer deserializer = new(byteArray, serializer.GetPosition());
            this.ValidateStoreResponseHeader(deserializer, ResponseIdentifiers.GlobalNRegionCommittedGLSN, 101.ToString(CultureInfo.InvariantCulture));
        }

        private static readonly IReadOnlyDictionary<string, string> httpHeadersMap = typeof(HttpConstants.HttpHeaders).GetFields(BindingFlags.Public | BindingFlags.Static)
                .ToDictionary(x => (string)x.GetValue(null), x => x.Name);

        private static readonly IReadOnlyDictionary<string, string> backendHeadersMap = typeof(WFConstants.BackendHeaders).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => !string.Equals(x.Name, "QuorumAckedLLSN"))
            .ToDictionary(x => (string)x.GetValue(null), x => x.Name);

        private void ValidateStoreResponseHeader(BytesDeserializer deserializer, ResponseIdentifiers headerIdentifier, string expectedValue)
        {
            Guid activityId = Guid.NewGuid();
            string serverVersion = Guid.NewGuid().ToString();
            StoreResponse storeResponse = TransportSerialization.MakeStoreResponse(
                status: StatusCodes.Accepted,
                activityId: activityId,
                body: null,
                serverVersion: serverVersion,
                ref deserializer);

            StoreResponseNameValueCollection headers = (StoreResponseNameValueCollection)storeResponse.Headers;
            Assert.AreEqual(activityId.ToString(), headers.ActivityId);
            Assert.AreEqual(serverVersion, headers.ServerVersion);

            string key = headers.Keys().Where(x => !string.Equals(x, HttpConstants.HttpHeaders.ActivityId) && !string.Equals(x, HttpConstants.HttpHeaders.ServerVersion)).FirstOrDefault();

            if (headerIdentifier == ResponseIdentifiers.PayloadPresent)
            {
                Assert.IsTrue(string.IsNullOrEmpty(key), $"Ignored header {headerIdentifier} was set in the collection");
                return;
            }

            Assert.IsFalse(string.IsNullOrEmpty(key), $"No header {headerIdentifier} was set in the collection");


            // There should be a constant that matches the name of the property
            PropertyInfo headerProperty = null;
            if (httpHeadersMap.TryGetValue(key, out string propertyName))
            {
                headerProperty = headers.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            }

            if (headerProperty == null &&
                backendHeadersMap.TryGetValue(key, out propertyName))
            {
                headerProperty = headers.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            }

            if (headerProperty == null)
            {
                throw new Exception($"A HttpConstants.HttpHeaders or WFConstants.BackendHeaders field should exist with the name as the header property: '{propertyName}'.");
            }

            string value = (string)headerProperty.GetValue(headers);
            Assert.AreEqual(value, headers[key]);
            Assert.AreEqual(value, headers.Get(key));
            Assert.AreEqual(value, headers[key.ToUpper()]);
            Assert.AreEqual(value, headers.Get(key.ToUpper()));

            string temp = Guid.NewGuid().ToString();
            headerProperty.SetValue(headers, value);
            Assert.AreEqual(value, headers[key]);
            Assert.AreEqual(value, headerProperty.GetValue(headers));
            Assert.AreEqual(value, headers[key.ToUpper()]);
            Assert.AreEqual(value, headers.Get(key.ToUpper()));

            temp = Guid.NewGuid().ToString();
            headers.Set(key, temp);
            Assert.AreEqual(temp, headers[key]);

            temp = Guid.NewGuid().ToString();
            headers.Set(key.ToUpper(), temp);
            Assert.AreEqual(temp, headers[key]);

            temp = Guid.NewGuid().ToString();
            headers[key] = temp;
            Assert.AreEqual(temp, headers[key]);

            temp = Guid.NewGuid().ToString();
            headers[key.ToUpper()] = temp;
            Assert.AreEqual(temp, headers[key]);

            Assert.AreEqual(expectedValue, value, $"Enexpected header value for {headerIdentifier}");
        }

        [TestMethod]
        [Owner("askagarw")]
        public void SerializeRequestWithCorrelatedActivityIdTest()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            Guid guidCorrelatedActivityId = new Guid("4e2463cf-e194-4674-8b13-3abf966cd8b6");
            request.Headers.Add(WFConstants.BackendHeaders.CorrelatedActivityId, guidCorrelatedActivityId.ToString());

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(rntbdRequest.correlatedActivityId.isPresent);
            Assert.AreEqual(rntbdRequest.correlatedActivityId.value.valueGuid, guidCorrelatedActivityId);
        }

        [TestMethod]
        [Owner("akotalwar")]
        public void SerializeRequestWithInvalidCorrelatedActivityIdTest()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            request.Headers.Add(WFConstants.BackendHeaders.CorrelatedActivityId, "RandomGUID");

            try
            {
                // serialize
                using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out int headerSize,
                out _);
                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
                contextMessage.CopyTo(buffer);

                MemoryStream stream = new MemoryStream();
                contextMessage.CopyToStreamAsync(stream).Wait();
                CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

                // deserialize
                RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
                BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
                reader.ReadBytes(24);
                rntbdRequest.ParseFrom(ref reader);

                Assert.Fail("Exception was not thrown");
            }
            catch (Exception exception)
            {
                Assert.IsTrue(exception is BadRequestException);
            }
        }

        [TestMethod]
        [Owner("romisra")]
        public void SerializeRequestWithCollectionOperationOnGatewayTransactionId()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            Guid guidTransactionId = new Guid("4e2463cf-e194-4674-8b13-3abf966cd8b6");
            request.Headers.Add(WFConstants.BackendHeaders.CosmosGatewayTransactionId, guidTransactionId.ToString());

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
            request,
            "DummyReplicaPath",
            new ResourceOperation(OperationType.Create, ResourceType.Collection),
            Guid.NewGuid(),
            this.bufferProvider,
            "1",
            out _,
            out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(rntbdRequest.cosmosGatewayTransactionId.isPresent);
            Assert.AreEqual(rntbdRequest.cosmosGatewayTransactionId.value.valueGuid, guidTransactionId);
        }

        [TestMethod]
        [Owner("akotalwar")]
        public void SerializeRequestWithEmptyCorrelatedActivityIdTest()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.AreEqual(rntbdRequest.correlatedActivityId.value.valueGuid, Guid.Empty);
            Assert.IsFalse(rntbdRequest.correlatedActivityId.isPresent);
        }

        [TestMethod]
        [Owner("visridha")]
        public async Task SerializeRequestWithBodyTestAsync()
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            request.Headers.Add(WFConstants.BackendHeaders.CorrelatedActivityId, "4e2463cf-e194-4674-8b13-3abf966cd8b6");

            MemoryStream internalStream = new MemoryStream();
            byte[] buffer1 = Enumerable.Repeat<byte>(20, 150).ToArray();

            internalStream.Write(buffer1, 0, buffer1.Length);
            internalStream.Position = 0;
            request.Body = new CloneableStream(internalStream);
            await request.EnsureBufferedBodyAsync();

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out int headerSize,
                out int? bodySize);

            Assert.AreEqual(buffer1.Length + 4, bodySize);
            Assert.AreEqual(headerSize + bodySize, contextMessage.RequestSize);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            MemoryStream stream = new MemoryStream();
            contextMessage.CopyToStreamAsync(stream).Wait();
            CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

            CollectionAssert.AreEqual(buffer.Skip(buffer.Count - buffer1.Length).ToArray(), buffer1);
            int contentLength = MemoryMarshal.Read<int>(buffer.AsSpan<byte>()[(buffer.Count - buffer1.Length - 4)..]);
            Assert.AreEqual(buffer1.Length, contentLength);

            // deserialize
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, headerSize); reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);
            Assert.IsTrue(rntbdRequest.payloadPresent.isPresent);
            Assert.AreEqual(1, rntbdRequest.payloadPresent.value.valueByte);
        }

        [TestMethod]
        [Owner("cosmosdbquery")] // dev maintainer: adityasa 
        public void SerializationTestWithSupportedSerializationFormats()
        {
            this.TestSupportedSerializationFormats(headerValue: "JsonText", expectSuccess: true, flagValue: 1);
            this.TestSupportedSerializationFormats(headerValue: "CosmosBinary", expectSuccess: true, flagValue: 2);
            this.TestSupportedSerializationFormats(headerValue: "HybridRow", expectSuccess: true, flagValue: 4);
            this.TestSupportedSerializationFormats(headerValue: "JsonText,JsonText", expectSuccess: true, flagValue: 1);
            this.TestSupportedSerializationFormats(headerValue: "JsonText,CosmosBinary", expectSuccess: true, flagValue: 3);
            this.TestSupportedSerializationFormats(headerValue: "HybridRow,CosmosBinary", expectSuccess: true, flagValue: 6);
            this.TestSupportedSerializationFormats(headerValue: "    JsonText, HybridRow  ", expectSuccess: true, flagValue: 5);
            this.TestSupportedSerializationFormats(headerValue: "JsonText,CosmosBinary,HybridRow", expectSuccess: true, flagValue: 7);
            this.TestSupportedSerializationFormats(headerValue: "HybridRow,CosmosBinary,JsonText", expectSuccess: true, flagValue: 7);
            this.TestSupportedSerializationFormats(headerValue: " HybridRow ,  CosmosBinary, JsonText   ", expectSuccess: true, flagValue: 7);
            this.TestSupportedSerializationFormats(headerValue: "HYBRIDROW,cosmosbinary,jsonText", expectSuccess: true, flagValue: 7);

            this.TestSupportedSerializationFormats(headerValue: "", expectSuccess: false, flagValue: 0);
            this.TestSupportedSerializationFormats(headerValue: ",,", expectSuccess: false, flagValue: 0);
            this.TestSupportedSerializationFormats(headerValue: " , , ", expectSuccess: false, flagValue: 0);
            this.TestSupportedSerializationFormats(headerValue: ",HybridRow", expectSuccess: false, flagValue: 0);
            this.TestSupportedSerializationFormats(headerValue: "HybridRow,", expectSuccess: false, flagValue: 0);
            this.TestSupportedSerializationFormats(headerValue: ",HybridRow,", expectSuccess: false, flagValue: 0);
            this.TestSupportedSerializationFormats(headerValue: "JsonText CosmosBinary", expectSuccess: false, flagValue: 0);
            this.TestSupportedSerializationFormats(headerValue: "NotText,isbinary", expectSuccess: false, flagValue: 0);
            this.TestSupportedSerializationFormats(headerValue: "HybridRow|CosmosBinary|JsonText", expectSuccess: false, flagValue: 0);
            this.TestSupportedSerializationFormats(headerValue: "Json Text,Cosmos Binary,Hybrid Row", expectSuccess: false, flagValue: 0);
        }

        [TestMethod]
        [Owner("cosmosdbquery")]
        public void SerializationTestWithSupportedSerializationFeatures()
        {
            // CosmosBinary with single feature
            this.TestSupportedSerializationFormatsWithFeatures(
                headerValue: "CosmosBinary+Base64Strings",
                expectSuccess: true,
                formatFlagValue: 2,
                featureFlagValue: 0x01);

            // CosmosBinary with all features
            this.TestSupportedSerializationFormatsWithFeatures(
                headerValue: "CosmosBinary+Base64Strings+NumberArrays+UInt64",
                expectSuccess: true,
                formatFlagValue: 2,
                featureFlagValue: 0x07);

            // Combined formats with features
            this.TestSupportedSerializationFormatsWithFeatures(
                headerValue: "JsonText,CosmosBinary+Base64Strings+NumberArrays+UInt64",
                expectSuccess: true,
                formatFlagValue: 3,
                featureFlagValue: 0x07);

            // CosmosBinary with UInt64 only
            this.TestSupportedSerializationFormatsWithFeatures(
                headerValue: "CosmosBinary+UInt64",
                expectSuccess: true,
                formatFlagValue: 2,
                featureFlagValue: 0x04);

            // All formats with features
            this.TestSupportedSerializationFormatsWithFeatures(
                headerValue: "JsonText,CosmosBinary+Base64Strings,HybridRow",
                expectSuccess: true,
                formatFlagValue: 7,
                featureFlagValue: 0x01);

            // Case insensitive features
            this.TestSupportedSerializationFormatsWithFeatures(
                headerValue: "CosmosBinary+base64strings+NUMBERARRAYS+uint64",
                expectSuccess: true,
                formatFlagValue: 2,
                featureFlagValue: 0x07);

            // CosmosBinary without features (no features token expected)
            this.TestSupportedSerializationFormatsWithFeatures(
                headerValue: "CosmosBinary",
                expectSuccess: true,
                formatFlagValue: 2,
                featureFlagValue: 0x00);

            // Invalid feature name
            this.TestSupportedSerializationFormatsWithFeatures(
                headerValue: "CosmosBinary+InvalidFeature",
                expectSuccess: false,
                formatFlagValue: 0,
                featureFlagValue: 0);

            // Features on non-CosmosBinary format (invalid - '+' treated as part of name)
            this.TestSupportedSerializationFormatsWithFeatures(
                headerValue: "JsonText+Base64Strings",
                expectSuccess: false,
                formatFlagValue: 0,
                featureFlagValue: 0);

            // Trailing '+' with no feature name
            this.TestSupportedSerializationFormatsWithFeatures(
                headerValue: "CosmosBinary+",
                expectSuccess: false,
                formatFlagValue: 0,
                featureFlagValue: 0);
        }

        /// <summary>
        /// Tests the scenario of an RNTB Stream being serialized and deserialized
        /// into a RntbTokenStream that has fewer tokens in its array.
        /// Deserialization should not fail if the stream has more tokens than what the receiver expects.
        /// </summary>
        [TestMethod]
        [Owner("kirankk")]
        public void DeserializeRequestWithOutOfBoundsToken()
        {
            // Create a request with all the tokens
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();

            // serialize
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Create, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);

            // deserialize into a structure with less tokens, should not throw
            TestToken smallerTestToken = new TestToken();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count); reader.ReadBytes(24);
            smallerTestToken.ParseFrom(ref reader);
        }

        [TestMethod]
        [Owner("kirankk")]
        public void Request_TokensAreInOrder()
        {
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            for (ushort i = 0; i < rntbdRequest.tokens.Length; i++)
            {
                if (rntbdRequest.tokens[i] == null)
                {
                    // null tokens are valid as gaps in the array
                    continue;
                }

                Assert.AreEqual(i, rntbdRequest.tokens[i].GetTokenIdentifier(), $"Token at position {i} has identifier {rntbdRequest.tokens[i].GetTokenIdentifier()}");
            }
        }

        [TestMethod]
        [Owner("kirankk")]
        public void ConnectionContextResponse_TokensAreInOrder()
        {
            ConnectionContextResponse connectionContextResponse = new ConnectionContextResponse();
            for (ushort i = 0; i < connectionContextResponse.tokens.Length; i++)
            {
                if (connectionContextResponse.tokens[i] == null)
                {
                    // null tokens are valid as gaps in the array
                    continue;
                }

                Assert.AreEqual(i, connectionContextResponse.tokens[i].GetTokenIdentifier(), $"Token at position {i} has identifier {connectionContextResponse.tokens[i].GetTokenIdentifier()}");
            }
        }

        [TestMethod]
        [Owner("kirankk")]
        public void ResourceTypeAndRntbResourceType()
        {
            foreach (ResourceType resourceType in Enum.GetValues(typeof(ResourceType)))
            {
                // Known not mapped cases because they are not sent through RNTBD
                if (resourceType == ResourceType.SchemaContainer
                    || resourceType == ResourceType.Batch
                    || resourceType == ResourceType.Snapshot
                    || resourceType == ResourceType.Telemetry
                    || resourceType == ResourceType.ControllerService
                    || resourceType == ResourceType.Address
                    || resourceType == ResourceType.Media
                    || resourceType == ResourceType.Key
                    || resourceType == ResourceType.Unknown)
                {
                    continue;
                }

                RntbdResourceType? rntbdResourceType = null;
                try
                {
                    rntbdResourceType = TransportSerialization.GetRntbdResourceType(resourceType);
                }
                catch
                {
                    Assert.Fail($"ResourceType {resourceType} has no RntbdResourceType mapping");
                }

                Assert.IsTrue(resourceType.ToString().Equals(rntbdResourceType.Value.ToString(), StringComparison.OrdinalIgnoreCase), "RntbdResourceType and ResourceType enums should have the same names.");
            }
        }

        [TestMethod]
        [Owner("kirankk")]
        public void OperationTypeAndRntbOperationType()
        {
            foreach (OperationType operationType in Enum.GetValues(typeof(OperationType)))
            {
                // Known not mapped cases because they are not sent through RNTBD
                if (operationType == OperationType.Execute
                    || operationType == OperationType.QueryPlan
                    || operationType == OperationType.CollectionTruncate
                    || operationType == OperationType.GetConfiguration
                    || operationType == OperationType.Invalid
                    || operationType == OperationType.Last)
                {
                    continue;
                }

                RntbdOperationType? rntbdOperationType = null;
                try
                {
                    rntbdOperationType = TransportSerialization.GetRntbdOperationType(operationType);
                }
                catch
                {
                    Assert.Fail($"OperationType {operationType} has no RntbdOperationType mapping");
                }

                Assert.IsTrue(operationType.ToString().Equals(rntbdOperationType.Value.ToString(), StringComparison.OrdinalIgnoreCase), "RntbdOperationType and OperationType enums should have the same names.");
            }
        }

        [TestMethod]
        [Owner("cosmosdbrg")]
        public void SerializeRequestWithThroughputBucketHeaderTest()
        {
            List<object> validThroughputBucketValues = new List<object> { 0, 1, 2, 3, 4, 5, 10, 20, 255, "1", "5" };
            List<object> invalidThroughputBucketValues = new List<object>() { -1, 256, 1000, "str", "0.0", "1.0", "2.5" };

            foreach (object throughputBucket in validThroughputBucketValues)
            {
                DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
                request.Properties = new Dictionary<string, object>();

                request.Headers.Add(HttpConstants.HttpHeaders.ThroughputBucket, throughputBucket.ToString());

                using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                    request,
                    "DummyReplicaPath",
                    new ResourceOperation(OperationType.Create, ResourceType.Document),
                    Guid.NewGuid(),
                    this.bufferProvider,
                    "1",
                    out int headerSize,
                    out _);
                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
                contextMessage.CopyTo(buffer);

                MemoryStream stream = new MemoryStream();
                contextMessage.CopyToStreamAsync(stream).Wait();
                CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

                // deserialize
                RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
                BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
                reader.ReadBytes(24);
                rntbdRequest.ParseFrom(ref reader);

                Assert.IsTrue(rntbdRequest.throughputBucket.isPresent, $"Expected {nameof(RntbdConstants.Request.throughputBucket)} is absent");

                Assert.AreEqual(byte.Parse(throughputBucket.ToString()), rntbdRequest.throughputBucket.value.valueByte, $"Unexpected {nameof(RntbdConstants.Request.throughputBucket)}");
            }

            foreach (object throughputBucket in invalidThroughputBucketValues)
            {
                DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
                request.Properties = new Dictionary<string, object>();

                request.Headers.Add(HttpConstants.HttpHeaders.ThroughputBucket, throughputBucket.ToString());

                try
                {
                    using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                        request,
                        "DummyReplicaPath",
                        new ResourceOperation(OperationType.Create, ResourceType.Document),
                        Guid.NewGuid(),
                        this.bufferProvider,
                        "1",
                        out int headerSize,
                        out _);
                    Assert.Fail("Expected BadRequestException");
                }
                catch (BadRequestException ex)
                {
                    // Expected
                    Console.WriteLine(ex.Message);
                }
            }
        }

        [TestMethod]
        [Owner("cosmosdbrg")]
        public void SerializeRequestWithWorkloadIdHeaderTest()
        {
            List<object> validWorkloadIdValues = new List<object> { 0, 1, 2, 3, 4, 5, 10, 20, 256, 1000, 10000, 65535, "1", "5" };
            List<object> invalidWorkloadIdValues = new List<object>() { -1, 65536, "str", "0.0", "1.0", "2.5" };

            foreach (object workloadId in validWorkloadIdValues)
            {
                DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
                request.Properties = new Dictionary<string, object>();

                request.Headers.Add(HttpConstants.HttpHeaders.WorkloadId, workloadId.ToString());

                using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                    request,
                    "DummyReplicaPath",
                    new ResourceOperation(OperationType.Create, ResourceType.Document),
                    Guid.NewGuid(),
                    this.bufferProvider,
                    "1",
                    out int headerSize,
                    out _);
                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
                contextMessage.CopyTo(buffer);

                MemoryStream stream = new MemoryStream();
                contextMessage.CopyToStreamAsync(stream).Wait();
                CollectionAssert.AreEqual(buffer.Array, stream.ToArray());

                // deserialize
                RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
                BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
                reader.ReadBytes(24);
                rntbdRequest.ParseFrom(ref reader);

                Assert.IsTrue(rntbdRequest.workloadId.isPresent, $"Expected {nameof(RntbdConstants.Request.workloadId)} is absent");

                Assert.AreEqual(ushort.Parse(workloadId.ToString()), rntbdRequest.workloadId.value.valueUShort, $"Unexpected {nameof(RntbdConstants.Request.workloadId)}");
            }

            foreach (object workloadId in invalidWorkloadIdValues)
            {
                DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
                request.Properties = new Dictionary<string, object>();

                request.Headers.Add(HttpConstants.HttpHeaders.WorkloadId, workloadId.ToString());

                try
                {
                    using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                        request,
                        "DummyReplicaPath",
                        new ResourceOperation(OperationType.Create, ResourceType.Document),
                        Guid.NewGuid(),
                        this.bufferProvider,
                        "1",
                        out int headerSize,
                        out _);
                    Assert.Fail("Expected BadRequestException");
                }
                catch (BadRequestException ex)
                {
                    // Expected
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private async Task TestUpdatesDuringTransportSerializationTestsAsync(INameValueCollection headers)
        {
            // initialize
            DocumentServiceRequest request = new DocumentServiceRequest(
                OperationType.Create,
                ResourceType.Document,
                "dbs/wo1ZAA==/colls/wo1ZAP7zFQA=/",
                null,
                AuthorizationTokenType.PrimaryMasterKey,
                headers)
            {
                Properties = new Dictionary<string, object>()
                {
                    { WFConstants.BackendHeaders.SkipRefreshDatabaseAccountConfigs, true }
                }
            };

            using CancellationTokenSource stopUpdating = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            TaskCompletionSource<bool> started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task updateHeadersTask = Task.Run(() =>
            {
                for (int i = 0; i < 64; i++)
                {
                    headers.Add($"unknown-header-{i}", "initial");
                    request.Properties.Add($"unknown-property-{i}", "initial");
                }

                started.SetResult(true);
                int iteration = 0;
                while (!stopUpdating.IsCancellationRequested)
                {
                    int slot = iteration++ % 64;
                    string value = iteration.ToString(CultureInfo.InvariantCulture);
                    headers.Remove($"unknown-header-{slot}");
                    headers.Add($"unknown-header-{slot}", value);

                    // RequestNameValueCollection has multiple storages based on the header popularity
                    if (headers is RequestNameValueCollection rHeaders)
                    {
                        rHeaders.A_IM = value;
                    }

                    request.Properties.Remove($"unknown-property-{slot}");
                    request.Properties.Add($"unknown-property-{slot}", value);
                }
            });

            try
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Task[] tasks = new Task[500];
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                            request,
                            "DummyReplicaPath",
                            new ResourceOperation(OperationType.Create, ResourceType.Document),
                            Guid.NewGuid(),
                            this.bufferProvider,
                            "1",
                            out int headerSize,
                            out _);
                    });
                }

                await Task.WhenAll(tasks);
                Assert.IsFalse(stopUpdating.IsCancellationRequested, "Serialization exceeded the update window.");
            }
            finally
            {
                stopUpdating.Cancel();
                await updateHeadersTask;
            }

            Assert.IsTrue(request.Headers.Count() > 10);
        }

        private DocumentServiceRequest CreateDummyDocumentServiceRequest(bool useStoreRequestNameValueCollection = false)
        {
            return new DocumentServiceRequest(
                OperationType.Create,
                ResourceType.Document,
                "dbs/wo1ZAA==/colls/wo1ZAP7zFQA=/",
                null,
                AuthorizationTokenType.PrimaryMasterKey,
                useStoreRequestNameValueCollection ? new RequestNameValueCollection() : new DictionaryNameValueCollection());
        }

        private void TestSupportedSerializationFormats(string headerValue, bool expectSuccess, byte flagValue)
        {
            // initialize
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            request.Headers.Add(WFConstants.BackendHeaders.SupportedSerializationFormats, headerValue);
            ArraySegment<byte> buffer = default;
            bool receivedException = false;
            try
            {
                // serialize
                using (TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                    request,
                    "DummyReplicaPath",
                    new ResourceOperation(OperationType.Create, ResourceType.Document),
                    Guid.NewGuid(),
                    this.bufferProvider,
                    "1",
                    out int headerSize,
                    out _))
                {
                    buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
                    contextMessage.CopyTo(buffer);

                    MemoryStream stream = new MemoryStream();
                    contextMessage.CopyToStreamAsync(stream).Wait();
                    CollectionAssert.AreEqual(buffer.Array, stream.ToArray());
                }
            }
            catch (Exception exception)
            {
                receivedException = exception is BadRequestException;
            }

            if (expectSuccess)
            {
                // deserialize
                RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
                BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
                reader.ReadBytes(24);
                rntbdRequest.ParseFrom(ref reader);
                Assert.IsTrue(rntbdRequest.supportedSerializationFormats.isPresent);
                Assert.AreEqual(rntbdRequest.supportedSerializationFormats.value.valueByte, flagValue);
                Assert.IsTrue(!receivedException);
            }
            else
            {
                Assert.IsTrue(receivedException);
            }
        }

        private void TestSupportedSerializationFormatsWithFeatures(string headerValue, bool expectSuccess, byte formatFlagValue, byte featureFlagValue)
        {
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            request.Headers.Add(WFConstants.BackendHeaders.SupportedSerializationFormats, headerValue);
            ArraySegment<byte> buffer = default;
            bool receivedException = false;
            try
            {
                using (TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                    request,
                    "DummyReplicaPath",
                    new ResourceOperation(OperationType.Create, ResourceType.Document),
                    Guid.NewGuid(),
                    this.bufferProvider,
                    "1",
                    out int headerSize,
                    out _))
                {
                    buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
                    contextMessage.CopyTo(buffer);
                }
            }
            catch (Exception exception)
            {
                receivedException = exception is BadRequestException;
            }

            if (expectSuccess)
            {
                RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
                BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
                reader.ReadBytes(24);
                rntbdRequest.ParseFrom(ref reader);
                Assert.IsTrue(rntbdRequest.supportedSerializationFormats.isPresent);
                Assert.AreEqual(formatFlagValue, rntbdRequest.supportedSerializationFormats.value.valueByte);

                if (featureFlagValue != 0x00)
                {
                    Assert.IsTrue(rntbdRequest.supportedSerializationFeatures.isPresent);
                    Assert.AreEqual(featureFlagValue, rntbdRequest.supportedSerializationFeatures.value.valueByte);
                }
                else
                {
                    Assert.IsFalse(rntbdRequest.supportedSerializationFeatures.isPresent);
                }

                Assert.IsFalse(receivedException);
            }
            else
            {
                Assert.IsTrue(receivedException);
            }
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        [DataRow("Default", DisplayName = "Default strategy (Java SDK) is silently skipped")]
        [DataRow("UnknownStrategy", DisplayName = "Arbitrary non-numeric string is silently skipped")]
        public void AddReadConsistencyStrategy_UnparseableValue_SilentlySkipped(string strategyValue)
        {
            // Arrange — values that Enum.TryParse cannot parse should be silently ignored.
            // ServerStoreModel validates upstream; TransportSerialization just skips unparseable values.
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            request.Headers[HttpConstants.HttpHeaders.ReadConsistencyStrategy] = strategyValue;

            // Act — should NOT throw
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Read, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);

            // Deserialize and verify the readConsistencyStrategy token is NOT present
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsFalse(rntbdRequest.readConsistencyStrategy.isPresent,
                $"'{strategyValue}' should be ignored — readConsistencyStrategy token must not be present");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        [DataRow("LatestCommitted", (byte)3, DisplayName = "Valid strategy: LatestCommitted")]
        [DataRow("Eventual", (byte)1, DisplayName = "Valid strategy: Eventual")]
        [DataRow("Session", (byte)2, DisplayName = "Valid strategy: Session")]
        [DataRow("GlobalStrong", (byte)4, DisplayName = "Valid strategy: GlobalStrong")]
        [DataRow("LastCommittedSingleWriteRegion", (byte)5, DisplayName = "Valid strategy: LastCommittedSingleWriteRegion")]
        public void AddReadConsistencyStrategy_ValidValue_IsSerializedCorrectly(string strategyName, byte expectedByte)
        {
            // Arrange
            DocumentServiceRequest request = this.CreateDummyDocumentServiceRequest();
            request.Headers[HttpConstants.HttpHeaders.ReadConsistencyStrategy] = strategyName;

            // Act
            using TransportSerialization.SerializedRequest contextMessage = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Read, ResourceType.Document),
                Guid.NewGuid(),
                this.bufferProvider,
                "1",
                out _,
                out _);

            // Deserialize and verify
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[contextMessage.RequestSize]);
            contextMessage.CopyTo(buffer);
            RntbdConstants.Request rntbdRequest = new RntbdConstants.Request();
            BytesDeserializer reader = new BytesDeserializer(buffer.Array, buffer.Count);
            reader.ReadBytes(24);
            rntbdRequest.ParseFrom(ref reader);

            Assert.IsTrue(rntbdRequest.readConsistencyStrategy.isPresent,
                $"readConsistencyStrategy token should be present for '{strategyName}'");
            Assert.AreEqual(expectedByte, rntbdRequest.readConsistencyStrategy.value.valueByte,
                $"readConsistencyStrategy byte should match enum value for '{strategyName}'");
        }

        private enum TestIdentifiers : ushort
        {
            PayloadPresent = 0x0000,
        }

        private class TestToken : RntbdTokenStream<TestIdentifiers>
        {
            public override int RequiredTokenCount => 0;

            public TestToken()
            {
                this.tokens = new RntbdToken[]
                {
                };
            }
        }
    }
}
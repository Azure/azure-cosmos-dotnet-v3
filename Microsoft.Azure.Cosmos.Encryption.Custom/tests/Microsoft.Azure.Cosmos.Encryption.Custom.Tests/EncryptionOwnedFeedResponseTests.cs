//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionOwnedFeedResponseTests
    {
        private const string MalformedFeed = "{\"Documents\":[!}";
        private const string ValidFeed = "{\"Documents\":[{\"id\":\"first\"}]}";
        private const string Query = "SELECT * FROM c";
        private const string LegacyAlgorithm = "AEAes256CbcHmacSha256Randomized";

        [DataTestMethod]
        [DynamicData(nameof(Routes), DynamicDataSourceType.Method)]
        public async Task ParseFailure_ReleasesResponseBeforeCallerCleanup(string route, string processor)
        {
            using Harness harness = new (MalformedFeed);
            Mock<Encryptor> encryptor = new (MockBehavior.Strict);
            Exception expected = await CaptureAsync(async () =>
            {
                using MemoryStream input = new (Encoding.UTF8.GetBytes(MalformedFeed));
                using Stream output = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                    input, encryptor.Object, Enum.Parse<JsonProcessor>(processor), CancellationToken.None);
            });

            Exception actual = await CaptureAsync(() => ReadRouteAsync(
                harness.Container.WithEncryptor(encryptor.Object), route, processor));

            AssertParseFailure(expected, actual);
            encryptor.VerifyNoOtherCalls();
            AssertReleased(harness, $"{route}/{processor}");
        }

        [DataTestMethod]
        [DynamicData(nameof(DisposalFaults), DynamicDataSourceType.Method)]
        public async Task CleanupFailure_PreservesParseFailureAndAttemptsContentRelease(
            string route, string processor, string fault)
        {
            using Harness harness = new (MalformedFeed);
            harness.Response.ThrowOnDispose = fault == "response";
            harness.Content.DisposeFailuresRemaining = fault == "content twice" ? 2 : fault == "content once" ? 1 : 0;
            Mock<Encryptor> encryptor = new (MockBehavior.Strict);
            Exception expected = await CaptureAsync(async () =>
            {
                using MemoryStream input = new (Encoding.UTF8.GetBytes(MalformedFeed));
                using Stream output = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                    input, encryptor.Object, Enum.Parse<JsonProcessor>(processor), CancellationToken.None);
            });

            Exception actual = await CaptureAsync(() => ReadRouteAsync(
                harness.Container.WithEncryptor(encryptor.Object), route, processor));

            AssertParseFailure(expected, actual);
            encryptor.VerifyNoOtherCalls();
            Assert.AreEqual(1, harness.Response.DisposeCalls, $"{route}/{processor}/{fault}");
            if (fault.StartsWith("content", StringComparison.Ordinal))
            {
                Assert.AreEqual(2, harness.Content.DisposeCalls, "Retry content cleanup after response disposal fails.");
            }

            Assert.AreEqual(fault == "content twice" ? 0 : 1, harness.Content.ReleaseCount);
            Assert.AreEqual(fault == "content twice", harness.Content.CanRead);
        }

        [DataTestMethod]
        [DynamicData(nameof(ReadFaults), DynamicDataSourceType.Method)]
        public async Task ReadFailure_PreservesExactExceptionAndCancellation(
            string route, string processor, bool cancellation)
        {
            using Harness harness = new (ValidFeed);
            using CancellationTokenSource source = new ();
            Exception primary = cancellation
                ? new OperationCanceledException("primary cancellation", source.Token)
                : new IOException("primary read failure");
            harness.Content.ReadFailure = primary;
            if (cancellation)
            {
                harness.Content.BeforeRead = source.Cancel;
            }

            harness.Response.ThrowOnDispose = true;
            Mock<Encryptor> encryptor = new (MockBehavior.Strict);
            Exception actual = await CaptureAsync(() => ReadRouteAsync(
                harness.Container.WithEncryptor(encryptor.Object), route, processor, source.Token));

            Assert.AreSame(primary, actual, $"{route}/{processor}");
            if (cancellation)
            {
                Assert.IsTrue(source.IsCancellationRequested);
                Assert.AreEqual(source.Token, ((OperationCanceledException)actual).CancellationToken);
            }

            encryptor.VerifyNoOtherCalls();
            AssertReleased(harness, $"{route}/{processor}");
        }

        [DataTestMethod]
        [DynamicData(nameof(EagerRoutes), DynamicDataSourceType.Method)]
        public async Task DecryptionFailure_ReleasesResponseAndPreservesExactException(
            string route, string processor)
        {
            const string payload = "{\"Documents\":[{\"id\":\"first\",\"_ei\":{" +
                "\"_ef\":2,\"_ea\":\"AEAes256CbcHmacSha256Randomized\",\"_en\":\"key\"," +
                "\"_ed\":\"AQID\",\"_ep\":[\"/secret\"]}}]}";
            using Harness harness = new (payload);
            IOException primary = new ("primary decrypt failure");
            Mock<Encryptor> encryptor = new (MockBehavior.Strict);
            encryptor.Setup(value => value.DecryptAsync(
                    It.IsAny<byte[]>(), "key", LegacyAlgorithm,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(primary);

            Exception actual = await CaptureAsync(() => ReadRouteAsync(
                harness.Container.WithEncryptor(encryptor.Object), route, processor));

            Assert.AreSame(primary, actual);
            encryptor.Verify(value => value.DecryptAsync(
                It.IsAny<byte[]>(), "key", LegacyAlgorithm,
                It.IsAny<CancellationToken>()), Times.Once);
            encryptor.VerifyNoOtherCalls();
            AssertReleased(harness, $"{route}/{processor}");
        }

        [DataTestMethod]
        [DynamicData(nameof(StreamRoutes), DynamicDataSourceType.Method)]
        public async Task SuccessfulStreamRead_TransfersOwnershipToCaller(string route, string processor)
        {
            using Harness harness = new (ValidFeed);
            Mock<Encryptor> encryptor = new (MockBehavior.Strict);
            using ResponseMessage response = await ReadStreamRouteAsync(
                harness.Container.WithEncryptor(encryptor.Object), route, processor, CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(0, harness.Response.DisposeCalls, "A returned response must remain caller-owned.");
            Assert.IsTrue(harness.Content.CanRead);
            using (StreamReader reader = new (response.Content, Encoding.UTF8, false, 1024, leaveOpen: true))
            {
                JObject feed = JObject.Parse(await reader.ReadToEndAsync());
                Assert.AreEqual("first", (string)feed["Documents"][0]["id"]);
            }

            response.Dispose();
            AssertReleased(harness, $"{route}/{processor}");
            encryptor.VerifyNoOtherCalls();
        }

        [DataTestMethod]
        [DynamicData(nameof(TypedControls), DynamicDataSourceType.Method)]
        public async Task TypedQuery_PreservesSuccessAndServiceErrorOwnership(
            string processor, string control)
        {
            string payload = control == "null" ? null : control == "scalar"
                ? "{\"Documents\":[42,7]}" : control == "HTTP 503" ? "{" : ValidFeed;
            using Harness harness = new (payload,
                control == "HTTP 503" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
            Mock<Encryptor> encryptor = new (MockBehavior.Strict);
            Container encrypted = harness.Container.WithEncryptor(encryptor.Object);
            QueryRequestOptions options = QueryOptions(processor);

            if (control == "HTTP 503")
            {
                CosmosException failure = await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                    encrypted.GetItemQueryIterator<JObject>(Query, requestOptions: options).ReadNextAsync());
                Assert.AreEqual(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
            }
            else if (control == "scalar")
            {
                FeedResponse<int> page = await encrypted.GetItemQueryIterator<int>(Query, requestOptions: options).ReadNextAsync();
                CollectionAssert.AreEqual(new[] { 42, 7 }, page.ToArray());
            }
            else
            {
                FeedResponse<JObject> page = await encrypted.GetItemQueryIterator<JObject>(Query, requestOptions: options).ReadNextAsync();
                Assert.AreEqual(control == "null" ? 0 : 1, page.Count);
                if (control != "null")
                {
                    Assert.AreEqual("first", (string)page.Single()["id"]);
                }
            }

            AssertReleased(harness, $"{control}/{processor}");
            encryptor.VerifyNoOtherCalls();
        }

        public static IEnumerable<object[]> Routes()
        {
            foreach (string processor in Processors())
            {
                foreach (string route in new[] { "query typed", "query lazy", "query stream",
                    "change feed typed", "change feed stream", "read many typed", "read many stream" })
                {
                    yield return new object[] { route, processor };
                }
            }
        }

        public static IEnumerable<object[]> DisposalFaults()
        {
            foreach (object[] route in Routes())
            {
                foreach (string fault in new[] { "response", "content once", "content twice" })
                {
                    yield return new object[] { route[0], route[1], fault };
                }
            }
        }

        public static IEnumerable<object[]> ReadFaults()
        {
            foreach (object[] route in Routes())
            {
                yield return new object[] { route[0], route[1], false };
                yield return new object[] { route[0], route[1], true };
            }
        }

        public static IEnumerable<object[]> EagerRoutes() =>
            Routes().Where(row => (string)row[0] != "query lazy");

        public static IEnumerable<object[]> StreamRoutes() =>
            Routes().Where(row => ((string)row[0]).EndsWith("stream", StringComparison.Ordinal));

        public static IEnumerable<object[]> TypedControls()
        {
            foreach (string processor in Processors())
            {
                foreach (string control in new[] { "valid", "null", "scalar", "HTTP 503" })
                {
                    yield return new object[] { processor, control };
                }
            }
        }

        private static IEnumerable<string> Processors()
        {
            yield return nameof(JsonProcessor.Newtonsoft);
#if NET8_0_OR_GREATER
            yield return nameof(JsonProcessor.Stream);
#endif
        }

        private static async Task ReadRouteAsync(
            Container encrypted, string route, string processor, CancellationToken token = default)
        {
            switch (route)
            {
                case "query typed":
                    await encrypted.GetItemQueryIterator<JObject>(
                        new QueryDefinition(Query), requestOptions: QueryOptions(processor)).ReadNextAsync(token);
                    break;
                case "query lazy":
                    FeedResponse<DecryptableItem> page = await encrypted.GetItemQueryIterator<DecryptableItem>(
                        FeedRange.FromPartitionKey(new PartitionKey("pk")),
                        new QueryDefinition(Query), requestOptions: QueryOptions(processor)).ReadNextAsync(token);
                    if (page is IAsyncDisposable disposable)
                    {
                        await disposable.DisposeAsync();
                    }

                    break;
                case "change feed typed":
                    await encrypted.GetChangeFeedIterator<JObject>(ChangeFeedStartFrom.Beginning(),
                        ChangeFeedMode.Incremental, ChangeFeedOptions(processor)).ReadNextAsync(token);
                    break;
                case "read many typed":
                    await encrypted.ReadManyItemsAsync<JObject>(
                        new[] { ("id", new PartitionKey("pk")) }, ReadManyOptions(processor), token);
                    break;
                default:
                    using (ResponseMessage response = await ReadStreamRouteAsync(encrypted, route, processor, token))
                    {
                        Assert.IsTrue(response.IsSuccessStatusCode);
                    }

                    break;
            }
        }

        private static Task<ResponseMessage> ReadStreamRouteAsync(
            Container encrypted, string route, string processor, CancellationToken token) => route switch
        {
            "query stream" => encrypted.GetItemQueryStreamIterator(
                new QueryDefinition(Query), requestOptions: QueryOptions(processor)).ReadNextAsync(token),
            "change feed stream" => encrypted.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.Beginning(), ChangeFeedMode.Incremental, ChangeFeedOptions(processor)).ReadNextAsync(token),
            "read many stream" => encrypted.ReadManyItemsStreamAsync(
                new[] { ("id", new PartitionKey("pk")) }, ReadManyOptions(processor), token),
            _ => throw new AssertFailedException($"Unknown route: {route}"),
        };

        private static QueryRequestOptions QueryOptions(string processor) =>
            new () { Properties = ProcessorProperties(processor) };

        private static ChangeFeedRequestOptions ChangeFeedOptions(string processor) =>
            new () { Properties = ProcessorProperties(processor) };

        private static ReadManyRequestOptions ReadManyOptions(string processor) =>
            new () { Properties = ProcessorProperties(processor) };

        private static Dictionary<string, object> ProcessorProperties(string processor) =>
            new () { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, processor } };

        private static void AssertParseFailure(Exception expected, Exception actual)
        {
            Assert.IsNotNull(expected, "The borrowed processor must reject the malformed fixture.");
            Assert.IsNotNull(actual, "The owning API must reject the same malformed fixture.");
            Assert.AreEqual(expected.GetType(), actual.GetType(), actual.ToString());
            Assert.AreEqual(expected.Message, actual.Message);
            Assert.AreEqual(expected.InnerException?.GetType(), actual.InnerException?.GetType());
            Assert.AreEqual(expected.InnerException?.Message, actual.InnerException?.Message);
        }

        private static void AssertReleased(Harness harness, string context)
        {
            Assert.AreEqual(1, harness.Response.DisposeCalls, context);
            if (harness.Content != null)
            {
                Assert.AreEqual(1, harness.Content.ReleaseCount, context);
                Assert.IsFalse(harness.Content.CanRead, context);
            }
        }

        private static async Task<Exception> CaptureAsync(Func<Task> operation)
        {
            try
            {
                await operation();
                return null;
            }
            catch (Exception failure)
            {
                return failure;
            }
        }

        private sealed class Harness : IDisposable
        {
            private readonly CosmosClient client;

            public Harness(string payload, HttpStatusCode status = HttpStatusCode.OK)
            {
                this.client = new CosmosClient("https://localhost:8081/", Convert.ToBase64String(new byte[64]),
                    new CosmosClientOptions { Serializer = new FeedSerializer() });
                Mock<Database> database = new ();
                database.SetupGet(value => value.Client).Returns(this.client);
                Mock<Container> container = new ();
                container.SetupGet(value => value.Database).Returns(database.Object);
                this.Content = payload == null ? null : new TrackingContent(Encoding.UTF8.GetBytes(payload));
                this.Response = new TrackingResponse(status) { Content = this.Content };
                Mock<FeedIterator> iterator = new ();
                iterator.Setup(value => value.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(this.Response);
                container.Setup(value => value.GetItemQueryStreamIterator(
                    It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>())).Returns(iterator.Object);
                container.Setup(value => value.GetItemQueryStreamIterator(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>())).Returns(iterator.Object);
                container.Setup(value => value.GetItemQueryStreamIterator(
                    It.IsAny<FeedRange>(), It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
                    .Returns(iterator.Object);
                container.Setup(value => value.GetChangeFeedStreamIterator(
                    It.IsAny<ChangeFeedStartFrom>(), It.IsAny<ChangeFeedMode>(), It.IsAny<ChangeFeedRequestOptions>()))
                    .Returns(iterator.Object);
                container.Setup(value => value.ReadManyItemsStreamAsync(
                    It.IsAny<IReadOnlyList<(string id, PartitionKey partitionKey)>>(),
                    It.IsAny<ReadManyRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(this.Response);
                this.Container = container.Object;
            }

            public Container Container { get; }

            public TrackingResponse Response { get; }

            public TrackingContent Content { get; }

            public void Dispose()
            {
                this.Response.ThrowOnDispose = false;
                if (this.Content != null)
                {
                    this.Content.DisposeFailuresRemaining = 0;
                }

                try
                {
                    this.Response.Dispose();
                }
                finally
                {
                    this.Content?.Dispose();
                    this.client.Dispose();
                }
            }
        }

        private sealed class TrackingResponse : ResponseMessage
        {
            public TrackingResponse(HttpStatusCode status)
                : base(status, errorMessage: status == HttpStatusCode.OK ? null : "Service unavailable")
            {
            }

            public int DisposeCalls { get; private set; }

            public bool ThrowOnDispose { get; set; }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.DisposeCalls++;
                }

                base.Dispose(disposing);
                if (disposing && this.ThrowOnDispose)
                {
                    throw new IOException("secondary response disposal");
                }
            }
        }

        private sealed class TrackingContent : MemoryStream
        {
            public TrackingContent(byte[] bytes)
                : base(bytes)
            {
            }

            public int DisposeCalls { get; private set; }

            public int ReleaseCount { get; private set; }

            public int DisposeFailuresRemaining { get; set; }

            public Exception ReadFailure { get; set; }

            public Action BeforeRead { get; set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                this.ThrowReadFailure();
                return base.Read(buffer, offset, count);
            }

            public override int Read(Span<byte> buffer)
            {
                this.ThrowReadFailure();
                return base.Read(buffer);
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                this.ThrowReadFailure();
                return base.ReadAsync(buffer, offset, count, token);
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
            {
                this.ThrowReadFailure();
                return base.ReadAsync(buffer, token);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.DisposeCalls++;
                    if (this.DisposeFailuresRemaining > 0)
                    {
                        this.DisposeFailuresRemaining--;
                        throw new IOException("secondary content disposal");
                    }

                    if (this.CanRead)
                    {
                        this.ReleaseCount++;
                    }
                }

                base.Dispose(disposing);
            }

            private void ThrowReadFailure()
            {
                this.BeforeRead?.Invoke();
                if (this.ReadFailure != null)
                {
                    throw this.ReadFailure;
                }
            }
        }

        private sealed class FeedSerializer : CosmosSerializer
        {
            public override T FromStream<T>(Stream stream)
            {
                using StreamReader reader = new (stream);
                return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
            }

            public override Stream ToStream<T>(T input) =>
                new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(input)));
        }
    }
}

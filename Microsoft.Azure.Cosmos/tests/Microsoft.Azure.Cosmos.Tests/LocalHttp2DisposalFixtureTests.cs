//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Authentication;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Exchange = Microsoft.Azure.Cosmos.Tests.LocalHttp2DisposalFixture.Exchange;
    using ObservedResponse = Microsoft.Azure.Cosmos.Tests.LocalHttp2DisposalFixture.ObservedResponse;

    [TestClass]
    [DoNotParallelize]
    [TestCategory("LocalHttp2Fixture")]
    public class LocalHttp2DisposalFixtureTests
    {
        private LocalHttp2DisposalFixture fixture;
        private CancellationTokenSource caseBudget;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize()
        {
            this.caseBudget = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            this.TestContext.WriteLine(
                $"OS={RuntimeInformation.OSDescription}; Architecture={RuntimeInformation.ProcessArchitecture}; " +
                $"Runtime={RuntimeInformation.FrameworkDescription}; Host={Environment.ProcessPath}; " +
                $"ASP.NET={typeof(IWebHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}; " +
                "Handler=SocketsHttpHandler; synthetic traffic only.");
        }

        [TestCleanup]
        public async Task CleanupAsync()
        {
            try
            {
                if (this.fixture != null)
                {
                    try
                    {
                        await this.fixture.DisposeAsync();
                        Assert.AreEqual(0, this.fixture.ActiveHandlers);
                        Assert.IsTrue(this.fixture.CleanupCompleted);
                    }
                    finally
                    {
                        this.TestContext.WriteLine(string.Join(Environment.NewLine, this.fixture.Journal));
                    }
                }
            }
            finally
            {
                this.caseBudget.Dispose();
            }
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task Transport_HealthyBaselineAndFollowingRequestRemainCallerOwnedAsync()
        {
            this.fixture = await LocalHttp2DisposalFixture.CreateAsync(this.caseBudget.Token);
            Exchange baseline = this.fixture.Plan("baseline");
            using ObservedResponse response = await this.fixture.SendAsync(baseline, this.caseBudget.Token);
            await this.AssertHealthyAsync(response);
            Assert.IsFalse(response.Witness.IsCompleted, "Reading success must not dispose caller-owned content.");
            response.Dispose();
            Assert.IsTrue(response.Witness.IsCompleted);

            Exchange following = this.fixture.Plan("following");
            using ObservedResponse next = await this.fixture.SendAsync(following, this.caseBudget.Token);
            await this.AssertHealthyAsync(next);
            Assert.AreEqual(2, this.fixture.SendCount);
            Assert.AreEqual(2, this.fixture.ServerRequestCount);
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task Transport_Unread408DisposalCompletesBeforeNextDispatchAsync()
        {
            this.fixture = await LocalHttp2DisposalFixture.CreateAsync(this.caseBudget.Token);
            using ObservedResponse first = await this.fixture.SendAsync(
                this.fixture.Plan("first", holdBody: true), this.caseBudget.Token);
            await this.AssertUnreadHeldAsync(first);
            Assert.ThrowsException<InvalidOperationException>(first.Exchange.RequireClientRelease);
            first.Dispose();

            using ObservedResponse second = await this.fixture.SendAsync(
                this.fixture.Plan("second", holdBody: true), this.caseBudget.Token, first.Witness);
            await this.AssertUnreadHeldAsync(second);
            second.Dispose();

            using ObservedResponse success = await this.fixture.SendAsync(
                this.fixture.Plan("success"), this.caseBudget.Token, second.Witness);
            await this.AssertHealthyAsync(success);
            Assert.IsFalse(success.Witness.IsCompleted);
            Assert.AreEqual(3, this.fixture.SendCount);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Dispatch:first", "Headers:first", "Disposed:first",
                    "Dispatch:second", "Headers:second", "Disposed:second",
                    "Dispatch:success", "Headers:success",
                },
                this.fixture.Journal.Where(IsClientEvent).ToArray());

            // Peer abort is eventual, not a prerequisite for the next dispatch.
            await this.fixture.RequireReleasedAsync(first.Exchange);
            await this.fixture.RequireReleasedAsync(second.Exchange);
            Assert.AreEqual(0, first.Content.ReadCalls);
            Assert.AreEqual(0, second.Content.ReadCalls);

            using ObservedResponse following = await this.fixture.SendAsync(
                this.fixture.Plan("following"), this.caseBudget.Token);
            await this.AssertHealthyAsync(following);
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task Transport_CancellationAfterCompletedDisposalDoesNotDispatchAsync()
        {
            this.fixture = await LocalHttp2DisposalFixture.CreateAsync(this.caseBudget.Token);
            using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(this.caseBudget.Token);
            using ObservedResponse first = await this.fixture.SendAsync(
                this.fixture.Plan("cancelled", holdBody: true), caller.Token, onDisposed: caller.Cancel);
            await this.AssertUnreadHeldAsync(first);
            first.Dispose();
            Assert.IsTrue(first.Witness.IsCompleted);

            await AssertCancellationAsync(
                () => this.fixture.SendAsync(this.fixture.Plan("forbidden"), caller.Token, first.Witness),
                caller.Token);
            Assert.AreEqual(1, this.fixture.SendCount);
            Assert.AreEqual(1, this.fixture.ServerRequestCount);
            await this.fixture.RequireReleasedAsync(first.Exchange);

            using ObservedResponse following = await this.fixture.SendAsync(
                this.fixture.Plan("following"), this.caseBudget.Token);
            await this.AssertHealthyAsync(following);
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task Transport_PreCancelledCallerDoesNotDispatchAsync()
        {
            this.fixture = await LocalHttp2DisposalFixture.CreateAsync(this.caseBudget.Token);
            using CancellationTokenSource caller = new CancellationTokenSource();
            caller.Cancel();
            await AssertCancellationAsync(
                () => this.fixture.SendAsync(this.fixture.Plan("forbidden"), caller.Token), caller.Token);
            Assert.AreEqual(0, this.fixture.SendCount);
            Assert.AreEqual(0, this.fixture.ServerRequestCount);

            using ObservedResponse following = await this.fixture.SendAsync(
                this.fixture.Plan("following"), this.caseBudget.Token);
            await this.AssertHealthyAsync(following);
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task Transport_HeldBodyReadCancellationPreservesContentOwnershipAsync()
        {
            this.fixture = await LocalHttp2DisposalFixture.CreateAsync(this.caseBudget.Token);
            using ObservedResponse response = await this.fixture.SendAsync(
                this.fixture.Plan("read-cancel", holdBody: true), this.caseBudget.Token);
            await this.AssertUnreadHeldAsync(response);
            using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(this.caseBudget.Token);
            Stream stream = await response.Content.ReadAsStreamAsync(caller.Token);
            Task<int> read = stream.ReadAsync(new byte[1].AsMemory(), caller.Token).AsTask();
            Assert.IsFalse(read.IsCompleted, "The held server must not have completed or supplied body bytes.");
            caller.Cancel();
            await AssertCancellationAsync(() => read, caller.Token);
            Assert.IsFalse(response.Witness.IsCompleted, "A cancelled stream read is not content disposal.");
            response.Dispose();
            Assert.IsTrue(response.Witness.IsCompleted);
            await this.fixture.RequireReleasedAsync(response.Exchange);
            Assert.AreEqual(1, this.fixture.SendCount);

            using ObservedResponse following = await this.fixture.SendAsync(
                this.fixture.Plan("following"), this.caseBudget.Token);
            await this.AssertHealthyAsync(following);
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task Transport_DeferredCleanupIsRejectedBeforeDispatchAndRestoredAsync()
        {
            this.fixture = await LocalHttp2DisposalFixture.CreateAsync(this.caseBudget.Token);
            using ObservedResponse first = await this.fixture.SendAsync(
                this.fixture.Plan("deferred", holdBody: true), this.caseBudget.Token);
            await this.AssertUnreadHeldAsync(first);
            Exchange next = this.fixture.Plan("restored");

            InvalidOperationException error = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => this.fixture.SendAsync(next, this.caseBudget.Token, first.Witness));
            StringAssert.Contains(error.Message, "disposal has not completed before dispatch");
            Assert.IsFalse(first.Witness.IsCompleted);
            Assert.IsFalse(first.Witness.Completed.IsCompleted);
            Assert.AreEqual(1, this.fixture.SendCount);
            Assert.AreEqual(1, this.fixture.ServerRequestCount);
            Assert.IsFalse(this.fixture.Journal.Contains("Disposed:deferred"));
            first.Exchange.RequireHeld();
            Assert.ThrowsException<InvalidOperationException>(first.Exchange.RequireClientRelease);

            first.Dispose();
            using ObservedResponse restored = await this.fixture.SendAsync(next, this.caseBudget.Token, first.Witness);
            await this.AssertHealthyAsync(restored);
            await this.fixture.RequireReleasedAsync(first.Exchange);
            Assert.AreEqual(2, this.fixture.SendCount);
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task Transport_RequestedHttp2FallbackIsRejectedAsync()
        {
            this.fixture = await LocalHttp2DisposalFixture.CreateAsync(this.caseBudget.Token, HttpProtocols.Http1);
            using ObservedResponse response = await this.fixture.SendAsync(
                this.fixture.Plan("http1-control"),
                this.caseBudget.Token,
                versionPolicy: HttpVersionPolicy.RequestVersionOrLower);
            Assert.AreEqual(HttpVersion.Version11, response.Message.Version);
            Assert.AreEqual("HTTP/1.1", response.Exchange.Protocol);
            InvalidOperationException error = Assert.ThrowsException<InvalidOperationException>(
                () => LocalHttp2DisposalFixture.RequireHttp2(response));
            StringAssert.Contains(error.Message, "HTTP/2 negotiation not proven");
            Assert.AreEqual(response.Exchange.Payload, await response.Content.ReadAsStringAsync(this.caseBudget.Token));
            Assert.IsFalse(response.Witness.IsCompleted);
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task Transport_ShutdownDoesNotFabricateMeasuredClientReleaseAsync()
        {
            this.fixture = await LocalHttp2DisposalFixture.CreateAsync(this.caseBudget.Token);
            ObservedResponse response = await this.fixture.SendAsync(
                this.fixture.Plan("shutdown", holdBody: true), this.caseBudget.Token);
            await this.AssertUnreadHeldAsync(response);
            Assert.ThrowsException<InvalidOperationException>(response.Exchange.RequireClientRelease);

            await this.fixture.DisposeAsync();
            Assert.IsTrue(this.fixture.CleanupCompleted);
            Assert.AreEqual(0, this.fixture.ActiveHandlers);
            Assert.IsTrue(response.Exchange.Exited.IsCompletedSuccessfully);
            Assert.AreEqual("FixtureShutdown", response.Exchange.ReleaseReason);
            Assert.ThrowsException<InvalidOperationException>(response.Exchange.RequireClientRelease);
            Assert.AreEqual(1, this.fixture.SendCount);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Observer_ReadStreamPreservesHeadersBodyAndOwnershipAsync(bool asynchronous)
        {
            ConcurrentQueue<string> journal = new ConcurrentQueue<string>();
            using MemoryStream payload = new MemoryStream(Encoding.UTF8.GetBytes("synthetic-body"));
            using SentinelContent inner = new SentinelContent(payload);
            inner.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            inner.Headers.ContentLength = payload.Length;
            inner.Headers.Add("x-synthetic", "preserved");
            DisposalWitness witness = new DisposalWitness("body", journal, () => Assert.IsTrue(inner.DisposeReturned));
            using DisposalObservedContent observed = new DisposalObservedContent(inner, witness);
            Assert.AreEqual("application/octet-stream", observed.Headers.ContentType.MediaType);
            Assert.AreEqual(payload.Length, observed.Headers.ContentLength);
            CollectionAssert.AreEqual(new[] { "preserved" }, observed.Headers.GetValues("x-synthetic").ToArray());
            Assert.AreEqual(0, observed.ReadCalls, "Construction must not consume content.");

            Stream stream = asynchronous
                ? await observed.ReadAsStreamAsync(this.caseBudget.Token)
                : observed.ReadAsStream(this.caseBudget.Token);
            Assert.AreSame(payload, stream, "Return the actual stream, not a buffered replacement.");
            Assert.AreEqual(this.caseBudget.Token, inner.LastToken);
            byte[] bytes = new byte[payload.Length];
            int count = asynchronous
                ? await stream.ReadAsync(bytes.AsMemory(), this.caseBudget.Token)
                : stream.Read(bytes, 0, bytes.Length);
            Assert.AreEqual(bytes.Length, count);
            Assert.AreEqual("synthetic-body", Encoding.UTF8.GetString(bytes));
            Assert.IsFalse(witness.IsCompleted);
            stream.Dispose();
            Assert.IsFalse(witness.IsCompleted, "Stream disposal alone must not claim completed content disposal.");
            observed.Dispose();
            observed.Dispose();
            Assert.IsTrue(witness.IsCompleted);
            Assert.AreEqual(1, inner.DisposeCalls);
            CollectionAssert.AreEqual(new[] { "Disposed:body" }, journal.ToArray());
            Assert.ThrowsException<ObjectDisposedException>(() => observed.ReadAsStream());
        }

        [TestMethod]
        public async Task Observer_CopyPreservesBodyAndCancellationAsync()
        {
            using SentinelContent inner = new SentinelContent(new MemoryStream(Encoding.UTF8.GetBytes("copy-body")));
            DisposalWitness witness = new DisposalWitness("copy", new ConcurrentQueue<string>());
            using DisposalObservedContent observed = new DisposalObservedContent(inner, witness);
            using MemoryStream copied = new MemoryStream();
            await observed.CopyToAsync(copied, this.caseBudget.Token);
            Assert.AreEqual("copy-body", Encoding.UTF8.GetString(copied.ToArray()));
            Assert.AreEqual(this.caseBudget.Token, inner.LastToken);
            Assert.IsNull(observed.Headers.ContentLength);
            Assert.IsFalse(witness.IsCompleted);

            using CancellationTokenSource caller = new CancellationTokenSource();
            caller.Cancel();
            await AssertCancellationAsync(() => observed.CopyToAsync(copied, caller.Token), caller.Token);
            Assert.AreEqual(caller.Token, inner.LastToken);
            Assert.IsFalse(witness.IsCompleted);
            observed.Dispose();
            Assert.IsTrue(witness.IsCompleted);
        }

        [TestMethod]
        public async Task Observer_CopyErrorIsNotReplacedOrSwallowedAsync()
        {
            InvalidOperationException sentinel = new InvalidOperationException("Synthetic copy failure.");
            using SentinelContent inner = new SentinelContent(new MemoryStream(Encoding.UTF8.GetBytes("restored-copy")))
            {
                CopyFailure = sentinel,
            };
            DisposalWitness witness = new DisposalWitness("copy-error", new ConcurrentQueue<string>());
            using DisposalObservedContent observed = new DisposalObservedContent(inner, witness);
            using MemoryStream target = new MemoryStream();
            Exception actual = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => observed.CopyToAsync(target, this.caseBudget.Token));
            Assert.AreSame(sentinel, actual);
            Assert.IsFalse(witness.IsCompleted);

            inner.CopyFailure = null;
            await observed.CopyToAsync(target, this.caseBudget.Token);
            Assert.AreEqual("restored-copy", Encoding.UTF8.GetString(target.ToArray()));
            observed.Dispose();
            Assert.IsTrue(witness.IsCompleted);
        }

        [TestMethod]
        public async Task Observer_StreamCreationCancellationIsForwardedAsync()
        {
            using SentinelContent inner = new SentinelContent(new MemoryStream());
            DisposalWitness witness = new DisposalWitness("stream-cancel", new ConcurrentQueue<string>());
            using DisposalObservedContent observed = new DisposalObservedContent(inner, witness);
            using CancellationTokenSource caller = new CancellationTokenSource();
            caller.Cancel();
            await AssertCancellationAsync(async () => await observed.ReadAsStreamAsync(caller.Token), caller.Token);
            Assert.AreEqual(caller.Token, inner.LastToken);
            Assert.IsFalse(witness.IsCompleted);
            Assert.ThrowsException<OperationCanceledException>(() => observed.ReadAsStream(caller.Token));
            observed.Dispose();
            Assert.IsTrue(witness.IsCompleted);
        }

        [TestMethod]
        public async Task Observer_StreamReadErrorAndCancellationAreForwardedAsync()
        {
            InvalidOperationException sentinel = new InvalidOperationException("Synthetic read failure.");
            using ControlledReadStream payload = new ControlledReadStream(sentinel);
            using SentinelContent inner = new SentinelContent(payload);
            DisposalWitness witness = new DisposalWitness("stream-read", new ConcurrentQueue<string>());
            using DisposalObservedContent observed = new DisposalObservedContent(inner, witness);
            Stream stream = await observed.ReadAsStreamAsync(this.caseBudget.Token);
            Exception actual = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => stream.ReadAsync(new byte[1].AsMemory(), this.caseBudget.Token).AsTask());
            Assert.AreSame(sentinel, actual);
            Assert.IsFalse(witness.IsCompleted);

            payload.Failure = null;
            using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(this.caseBudget.Token);
            Task<int> read = stream.ReadAsync(new byte[1].AsMemory(), caller.Token).AsTask();
            await payload.ReadEntered.WaitAsync(LocalHttp2DisposalFixture.ExchangeBudget, this.caseBudget.Token);
            Assert.IsFalse(read.IsCompleted);
            caller.Cancel();
            await AssertCancellationAsync(() => read, caller.Token);
            Assert.IsFalse(witness.IsCompleted);
            observed.Dispose();
            Assert.IsTrue(witness.IsCompleted);
        }

        [TestMethod]
        public void Observer_DisposalWitnessFollowsUnderlyingReturnAndIsNotInvented()
        {
            ConcurrentQueue<string> journal = new ConcurrentQueue<string>();
            DisposalWitness witness = new DisposalWitness("ordered", journal);
            using SentinelContent inner = new SentinelContent(new MemoryStream())
            {
                OnDispose = () =>
                {
                    Assert.IsFalse(witness.IsCompleted);
                    journal.Enqueue("InnerDispose:entered");
                },
            };
            using DisposalObservedContent observed = new DisposalObservedContent(inner, witness);
            Assert.ThrowsException<InvalidOperationException>(witness.RequireCompleted);
            Assert.IsFalse(witness.Completed.IsCompleted);
            observed.Dispose();
            Assert.IsTrue(inner.DisposeReturned);
            witness.RequireCompleted();
            CollectionAssert.AreEqual(new[] { "InnerDispose:entered", "Disposed:ordered" }, journal.ToArray());
        }

        [TestMethod]
        public void Observer_DisposalFailureCannotPublishCompletionAndPositiveControlRestores()
        {
            InvalidOperationException sentinel = new InvalidOperationException("Synthetic disposal failure.");
            ConcurrentQueue<string> journal = new ConcurrentQueue<string>();
            using SentinelContent inner = new SentinelContent(new MemoryStream()) { DisposeFailure = sentinel };
            DisposalWitness witness = new DisposalWitness("failed", journal);
            using DisposalObservedContent observed = new DisposalObservedContent(inner, witness);
            Exception actual = Assert.ThrowsException<InvalidOperationException>(observed.Dispose);
            Assert.AreSame(sentinel, actual);
            Assert.IsFalse(witness.IsCompleted);
            Assert.IsFalse(witness.Completed.IsCompleted);
            Assert.ThrowsException<InvalidOperationException>(witness.RequireCompleted);
            Assert.AreEqual(0, journal.Count);
            inner.DisposeFailure = null;
            inner.Dispose();
            Assert.IsFalse(witness.IsCompleted, "Later cleanup cannot retroactively repair the failed observation.");

            using SentinelContent restoredInner = new SentinelContent(new MemoryStream());
            DisposalWitness restoredWitness = new DisposalWitness("restored", journal);
            using DisposalObservedContent restored = new DisposalObservedContent(restoredInner, restoredWitness);
            restored.Dispose();
            restoredWitness.RequireCompleted();
            CollectionAssert.AreEqual(new[] { "Disposed:restored" }, journal.ToArray());
        }

        [TestMethod]
        public void Observer_CertificateRetainsUsablePrivateKey()
        {
            using X509Certificate2 certificate = LocalHttp2DisposalFixture.CreateCertificate();
            Assert.IsTrue(certificate.HasPrivateKey);
            Assert.AreEqual("CN=localhost", certificate.Subject);
            using RSA privateKey = certificate.GetRSAPrivateKey();
            using RSA publicKey = certificate.GetRSAPublicKey();
            byte[] data = Encoding.ASCII.GetBytes("synthetic-certificate-control");
            byte[] signature = privateKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            Assert.IsTrue(publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            data[0] ^= 1;
            Assert.IsFalse(publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }

        [TestMethod]
        public void Observer_ServerErrorsRetainOnlyExceptionTypesAndCodes()
        {
            ConcurrentQueue<string> journal = new ConcurrentQueue<string>();
            using LocalHttp2DisposalFixture.KestrelErrorJournal provider =
                new LocalHttp2DisposalFixture.KestrelErrorJournal(journal);
            Exception native = new Win32Exception(unchecked((int)0x8009030E), "Do not retain exception messages.");
            Exception authentication = new AuthenticationException("Do not retain certificate details.", native);
            ILogger logger = provider.CreateLogger("Microsoft.AspNetCore.Server.Kestrel.Https");
            logger.LogDebug(authentication, "Do not retain arbitrary state: {Payload}", "synthetic-secret");
            CollectionAssert.AreEqual(
                new[]
                {
                    $"ServerError:AuthenticationException:0x{authentication.HResult:X8}",
                    $"ServerError:Win32Exception:0x{native.HResult:X8}:Native=0x8009030E",
                },
                journal.ToArray());
            logger.LogDebug("No exception.");
            provider.CreateLogger("Unrelated.Category").LogError(authentication, "Do not retain this.");
            Assert.AreEqual(2, journal.Count);
            Assert.IsFalse(string.Join(",", journal).Contains("synthetic-secret", StringComparison.Ordinal));
        }

        private static bool IsClientEvent(string entry)
        {
            return entry.StartsWith("Dispatch:", StringComparison.Ordinal) ||
                entry.StartsWith("Headers:", StringComparison.Ordinal) ||
                entry.StartsWith("Disposed:", StringComparison.Ordinal);
        }

        private static async Task AssertCancellationAsync(Func<Task> action, CancellationToken expectedToken)
        {
            OperationCanceledException cancellation = null;
            try
            {
                await action();
            }
            catch (OperationCanceledException error)
            {
                cancellation = error;
            }

            Assert.IsNotNull(cancellation, "Cancellation must propagate rather than return success.");
            Assert.AreEqual(expectedToken, cancellation.CancellationToken);
        }

        private async Task AssertHealthyAsync(ObservedResponse response)
        {
            LocalHttp2DisposalFixture.RequireHttp2(response);
            Assert.AreEqual(HttpStatusCode.OK, response.Message.StatusCode);
            Assert.AreEqual(response.Exchange.Id, response.Message.Headers.GetValues("x-local-fixture").Single());
            Assert.IsFalse(response.Witness.IsCompleted);
            Assert.AreEqual(response.Exchange.Payload, await response.Content.ReadAsStringAsync(this.caseBudget.Token));
        }

        private async Task AssertUnreadHeldAsync(ObservedResponse response)
        {
            LocalHttp2DisposalFixture.RequireHttp2(response);
            await this.fixture.RequireHeldAsync(response);
            Assert.AreEqual(HttpStatusCode.RequestTimeout, response.Message.StatusCode);
            Assert.IsNull(response.Content.Headers.ContentLength);
            Assert.AreEqual(0, response.Content.ReadCalls);
            Assert.IsFalse(response.Witness.IsCompleted);
            Assert.IsFalse(response.Exchange.Aborted.IsCompleted);
            Assert.IsFalse(response.Exchange.Exited.IsCompleted);
        }

        private sealed class SentinelContent : HttpContent
        {
            private readonly Stream payload;

            public SentinelContent(Stream payload)
            {
                this.payload = payload;
            }

            public Exception CopyFailure { get; set; }

            public Exception DisposeFailure { get; set; }

            public Action OnDispose { get; set; }

            public CancellationToken LastToken { get; private set; }

            public int DisposeCalls { get; private set; }

            public bool DisposeReturned { get; private set; }

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }

            protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
            {
                this.LastToken = cancellationToken;
                cancellationToken.ThrowIfCancellationRequested();
                return this.payload;
            }

            protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(this.CreateContentReadStream(cancellationToken));
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return this.SerializeToStreamAsync(stream, context, CancellationToken.None);
            }

            protected override Task SerializeToStreamAsync(
                Stream stream,
                TransportContext context,
                CancellationToken cancellationToken)
            {
                this.LastToken = cancellationToken;
                cancellationToken.ThrowIfCancellationRequested();
                return this.CopyFailure != null
                    ? Task.FromException(this.CopyFailure)
                    : this.payload.CopyToAsync(stream, cancellationToken);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !this.DisposeReturned)
                {
                    this.DisposeCalls++;
                    this.OnDispose?.Invoke();
                    if (this.DisposeFailure != null)
                    {
                        throw this.DisposeFailure;
                    }

                    this.payload.Dispose();
                }

                base.Dispose(disposing);
                this.DisposeReturned = true;
            }
        }

        private sealed class ControlledReadStream : MemoryStream
        {
            private readonly TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<int> pending = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public ControlledReadStream(Exception failure)
            {
                this.Failure = failure;
            }

            public Exception Failure { get; set; }

            public Task ReadEntered => this.entered.Task;

            public override async ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                if (this.Failure != null)
                {
                    throw this.Failure;
                }

                this.entered.TrySetResult(true);
                return await this.pending.Task.WaitAsync(cancellationToken);
            }
        }
    }
}
//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Connections.Features;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Hosting.Server.Features;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
    using Microsoft.Extensions.Logging;

    internal sealed class LocalHttp2DisposalFixture : IAsyncDisposable
    {
        internal static readonly TimeSpan ExchangeBudget = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ReadinessBudget = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan ReleaseBudget = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan CleanupBudget = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CaseBudget = TimeSpan.FromSeconds(120);
        private readonly ConcurrentDictionary<string, Exchange> exchanges = new ConcurrentDictionary<string, Exchange>();
        private readonly ConcurrentBag<Task> handlers = new ConcurrentBag<Task>();
        private readonly ConcurrentBag<HttpResponseMessage> responses = new ConcurrentBag<HttpResponseMessage>();
        private readonly TaskCompletionSource<bool> stopping = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource deadline;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly X509Certificate2 certificate;
        private readonly IWebHost host;
        private HttpClient client;
        private Task startup = Task.CompletedTask;
        private Task cleanup;
        private int sends;
        private int serverRequests;

        private LocalHttp2DisposalFixture(HttpProtocols protocols, CancellationToken cancellationToken)
        {
            this.deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.deadline.CancelAfter(CaseBudget);
            try
            {
                this.certificate = CreateCertificate();
                this.host = new WebHostBuilder()
                    .UseKestrel(options => options.Listen(
                        IPAddress.Loopback,
                        0,
                        listen =>
                        {
                            listen.Protocols = protocols;
                            listen.UseHttps(this.certificate);
                        }))
                    .ConfigureLogging(logging => logging.ClearProviders())
                    .Configure(app => app.Run(context =>
                    {
                        Task handler = this.HandleAsync(context);
                        this.handlers.Add(handler);
                        return handler;
                    }))
                    .Build();
            }
            catch
            {
                this.certificate?.Dispose();
                this.deadline.Dispose();
                throw;
            }
        }

        public ConcurrentQueue<string> Journal { get; } = new ConcurrentQueue<string>();

        public Uri Endpoint { get; private set; }

        public int SendCount => Volatile.Read(ref this.sends);

        public int ServerRequestCount => Volatile.Read(ref this.serverRequests);

        public int ActiveHandlers => this.handlers.Count(task => !task.IsCompleted);

        public bool CleanupCompleted { get; private set; }

        public static async Task<LocalHttp2DisposalFixture> CreateAsync(
            CancellationToken cancellationToken,
            HttpProtocols protocols = HttpProtocols.Http2)
        {
            LocalHttp2DisposalFixture fixture = new LocalHttp2DisposalFixture(protocols, cancellationToken);
            try
            {
                await fixture.StartAsync();
                return fixture;
            }
            catch (Exception startupFailure)
            {
                try
                {
                    await fixture.DisposeAsync();
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Fixture startup and cleanup failed.", startupFailure, cleanupFailure);
                }

                throw;
            }
        }

        public Exchange Plan(string id, bool holdBody = false)
        {
            Exchange exchange = new Exchange(id, holdBody, this.Journal);
            if (!this.exchanges.TryAdd(id, exchange))
            {
                throw new InvalidOperationException("Duplicate synthetic exchange.");
            }

            return exchange;
        }

        public async Task<ObservedResponse> SendAsync(
            Exchange exchange,
            CancellationToken cancellationToken,
            DisposalWitness previous = null,
            Action onDisposed = null,
            HttpVersionPolicy versionPolicy = HttpVersionPolicy.RequestVersionExact)
        {
            cancellationToken.ThrowIfCancellationRequested();
            previous?.RequireCompleted();
            using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(
                this.deadline.Token, cancellationToken);
            budget.CancelAfter(this.Remaining(ExchangeBudget));
            using HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Get, new Uri(this.Endpoint, Uri.EscapeDataString(exchange.Id)))
            {
                Version = HttpVersion.Version20,
                VersionPolicy = versionPolicy,
            };

            Interlocked.Increment(ref this.sends);
            this.Journal.Enqueue("Dispatch:" + exchange.Id);
            HttpResponseMessage response;
            try
            {
                response = await this.client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, budget.Token);
            }
            catch (OperationCanceledException error) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Synthetic caller cancelled.", error, cancellationToken);
            }
            catch (OperationCanceledException error) when (budget.IsCancellationRequested)
            {
                throw new TimeoutException("Local fixture exchange exceeded its finite guard.", error);
            }

            this.responses.Add(response);
            DisposalWitness witness = new DisposalWitness(exchange.Id, this.Journal, onDisposed);
            DisposalObservedContent content = new DisposalObservedContent(response.Content, witness);
            response.Content = content;
            this.Journal.Enqueue("Headers:" + exchange.Id);
            return new ObservedResponse(response, content, witness, exchange);
        }

        public static void RequireHttp2(ObservedResponse response)
        {
            if (response.Message.Version != HttpVersion.Version20 ||
                response.Exchange.Protocol != "HTTP/2" ||
                response.Exchange.ApplicationProtocol != "h2")
            {
                throw new InvalidOperationException(
                    $"HTTP/2 negotiation not proven: client={response.Message.Version}, " +
                    $"server={response.Exchange.Protocol}, ALPN={response.Exchange.ApplicationProtocol}.");
            }
        }

        public async Task RequireHeldAsync(ObservedResponse response)
        {
            await response.Exchange.BodyHeld.WaitAsync(this.Remaining(ExchangeBudget), this.deadline.Token);
            response.Exchange.RequireHeld();
        }

        public async Task RequireReleasedAsync(Exchange exchange)
        {
            await Task.WhenAll(exchange.Aborted, exchange.Exited)
                .WaitAsync(this.Remaining(ReleaseBudget), this.deadline.Token);
            exchange.RequireClientRelease();
        }

        public ValueTask DisposeAsync()
        {
            this.cleanup ??= this.CleanupAsync();
            return new ValueTask(this.cleanup);
        }

        private static X509Certificate2 CreateCertificate()
        {
            using RSA key = RSA.Create(2048);
            CertificateRequest request = new CertificateRequest(
                "CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            SubjectAlternativeNameBuilder names = new SubjectAlternativeNameBuilder();
            names.AddIpAddress(IPAddress.Loopback);
            names.AddDnsName("localhost");
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
            return request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        }

        private async Task StartAsync()
        {
            using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(this.deadline.Token);
            budget.CancelAfter(this.Remaining(ReadinessBudget));
            this.startup = this.host.StartAsync(budget.Token);
            await this.startup.WaitAsync(budget.Token);
            IServerAddressesFeature addresses = this.host.ServerFeatures.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("Kestrel did not report its bound address.");
            this.Endpoint = new Uri(addresses.Addresses.Single());
            if (this.Endpoint.Scheme != Uri.UriSchemeHttps ||
                this.Endpoint.Host != IPAddress.Loopback.ToString() ||
                this.Endpoint.Port == 0)
            {
                throw new InvalidOperationException("Kestrel did not bind the required ephemeral TLS loopback endpoint.");
            }

            SocketsHttpHandler sockets = new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                ConnectTimeout = ReadinessBudget,
                ConnectCallback = this.ConnectAsync,
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (sender, peer, chain, errors) =>
                        peer != null &&
                        peer.GetRawCertData().AsSpan().SequenceEqual(this.certificate.RawData) &&
                        (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None &&
                        DateTime.UtcNow >= this.certificate.NotBefore.ToUniversalTime() &&
                        DateTime.UtcNow <= this.certificate.NotAfter.ToUniversalTime(),
                },
            };

            this.client = new HttpClient(sockets) { Timeout = Timeout.InfiniteTimeSpan };
            this.Journal.Enqueue("Ready:TLS:loopback");
        }

        private async ValueTask<Stream> ConnectAsync(
            SocketsHttpConnectionContext context,
            CancellationToken cancellationToken)
        {
            if (context.DnsEndPoint.Host != this.Endpoint.Host || context.DnsEndPoint.Port != this.Endpoint.Port)
            {
                throw new InvalidOperationException("The fixture client cannot connect outside its exact loopback endpoint.");
            }

            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, this.Endpoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private async Task HandleAsync(HttpContext context)
        {
            Interlocked.Increment(ref this.serverRequests);
            string id = context.Request.Path.Value.TrimStart('/');
            if (!this.exchanges.TryGetValue(id, out Exchange exchange) ||
                context.Connection.RemoteIpAddress == null ||
                !IPAddress.IsLoopback(context.Connection.RemoteIpAddress))
            {
                throw new InvalidOperationException("Unexpected local fixture request.");
            }

            ITlsApplicationProtocolFeature alpn = context.Features.Get<ITlsApplicationProtocolFeature>();
            ITlsHandshakeFeature tls = context.Features.Get<ITlsHandshakeFeature>();
            exchange.Begin(context.Request.Protocol, alpn == null ? string.Empty : Encoding.ASCII.GetString(alpn.ApplicationProtocol.Span));
            this.Journal.Enqueue($"Protocol:{id}:{exchange.Protocol}:ALPN={exchange.ApplicationProtocol}:TLS={tls?.Protocol}");
            using CancellationTokenRegistration aborted = context.RequestAborted.Register(exchange.ObserveAbort);
            string reason = "Fault";
            try
            {
                context.Response.StatusCode = exchange.HoldBody ? StatusCodes.Status408RequestTimeout : StatusCodes.Status200OK;
                context.Response.ContentType = "application/octet-stream";
                context.Response.Headers.Add("x-local-fixture", id);
                if (exchange.HoldBody)
                {
                    await context.Response.StartAsync(context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                    exchange.MarkHeld();
                    await Task.WhenAny(exchange.Aborted, this.stopping.Task);
                    reason = this.stopping.Task.IsCompleted ? "FixtureShutdown" : "ClientAbort";
                }
                else
                {
                    byte[] payload = Encoding.UTF8.GetBytes(exchange.Payload);
                    context.Response.ContentLength = payload.Length;
                    await context.Response.Body.WriteAsync(payload, context.RequestAborted);
                    reason = "Success";
                }
            }
            catch (OperationCanceledException) when (this.stopping.Task.IsCompleted)
            {
                reason = "FixtureShutdown";
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                reason = "ClientAbort";
            }
            finally
            {
                exchange.Finish(reason);
            }
        }

        private TimeSpan Remaining(TimeSpan cap)
        {
            TimeSpan remaining = CaseBudget - this.clock.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("Local fixture whole-case guard expired.");
            }

            return remaining < cap ? remaining : cap;
        }

        private async Task CleanupAsync()
        {
            List<Exception> failures = new List<Exception>();
            this.Journal.Enqueue("Cleanup:begin");
            this.stopping.TrySetResult(true);
            try
            {
                this.deadline.Cancel();
            }
            catch (Exception error)
            {
                failures.Add(error);
            }
            using CancellationTokenSource budget = new CancellationTokenSource();
            try
            {
                budget.CancelAfter(this.Remaining(CleanupBudget));
            }
            catch (TimeoutException error)
            {
                failures.Add(error);
                budget.Cancel();
            }

            try
            {
                await this.startup.WaitAsync(budget.Token);
            }
            catch (Exception error)
            {
                failures.Add(error);
            }

            try
            {
                await this.host.StopAsync(budget.Token).WaitAsync(budget.Token);
            }
            catch (Exception error)
            {
                failures.Add(error);
            }

            try
            {
                await Task.WhenAll(this.handlers.ToArray()).WaitAsync(budget.Token);
            }
            catch (Exception error)
            {
                failures.Add(error);
            }

            foreach (HttpResponseMessage response in this.responses)
            {
                try
                {
                    response.Dispose();
                }
                catch (Exception error)
                {
                    failures.Add(error);
                }
            }

            try
            {
                this.client?.Dispose();
            }
            catch (Exception error)
            {
                failures.Add(error);
            }

            try
            {
                this.host.Dispose();
            }
            catch (Exception error)
            {
                failures.Add(error);
            }
            finally
            {
                try
                {
                    this.certificate.Dispose();
                }
                catch (Exception error)
                {
                    failures.Add(error);
                }
                finally
                {
                    this.deadline.Dispose();
                }
            }

            if (this.ActiveHandlers != 0)
            {
                failures.Add(new TimeoutException("Fixture cleanup left an owned server handler running."));
            }

            if (failures.Count != 0)
            {
                this.Journal.Enqueue("Cleanup:failed");
                throw new AggregateException("Local fixture cleanup failed.", failures);
            }

            this.CleanupCompleted = true;
            this.Journal.Enqueue("Cleanup:complete");
        }

        internal sealed class ObservedResponse : IDisposable
        {
            public ObservedResponse(
                HttpResponseMessage message,
                DisposalObservedContent content,
                DisposalWitness witness,
                Exchange exchange)
            {
                this.Message = message;
                this.Content = content;
                this.Witness = witness;
                this.Exchange = exchange;
            }

            public HttpResponseMessage Message { get; }

            public DisposalObservedContent Content { get; }

            public DisposalWitness Witness { get; }

            public Exchange Exchange { get; }

            public void Dispose()
            {
                this.Message.Dispose();
            }
        }

        internal sealed class Exchange
        {
            private readonly ConcurrentQueue<string> journal;
            private readonly TaskCompletionSource<bool> bodyHeld = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> aborted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> exited = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public Exchange(string id, bool holdBody, ConcurrentQueue<string> journal)
            {
                this.Id = id;
                this.HoldBody = holdBody;
                this.journal = journal;
            }

            public string Id { get; }

            public bool HoldBody { get; }

            public string Payload => "synthetic:" + this.Id;

            public string Protocol { get; private set; }

            public string ApplicationProtocol { get; private set; }

            public string ReleaseReason { get; private set; }

            public Task BodyHeld => this.bodyHeld.Task;

            public Task Aborted => this.aborted.Task;

            public Task Exited => this.exited.Task;

            public void RequireHeld()
            {
                if (!this.BodyHeld.IsCompletedSuccessfully || this.Aborted.IsCompleted || this.Exited.IsCompleted)
                {
                    throw new InvalidOperationException("The failed response is not an incomplete held stream.");
                }
            }

            public void RequireClientRelease()
            {
                if (!this.Aborted.IsCompletedSuccessfully ||
                    !this.Exited.IsCompletedSuccessfully ||
                    this.ReleaseReason != "ClientAbort")
                {
                    throw new InvalidOperationException("Actual client abort and server handler exit have not both been observed.");
                }
            }

            internal void Begin(string protocol, string applicationProtocol)
            {
                this.Protocol = protocol;
                this.ApplicationProtocol = applicationProtocol;
            }

            internal void MarkHeld()
            {
                this.journal.Enqueue("Held:" + this.Id);
                this.bodyHeld.SetResult(true);
            }

            internal void ObserveAbort()
            {
                this.journal.Enqueue("Abort:" + this.Id);
                this.aborted.TrySetResult(true);
            }

            internal void Finish(string reason)
            {
                this.ReleaseReason = reason;
                this.journal.Enqueue("ServerExit:" + this.Id + ":" + reason);
                this.exited.SetResult(true);
            }
        }
    }
}
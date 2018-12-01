//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RntbdTests
    {
        [TestMethod]
        public void DisposeNewChannelTest()
        {
            Guid activityId = Guid.NewGuid();
            Trace.CorrelationManager.ActivityId = activityId;
            // Assuming that this machine isn't running SMTP.
            using (Rntbd.Channel channel = new Rntbd.Channel(
                new Uri("rntbd://localhost:25"),
                new Rntbd.ChannelProperties(
                    new UserAgentContainer(),
                    certificateHostNameOverride: null,
                    timerPool: new TimerPool(1),
                    requestTimeout: TimeSpan.FromSeconds(1.0),
                    openTimeout: TimeSpan.FromSeconds(1.0),
                    maxChannels: ushort.MaxValue,
                    maxRequestsPerChannel: 100,
                    receiveHangDetectionTime: TimeSpan.FromSeconds(2.0),
                    sendHangDetectionTime: TimeSpan.FromSeconds(0.5))))
            {
            }
        }

        [TestMethod]
        public void DisposeInitializeFailedChannelTest()
        {
            Stopwatch sw = Stopwatch.StartNew();
            TimeSpan runTime = TimeSpan.FromSeconds(10);
            while (sw.Elapsed < runTime)
            {
                Guid activityId = Guid.NewGuid();
                Trace.CorrelationManager.ActivityId = activityId;
                // Assuming that this machine isn't running SMTP.
                using (Rntbd.Channel channel = new Rntbd.Channel(
                    new Uri("rntbd://localhost:25"),
                    new Rntbd.ChannelProperties(
                        new UserAgentContainer(),
                        certificateHostNameOverride: null,
                        timerPool: new TimerPool(1),
                        requestTimeout: TimeSpan.FromSeconds(1.0),
                        openTimeout: TimeSpan.FromSeconds(1.0),
                        maxChannels: ushort.MaxValue,
                        maxRequestsPerChannel: 100,
                        receiveHangDetectionTime: TimeSpan.FromSeconds(2.0),
                        sendHangDetectionTime: TimeSpan.FromSeconds(0.5))))
                {
                    channel.Initialize(activityId);
                }
            }
        }

        [TestMethod]
        public async Task DisposeInitializeFailedChannelAsyncTest()
        {
            const int msPerBucket = 50;
            const int buckets = 2 * 1000 / msPerBucket + 1;
            const int seconds = 10;
            Random r = new Random(RntbdTests.RandomSeed());
            Stopwatch sw = Stopwatch.StartNew();
            TimeSpan runTime = TimeSpan.FromSeconds(seconds);
            while (sw.Elapsed < runTime)
            {
                Guid activityId = Guid.NewGuid();
                Trace.CorrelationManager.ActivityId = activityId;
                // Assuming that this machine isn't running SMTP.
                using (Rntbd.Channel channel = new Rntbd.Channel(
                    new Uri("rntbd://localhost:25"),
                    new Rntbd.ChannelProperties(
                        new UserAgentContainer(),
                        certificateHostNameOverride: null,
                        timerPool: new TimerPool(1),
                        requestTimeout: TimeSpan.FromSeconds(1.0),
                        openTimeout: TimeSpan.FromSeconds(1.0),
                        maxChannels: ushort.MaxValue,
                        maxRequestsPerChannel: 100,
                        receiveHangDetectionTime: TimeSpan.FromSeconds(2.0),
                        sendHangDetectionTime: TimeSpan.FromSeconds(0.5))))
                {
                    channel.Initialize(activityId);
                    await Task.Delay(r.Next(1, buckets) * msPerBucket);
                }
            }
        }

        [TestMethod]
        public async Task CallInitializeFailedChannelAsyncTest()
        {
            TimeSpan totalRunTime = TimeSpan.FromSeconds(15.0);
            TimeSpan channelRunTime = TimeSpan.FromSeconds(5.0);
            Stopwatch outerSw = Stopwatch.StartNew();
            while (outerSw.Elapsed < totalRunTime)
            {
                Guid activityId = Guid.NewGuid();
                Trace.CorrelationManager.ActivityId = activityId;
                using (Rntbd.Channel channel = new Rntbd.Channel(
                    new Uri("rntbd://localhost:25"),
                    new Rntbd.ChannelProperties(
                        new UserAgentContainer(),
                        certificateHostNameOverride: null,
                        timerPool: new TimerPool(1),
                        requestTimeout: TimeSpan.FromSeconds(1.0),
                        openTimeout: TimeSpan.FromSeconds(1.0),
                        maxChannels: ushort.MaxValue,
                        maxRequestsPerChannel: 100,
                        receiveHangDetectionTime: TimeSpan.FromSeconds(2.0),
                        sendHangDetectionTime: TimeSpan.FromSeconds(0.5))))
                {
                    channel.Initialize(activityId);
                    Stopwatch innerSw = Stopwatch.StartNew();
                    while (innerSw.Elapsed < channelRunTime)
                    {
                        try
                        {
                            StoreResponse response = await channel.RequestAsync(
                                DocumentServiceRequest.Create(
                                    OperationType.Read,
                                    ResourceType.Document,
                                    AuthorizationTokenType.SecondaryReadonlyMasterKey),
                                new Uri("rntbd://localhost:25/foo/bar/baz"),
                                ResourceOperation.ReadDocument,
                                activityId);
                        }
                        catch (DocumentClientException e)
                        {
                            Assert.AreEqual(activityId.ToString(), e.ActivityId);
                            Assert.AreEqual("Gone", e.Error.Code);
                            Exception rootCause = e;
                            Exception nextRootCause = e.InnerException;
                            while (nextRootCause != null)
                            {
                                rootCause = nextRootCause;
                                nextRootCause = nextRootCause.InnerException;
                            }
                            SocketException socketException = rootCause as SocketException;
                            if (socketException != null)
                            {
                                if (socketException.SocketErrorCode != SocketError.ConnectionRefused)
                                {
                                    Assert.Fail(
                                        "Expected connection reset or " +
                                        "connection aborted. Actual: {0}",
                                        socketException.SocketErrorCode.ToString());
                                }
                            }
                            else
                            {
                                GoneException goneException = rootCause as GoneException;
                                IOException ioException = rootCause as IOException;
                                Assert.IsTrue(goneException != null || ioException != null,
                                    "Expected GoneException or IOException. Actual: {0} ({1})",
                                    rootCause.Message, rootCause.GetType());
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public async Task ServerCloseOnConnectAsyncTest()
        {
            using (FaultyServer server = new FaultyServer())
            {
                server.OnConnectionEvent += (sender, args) =>
                {
                    if (args.EventType == FaultyServer.ConnectionEventType.ConnectionAccepted)
                    {
                        RntbdTests.RandomSleep();
                        args.Close = true;
                    }
                };
                try
                {
                    await server.StartAsync();
                    Dictionary<OperationType, ResourceOperation> operationMap =
                        new Dictionary<OperationType, ResourceOperation>
                        {
                            [OperationType.Read] = ResourceOperation.ReadDocument,
                            [OperationType.Upsert] = ResourceOperation.UpsertDocument,
                        };
                    foreach (OperationType operationType in new OperationType[]
                    {
                        OperationType.Read,
                        OperationType.Upsert
                    })
                    {
                        TimeSpan runTime = TimeSpan.FromSeconds(10.0);
                        Stopwatch sw = Stopwatch.StartNew();
                        while (sw.Elapsed < runTime)
                        {
                            Guid activityId = Guid.NewGuid();
                            Trace.CorrelationManager.ActivityId = activityId;

                            using (Rntbd.Channel channel = new Rntbd.Channel(
                                server.Uri,
                                new Rntbd.ChannelProperties(
                                    new UserAgentContainer(),
                                    certificateHostNameOverride: "localhost",
                                    timerPool: new TimerPool(1),
                                    requestTimeout: TimeSpan.FromSeconds(1.0),
                                    openTimeout: TimeSpan.FromSeconds(1.0),
                                    maxChannels: ushort.MaxValue,
                                    maxRequestsPerChannel: 100,
                                    receiveHangDetectionTime: TimeSpan.FromSeconds(2.0),
                                    sendHangDetectionTime: TimeSpan.FromSeconds(0.5))))
                            {
                                channel.Initialize(activityId);

                                try
                                {
                                    ResourceType resourceType = ResourceType.Document;
                                    ResourceOperation resourceOperation = operationMap[operationType];
                                    StoreResponse response = await channel.RequestAsync(
                                        DocumentServiceRequest.Create(
                                            operationType,
                                            resourceType,
                                            AuthorizationTokenType.SecondaryReadonlyMasterKey),
                                        new Uri(server.Uri, "foo/bar/baz"),
                                        resourceOperation,
                                        activityId);
                                }
                                catch (DocumentClientException e)
                                {
                                    Assert.AreEqual(activityId.ToString(), e.ActivityId);
                                    Assert.AreEqual("Gone", e.Error.Code);
                                    Exception rootCause = e;
                                    Exception nextRootCause = e.InnerException;
                                    while (nextRootCause != null)
                                    {
                                        rootCause = nextRootCause;
                                        nextRootCause = nextRootCause.InnerException;
                                    }
                                    SocketException socketException = rootCause as SocketException;
                                    if (socketException != null)
                                    {
                                        if (socketException.SocketErrorCode != SocketError.ConnectionReset &&
                                            socketException.SocketErrorCode != SocketError.ConnectionAborted)
                                        {
                                            Assert.Fail(
                                                "Expected connection reset or " +
                                                "connection aborted. Actual: {0}",
                                                socketException.SocketErrorCode.ToString());
                                        }
                                    }
                                    else
                                    {
                                        IOException ioException = rootCause as IOException;
                                        Assert.IsNotNull(ioException,
                                            "Expected IOException. Actual: {0} ({1})",
                                            rootCause.Message, rootCause.GetType());
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    await server.StopAsync();
                }
            }
        }

        [TestMethod]
        public async Task ServerCloseOnSslNegotiationCompleteAsyncTest()
        {
            using (FaultyServer server = new FaultyServer())
            {
                server.OnConnectionEvent += (sender, args) =>
                {
                    if (args.EventType == FaultyServer.ConnectionEventType.SslNegotiationComplete)
                    {
                        RntbdTests.RandomSleep();
                        args.Close = true;
                    }
                };
                try
                {
                    await server.StartAsync();
                    Dictionary<OperationType, ResourceOperation> operationMap =
                        new Dictionary<OperationType, ResourceOperation>
                        {
                            [OperationType.Read] = ResourceOperation.ReadDocument,
                            [OperationType.Upsert] = ResourceOperation.UpsertDocument,
                        };
                    foreach (OperationType operationType in new OperationType[]
                    {
                        OperationType.Read,
                        OperationType.Upsert
                    })
                    {
                        TimeSpan runTime = TimeSpan.FromSeconds(10.0);
                        Stopwatch sw = Stopwatch.StartNew();
                        while (sw.Elapsed < runTime)
                        {
                            Guid activityId = Guid.NewGuid();
                            Trace.CorrelationManager.ActivityId = activityId;

                            using (Rntbd.Channel channel = new Rntbd.Channel(
                                server.Uri,
                                new Rntbd.ChannelProperties(
                                    new UserAgentContainer(),
                                    certificateHostNameOverride: "localhost",
                                    timerPool: new TimerPool(1),
                                    requestTimeout: TimeSpan.FromSeconds(1.0),
                                    openTimeout: TimeSpan.FromSeconds(1.0),
                                    maxChannels: ushort.MaxValue,
                                    maxRequestsPerChannel: 100,
                                    receiveHangDetectionTime: TimeSpan.FromSeconds(2.0),
                                    sendHangDetectionTime: TimeSpan.FromSeconds(0.5))))
                            {
                                channel.Initialize(activityId);

                                try
                                {
                                    ResourceType resourceType = ResourceType.Document;
                                    ResourceOperation resourceOperation = operationMap[operationType];
                                    StoreResponse response = await channel.RequestAsync(
                                        DocumentServiceRequest.Create(
                                            operationType,
                                            resourceType,
                                            AuthorizationTokenType.SecondaryReadonlyMasterKey),
                                        new Uri(server.Uri, "foo/bar/baz"),
                                        resourceOperation,
                                        activityId);
                                }
                                catch (DocumentClientException e)
                                {
                                    Assert.AreEqual(activityId.ToString(), e.ActivityId);
                                    if (!string.Equals("ServiceUnavailable", e.Error.Code) &&
                                        !string.Equals("Gone", e.Error.Code))
                                    {
                                        Assert.Fail("Unexpected error code: {0}", e.Error.Code);
                                    }
                                    Exception rootCause = e;
                                    Exception nextRootCause = e.InnerException;
                                    while (nextRootCause != null)
                                    {
                                        rootCause = nextRootCause;
                                        nextRootCause = nextRootCause.InnerException;
                                    }
                                    SocketException socketException = rootCause as SocketException;
                                    if (socketException != null)
                                    {
                                        if (socketException.SocketErrorCode != SocketError.ConnectionReset &&
                                            socketException.SocketErrorCode != SocketError.ConnectionAborted)
                                        {
                                            Assert.Fail(
                                                "Expected connection reset or " +
                                                "connection aborted. Actual: {0}",
                                                socketException.SocketErrorCode.ToString());
                                        }
                                    }
                                    else
                                    {
                                        GoneException goneException = rootCause as GoneException;
                                        IOException ioException = rootCause as IOException;
                                        Assert.IsTrue(goneException != null || ioException != null,
                                            "Expected GoneException or IOException. Actual: {0} ({1})",
                                            rootCause.Message, rootCause.GetType());
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    await server.StopAsync();
                }
            }
        }

        #region Static helpers

        // Suspends the calling thread for a brief amount of time
        // (between 0 and 3 64 Hz ticks).
        private static void RandomSleep()
        {
            int ticks = 0;
            lock (RntbdTests.randomLock)
            {
                ticks = RntbdTests.random.Next(4);
            }
            if (ticks == 0)
            {
                return;
            }
            Thread.Sleep(TimeSpan.FromMilliseconds(ticks * 1000.0 / 64));
        }

        // Delays the calling task for a brief amount of time
        // (between 0 and 3 64 Hz ticks).
        private static async Task RandomSleepAsync()
        {
            int ticks = 0;
            lock (RntbdTests.randomLock)
            {
                ticks = RntbdTests.random.Next(4);
            }
            if (ticks == 0)
            {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(ticks * 1000.0 / 64));
        }

        private static int RandomSeed()
        {
            return DateTime.UtcNow.Date.Subtract(
                new DateTime(2018, 3, 5, 13, 47, 31, DateTimeKind.Utc)).Milliseconds;
        }

        private static object randomLock = new object();
        // Guarded by randomLock.
        private static Random random = new Random(RntbdTests.RandomSeed());

        #endregion

        #region Faulty server implementation

        // TODO(ovplaton): Move to a separate class if it's useful more broadly.

        private sealed class FaultyServer : IDisposable
        {
            private static readonly TimeSpan gracefulShutdownDelay =
                TimeSpan.FromSeconds(2.0);

            private readonly CancellationTokenSource serverShutdown =
                new CancellationTokenSource();

            private Uri uri = null;
            private TcpListener listener = null;
            private Task acceptLoop = null;

            public class ConnectionEventArgs
            {
                public ConnectionEventArgs(ConnectionEventType t)
                {
                    EventType = t;
                    Close = false;
                }

                public ConnectionEventType EventType { get; private set; }

                public bool Close { get; set; }
            }

            public enum ConnectionEventType
            {
                ConnectionAccepted,
                SslNegotiationComplete,
            }

            public delegate void ConnectionEventDelegate(object sender, ConnectionEventArgs args);

            public event ConnectionEventDelegate OnConnectionEvent;

            public FaultyServer()
            {
            }

            public Uri Uri
            {
                get
                {
                    Debug.Assert(this.uri != null);
                    return this.uri;
                }
            }

            public async Task StartAsync()
            {
                IPAddress localhost = (await Dns.GetHostAddressesAsync("localhost"))[0];
                this.listener = new TcpListener(localhost, 0 /* any port */);
                this.listener.Start(5);
                IPEndPoint serverEndpoint = (IPEndPoint)this.listener.LocalEndpoint;
                UriBuilder uriBuilder = new UriBuilder(
                    "rntbd",
                    serverEndpoint.Address.ToString(),
                    serverEndpoint.Port);
                this.uri = uriBuilder.Uri;
                CancellationToken cancellation = this.serverShutdown.Token;
                this.acceptLoop = Task.Factory.StartNew(
                    async () => { await this.AcceptAsync(cancellation); },
                    serverShutdown.Token,
                    TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Current);
            }

            public async Task StopAsync()
            {
                this.serverShutdown.Cancel();
                this.listener.Stop();
                Task completedTask = await Task.WhenAny(
                    this.acceptLoop, Task.Delay(FaultyServer.gracefulShutdownDelay));
                if (!object.ReferenceEquals(completedTask, this.acceptLoop))
                {
                    Trace.WriteLine("Timed out while waiting for graceful shutdown");
                    // Timeout
                    this.ConsumeException(this.acceptLoop, "accept loop");
                }
            }

            void IDisposable.Dispose()
            {
                if (this.acceptLoop != null &&
                    !this.serverShutdown.IsCancellationRequested)
                {
                    this.StopAsync().Wait();
                }

                this.serverShutdown.Dispose();
                if (this.acceptLoop != null)
                {
                    this.acceptLoop.Wait();
                    this.acceptLoop.Dispose();
                }
            }

            private async Task AcceptAsync(CancellationToken shutdownSignal)
            {
                object connectionCountLock = new object();
                int openConnections = 0;
                TaskCompletionSource<object> allClosed = null;

                try
                {
                    TaskCompletionSource<object> shutdown = new TaskCompletionSource<object>();
                    using (CancellationTokenRegistration shutdownRegistration =
                        shutdownSignal.Register(() => { shutdown.SetResult(null); }))
                    {
                        Task[] tasks = new Task[2];
                        tasks[1] = shutdown.Task;
                        while (!shutdownSignal.IsCancellationRequested)
                        {
                            Task<TcpClient> acceptTask = this.listener.AcceptTcpClientAsync();
                            tasks[0] = acceptTask;
                            Task completedTask = await Task.WhenAny(tasks);
                            if (!object.ReferenceEquals(completedTask, acceptTask))
                            {
                                // Server shutdown
                                this.ConsumeException(acceptTask, "accept");
                                continue;
                            }
                            // New connection
                            TcpClient connection = acceptTask.Result;
                            lock (connectionCountLock)
                            {
                                Debug.Assert(openConnections >= 0);
                                openConnections++;
                            }
                            Task connectionTask = Task.Factory.StartNew(
                                async () =>
                                {
                                    await this.ConnectionLoopAsync(connection, shutdownSignal);
                                },
                                shutdownSignal,
                                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                TaskScheduler.Current);
                            var ignored = connectionTask.ContinueWith(t =>
                            {
                                TaskCompletionSource<object> done = null;
                                lock (connectionCountLock)
                                {
                                    Debug.Assert(openConnections > 0);
                                    openConnections--;
                                    if (openConnections == 0 && allClosed != null)
                                    {
                                        done = allClosed;
                                    }
                                }
                                if (done != null)
                                {
                                    done.SetResult(null);
                                }
                            });
                        }
                    }
                }
                finally
                {
                    Task done = null;
                    lock (connectionCountLock)
                    {
                        Debug.Assert(openConnections >= 0);
                        if (openConnections != 0)
                        {
                            Debug.Assert(allClosed == null);
                            allClosed = new TaskCompletionSource<object>();
                            done = allClosed.Task;
                        }
                    }
                    if (done != null)
                    {
                        await done;
                    }
                }
            }

            private async Task ConnectionLoopAsync(TcpClient connection, CancellationToken shutdownSignal)
            {
                RemoteCertificateValidationCallback verifyRemoteCertificate =
                    (object sender, X509Certificate certificate,
                    X509Chain chain, SslPolicyErrors errors) =>
                    {
                        return true;
                    };
                LocalCertificateSelectionCallback selectLocalCertificate =
                    (object sender, string targetHost,
                    X509CertificateCollection localCertificates,
                    X509Certificate remoteCertificate,
                    string[] acceptableIssuers) =>
                    {
                        if (localCertificates.Count != 1)
                        {
                            throw new ArgumentException(
                                string.Format(
                                    "Unexpected certificate count. Expected 1. Actual: {0}",
                                    localCertificates.Count),
                                nameof(localCertificates));
                        }
                        return localCertificates[0];
                    };
                try
                {
                    using (connection)
                    {
                        if (this.CloseConnection(ConnectionEventType.ConnectionAccepted))
                        {
                            return;
                        }
                        connection.NoDelay = true;
                        connection.ReceiveTimeout = 2 * 60 * 1000;  // ms
                        connection.SendTimeout = 10 * 1000; // ms

                        using (SslStream encryptedStream =
                            new SslStream(connection.GetStream(), false,
                            verifyRemoteCertificate, selectLocalCertificate,
                            EncryptionPolicy.RequireEncryption))
                        {
                            X509Certificate2 cert = LoadTestCertificate();
                            await encryptedStream.AuthenticateAsServerAsync(cert, false, System.Security.Authentication.SslProtocols.Tls12, false);

                            if (this.CloseConnection(ConnectionEventType.SslNegotiationComplete))
                            {
                                return;
                            }

                            byte[] readBuffer = new byte[16 * 1024];
                            int totalAvailable = 0;
                            while (!shutdownSignal.IsCancellationRequested)
                            {
                                int bytesRead = await encryptedStream.ReadAsync(readBuffer, totalAvailable, readBuffer.Length - totalAvailable);
                                if (bytesRead == 0)
                                {
                                    Trace.WriteLine("Server: Connection closed by peer");
                                    return;
                                }
                                totalAvailable += bytesRead;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.Message);
                    throw;
                }
            }

            private bool CloseConnection(ConnectionEventType eventType)
            {
                if (this.OnConnectionEvent == null)
                {
                    return false;
                }
                ConnectionEventArgs args = new ConnectionEventArgs(eventType);
                this.OnConnectionEvent(this, args);
                return args.Close;
            }

            private static X509Certificate2 LoadTestCertificate()
            {
                string thumbprint = "174612E096C8535D71F90E4B47F61BA1EE8D810B";
                X509Certificate2 cert = LoadCertificateByThumbprint(thumbprint, StoreLocation.CurrentUser);
                if (cert != null)
                {
                    return cert;
                }
                cert = LoadCertificateByThumbprint(thumbprint, StoreLocation.LocalMachine);
                if (cert == null)
                {
                    throw new ArgumentException("Certificate not found");
                }
                return cert;
            }

            private static X509Certificate2 LoadCertificateByThumbprint(string thumbprint, StoreLocation location)
            {
                X509Store store = null;
                try
                {
                    store = new X509Store(StoreName.My, location);

                    store.Open(OpenFlags.ReadOnly);
                    X509Certificate2Collection certs =
                        store.Certificates.Find(
                            X509FindType.FindByThumbprint,
                            thumbprint,
                            false);
                    if (certs.Count == 0)
                    {
                        return null;
                    }
                    if (certs.Count > 1)
                    {
                        throw new ArgumentException(nameof(thumbprint));
                    }
                    return certs[0];
                }
                finally
                {
                    store?.Close();
                }
            }

            private void ConsumeException(Task task, string taskDescription)
            {
                var ignored = task.ContinueWith(faultedTask =>
                {
                    Debug.Assert(faultedTask.IsFaulted);
                    Trace.WriteLine(string.Format(
                        "Task {0} on server {1} completed with exception {2}",
                        taskDescription, this.uri, faultedTask.Exception.Message));
                },
                TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        #endregion
    }
}
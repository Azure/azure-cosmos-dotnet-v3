namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ConnectionStateMuxListenerTests
    {
        [Owner("kirankk")]
        [TestMethod]
        public void StoreClientFactoryV2Setup()
        {
            StoreClientFactory storeClientFactory = new StoreClientFactory(Protocol.Tcp,
                requestTimeoutInSeconds: 10,
                maxConcurrentConnectionOpenRequests: 10);
            Assert.IsInstanceOfType(storeClientFactory.GetConnectionStateListener(), typeof(ConnectionStateMuxListener));

        }

        [Owner("kirankk")]
        [TestMethod]
        public async Task DisableEndpointRediscoveryAsync()
        {
            bool enableTcpConnectionEndpointRediscovery = false;
            ConnectionStateMuxListener connectionStateMuxListener = new ConnectionStateMuxListener(enableTcpConnectionEndpointRediscovery);

            ServerKey serverKey = new ServerKey(new Uri("http://localhost:8081/ep1"));
            ServerKey calledBackServerKey = null;
            connectionStateMuxListener.Register(serverKey,
                async (sk) =>
                {
                    await Task.Yield();
                    calledBackServerKey = sk;
                });

            await connectionStateMuxListener.OnConnectionEventAsync(ConnectionEvent.ReadEof, DateTime.Now, serverKey);

            Assert.IsFalse(connectionStateMuxListener.enableTcpConnectionEndpointRediscovery);
            Assert.IsFalse(connectionStateMuxListener.serverKeyEventHandlers.Any());
            Assert.IsNull(calledBackServerKey);
        }

        [Owner("kirankk")]
        [TestMethod]
        public async Task RegisterUnregisterNotifyAsync()
        {
            bool enableTcpConnectionEndpointRediscovery = true;
            ConnectionStateMuxListener connectionStateMuxListener = new ConnectionStateMuxListener(enableTcpConnectionEndpointRediscovery);

            ServerKey serverKey = new ServerKey(new Uri("http://localhost:8081/ep1"));
            ServerKey calledBackServerKey = null;
            Func<ServerKey, Task> callback = async (sk) =>
            {
                await Task.Yield();
                calledBackServerKey = sk;
            };

            // Register
            connectionStateMuxListener.Register(serverKey, callback);
            Assert.IsTrue(connectionStateMuxListener.enableTcpConnectionEndpointRediscovery);
            Assert.AreEqual(1, connectionStateMuxListener.serverKeyEventHandlers.Count);
            Assert.AreEqual(1, connectionStateMuxListener.serverKeyEventHandlers.First().Value.Count);

            await connectionStateMuxListener.OnConnectionEventAsync(ConnectionEvent.ReadEof, DateTime.Now, serverKey);
            Assert.AreEqual(serverKey, calledBackServerKey);

            // Call with a different equivalent object
            calledBackServerKey = null;
            ServerKey serverKeyDuplicate = new ServerKey(new Uri("http://localhost:8081/ep1"));
            await connectionStateMuxListener.OnConnectionEventAsync(ConnectionEvent.ReadEof, DateTime.Now, serverKeyDuplicate);

            Assert.AreEqual(1, connectionStateMuxListener.serverKeyEventHandlers.Count);
            Assert.AreEqual(serverKey, serverKeyDuplicate);

            // UnRegister
            calledBackServerKey = null;
            connectionStateMuxListener.UnRegister(serverKey, callback);
            await connectionStateMuxListener.OnConnectionEventAsync(ConnectionEvent.ReadEof, DateTime.Now, serverKey);

            Assert.IsTrue(connectionStateMuxListener.enableTcpConnectionEndpointRediscovery);
            Assert.AreEqual(0, connectionStateMuxListener.serverKeyEventHandlers.Count); // Unregiter will not remove entry fully
            Assert.IsNull(calledBackServerKey);
        }

        [Owner("kirankk")]
        [TestMethod]
        public async Task DuplicateRegisterAndUnRegisterSingleCallbackAsync()
        {
            bool enableTcpConnectionEndpointRediscovery = true;
            ConnectionStateMuxListener connectionStateMuxListener = new ConnectionStateMuxListener(enableTcpConnectionEndpointRediscovery);

            ServerKey serverKey = new ServerKey(new Uri("http://localhost:8081/ep1"));
            ServerKey calledBackServerKey = null;
            int callCount = 0;
            Func<ServerKey, Task> callback = async (sk) =>
            {
                await Task.Yield();
                callCount++;
                calledBackServerKey = sk;
            };

            // Register
            connectionStateMuxListener.Register(serverKey, callback);
            await connectionStateMuxListener.OnConnectionEventAsync(ConnectionEvent.ReadEof, DateTime.Now, serverKey);

            Assert.IsTrue(connectionStateMuxListener.enableTcpConnectionEndpointRediscovery);
            Assert.AreEqual(1, connectionStateMuxListener.serverKeyEventHandlers.Count);
            Assert.AreEqual(serverKey, calledBackServerKey);
            Assert.AreEqual(1, callCount);

            // Re-register the name key and callback => also should get called only once
            connectionStateMuxListener.Register(serverKey, callback);
            calledBackServerKey = null;
            await connectionStateMuxListener.OnConnectionEventAsync(ConnectionEvent.ReadEof, DateTime.Now, serverKey);

            Assert.AreEqual(serverKey, calledBackServerKey);
            Assert.AreEqual(2, callCount);

            // UnRegister
            calledBackServerKey = null;
            connectionStateMuxListener.UnRegister(serverKey, callback);
            await connectionStateMuxListener.OnConnectionEventAsync(ConnectionEvent.ReadEof, DateTime.Now, serverKey);

            Assert.IsTrue(connectionStateMuxListener.enableTcpConnectionEndpointRediscovery);
            Assert.AreEqual(0, connectionStateMuxListener.serverKeyEventHandlers.Count);
            Assert.IsNull(calledBackServerKey);

            // Double un-register
            connectionStateMuxListener.UnRegister(serverKey, callback);
            Assert.IsTrue(connectionStateMuxListener.enableTcpConnectionEndpointRediscovery);
            Assert.AreEqual(0, connectionStateMuxListener.serverKeyEventHandlers.Count);
        }

        [Owner("kirankk")]
        [TestMethod]
        public async Task DuplicateRegisterAndUnRegisterDifferentCallbacksAsync()
        {
            bool enableTcpConnectionEndpointRediscovery = true;
            ConnectionStateMuxListener connectionStateMuxListener = new ConnectionStateMuxListener(enableTcpConnectionEndpointRediscovery);

            ServerKey serverKey = new ServerKey(new Uri("http://localhost:8081/ep1"));
            int callCount = 0;
            Func<ServerKey, Task> callback1 = async (sk) =>
            {
                await Task.Yield();
                callCount++;
            };
            Func<ServerKey, Task> callback2 = async (sk) =>
            {
                await Task.Yield();
                callCount++;
            };

            // Register
            connectionStateMuxListener.Register(serverKey, callback1);
            connectionStateMuxListener.Register(serverKey, callback2);
            await connectionStateMuxListener.OnConnectionEventAsync(ConnectionEvent.ReadEof, DateTime.Now, serverKey);

            Assert.IsTrue(connectionStateMuxListener.enableTcpConnectionEndpointRediscovery);
            Assert.AreEqual(1, connectionStateMuxListener.serverKeyEventHandlers.Count);
            Assert.AreEqual(2, connectionStateMuxListener.serverKeyEventHandlers.First().Value.Count);
            Assert.AreEqual(2, callCount);

            // UnRegister
            connectionStateMuxListener.UnRegister(serverKey, callback1);
            await connectionStateMuxListener.OnConnectionEventAsync(ConnectionEvent.ReadEof, DateTime.Now, serverKey);

            Assert.IsTrue(connectionStateMuxListener.enableTcpConnectionEndpointRediscovery);
            Assert.AreEqual(1, connectionStateMuxListener.serverKeyEventHandlers.Count);
            Assert.AreEqual(3, callCount);

            // Double un-register
            connectionStateMuxListener.UnRegister(serverKey, callback2);
            Assert.IsTrue(connectionStateMuxListener.enableTcpConnectionEndpointRediscovery);
            Assert.AreEqual(0, connectionStateMuxListener.serverKeyEventHandlers.Count);
        }

        [Owner("kirankk")]
        [TestMethod]
        public void ArgumentCheck()
        {
            ConnectionStateMuxListener connectionStateMuxListener = new ConnectionStateMuxListener(true);

            Assert.ThrowsException<ArgumentNullException>(() => connectionStateMuxListener.Register(null, null));
            Assert.ThrowsException<ArgumentNullException>(() => connectionStateMuxListener.Register(new ServerKey(new Uri("http://localost:8081")), null));

            Assert.ThrowsException<ArgumentNullException>(() => connectionStateMuxListener.UnRegister(null, null));
            Assert.ThrowsException<ArgumentNullException>(() => connectionStateMuxListener.UnRegister(new ServerKey(new Uri("http://localost:8081")), null));
        }

        [Owner("kirankk")]
        [TestMethod]
        public async Task DynamicConcurrencyMuxListenerTestAsync()
        {
            ConnectionStateMuxListener connectionStateMuxListener = new ConnectionStateMuxListener(true);
            Assert.AreEqual(Environment.ProcessorCount, connectionStateMuxListener.notificationConcurrency);
            Assert.IsTrue(connectionStateMuxListener.enableTcpConnectionEndpointRediscovery);

            int callbackCount = 0;
            ServerKey serverKey = new ServerKey(new Uri("http://localhost:8081/"));
            connectionStateMuxListener.Register(serverKey,
                async (sk) =>
                {
                    callbackCount++;
                    await Task.Yield();
                });

            await connectionStateMuxListener.OnConnectionEventAsync(ConnectionEvent.ReadEof, DateTime.Now, serverKey);
            Assert.AreEqual(1, callbackCount);

            connectionStateMuxListener.SetConnectionEventConcurrency(0);
            Assert.AreEqual(0, connectionStateMuxListener.notificationConcurrency);
            Assert.IsTrue(connectionStateMuxListener.enableTcpConnectionEndpointRediscovery);

            callbackCount = 0;
            await connectionStateMuxListener.OnConnectionEventAsync(ConnectionEvent.ReadEof, DateTime.Now, serverKey);
            Assert.AreEqual(0, callbackCount);
        }

        [Owner("kirankk")]
        [TestMethod]
        public async Task NotificationsConcurrencyListenerTestAsync()
        {
            int concurrentToTest = 2;
            int totalRequests = concurrentToTest * 2;
            IConnectionStateListener connectionStateMuxListener = new ConnectionStateMuxListener(true);
            connectionStateMuxListener.SetConnectionEventConcurrency(concurrentToTest);

            SemaphoreSlim mainTaskSemsphore = new SemaphoreSlim(concurrentToTest);
            ManualResetEvent manualResetEvent = new ManualResetEvent(false);

            int callbackCount = 0;
            ServerKey serverKey = new ServerKey(new Uri("http://localhost:8081/"));
            connectionStateMuxListener.Register(serverKey,
                async (sk) => {
                    await mainTaskSemsphore.WaitAsync();

                    lock (sk)
                    {
                        callbackCount++;
                        if (callbackCount >= concurrentToTest)
                        {
                            manualResetEvent.Set();
                        }
                    }
                });

            Task[] allTasks = new Task[totalRequests];
            for (int i = 0; i < totalRequests; i++)
            {
                allTasks[i] = connectionStateMuxListener.OnConnectionEventAsync(ConnectionEvent.ReadEof, DateTime.Now, serverKey);
            }

            manualResetEvent.WaitOne();
            Assert.AreEqual(concurrentToTest, callbackCount);

            mainTaskSemsphore.Release(concurrentToTest);
            await Task.WhenAll(allTasks);
            Assert.AreEqual(totalRequests, callbackCount);

            mainTaskSemsphore.Release(concurrentToTest);
        }
    }
}

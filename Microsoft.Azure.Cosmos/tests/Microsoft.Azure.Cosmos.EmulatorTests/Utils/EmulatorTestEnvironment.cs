//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.InMemoryEmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class EmulatorTestEnvironment
    {
        private static readonly List<IDisposable> clients = new List<IDisposable>();
        private static InMemoryEmulator emulator;
        private static IWebProxy originalProxy;

        internal static bool IsInMemory
        {
            get
            {
                string backend = Environment.GetEnvironmentVariable("COSMOS_EMULATOR_BACKEND");
                return backend switch
                {
                    null or "" or "Emulator" => false,
                    "InMemory" => true,
                    _ => throw new InvalidOperationException($"Unknown COSMOS_EMULATOR_BACKEND: {backend}"),
                };
            }
        }

        internal static Uri AccountEndpoint => emulator?.AccountEndpoint
            ?? throw new InvalidOperationException("The owned in-memory emulator has not started.");

        internal static string DummyKey => Convert.ToBase64String(new byte[64]);

        [AssemblyInitialize]
        public static async Task InitializeAsync(TestContext context)
        {
            if (!IsInMemory)
            {
                return;
            }

            string executable = Environment.GetEnvironmentVariable(InMemoryEmulator.ExecutableVariable);
            if (string.IsNullOrEmpty(executable))
            {
                throw new InvalidOperationException(
                    $"The InMemory backend requires {InMemoryEmulator.ExecutableVariable}.");
            }

            emulator = await InMemoryEmulator.StartAsync(
                executable, Path.Combine(AppContext.BaseDirectory, "inmemory-emulator-config.json"));
            originalProxy = HttpClient.DefaultProxy;
            HttpClient.DefaultProxy = new WebProxy();
            context.WriteLine($"Owned in-memory backend: {emulator.AccountEndpoint}");
        }

        [AssemblyCleanup]
        public static async Task CleanupAsync()
        {
            try
            {
                lock (clients)
                {
                    foreach (IDisposable client in clients)
                    {
                        client.Dispose();
                    }

                    clients.Clear();
                }
            }
            finally
            {
                if (emulator != null)
                {
                    try
                    {
                        await emulator.DisposeAsync();
                    }
                    finally
                    {
                        emulator = null;
                        HttpClient.DefaultProxy = originalProxy;
                    }
                }
            }
        }

        internal static T TrackClient<T>(T client)
            where T : IDisposable
        {
            if (IsInMemory)
            {
                if (client is CosmosClient cosmosClient
                    && (cosmosClient.Endpoint != AccountEndpoint
                        || cosmosClient.ClientOptions.ConnectionMode != ConnectionMode.Gateway))
                {
                    client.Dispose();
                    throw new InvalidOperationException(
                        "In-memory scenarios must use Gateway mode and the owned loopback endpoint.");
                }

                lock (clients)
                {
                    clients.Add(client);
                }
            }

            return client;
        }

        internal static void RequireFullEmulator(string feature)
        {
            if (IsInMemory)
            {
                throw new NotSupportedException($"{feature} requires the full emulator or a live account.");
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverBenchmark
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Running;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.NativeDriverPoc;

    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // `dotnet run -- validate` — sanity check before a real run.
            if (args.Length > 0 && string.Equals(args[0], "validate", StringComparison.OrdinalIgnoreCase))
            {
                return await ValidateAsync().ConfigureAwait(false);
            }

            BenchmarkRunner.Run<ReadItemBenchmark>(
                ManualConfig.CreateMinimumViable()
                    .WithOption(ConfigOptions.JoinSummary, true));
            return 0;
        }

        /// <summary>
        /// Run each path once, log status + body length, and confirm both
        /// paths see the same document. Use this to verify env vars + the
        /// native DLL drop before kicking off the full benchmark.
        /// </summary>
        private static async Task<int> ValidateAsync()
        {
            BenchmarkSettings settings;
            try
            {
                settings = BenchmarkSettings.FromEnvironment();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — configuration: {ex.Message}");
                return 2;
            }

            Console.WriteLine($"validate: {settings.Describe()}");
            Console.WriteLine();

            // --- V3 SDK Gateway -------------------------------------------
            long sdkGwBytes;
            int sdkGwHttp;
            try
            {
                using var sdk = new CosmosClient(
                    settings.Endpoint, settings.Key,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        ApplicationName = "cosmos-bench-validate-gw",
                    });
                Container container = sdk.GetDatabase(settings.Database).GetContainer(settings.Container);
                using ResponseMessage rm = await container
                    .ReadItemStreamAsync(settings.ItemId, new PartitionKey(settings.PartitionKey))
                    .ConfigureAwait(false);
                sdkGwHttp = (int)rm.StatusCode;
                using var ms = new MemoryStream();
                if (rm.Content != null) await rm.Content.CopyToAsync(ms).ConfigureAwait(false);
                sdkGwBytes = ms.Length;
                Console.WriteLine($"[V3 SDK Gateway] http={sdkGwHttp} bytes={sdkGwBytes} ru={rm.Headers?.RequestCharge:F2} activityId={rm.Headers?.ActivityId}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — V3 SDK Gateway: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }

            // --- V3 SDK Direct --------------------------------------------
            long sdkDirBytes;
            int sdkDirHttp;
            try
            {
                using var sdk = new CosmosClient(
                    settings.Endpoint, settings.Key,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Direct,
                        ApplicationName = "cosmos-bench-validate-dir",
                    });
                Container container = sdk.GetDatabase(settings.Database).GetContainer(settings.Container);
                using ResponseMessage rm = await container
                    .ReadItemStreamAsync(settings.ItemId, new PartitionKey(settings.PartitionKey))
                    .ConfigureAwait(false);
                sdkDirHttp = (int)rm.StatusCode;
                using var ms = new MemoryStream();
                if (rm.Content != null) await rm.Content.CopyToAsync(ms).ConfigureAwait(false);
                sdkDirBytes = ms.Length;
                Console.WriteLine($"[V3 SDK Direct]  http={sdkDirHttp} bytes={sdkDirBytes} ru={rm.Headers?.RequestCharge:F2} activityId={rm.Headers?.ActivityId}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — V3 SDK Direct: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine("  Hint: Direct mode requires outbound TCP to backend replicas (ephemeral port range).");
                Console.Error.WriteLine("        If you're behind a restrictive firewall, fall back to Gateway-only.");
                return 1;
            }

            // --- Native driver --------------------------------------------
            long nativeBytes;
            int nativeHttp;
            try
            {
                using var native = new NativeCosmosClient(
                    settings.Endpoint, settings.Key,
                    settings.Database, settings.Container, settings.PartitionKey,
                    userAgentSuffix: "cosmos-bench-validate");
                CosmosNativeResponse r = await native.ReadItemAsync(settings.ItemId).ConfigureAwait(false);
                nativeHttp = r.HttpStatusCode;
                nativeBytes = r.Body.LongLength;
                Console.WriteLine($"[Native driver]  http={nativeHttp} bytes={nativeBytes} ru={r.RequestCharge:F2} activityId={r.ActivityId}");
            }
            catch (DllNotFoundException ex)
            {
                Console.Error.WriteLine($"FAIL — Native driver: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine("  Cause: azurecosmosdriver.dll is not on the probing path.");
                Console.Error.WriteLine("  Build it: pwsh ..\\Microsoft.Azure.Cosmos.NativeDriverPoc\\scripts\\build-native-dll.ps1");
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — Native driver: {ex.GetType().Name}: {ex.Message}");
                if (ex.Message.Contains("InvalidOptionValue", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine("  Hint: the value passed to that FFI option violates the driver's contract");
                    Console.Error.WriteLine("        (e.g. user-agent suffix must be <=25 chars / [A-Za-z0-9._~-]).");
                }
                return 1;
            }

            Console.WriteLine();
            if (sdkGwHttp == 200 && sdkDirHttp == 200 && nativeHttp == 200 &&
                sdkGwBytes == nativeBytes && sdkDirBytes == nativeBytes)
            {
                Console.WriteLine($"PASS — all three paths returned HTTP 200 with {nativeBytes} bytes.");
                return 0;
            }

            Console.Error.WriteLine(
                $"MISMATCH — SDK Gateway: http={sdkGwHttp} bytes={sdkGwBytes} / " +
                $"SDK Direct: http={sdkDirHttp} bytes={sdkDirBytes} / " +
                $"Native: http={nativeHttp} bytes={nativeBytes}");
            return 1;
        }
    }
}

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

            // --- V3 SDK ---------------------------------------------------
            long sdkBytes;
            int sdkHttp;
            try
            {
                using var sdk = new CosmosClient(
                    settings.Endpoint, settings.Key,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        ApplicationName = "cosmos-nativedriver-benchmark-validate",
                    });
                Container container = sdk.GetDatabase(settings.Database).GetContainer(settings.Container);
                using ResponseMessage rm = await container
                    .ReadItemStreamAsync(settings.ItemId, new PartitionKey(settings.PartitionKey))
                    .ConfigureAwait(false);
                sdkHttp = (int)rm.StatusCode;
                using var ms = new MemoryStream();
                if (rm.Content != null) await rm.Content.CopyToAsync(ms).ConfigureAwait(false);
                sdkBytes = ms.Length;
                Console.WriteLine($"[V3 SDK]        http={sdkHttp} bytes={sdkBytes} ru={rm.Headers?.RequestCharge:F2} activityId={rm.Headers?.ActivityId}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — V3 SDK: {ex.GetType().Name}: {ex.Message}");
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
                    userAgentSuffix: "cosmos-nativedriver-benchmark-validate");
                CosmosNativeResponse r = await native.ReadItemAsync(settings.ItemId).ConfigureAwait(false);
                nativeHttp = r.HttpStatusCode;
                nativeBytes = r.Body.LongLength;
                Console.WriteLine($"[Native driver] http={nativeHttp} bytes={nativeBytes} ru={r.RequestCharge:F2} activityId={r.ActivityId}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL — Native driver: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine("  Most common cause: azurecosmosdriver.dll is not on the probing path.");
                Console.Error.WriteLine("  See ..\\Microsoft.Azure.Cosmos.NativeDriverPoc\\scripts\\build-native-dll.ps1");
                return 1;
            }

            Console.WriteLine();
            if (sdkHttp == 200 && nativeHttp == 200 && sdkBytes == nativeBytes)
            {
                Console.WriteLine($"PASS — both paths returned HTTP 200 with {sdkBytes} bytes.");
                return 0;
            }

            Console.Error.WriteLine($"MISMATCH — SDK: http={sdkHttp} bytes={sdkBytes} / Native: http={nativeHttp} bytes={nativeBytes}");
            return 1;
        }
    }
}

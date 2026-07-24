//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests.Utils;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CrossVersionCompatibilityTests
    {
        private static readonly TimeSpan WorkerTimeout = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan MatrixTimeout = TimeSpan.FromMinutes(12);
        private static readonly TimeSpan ProcessTerminationTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan CleanupTimeout = TimeSpan.FromMinutes(2);

        [TestMethod]
        [Timeout(15 * 60 * 1000)]
        public async Task ReleasedPreview07AndCurrentSourceRemainCompatible()
        {
            using CancellationTokenSource matrixTimeout = new(MatrixTimeout);
            IReadOnlyDictionary<string, string> workers = LoadWorkers();
            ValidateDependencyClosure(workers["released"], "package", "1.0.0-preview07");
            ValidateDependencyClosure(workers["current"], "project", expectedVersion: null);

            WorkerInvocation releasedIdentityRun = await RunWorkerAsync(
                workers["released"],
                matrixTimeout.Token,
                "--action=identity");
            WorkerInvocation currentIdentityRun = await RunWorkerAsync(
                workers["current"],
                matrixTimeout.Token,
                "--action=identity");
            CompatibilityMatrixRecord releasedIdentity = GetSingleRecord(releasedIdentityRun, "identity");
            CompatibilityMatrixRecord currentIdentity = GetSingleRecord(currentIdentityRun, "identity");
            CompatibilityMatrixIdentityValidator.Validate(
                releasedIdentity,
                currentIdentity,
                GetCurrentSourceAssemblySha256());

            string databaseId = "compat-matrix-" + Guid.NewGuid().ToString("N");
            (string endpoint, string key) = TestCommon.GetAccountInfo();
            string[] commonArguments =
            {
                "--endpoint=" + endpoint,
                "--key=" + key,
                "--database=" + databaseId,
            };

            Exception primaryFailure = null;
            try
            {
                ValidateObservations(
                    await RunWorkerAsync(
                        workers["released"],
                        matrixTimeout.Token,
                        commonArguments.Prepend("--action=write").ToArray()),
                    GetWriteScenarios("released"));
                ValidateObservations(
                    await RunWorkerAsync(
                        workers["current"],
                        matrixTimeout.Token,
                        commonArguments.Prepend("--action=write").ToArray()),
                    GetWriteScenarios("current"));
                ValidateObservations(
                    await RunWorkerAsync(
                        workers["current"],
                        matrixTimeout.Token,
                        commonArguments
                            .Prepend("--writer=released")
                            .Prepend("--action=read")
                            .ToArray()),
                    GetReadScenarios("released", "current"));
                ValidateObservations(
                    await RunWorkerAsync(
                        workers["released"],
                        matrixTimeout.Token,
                        commonArguments
                            .Prepend("--writer=current")
                            .Prepend("--action=read")
                            .ToArray()),
                    GetReadScenarios("current", "released"));
                ValidateObservations(
                    await RunWorkerAsync(
                        workers["current"],
                        matrixTimeout.Token,
                        commonArguments.Prepend("--action=tamper").ToArray()),
                    new[] { "guard:plaintext-rejected" });
            }
            catch (Exception exception)
            {
                primaryFailure = exception;
                throw;
            }
            finally
            {
                try
                {
                    await DeleteDatabaseAsync(databaseId, endpoint, key);
                }
                catch (Exception cleanupException) when (primaryFailure != null)
                {
                    TestContext.WriteLine($"Compatibility cleanup also failed: {cleanupException}");
                }
            }
        }

        public TestContext TestContext { get; set; }

        private static IReadOnlyDictionary<string, string> LoadWorkers()
        {
            string manifestPath = Path.Combine(AppContext.BaseDirectory, "CompatMatrix.Workers.txt");
            if (!File.Exists(manifestPath))
            {
                throw new InvalidOperationException($"Compatibility worker manifest was not found: {manifestPath}");
            }

            IReadOnlyDictionary<string, string> workers =
                CompatibilityMatrixWorkerManifest.Parse(File.ReadAllLines(manifestPath));
            foreach (KeyValuePair<string, string> worker in workers)
            {
                if (!File.Exists(worker.Value))
                {
                    throw new InvalidOperationException($"{worker.Key} compatibility worker was not found: {worker.Value}");
                }

                string runtimeConfigPath = Path.ChangeExtension(worker.Value, ".runtimeconfig.json");
                string depsPath = Path.ChangeExtension(worker.Value, ".deps.json");
                if (!File.Exists(runtimeConfigPath) || !File.Exists(depsPath))
                {
                    throw new InvalidOperationException(
                        $"{worker.Key} compatibility worker is missing its runtime configuration or dependency graph.");
                }
            }

            return workers;
        }

        private static void ValidateDependencyClosure(
            string workerPath,
            string expectedType,
            string expectedVersion)
        {
            string depsPath = Path.ChangeExtension(workerPath, ".deps.json");
            JObject dependencies = JObject.Parse(File.ReadAllText(depsPath));
            JObject libraries = dependencies["libraries"] as JObject
                ?? throw new InvalidOperationException($"Worker dependency graph has no libraries section: {depsPath}");
            JProperty library = libraries.Properties().SingleOrDefault(
                property => property.Name.StartsWith(
                    "Microsoft.Azure.Cosmos.Encryption.Custom/",
                    StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Worker dependency graph does not contain Encryption.Custom: {depsPath}");
            string actualType = library.Value.Value<string>("type");
            if (!string.Equals(actualType, expectedType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Worker dependency graph loaded Encryption.Custom as {actualType}, expected {expectedType}: {depsPath}");
            }

            if (expectedVersion != null &&
                !string.Equals(
                    library.Name,
                    "Microsoft.Azure.Cosmos.Encryption.Custom/" + expectedVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Worker dependency graph loaded {library.Name}, expected Encryption.Custom/{expectedVersion}.");
            }
        }

        private static async Task<WorkerInvocation> RunWorkerAsync(
            string workerPath,
            CancellationToken matrixCancellationToken,
            params string[] arguments)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "dotnet",
                WorkingDirectory = Path.GetDirectoryName(workerPath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(workerPath);
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start compatibility worker: {workerPath}");
            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
            using CancellationTokenSource timeout =
                CancellationTokenSource.CreateLinkedTokenSource(matrixCancellationToken);
            timeout.CancelAfter(WorkerTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                await TerminateProcessAsync(process);
                _ = await standardOutputTask;
                _ = await standardErrorTask;

                throw new TimeoutException(
                    $"Compatibility matrix or worker deadline expired: {Path.GetFileName(workerPath)} {SanitizeArguments(arguments)}");
            }

            string standardOutput = await standardOutputTask;
            string standardError = await standardErrorTask;
            CompatibilityMatrixRecord[] records = standardOutput
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(CompatibilityMatrixProtocol.ParseRecord)
                .ToArray();

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                throw new InvalidOperationException(
                    $"Compatibility worker wrote to stderr: {standardError.Trim()}");
            }

            CompatibilityMatrixRecord[] completions = records
                .Where(record => string.Equals(record.Kind, "completion", StringComparison.Ordinal))
                .ToArray();
            if (completions.Length != 1 ||
                !ReferenceEquals(completions[0], records.LastOrDefault()))
            {
                throw new InvalidOperationException(
                    $"Compatibility worker emitted an invalid completion record.{Environment.NewLine}{standardOutput}");
            }

            if (process.ExitCode != 0 ||
                !string.Equals(completions[0].Status, "pass", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Compatibility worker failed with exit code {process.ExitCode}.{Environment.NewLine}{standardOutput}");
            }

            return new WorkerInvocation(workerPath, records);
        }

        private static async Task TerminateProcessAsync(Process process)
        {
            if (process.HasExited)
            {
                return;
            }

            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                return;
            }

            using CancellationTokenSource terminationTimeout = new(ProcessTerminationTimeout);
            try
            {
                await process.WaitForExitAsync(terminationTimeout.Token);
            }
            catch (OperationCanceledException exception)
            {
                throw new TimeoutException(
                    $"Compatibility worker did not terminate within {ProcessTerminationTimeout}.",
                    exception);
            }
        }

        private static void ValidateObservations(
            WorkerInvocation invocation,
            IReadOnlyCollection<string> expectedScenarios)
        {
            CompatibilityMatrixRecord[] observations = invocation.Records
                .Where(record => string.Equals(record.Kind, "observation", StringComparison.Ordinal))
                .ToArray();
            CompatibilityMatrixResultOracle.Validate(expectedScenarios, observations);
        }

        private static CompatibilityMatrixRecord GetSingleRecord(WorkerInvocation invocation, string kind)
        {
            CompatibilityMatrixRecord[] records = invocation.Records
                .Where(record => string.Equals(record.Kind, kind, StringComparison.Ordinal))
                .ToArray();
            if (records.Length != 1)
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(invocation.WorkerPath)} emitted {records.Length} {kind} records.");
            }

            return records[0];
        }

        private static IReadOnlyCollection<string> GetWriteScenarios(string writer)
        {
            List<string> scenarios = new()
            {
                $"write:{writer}:MDE:Newtonsoft",
                $"write:{writer}:AEAD:Newtonsoft",
            };
            if (writer == "current")
            {
                scenarios.Insert(1, "write:current:MDE:Stream");
            }

            return scenarios;
        }

        private static IReadOnlyCollection<string> GetReadScenarios(string writer, string reader)
        {
            List<string> scenarios = new();
            IEnumerable<(string Family, string WriteProcessor, string ReadProcessor)> combinations =
                writer == "released"
                    ? new[]
                    {
                        ("MDE", "Newtonsoft", "Newtonsoft"),
                        ("MDE", "Newtonsoft", "Stream"),
                        ("AEAD", "Newtonsoft", "Newtonsoft"),
                    }
                    : new[]
                    {
                        ("MDE", "Newtonsoft", "Newtonsoft"),
                        ("MDE", "Stream", "Newtonsoft"),
                        ("AEAD", "Newtonsoft", "Newtonsoft"),
                    };
            foreach ((string family, string writeProcessor, string readProcessor) in combinations)
            {
                foreach (string path in new[] { "point", "query", "feed" })
                {
                    scenarios.Add(
                        $"read:{writer}->{reader}:{family}:{writeProcessor}->{readProcessor}:{path}");
                }
            }

            return scenarios;
        }

        private static string GetCurrentSourceAssemblySha256()
        {
            string assemblyPath = typeof(EncryptionContainerExtensions).Assembly.Location;
            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath)));
        }

        private static async Task DeleteDatabaseAsync(string databaseId, string endpoint, string key)
        {
            using CosmosClient client = new(
                endpoint,
                key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    LimitToEndpoint = true,
                    HttpClientFactory = () => new System.Net.Http.HttpClient(
                        new System.Net.Http.HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                        }),
                });
            using CancellationTokenSource timeout = new(CleanupTimeout);
            try
            {
                using ResponseMessage response = await client
                    .GetDatabase(databaseId)
                    .DeleteStreamAsync(cancellationToken: timeout.Token);
                if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException(
                        $"Compatibility database cleanup failed with {response.StatusCode}: {response.ErrorMessage}");
                }
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        private static string SanitizeArguments(IEnumerable<string> arguments)
        {
            return string.Join(
                " ",
                arguments.Select(argument =>
                    argument.StartsWith("--key=", StringComparison.Ordinal)
                        ? "--key=<redacted>"
                        : argument));
        }

        private sealed class WorkerInvocation
        {
            public WorkerInvocation(string workerPath, IReadOnlyList<CompatibilityMatrixRecord> records)
            {
                this.WorkerPath = workerPath;
                this.Records = records;
            }

            public string WorkerPath { get; }

            public IReadOnlyList<CompatibilityMatrixRecord> Records { get; }
        }
    }
}
#endif

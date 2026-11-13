//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.InMemoryEmulatorTests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal sealed class InMemoryEmulator : IAsyncDisposable
    {
        internal const string ExecutableVariable = "COSMOS_IN_MEMORY_EMULATOR_PATH";

        private readonly Process process;
        private readonly Task<string> standardError;
        private Task<string> standardOutput;

        private InMemoryEmulator(Process process)
        {
            this.process = process;
            this.standardError = process.StandardError.ReadToEndAsync();
        }

        internal Uri AccountEndpoint { get; private set; }

        internal Uri ManagementEndpoint { get; private set; }

        internal static async Task<InMemoryEmulator> StartAsync(string executable, string config)
        {
            if (!File.Exists(executable))
            {
                throw new FileNotFoundException(
                    $"Set {ExecutableVariable} to the built azure_data_cosmos_emulator executable.", executable);
            }

            if (!File.Exists(config))
            {
                throw new FileNotFoundException("The in-memory emulator configuration was not found.", config);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo(Path.GetFullPath(executable))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("--config");
            startInfo.ArgumentList.Add(Path.GetFullPath(config));

            Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The in-memory emulator process did not start.");
            InMemoryEmulator emulator = new InMemoryEmulator(process);
            try
            {
                string ready = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
                emulator.standardOutput = process.StandardOutput.ReadToEndAsync();
                (emulator.AccountEndpoint, emulator.ManagementEndpoint) = ParseReadyRecord(ready);
                using HttpClient http = new HttpClient(new HttpClientHandler { UseProxy = false })
                {
                    Timeout = TimeSpan.FromSeconds(10),
                };
                using HttpResponseMessage health = await http.GetAsync(new Uri(emulator.ManagementEndpoint, "health"));
                health.EnsureSuccessStatusCode();
                using HttpResponseMessage capabilities = await http.GetAsync(new Uri(emulator.ManagementEndpoint, "capabilities"));
                capabilities.EnsureSuccessStatusCode();
                using JsonDocument document = JsonDocument.Parse(await capabilities.Content.ReadAsStringAsync());
                if (document.RootElement.GetProperty("apiVersion").GetInt32() != 1
                    || !document.RootElement.GetProperty("protocols").GetProperty("gatewayV1").GetBoolean())
                {
                    throw new InvalidOperationException("The emulator must advertise capabilities API v1 and Gateway V1.");
                }

                return emulator;
            }
            catch (Exception error)
            {
                await emulator.DisposeAsync();
                throw new InvalidOperationException(
                    $"In-memory emulator startup failed: {error.Message}{Environment.NewLine}{await emulator.standardError}",
                    error);
            }
        }

        internal static (Uri AccountEndpoint, Uri ManagementEndpoint) ParseReadyRecord(string ready)
        {
            if (string.IsNullOrEmpty(ready))
            {
                throw new InvalidOperationException("The emulator exited before writing its JSON ready record.");
            }

            using JsonDocument document = JsonDocument.Parse(ready);
            JsonElement root = document.RootElement;
            if (root.GetProperty("event").GetString() != "ready")
            {
                throw new InvalidOperationException("Expected the emulator's JSON ready record.");
            }

            return (ReadLoopbackEndpoint(root, "accountEndpoint"), ReadLoopbackEndpoint(root, "managementEndpoint"));
        }

        public async ValueTask DisposeAsync()
        {
            if (!this.process.HasExited)
            {
                this.process.Kill(entireProcessTree: true);
            }

            await this.process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await this.standardError;
            if (this.standardOutput != null)
            {
                await this.standardOutput;
            }

            this.process.Dispose();
        }

        internal static Uri ReadLoopbackEndpoint(JsonElement root, string property)
        {
            if (!Uri.TryCreate(root.GetProperty(property).GetString(), UriKind.Absolute, out Uri endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttp
                || !endpoint.IsLoopback
                || endpoint.UserInfo.Length != 0
                || endpoint.AbsolutePath != "/"
                || endpoint.Query.Length != 0
                || endpoint.Fragment.Length != 0)
            {
                throw new InvalidOperationException($"The emulator's {property} must be a loopback HTTP root endpoint.");
            }

            return endpoint;
        }
    }
}
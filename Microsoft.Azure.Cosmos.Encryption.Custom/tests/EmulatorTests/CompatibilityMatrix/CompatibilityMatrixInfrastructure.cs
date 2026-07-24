//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;

    internal sealed class CompatibilityMatrixRecord
    {
        public string Kind { get; set; }

        public string Role { get; set; }

        public string ScenarioId { get; set; }

        public string Status { get; set; }

        public string Detail { get; set; }

        public string PackageVersion { get; set; }

        public string InformationalVersion { get; set; }

        public string AssemblyVersion { get; set; }

        public string AssemblyMvid { get; set; }

        public string AssemblySha256 { get; set; }

        public string AssemblyPath { get; set; }

        public string CosmosVersion { get; set; }

        public string MdeVersion { get; set; }

        public string Processor { get; set; }

        public IReadOnlyList<string> ObservedScopes { get; set; }
    }

    internal static class CompatibilityMatrixProtocol
    {
        public static CompatibilityMatrixRecord ParseRecord(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new InvalidOperationException("Compatibility worker emitted an empty record.");
            }

            CompatibilityMatrixRecord record;
            try
            {
                record = JsonConvert.DeserializeObject<CompatibilityMatrixRecord>(line);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"Compatibility worker emitted malformed JSON: {line}", exception);
            }

            if (record == null || string.IsNullOrWhiteSpace(record.Kind))
            {
                throw new InvalidOperationException($"Compatibility worker emitted an invalid record: {line}");
            }

            return record;
        }
    }

    internal static class CompatibilityMatrixResultOracle
    {
        public static void Validate(
            IReadOnlyCollection<string> expectedScenarioIds,
            IReadOnlyCollection<CompatibilityMatrixRecord> actualRecords)
        {
            ArgumentNullException.ThrowIfNull(expectedScenarioIds);
            ArgumentNullException.ThrowIfNull(actualRecords);

            HashSet<string> expected = new(expectedScenarioIds, StringComparer.Ordinal);
            if (expected.Count != expectedScenarioIds.Count)
            {
                throw new InvalidOperationException("The expected compatibility scenario set contains duplicates.");
            }

            Dictionary<string, CompatibilityMatrixRecord> actual = new(StringComparer.Ordinal);
            foreach (CompatibilityMatrixRecord record in actualRecords)
            {
                if (record == null ||
                    !string.Equals(record.Kind, "observation", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(record.ScenarioId))
                {
                    throw new InvalidOperationException("The compatibility result set contains an invalid observation.");
                }

                if (!actual.TryAdd(record.ScenarioId, record))
                {
                    throw new InvalidOperationException($"Duplicate compatibility scenario: {record.ScenarioId}");
                }
            }

            string[] missing = expected.Except(actual.Keys, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            string[] unexpected = actual.Keys.Except(expected, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (missing.Length != 0 || unexpected.Length != 0)
            {
                throw new InvalidOperationException(
                    $"Compatibility scenario mismatch. Missing=[{string.Join(", ", missing)}] Unexpected=[{string.Join(", ", unexpected)}]");
            }

            CompatibilityMatrixRecord[] failures = actual.Values
                .Where(record => !string.Equals(record.Status, "pass", StringComparison.Ordinal))
                .OrderBy(record => record.ScenarioId, StringComparer.Ordinal)
                .ToArray();
            if (failures.Length != 0)
            {
                throw new InvalidOperationException(
                    "Compatibility failures: " +
                    string.Join(
                        "; ",
                        failures.Select(record => $"{record.ScenarioId}: {record.Detail ?? record.Status}")));
            }
        }
    }

    internal static class CompatibilityMatrixIdentityValidator
    {
        private const string ReleasedVersion = "1.0.0-preview07";

        public static void Validate(
            CompatibilityMatrixRecord released,
            CompatibilityMatrixRecord current,
            string expectedCurrentAssemblySha256)
        {
            ValidateIdentity(released, "released");
            ValidateIdentity(current, "current");

            if (!string.Equals(released.PackageVersion, ReleasedVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Released worker loaded {released.PackageVersion}, expected {ReleasedVersion}.");
            }

            if (string.Equals(released.AssemblySha256, current.AssemblySha256, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(released.AssemblyMvid, current.AssemblyMvid, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Released and current workers loaded the same Encryption.Custom binary.");
            }

            if (string.IsNullOrWhiteSpace(expectedCurrentAssemblySha256) ||
                !string.Equals(
                    current.AssemblySha256,
                    expectedCurrentAssemblySha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Current worker did not load the Encryption.Custom binary built for the emulator test project.");
            }
        }

        private static void ValidateIdentity(CompatibilityMatrixRecord record, string expectedRole)
        {
            if (record == null ||
                !string.Equals(record.Kind, "identity", StringComparison.Ordinal) ||
                !string.Equals(record.Role, expectedRole, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(record.PackageVersion) ||
                string.IsNullOrWhiteSpace(record.AssemblyMvid) ||
                string.IsNullOrWhiteSpace(record.AssemblySha256) ||
                string.IsNullOrWhiteSpace(record.AssemblyPath))
            {
                throw new InvalidOperationException($"Invalid {expectedRole} worker identity.");
            }
        }
    }

    internal static class CompatibilityMatrixWorkerManifest
    {
        public static IReadOnlyDictionary<string, string> Parse(IEnumerable<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            Dictionary<string, string> workers = new(StringComparer.Ordinal);
            foreach (string line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                string[] parts = line.Split('|');
                if (parts.Length != 2)
                {
                    throw new InvalidOperationException($"Invalid compatibility worker manifest line: {line}");
                }

                string role = GetRole(parts[0]);
                if (!workers.TryAdd(role, parts[1]))
                {
                    throw new InvalidOperationException($"Duplicate {role} compatibility worker.");
                }
            }

            if (workers.Count != 2 || !workers.ContainsKey("released") || !workers.ContainsKey("current"))
            {
                throw new InvalidOperationException("Compatibility worker manifest must contain one released and one current worker.");
            }

            return workers;
        }

        private static string GetRole(string projectPath)
        {
            if (projectPath.EndsWith("CompatMatrix.Released.csproj", StringComparison.OrdinalIgnoreCase))
            {
                return "released";
            }

            if (projectPath.EndsWith("CompatMatrix.Current.csproj", StringComparison.OrdinalIgnoreCase))
            {
                return "current";
            }

            throw new InvalidOperationException($"Unknown compatibility worker project: {projectPath}");
        }
    }
}
#endif

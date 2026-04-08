// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Text.Json;
    using System.Threading;
    using ModelContextProtocol.Server;

    /// <summary>
    /// MCP tool for analyzing CosmosDiagnostics JSON blobs.
    /// </summary>
    [McpServerToolType]
    public class DiagnosticsTool
    {
        [McpServerTool(Name = "cosmos_analyze_diagnostics"), Description("Parse and explain a CosmosDiagnostics JSON blob. Identifies issues like high latency, 429 throttling, retries, and region failovers. Returns a human-readable summary with actionable recommendations.")]
        public static string AnalyzeDiagnostics(
            [Description("Raw CosmosDiagnostics JSON string")] string diagnostics_json,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(diagnostics_json);
                JsonElement root = doc.RootElement;

                List<string> issues = new();
                List<string> recommendations = new();
                double totalLatencyMs = 0;
                double requestCharge = 0;
                int retryCount = 0;
                List<string> regionsContacted = new();

                // Extract summary diagnostics info
                if (root.TryGetProperty("Summary", out JsonElement summary))
                {
                    if (summary.TryGetProperty("DirectLatency", out JsonElement latency) ||
                        summary.TryGetProperty("GatewayLatency", out latency))
                    {
                        totalLatencyMs = latency.GetDouble();
                    }

                    if (summary.TryGetProperty("RequestCharge", out JsonElement ru))
                    {
                        requestCharge = ru.GetDouble();
                    }

                    if (summary.TryGetProperty("RetryCount", out JsonElement retries))
                    {
                        retryCount = retries.GetInt32();
                    }
                }

                // Check for common patterns in the raw JSON
                string rawText = diagnostics_json;

                if (rawText.Contains("429", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add("429 (Too Many Requests) detected — the operation was throttled.");
                    recommendations.Add("Consider increasing provisioned RU/s or enabling autoscale.");
                    recommendations.Add("Implement retry with exponential backoff (the SDK does this by default).");
                }

                if (rawText.Contains("\"retryAfterInMs\"", StringComparison.OrdinalIgnoreCase) ||
                    rawText.Contains("RetryAfter", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add("Retry-after headers detected, indicating throttling occurred.");
                }

                if (rawText.Contains("\"failover\"", StringComparison.OrdinalIgnoreCase) ||
                    rawText.Contains("\"RegionFailover\"", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add("Region failover detected during the operation.");
                    recommendations.Add("Verify multi-region write configuration if this is unexpected.");
                }

                if (rawText.Contains("\"StorePhysicalAddress\"", StringComparison.OrdinalIgnoreCase))
                {
                    // Direct mode diagnostics available
                    if (rawText.Contains("\"GoneException\"", StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add("GoneException detected — partition may have moved to a different replica.");
                        recommendations.Add("This is usually transient. If persistent, check for partition splits or heavy load.");
                    }
                }

                if (totalLatencyMs > 100)
                {
                    issues.Add($"High end-to-end latency: {totalLatencyMs:F1}ms.");
                    if (totalLatencyMs > 500)
                    {
                        recommendations.Add("Consider using Direct mode for lower latency if currently using Gateway mode.");
                    }
                    recommendations.Add("Check if the query is cross-partition and scope it with a partition key.");
                }

                if (requestCharge > 50)
                {
                    recommendations.Add($"High RU cost ({requestCharge:F1} RUs). Consider adding indexes on filtered/sorted fields.");
                }

                if (retryCount > 0)
                {
                    issues.Add($"{retryCount} retry attempt(s) during the operation.");
                }

                if (issues.Count == 0)
                {
                    issues.Add("No significant issues detected in the diagnostics.");
                }

                // Build summary
                string summaryText = $"Latency: {totalLatencyMs:F1}ms | RU: {requestCharge:F1} | Retries: {retryCount}";
                if (regionsContacted.Count > 0)
                {
                    summaryText += $" | Regions: {string.Join(", ", regionsContacted)}";
                }

                var result = new
                {
                    summary = summaryText,
                    issues,
                    recommendations,
                    metrics = new
                    {
                        totalLatencyMs = Math.Round(totalLatencyMs, 2),
                        requestCharge = Math.Round(requestCharge, 2),
                        retryCount,
                        regionsContacted
                    }
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (JsonException)
            {
                return JsonSerializer.Serialize(new { error = "Invalid JSON. Please provide a raw CosmosDiagnostics JSON string." });
            }
        }
    }
}

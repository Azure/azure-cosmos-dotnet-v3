//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs every point operation against item ids whose characters need special handling when the id
    /// is placed in a request URI. Only public APIs are used so the same scenarios can run against the
    /// emulator, a thin-client account, or any live account in Direct, Gateway, or thin-client mode.
    /// </summary>
    internal static class SpecialCharacterIdScenarios
    {
        internal sealed class Scenario
        {
            public Scenario(string category, string name, Func<string, string> createId, bool succeedsOverGateway = true)
            {
                this.Category = category;
                this.Name = name;
                this.CreateId = createId;
                this.SucceedsOverGateway = succeedsOverGateway;
            }

            public string Category { get; }

            public string Name { get; }

            public Func<string, string> CreateId { get; }

            /// <summary>
            /// False for ids whose point operations are known to fail in Gateway and thin-client modes because the
            /// id isn't escaped in the request URI. Direct mode doesn't build a request URI, so every scenario
            /// must succeed there.
            /// </summary>
            public bool SucceedsOverGateway { get; }

            public bool ExpectedToSucceed(bool isDirect)
            {
                return isDirect || this.SucceedsOverGateway;
            }
        }

        internal sealed class Result
        {
            public Result(Scenario scenario, string id, string failedOperation, string error, IReadOnlyCollection<string> transports)
            {
                this.Scenario = scenario;
                this.Id = id;
                this.FailedOperation = failedOperation;
                this.Error = error;
                this.Transports = transports;
            }

            public Scenario Scenario { get; }

            public string Id { get; }

            public string FailedOperation { get; }

            public string Error { get; }

            public IReadOnlyCollection<string> Transports { get; }

            public bool Succeeded => this.FailedOperation == null;
        }

        internal const string Direct = "Direct";
        internal const string Gateway = "Gateway";
        internal const string ThinClient = "ThinClient";

        private const string WorksToday = "Works today";
        private const string TrailingSpaces = "Trailing spaces";
        private const string NotHandled = "Not handled";

        internal static readonly IReadOnlyList<Scenario> Scenarios = new List<Scenario>
        {
            // Ids that already work in every mode. They guard against regressions.
            new Scenario(WorksToday, "GUID", b => b),
            new Scenario(WorksToday, "legal punctuation", b => b + "-_.!~*'();:@&=+$,"),
            new Scenario(WorksToday, "base64-like", b => b + "Ab+Cd=="),
            new Scenario(WorksToday, "interior dot", b => "a." + b),
            new Scenario(WorksToday, "leading space", b => " " + b),
            new Scenario(WorksToday, "leading tab", b => "\t" + b),
            new Scenario(WorksToday, "interior space", b => "a b" + b),
            new Scenario(WorksToday, "double quote", b => "a\"b" + b),
            new Scenario(WorksToday, "angle brackets", b => "a<b>" + b),
            new Scenario(WorksToday, "braces", b => "a{b}" + b),
            new Scenario(WorksToday, "square brackets", b => "a[b]" + b),
            new Scenario(WorksToday, "pipe, caret, backtick", b => "a|^`" + b),
            new Scenario(WorksToday, "question mark", b => "a?b" + b),
            new Scenario(WorksToday, "trailing question mark", b => b + "?"),
            new Scenario(WorksToday, "trailing percent", b => b + "100%"),
            new Scenario(WorksToday, "invalid escape", b => "a%zz" + b),
            new Scenario(WorksToday, "trailing dot", b => b + "."),
            new Scenario(WorksToday, "two trailing dots", b => b + ".."),
            new Scenario(WorksToday, "space then dot", b => b + " ."),
            new Scenario(WorksToday, "trailing NBSP", b => b + "\u00A0"),
            new Scenario(WorksToday, "trailing ideographic space", b => b + "\u3000"),
            new Scenario(WorksToday, "accented and CJK", b => "\u00E9\u6F22\u5B57" + b),

            // Ids ending in spaces, which System.Uri used to trim from the request URI.
            new Scenario(TrailingSpaces, "trailing space", b => b + " "),
            new Scenario(TrailingSpaces, "two trailing spaces", b => b + "  "),
            new Scenario(TrailingSpaces, "leading and trailing space", b => " " + b + " "),
            new Scenario(TrailingSpaces, "interior and trailing space", b => "a b" + b + " "),
            new Scenario(TrailingSpaces, "dot then space", b => b + ". "),
            new Scenario(TrailingSpaces, "NBSP then space", b => b + "\u00A0 "),
            new Scenario(TrailingSpaces, "emoji then space", b => "\uD83D\uDE00" + b + " "),
            new Scenario(TrailingSpaces, "question mark then space", b => "a?b" + b + " "),
            new Scenario(TrailingSpaces, "960 trailing spaces", b => b + new string(' ', 960)),

            // Ids that still fail over Gateway and thin client: other trailing whitespace, '#', '\' and literal '%XX'.
            new Scenario(NotHandled, "trailing tab", b => b + "\t", succeedsOverGateway: false),
            new Scenario(NotHandled, "trailing LF", b => b + "\n", succeedsOverGateway: false),
            new Scenario(NotHandled, "trailing CR", b => b + "\r", succeedsOverGateway: false),
            new Scenario(NotHandled, "trailing CRLF", b => b + "\r\n", succeedsOverGateway: false),
            new Scenario(NotHandled, "hash", b => "a#b" + b, succeedsOverGateway: false),
            new Scenario(NotHandled, "trailing hash", b => b + "#", succeedsOverGateway: false),
            new Scenario(NotHandled, "backslash", b => "a\\b" + b, succeedsOverGateway: false),
            new Scenario(NotHandled, "literal %20", b => "a%20b" + b, succeedsOverGateway: false),
            new Scenario(NotHandled, "literal %2F", b => "a%2Fb" + b, succeedsOverGateway: false),
            new Scenario(NotHandled, "literal %3F", b => "a%3Fb" + b, succeedsOverGateway: false),
            new Scenario(NotHandled, "literal %2520", b => "a%2520b" + b, succeedsOverGateway: false),
            new Scenario(NotHandled, "literal %20 then space", b => "a%20b" + b + " ", succeedsOverGateway: false),
        };

        /// <summary>
        /// Pairs of ids that must resolve to different items.
        /// </summary>
        internal static readonly IReadOnlyList<(string Name, Func<string, string> First, Func<string, string> Second, bool SucceedsOverGateway)> Twins =
            new List<(string, Func<string, string>, Func<string, string>, bool)>
            {
                ("trailing space vs. trimmed", b => b + " ", b => b, true),
                ("two trailing spaces vs. one", b => b + "  ", b => b + " ", true),
                ("leading space vs. trimmed", b => " " + b, b => b, true),
                ("trailing dot vs. trimmed", b => b + ".", b => b, true),
                ("literal %20 vs. space", b => b + "a%20b", b => b + "a b", false),
            };

        /// <summary>
        /// Returns the results whose outcome differs from the scenario's expectation for the given mode.
        /// </summary>
        internal static List<Result> GetUnexpected(IEnumerable<Result> results, bool isDirect)
        {
            return results.Where(r => r.Succeeded != r.Scenario.ExpectedToSucceed(isDirect)).ToList();
        }
        /// <summary>
        /// Runs Create, Read, ReadMany, Replace, Upsert, Patch, query, Delete and a read-after-delete for every
        /// scenario, then checks that each twin pair resolves to two distinct items.
        /// </summary>
        internal static async Task<IReadOnlyList<Result>> RunAsync(Container container, string partitionKeyPath)
        {
            string partitionKeyProperty = partitionKeyPath.TrimStart('/');
            string partitionKeyValue = "special-id-" + Guid.NewGuid().ToString("N");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            List<Result> results = new List<Result>();

            foreach (Scenario scenario in SpecialCharacterIdScenarios.Scenarios)
            {
                string id = scenario.CreateId(Guid.NewGuid().ToString("N").Substring(0, 8));
                results.Add(await SpecialCharacterIdScenarios.RunScenarioAsync(container, scenario, id, partitionKeyProperty, partitionKeyValue, partitionKey));
            }

            foreach ((string name, Func<string, string> first, Func<string, string> second, bool succeedsOverGateway) in SpecialCharacterIdScenarios.Twins)
            {
                string baseId = Guid.NewGuid().ToString("N").Substring(0, 8);
                results.Add(await SpecialCharacterIdScenarios.RunTwinsAsync(container, name, succeedsOverGateway, first(baseId), second(baseId), partitionKeyProperty, partitionKeyValue, partitionKey));
            }

            return results;
        }

        internal static string Describe(IEnumerable<Result> results)
        {
            return string.Join(
                Environment.NewLine,
                results.Select(r => $"[{r.Scenario.Category}] {r.Scenario.Name} ({Escape(r.Id)}): " +
                    (r.Succeeded ? "succeeded" : $"{r.FailedOperation} failed: {r.Error}")));
        }

        internal static string Escape(string id)
        {
            string shown = id.Length > 40 ? id.Substring(0, 40) + "..." : id;
            return string.Concat(shown.Select(c => c < 0x20 || c == 0x7F || c == 0xA0 || c == 0x3000 ? $"\\u{(int)c:X4}" : c.ToString()));
        }

        private static async Task<Result> RunScenarioAsync(
            Container container,
            Scenario scenario,
            string id,
            string partitionKeyProperty,
            string partitionKeyValue,
            PartitionKey partitionKey)
        {
            HashSet<string> transports = new HashSet<string>();
            string operation = "Create";
            bool created = false;
            try
            {
                ItemResponse<Dictionary<string, object>> response = await container.CreateItemAsync(
                    CreateItem(id, partitionKeyProperty, partitionKeyValue, "created"),
                    partitionKey);
                created = true;
                Expect(response, HttpStatusCode.Created, id, "created", transports);

                operation = "Read";
                response = await container.ReadItemAsync<Dictionary<string, object>>(id, partitionKey);
                Expect(response, HttpStatusCode.OK, id, "created", transports);

                operation = "ReadMany";
                FeedResponse<Dictionary<string, object>> readMany = await container.ReadManyItemsAsync<Dictionary<string, object>>(
                    new List<(string, PartitionKey)> { (id, partitionKey) });
                Check(readMany.Count == 1, $"expected 1 item, got {readMany.Count}");
                Check(readMany.First()["id"].ToString() == id, "returned a different id");

                operation = "Replace";
                response = await container.ReplaceItemAsync(CreateItem(id, partitionKeyProperty, partitionKeyValue, "replaced"), id, partitionKey);
                Expect(response, HttpStatusCode.OK, id, "replaced", transports);

                operation = "Upsert";
                response = await container.UpsertItemAsync(CreateItem(id, partitionKeyProperty, partitionKeyValue, "upserted"), partitionKey);
                Expect(response, HttpStatusCode.OK, id, "upserted", transports);

                operation = "Patch";
                response = await container.PatchItemAsync<Dictionary<string, object>>(id, partitionKey, new[] { PatchOperation.Set("/state", "patched") });
                Expect(response, HttpStatusCode.OK, id, "patched", transports);

                operation = "Query";
                List<Dictionary<string, object>> matches = await QueryByIdAsync(container, id, partitionKey);
                Check(matches.Count == 1, $"expected 1 item, got {matches.Count}");
                Check(matches[0]["state"].ToString() == "patched", $"state was '{matches[0]["state"]}'");

                operation = "Delete";
                ItemResponse<Dictionary<string, object>> delete = await container.DeleteItemAsync<Dictionary<string, object>>(id, partitionKey);
                Check(delete.StatusCode == HttpStatusCode.NoContent, $"status {(int)delete.StatusCode}");
                created = false;

                operation = "Read after delete";
                try
                {
                    await container.ReadItemAsync<Dictionary<string, object>>(id, partitionKey);
                    Check(false, "item still exists");
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                }

                return new Result(scenario, id, null, null, transports);
            }
            catch (Exception ex)
            {
                return new Result(scenario, id, operation, Summarize(ex), transports);
            }
            finally
            {
                if (created)
                {
                    await DeleteIgnoringErrorsAsync(container, id, partitionKey);
                }
            }
        }

        private static async Task<Result> RunTwinsAsync(
            Container container,
            string name,
            bool succeedsOverGateway,
            string firstId,
            string secondId,
            string partitionKeyProperty,
            string partitionKeyValue,
            PartitionKey partitionKey)
        {
            Scenario scenario = new Scenario("Twins", name, _ => firstId, succeedsOverGateway);
            HashSet<string> transports = new HashSet<string>();
            string operation = "Create";
            try
            {
                await container.CreateItemAsync(CreateItem(firstId, partitionKeyProperty, partitionKeyValue, "first"), partitionKey);
                await container.CreateItemAsync(CreateItem(secondId, partitionKeyProperty, partitionKeyValue, "second"), partitionKey);

                operation = "Read first";
                Expect(await container.ReadItemAsync<Dictionary<string, object>>(firstId, partitionKey), HttpStatusCode.OK, firstId, "first", transports);

                operation = "Read second";
                Expect(await container.ReadItemAsync<Dictionary<string, object>>(secondId, partitionKey), HttpStatusCode.OK, secondId, "second", transports);

                operation = "Delete first";
                await container.DeleteItemAsync<Dictionary<string, object>>(firstId, partitionKey);

                operation = "Read second after deleting first";
                Expect(await container.ReadItemAsync<Dictionary<string, object>>(secondId, partitionKey), HttpStatusCode.OK, secondId, "second", transports);

                return new Result(scenario, firstId, null, null, transports);
            }
            catch (Exception ex)
            {
                return new Result(scenario, firstId, operation, Summarize(ex), transports);
            }
            finally
            {
                await DeleteIgnoringErrorsAsync(container, firstId, partitionKey);
                await DeleteIgnoringErrorsAsync(container, secondId, partitionKey);
            }
        }

        private static Dictionary<string, object> CreateItem(string id, string partitionKeyProperty, string partitionKeyValue, string state)
        {
            return new Dictionary<string, object>
            {
                ["id"] = id,
                [partitionKeyProperty] = partitionKeyValue,
                ["state"] = state,
            };
        }

        private static void Expect(
            ItemResponse<Dictionary<string, object>> response,
            HttpStatusCode expectedStatus,
            string expectedId,
            string expectedState,
            HashSet<string> transports)
        {
            transports.Add(GetTransport(response.Diagnostics));
            Check(response.StatusCode == expectedStatus, $"status {(int)response.StatusCode}, expected {(int)expectedStatus}");
            Check(response.Resource["id"].ToString() == expectedId, $"returned id '{Escape(response.Resource["id"].ToString())}'");
            Check(response.Resource["state"].ToString() == expectedState, $"state was '{response.Resource["state"]}', expected '{expectedState}'");
        }

        private static string GetTransport(CosmosDiagnostics diagnostics)
        {
            string text = diagnostics.ToString();
            if (text.Contains("rntbd://"))
            {
                return SpecialCharacterIdScenarios.Direct;
            }

            return text.Contains(":10250/") ? SpecialCharacterIdScenarios.ThinClient : SpecialCharacterIdScenarios.Gateway;
        }

        private static async Task<List<Dictionary<string, object>>> QueryByIdAsync(Container container, string id, PartitionKey partitionKey)
        {
            List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();
            using (FeedIterator<Dictionary<string, object>> iterator = container.GetItemQueryIterator<Dictionary<string, object>>(
                new QueryDefinition("SELECT * FROM c WHERE c.id = @id").WithParameter("@id", id),
                requestOptions: new QueryRequestOptions { PartitionKey = partitionKey }))
            {
                while (iterator.HasMoreResults)
                {
                    items.AddRange(await iterator.ReadNextAsync());
                }
            }

            return items;
        }

        private static async Task DeleteIgnoringErrorsAsync(Container container, string id, PartitionKey partitionKey)
        {
            try
            {
                // Batch carries the id in the body, so cleanup works even when the URI is wrong.
                await container.CreateTransactionalBatch(partitionKey).DeleteItem(id).ExecuteAsync();
            }
            catch (Exception)
            {
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static string Summarize(Exception ex)
        {
            return ex is CosmosException cosmosException
                ? $"{(int)cosmosException.StatusCode}/{cosmosException.SubStatusCode}"
                : ex.Message;
        }
    }
}

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
            public Scenario(string category, string name, Func<string, string> createId)
            {
                this.Category = category;
                this.Name = name;
                this.CreateId = createId;
            }

            public string Category { get; }

            public string Name { get; }

            public Func<string, string> CreateId { get; }
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

        internal static readonly IReadOnlyList<Scenario> Scenarios = new List<Scenario>
        {
            // Legal in a URI path: the request URI must stay exactly as it was before the fix.
            new Scenario("No escaping", "GUID", b => b),
            new Scenario("No escaping", "legal punctuation", b => b + "-_.!~*'();:@&=+$,"),
            new Scenario("No escaping", "base64-like", b => b + "Ab+Cd=="),
            new Scenario("No escaping", "interior dot", b => "a." + b),

            // Whitespace that System.Uri trims from the ends of a URI string.
            new Scenario("Trimmed whitespace", "trailing space", b => b + " "),
            new Scenario("Trimmed whitespace", "two trailing spaces", b => b + "  "),
            new Scenario("Trimmed whitespace", "trailing tab", b => b + "\t"),
            new Scenario("Trimmed whitespace", "trailing LF", b => b + "\n"),
            new Scenario("Trimmed whitespace", "trailing CR", b => b + "\r"),
            new Scenario("Trimmed whitespace", "trailing CRLF", b => b + "\r\n"),
            new Scenario("Trimmed whitespace", "leading space", b => " " + b),
            new Scenario("Trimmed whitespace", "leading tab", b => "\t" + b),
            new Scenario("Trimmed whitespace", "leading and trailing space", b => " " + b + " "),

            // ASCII characters that are not legal in a URI path.
            new Scenario("Illegal ASCII", "interior space", b => "a b" + b),
            new Scenario("Illegal ASCII", "double quote", b => "a\"b" + b),
            new Scenario("Illegal ASCII", "angle brackets", b => "a<b>" + b),
            new Scenario("Illegal ASCII", "braces", b => "a{b}" + b),
            new Scenario("Illegal ASCII", "square brackets", b => "a[b]" + b),
            new Scenario("Illegal ASCII", "pipe, caret, backtick", b => "a|^`" + b),

            // URI delimiters that would otherwise start a query or fragment, or act as a path separator.
            new Scenario("URI delimiter", "question mark", b => "a?b" + b),
            new Scenario("URI delimiter", "hash", b => "a#b" + b),
            new Scenario("URI delimiter", "backslash", b => "a\\b" + b),
            new Scenario("URI delimiter", "trailing question mark", b => b + "?"),
            new Scenario("URI delimiter", "trailing hash", b => b + "#"),

            // Literal percent signs must be sent as %25 and never decoded into another character.
            new Scenario("Literal percent", "literal %20", b => "a%20b" + b),
            new Scenario("Literal percent", "literal %2F", b => "a%2Fb" + b),
            new Scenario("Literal percent", "literal %3F", b => "a%3Fb" + b),
            new Scenario("Literal percent", "literal %2520", b => "a%2520b" + b),
            new Scenario("Literal percent", "trailing percent", b => b + "100%"),
            new Scenario("Literal percent", "invalid escape", b => "a%zz" + b),

            // Trailing dots, alone or mixed with whitespace.
            new Scenario("Dots", "trailing dot", b => b + "."),
            new Scenario("Dots", "two trailing dots", b => b + ".."),
            new Scenario("Dots", "dot then space", b => b + ". "),
            new Scenario("Dots", "space then dot", b => b + " ."),

            // Non-ASCII characters, which System.Uri already encodes as UTF-8.
            new Scenario("Non-ASCII", "trailing NBSP", b => b + "\u00A0"),
            new Scenario("Non-ASCII", "NBSP then space", b => b + "\u00A0 "),
            new Scenario("Non-ASCII", "trailing ideographic space", b => b + "\u3000"),
            new Scenario("Non-ASCII", "accented and CJK", b => "\u00E9\u6F22\u5B57" + b),
            new Scenario("Non-ASCII", "emoji then space", b => "\uD83D\uDE00" + b + " "),

            // Every character escaped, close to the 1023-byte id limit.
            new Scenario("Long", "1000 escaped characters", b => b + new string('%', 500) + new string(' ', 464)),
        };

        /// <summary>
        /// Pairs of ids that must resolve to different items.
        /// </summary>
        internal static readonly IReadOnlyList<(string Name, Func<string, string> First, Func<string, string> Second)> Twins =
            new List<(string, Func<string, string>, Func<string, string>)>
            {
                ("trailing space vs. trimmed", b => b + " ", b => b),
                ("leading space vs. trimmed", b => " " + b, b => b),
                ("trailing dot vs. trimmed", b => b + ".", b => b),
                ("literal %20 vs. space", b => b + "a%20b", b => b + "a b"),
                ("literal %3F vs. question mark", b => b + "a%3Fb", b => b + "a?b"),
            };

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

            foreach ((string name, Func<string, string> first, Func<string, string> second) in SpecialCharacterIdScenarios.Twins)
            {
                string baseId = Guid.NewGuid().ToString("N").Substring(0, 8);
                results.Add(await SpecialCharacterIdScenarios.RunTwinsAsync(container, name, first(baseId), second(baseId), partitionKeyProperty, partitionKeyValue, partitionKey));
            }

            return results;
        }

        internal static string Describe(IEnumerable<Result> results)
        {
            return string.Join(
                Environment.NewLine,
                results.Select(r => $"[{r.Scenario.Category}] {r.Scenario.Name} ({Escape(r.Id)}): " +
                    (r.Succeeded ? "OK" : $"{r.FailedOperation} failed: {r.Error}")));
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
            string firstId,
            string secondId,
            string partitionKeyProperty,
            string partitionKeyValue,
            PartitionKey partitionKey)
        {
            Scenario scenario = new Scenario("Twins", name, _ => firstId);
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

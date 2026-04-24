//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Mocks
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// HTTP handler for the Direct-mode end-to-end benchmark harness. Intercepts every gateway
    /// request the SDK issues during cold start and serves it locally, using the same JSON shapes
    /// that <c>Microsoft.Azure.Cosmos.Tests.MockSetupsHelper</c> uses for its production-shaped
    /// "real-CosmosClient with faked gateway" tests. Dispatches by request path so one handler
    /// instance serves the global endpoint, the regional endpoints, and the addresses endpoint.
    ///
    /// Supported routes (any other route triggers <see cref="InvalidOperationException"/>):
    ///   GET /                                                 → <see cref="AccountProperties"/>
    ///   GET /dbs/{dbName}                                     → <see cref="DatabaseProperties"/>
    ///   GET /dbs/{dbName}/colls/{collName}                    → <see cref="ContainerProperties"/>  (also matches RID)
    ///   GET /dbs/{dbRid}/colls/{collRid}/pkranges             → /pkranges feed (200 → 304 …)
    ///   GET //addresses/?$resolveFor=…&$partitionKeyRangeIds= → addresses feed
    /// </summary>
    internal sealed class PkRangeMetadataHandler : HttpMessageHandler
    {
        internal const string PkRangeFeedEtag = "pkr-v1";

        private readonly string accountName;
        private readonly string regionEndpoint;
        private readonly string databaseName;
        private readonly string databaseRid;
        private readonly string containerName;
        private readonly string containerRid;
        private readonly ResourceId containerResourceId;
        private readonly IReadOnlyList<PartitionKeyRange> ranges;
        private readonly string accountJson;
        private readonly string databaseJson;
        private readonly string containerJson;
        private readonly string pkRangesJson;

        public int AccountHits;
        public int DatabaseHits;
        public int ContainerHits;
        public int PkRangesHits200;
        public int PkRangesHits304;
        public int AddressesHits;
        public readonly List<string> UnknownUrls = new List<string>();

        public PkRangeMetadataHandler(
            string accountName,
            string regionEndpoint,
            string databaseName,
            string databaseRid,
            string containerName,
            string containerRid,
            IReadOnlyList<PartitionKeyRange> ranges)
        {
            this.accountName = accountName;
            this.regionEndpoint = regionEndpoint.TrimEnd('/');
            this.databaseName = databaseName;
            this.databaseRid = databaseRid;
            this.containerName = containerName;
            this.containerRid = containerRid;
            this.containerResourceId = ResourceId.Parse(containerRid);
            this.ranges = ranges;

            this.accountJson = BuildAccountJson(accountName, this.regionEndpoint);
            this.databaseJson = BuildDatabaseJson(databaseRid, databaseName);
            this.containerJson = BuildContainerJson(containerRid, containerName);
            this.pkRangesJson = BuildPkRangesJson(this.containerResourceId, ranges);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri.AbsolutePath;
            string query = request.RequestUri.Query;

            // 1) Account root: GET /
            if (string.IsNullOrEmpty(path) || path == "/")
            {
                Interlocked.Increment(ref this.AccountHits);
                return Task.FromResult(Ok(this.accountJson));
            }

            // 2) Addresses: path contains "addresses" AND query has $resolveFor.
            //    The SDK emits a leading double-slash (//addresses/), so a strict segment-count
            //    check would mis-match; instead we require both a path token and the resolveFor
            //    query parameter to avoid colliding with future container/document routes.
            if (path.IndexOf("addresses", StringComparison.OrdinalIgnoreCase) >= 0
                && query.IndexOf("$resolveFor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Interlocked.Increment(ref this.AddressesHits);
                return Task.FromResult(Ok(this.BuildAddressesResponse(query)));
            }

            // 3) PKRanges: path ends with /pkranges
            if (path.EndsWith("/pkranges", StringComparison.OrdinalIgnoreCase))
            {
                string ifNoneMatch = request.Headers.IfNoneMatch?.ToString();
                if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch.Contains(PkRangeFeedEtag))
                {
                    Interlocked.Increment(ref this.PkRangesHits304);
                    HttpResponseMessage notModified = new HttpResponseMessage(HttpStatusCode.NotModified);
                    notModified.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue($"\"{PkRangeFeedEtag}\"");
                    return Task.FromResult(notModified);
                }

                Interlocked.Increment(ref this.PkRangesHits200);
                HttpResponseMessage ok = Ok(this.pkRangesJson);
                ok.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue($"\"{PkRangeFeedEtag}\"");
                return Task.FromResult(ok);
            }

            // 4) Container metadata: /dbs/{x}/colls/{y}
            if (IsContainerPath(path))
            {
                Interlocked.Increment(ref this.ContainerHits);
                return Task.FromResult(Ok(this.containerJson));
            }

            // 5) Database metadata: /dbs/{x}
            if (IsDatabasePath(path))
            {
                Interlocked.Increment(ref this.DatabaseHits);
                return Task.FromResult(Ok(this.databaseJson));
            }

            // Unknown — capture and fail.
            lock (this.UnknownUrls)
            {
                this.UnknownUrls.Add(request.RequestUri.ToString());
            }

            throw new InvalidOperationException(
                $"PkRangeMetadataHandler: unexpected URL {request.Method} {request.RequestUri}");
        }

        private static bool IsContainerPath(string absolutePath)
        {
            string[] parts = absolutePath.Trim('/').Split('/');
            return parts.Length == 4
                && string.Equals(parts[0], "dbs", StringComparison.OrdinalIgnoreCase)
                && string.Equals(parts[2], "colls", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDatabasePath(string absolutePath)
        {
            string[] parts = absolutePath.Trim('/').Split('/');
            return parts.Length == 2
                && string.Equals(parts[0], "dbs", StringComparison.OrdinalIgnoreCase);
        }

        private static HttpResponseMessage Ok(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private string BuildAddressesResponse(string rawQuery)
        {
            NameValueCollection qs = System.Web.HttpUtility.ParseQueryString(rawQuery ?? string.Empty);
            string csv = qs["$partitionKeyRangeIds"] ?? string.Empty;
            string[] rangeIds = csv.Length == 0
                ? new[] { "M" } // master partition: SDK didn't ask for specific PKRs
                : csv.Split(',');

            List<Address> addresses = new List<Address>();
            int port = 14382;
            foreach (string rangeId in rangeIds)
            {
                for (int i = 0; i < 4; i++)
                {
                    addresses.Add(new Address()
                    {
                        IsPrimary = i == 0,
                        PartitionKeyRangeId = rangeId,
                        PhysicalUri = $"rntbd://mock-replica-{rangeId}-{i}.documents.azure.com:{port++}/apps/a/services/s/partitions/p/replicas/{rangeId}-{i}{(i == 0 ? "p" : "s")}/",
                        Protocol = "rntbd",
                        PartitionIndex = "7718513@164605136"
                    });
                }
            }

            JObject body = new JObject
            {
                { "_rid", this.containerRid },
                { "_count", addresses.Count },
                { "Addresss", JArray.FromObject(addresses) }
            };
            return body.ToString(Formatting.None);
        }

        private static string BuildAccountJson(string accountName, string regionEndpoint)
        {
            AccountRegion region = new AccountRegion()
            {
                Name = "East US",
                Endpoint = regionEndpoint + "/"
            };
            AccountProperties account = new AccountProperties()
            {
                Id = accountName,
                WriteLocationsInternal = new Collection<AccountRegion>() { region },
                ReadLocationsInternal = new Collection<AccountRegion>() { region },
                EnableMultipleWriteLocations = false,
                Consistency = new AccountConsistency()
                {
                    DefaultConsistencyLevel = Cosmos.ConsistencyLevel.Session
                },
                SystemReplicationPolicy = new ReplicationPolicy()
                {
                    MinReplicaSetSize = 3,
                    MaxReplicaSetSize = 4
                },
                ReadPolicy = new ReadPolicy()
                {
                    PrimaryReadCoefficient = 1,
                    SecondaryReadCoefficient = 1
                },
                ReplicationPolicy = new ReplicationPolicy()
                {
                    AsyncReplication = false,
                    MinReplicaSetSize = 3,
                    MaxReplicaSetSize = 4
                }
            };
            return JsonConvert.SerializeObject(account);
        }

        private static string BuildDatabaseJson(string databaseRid, string databaseName)
        {
            JObject db = new JObject
            {
                { "id", databaseName },
                { "_rid", databaseRid },
                { "_self", $"dbs/{databaseRid}/" },
                { "_etag", "\"00000000-0000-0000-0000-000000000000\"" },
                { "_colls", "colls/" },
                { "_users", "users/" },
                { "_ts", 0 },
            };
            return db.ToString(Formatting.None);
        }

        private static string BuildContainerJson(string containerRid, string containerName)
        {
            ContainerProperties container = ContainerProperties.CreateWithResourceId(containerRid);
            container.Id = containerName;
            container.IndexingPolicy = new Cosmos.IndexingPolicy()
            {
                IndexingMode = Cosmos.IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths = new Collection<Cosmos.IncludedPath>() { new Cosmos.IncludedPath() { Path = "/*" } },
                ExcludedPaths = new Collection<Cosmos.ExcludedPath>() { new Cosmos.ExcludedPath() { Path = "/_etag/?" } }
            };
            container.PartitionKey = new Documents.PartitionKeyDefinition()
            {
                Paths = new Collection<string>() { "/pk" },
                Kind = Documents.PartitionKind.Hash,
                Version = Documents.PartitionKeyDefinitionVersion.V2
            };
            return JsonConvert.SerializeObject(container);
        }

        private static string BuildPkRangesJson(ResourceId containerResourceId, IReadOnlyList<PartitionKeyRange> ranges)
        {
            JObject body = new JObject
            {
                { "_rid", containerResourceId.DocumentCollectionId.ToString() },
                { "_count", ranges.Count },
                { "PartitionKeyRanges", JArray.FromObject(ranges) }
            };
            return body.ToString(Formatting.None);
        }
    }
}

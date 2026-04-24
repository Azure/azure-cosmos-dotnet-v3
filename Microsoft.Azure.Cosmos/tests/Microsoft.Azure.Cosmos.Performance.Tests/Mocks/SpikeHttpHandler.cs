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
    /// Stage 2 spike: intercepts every HTTP request the SDK issues during cold start
    /// and answers it locally with the same JSON shapes that
    /// <c>Microsoft.Azure.Cosmos.Tests.MockSetupsHelper</c> uses for its existing
    /// "real-CosmosClient with faked gateway" tests. No pattern matching on hosts —
    /// we dispatch by path so the same handler serves the global endpoint, the
    /// regional endpoints, and the addresses endpoint.
    ///
    /// Supported routes (fail fast on anything else):
    ///   GET /                                                → AccountProperties
    ///   GET /dbs/{dbName}/colls/{collName}                   → ContainerProperties
    ///   GET /dbs/{dbRid}/colls/{collRid}/pkranges            → feed (200 → 304 → 304 …)
    ///   GET /addresses?...&$partitionKeyRangeIds={csv}       → echo
    /// </summary>
    internal sealed class SpikeHttpHandler : HttpMessageHandler
    {
        internal const string PkRangeFeedEtag = "pkr-v1";

        private readonly string accountName;
        private readonly string regionEndpoint;
        private readonly string databaseName;
        private readonly string containerName;
        private readonly string containerRid;
        private readonly ResourceId containerResourceId;
        private readonly IReadOnlyList<PartitionKeyRange> ranges;
        private readonly string accountJson;
        private readonly string containerJson;
        private readonly string pkRangesJson;

        public int AccountHits;
        public int ContainerHits;
        public int PkRangesHits200;
        public int PkRangesHits304;
        public int AddressesHits;
        public readonly List<string> UnknownUrls = new List<string>();

        public SpikeHttpHandler(
            string accountName,
            string regionEndpoint,
            string databaseName,
            string containerName,
            string containerRid,
            IReadOnlyList<PartitionKeyRange> ranges)
        {
            this.accountName = accountName;
            this.regionEndpoint = regionEndpoint.TrimEnd('/');
            this.databaseName = databaseName;
            this.containerName = containerName;
            this.containerRid = containerRid;
            this.containerResourceId = ResourceId.Parse(containerRid);
            this.ranges = ranges;

            this.accountJson = BuildAccountJson(accountName, this.regionEndpoint);
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

            // 2) Addresses: path contains /addresses
            if (path.IndexOf("/addresses", StringComparison.OrdinalIgnoreCase) >= 0)
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

            // 4) Container metadata: /dbs/{x}/colls/{y}  (no trailing /pkranges, /docs, etc.)
            if (IsContainerPath(path))
            {
                Interlocked.Increment(ref this.ContainerHits);
                return Task.FromResult(Ok(this.containerJson));
            }

            // Unknown — capture and fail.
            lock (this.UnknownUrls)
            {
                this.UnknownUrls.Add(request.RequestUri.ToString());
            }

            throw new InvalidOperationException(
                $"SpikeHttpHandler: unexpected URL {request.Method} {request.RequestUri}");
        }

        private static bool IsContainerPath(string absolutePath)
        {
            // /dbs/{db}/colls/{coll}   (4 segments after leading slash) — no /docs or /pkranges
            string[] parts = absolutePath.Trim('/').Split('/');
            return parts.Length == 4
                && string.Equals(parts[0], "dbs", StringComparison.OrdinalIgnoreCase)
                && string.Equals(parts[2], "colls", StringComparison.OrdinalIgnoreCase);
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
                ? Array.Empty<string>()
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

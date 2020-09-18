//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    public sealed class QueryOracle
    {
        private readonly string collectionLink;
        private readonly int targetNumberOfQueriesToValidate;
        private readonly DocumentClient client;
        private readonly Dictionary<SqlKeyValueQueryBuilder, HashSet<string>> invertedIndex;
        private readonly Dictionary<SqlKeyValueQueryBuilder, HashSet<string>> failedQueries;
        private int retryCount;
        //retry failed queries for a fixed number of times before bailing out
        private readonly bool enableRetries;

        public QueryOracle(Uri gatewayUri, string masterKey, string collectionLink, bool enableRetries,
                           int targetNumberOfQueriesToValidate = int.MaxValue)
            : this(
                new DocumentClient(gatewayUri, masterKey, desiredConsistencyLevel: Documents.ConsistencyLevel.Session, handler: null),
                collectionLink, enableRetries, targetNumberOfQueriesToValidate)
        {
        }

        internal QueryOracle(DocumentClient client, string collectionLink, bool enableRetries, int targetNumberOfQueriesToValidate = int.MaxValue)
        {
            this.collectionLink = collectionLink;
            this.targetNumberOfQueriesToValidate = targetNumberOfQueriesToValidate;
            this.invertedIndex = new Dictionary<SqlKeyValueQueryBuilder, HashSet<string>>();
            this.failedQueries = new Dictionary<SqlKeyValueQueryBuilder, HashSet<string>>();
            this.client = client;
            this.retryCount = 0;
            this.enableRetries = enableRetries;
        }

        public int IndexAndValidate(int pageSize = 1000)
        {
            Trace.TraceInformation("Using collection {0}", this.collectionLink);

            DateTime startTime = DateTime.Now;
            this.ReadAllDocsAndBuildInvertedIndex();

            int toReturn = this.QueryAndVerifyDocuments(pageSize).Result;
            Trace.TraceInformation("Inverted index creation and query took {0} ms", (DateTime.Now - startTime).TotalMilliseconds);
            return toReturn;
        }

        private static async Task<T> AsyncRetryRateLimiting<T>(Func<Task<T>> work)
        {
            while (true)
            {
                TimeSpan retryAfter = TimeSpan.FromSeconds(1);

                try
                {
                    return await work();
                }
                catch (DocumentClientException e)
                {
                    const int TooManyRequests = 429;
                    if ((int)e.StatusCode == TooManyRequests)
                    {
                        if (e.RetryAfter.TotalMilliseconds > 0)
                        {
                            retryAfter = new[] { e.RetryAfter, TimeSpan.FromSeconds(1) }.Max();
                        }
                    }
                    else
                    {
                        throw;
                    }
                }

                await Task.Delay(retryAfter);
            }
        }

        private void ReadAllDocsAndBuildInvertedIndex()
        {
            string cont = null;
            do
            {
                DocumentFeedResponse<dynamic> response = AsyncRetryRateLimiting(() => this.client.ReadDocumentFeedAsync(this.collectionLink, new FeedOptions { RequestContinuationToken = cont, MaxItemCount = 1000, EnableCrossPartitionQuery = true })).Result;

                Trace.TraceInformation(DateTime.Now.ToString("HH:mm:ss.ffff") + ": Indexing {0} documents", response.Count);

                foreach (JToken doc in response)
                {
                    this.BuildInvertedIndex(doc, doc["_rid"].ToString(), "r");
                }

                cont = response.ResponseContinuation;
            } while (cont != null);
        }

        private void BuildInvertedIndex(JToken token, string id, string pathSoFar)
        {
            if (token.Type == JTokenType.Object)
            {
                foreach (KeyValuePair<string, JToken> kv in (JObject)token)
                {
                    if (kv.Key == "_attachments")
                    {
                        continue;
                    }

                    string indexKey = pathSoFar + "[\"" + kv.Key + "\"]";
                    this.BuildInvertedIndex(kv.Value, id, indexKey);
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                JArray array = (JArray)token;
                if (array.HasValues)
                {
                    int index = 0;
                    foreach (JToken elem in array)
                    {
                        string indexKey = pathSoFar + "[" + index + "]";
                        this.BuildInvertedIndex(elem, id, indexKey);
                        index++;
                    }
                }
            }
            else if (token.Type == JTokenType.Integer ||
                token.Type == JTokenType.Float ||
                token.Type == JTokenType.Boolean)
            {
                this.AddToInvertedIndex(new SqlKeyValueQueryBuilder(pathSoFar, token.ToString().ToLower()), id);
            }
            else if (token.Type == JTokenType.String)
            {
                string tokenStr = token.ToString().Replace("\"", "\\\"");
                this.AddToInvertedIndex(new SqlKeyValueQueryBuilder(pathSoFar, "\"" + tokenStr + "\""), id);
            }
            else if (token.Type == JTokenType.Null)
            {
                this.AddToInvertedIndex(new SqlKeyValueQueryBuilder(pathSoFar, "/null"), id);
            }
            else if (token.Type == JTokenType.Date)
            {
                // Convert the JToken to raw string format. Serializer reutrns a strign of form "new Date(13234820394)".
                // We need to make it to "/Date(13234820394)/
                JavaScriptDateTimeConverter dateTimeConverter = new JavaScriptDateTimeConverter();
                string jsonString = JsonConvert.SerializeObject(token, dateTimeConverter);
                this.AddToInvertedIndex(new SqlKeyValueQueryBuilder(pathSoFar, "/\"/" + jsonString.Substring(4) + "/\""), id);
            }
            else
            {
                string errorMessage = string.Format("Unknown type: {0}, token: {1}", token.Type, token.ToString());
                Debug.Assert(false, errorMessage);
                throw new NotSupportedException(errorMessage);
            }
        }

        private async Task<int> QueryAndVerifyDocuments(int pageSize = 1000)
        {
            Random rnd = new Random();

            TimeSpan totalQueryLatencyAllPages = TimeSpan.FromSeconds(0);
            int numberOfPages = 0;
            int numberOfQueries = 0;
            int resultCount = 0;

            bool anyFailures = false;

            foreach (KeyValuePair<SqlKeyValueQueryBuilder, HashSet<string>> keyVal in this.invertedIndex)
            {
                HashSet<string> idSet = new HashSet<string>();
                List<string> activityIDsAllQueryPages = new List<string>();
                string query = keyVal.Key.GetQuery();

                if (this.invertedIndex.Count > this.targetNumberOfQueriesToValidate)
                {
                    double percentageOfQueriesToExecute = this.targetNumberOfQueriesToValidate / (1.0 * this.invertedIndex.Count);
                    if (rnd.NextDouble() > percentageOfQueriesToExecute)
                    {
                        continue;
                    }
                }

                if (numberOfQueries > 0 && numberOfQueries % 100 == 0)
                {
                    Trace.TraceInformation(DateTime.Now.ToString("HH:mm:ss.ffff") + @": Executing query {0} of {1}",
                                           numberOfQueries + 1, this.invertedIndex.Count);
                    Trace.TraceInformation(@"    Query latency per page (avg ms) {0} after {1} pages",
                                           totalQueryLatencyAllPages.TotalMilliseconds / numberOfPages, numberOfPages);
                    Trace.TraceInformation(@"    Query latency per query (avg ms) {0} after {1} queries",
                                           totalQueryLatencyAllPages.TotalMilliseconds / numberOfQueries, numberOfQueries);
                    Trace.TraceInformation(@"    Number of results per page {0} after {1} pages",
                                           resultCount / (double)numberOfPages, numberOfPages);
                    Trace.TraceInformation(@"    Number of results per query {0} after {1} queries",
                                           resultCount / (double)numberOfQueries, numberOfQueries);
                }

                IDocumentQuery<dynamic> docQuery = this.client.CreateDocumentQuery(this.collectionLink, query, feedOptions: new FeedOptions { MaxItemCount = pageSize, EnableCrossPartitionQuery = true }).AsDocumentQuery();
                while (docQuery.HasMoreResults)
                {
                    DateTime startTime = DateTime.Now;
                    DocumentFeedResponse<dynamic> queryResultsPage = await QueryOnePageWithRetry(docQuery, query);
                    activityIDsAllQueryPages.Add(queryResultsPage.ActivityId);
                    totalQueryLatencyAllPages += DateTime.Now - startTime;
                    numberOfPages++;
                    foreach (JObject result in queryResultsPage)
                    {
                        resultCount++;
                        string id = result["_rid"].ToString();
                        if (!keyVal.Value.Contains(id))
                        {

                            Trace.TraceInformation(
                                DateTime.Now.ToString("HH:mm:ss.ffff") +
                                @": The doc id {0} for query {1} was not expected in the results, query activityId: {2}", id, query, queryResultsPage.ActivityId);

                            anyFailures = true;
                            continue;
                        }

                        if (idSet.Contains(id))
                        {
                            Trace.TraceInformation(
                                DateTime.Now.ToString("HH:mm:ss.ffff") +
                                @": Same document id {0} returned twice for query ({1}), query activityId: {2}", id, query, queryResultsPage.ActivityId);

                            anyFailures = true;
                        }
                        else
                        {
                            idSet.Add(id);
                        }
                    }
                }
                numberOfQueries++;

                foreach (string queryOracleId in keyVal.Value)
                {
                    if (!idSet.Contains(queryOracleId))
                    {
                        Trace.TraceInformation(
                            DateTime.Now.ToString("HH:mm:ss.ffff") +
                            @": The doc id {0} was expected for query {1} but was not obtained, query all pages activitiIDs: ({2})", queryOracleId, query, string.Join(",", activityIDsAllQueryPages));

                        if (!this.failedQueries.ContainsKey(keyVal.Key))
                        {
                            this.failedQueries.Add(keyVal.Key, keyVal.Value);
                        }

                        anyFailures = true;
                        continue;
                    }
                }
            }

            if (!anyFailures)
            {
                Trace.TraceInformation(@"*** TEST PASSED ***");
                return 0;
            }
            else
            {
                Trace.TraceInformation(@"*** TEST FAILED ***");
                int result = -1;

                //In case of a failure, retry only failed queries after sleeping for couple of minutes.
                if (this.enableRetries && this.retryCount < 10)
                {
                    Trace.TraceInformation(string.Format(CultureInfo.InvariantCulture, @"*** Retrying {0} Failed queries ***", this.failedQueries.Count));
                    this.retryCount++;
                    Thread.Sleep(120 * 1000);
                    result = await this.RetryFailedQueries(pageSize);
                }

                return result;
            }
        }

        public async Task<int> RetryFailedQueries(int pageSize)
        {
            this.invertedIndex.Clear();
            foreach (SqlKeyValueQueryBuilder key in this.failedQueries.Keys)
            {
                this.invertedIndex.Add(key, this.failedQueries[key]);
            }

            this.failedQueries.Clear();
            int result = await this.QueryAndVerifyDocuments(pageSize);
            return result;
        }

        private void AddToInvertedIndex(SqlKeyValueQueryBuilder indexKey, string val)
        {
            if (!this.invertedIndex.ContainsKey(indexKey))
            {
                this.invertedIndex.Add(indexKey, new HashSet<string>());
            }
            Debug.Assert(!this.invertedIndex[indexKey].Contains(val), string.Format(CultureInfo.InvariantCulture, "Same path ({0}) is found twice in the same document {1}", indexKey, val));
            this.invertedIndex[indexKey].Add(val);
        }

        private static async Task<DocumentFeedResponse<dynamic>> QueryOnePageWithRetry(IDocumentQuery<dynamic> query, string queryString)
        {
            int nMaxRetry = 5;

            do
            {
                try
                {
                    return await query.ExecuteNextAsync();
                }
                catch (DocumentClientException exc)
                {
                    Trace.TraceInformation("Activity Id: {0}", exc.ActivityId);
                    Trace.TraceInformation("Query String: {0}", queryString);
                    if ((int)exc.StatusCode != 429)
                    {
                        throw;
                    }

                    if (--nMaxRetry > 0)
                    {
                        Trace.TraceInformation("Sleeping for {0} due to throttle", exc.RetryAfter.TotalSeconds);
                        Thread.Sleep(exc.RetryAfter);
                    }
                    else
                    {
                        throw;
                    }
                }
            } while (true);
        }

        private class SqlKeyValueQueryBuilder : IEquatable<SqlKeyValueQueryBuilder>
        {
            private readonly string strKey;
            private readonly string strValue;

            public SqlKeyValueQueryBuilder(string predicate, string value)
            {
                this.strKey = predicate;
                this.strValue = value;
            }

            public string GetQuery()
            {
                return string.Format(CultureInfo.InvariantCulture, @"SELECT r._rid FROM root r WHERE {0} = {1}", this.strKey, this.strValue);
            }

            public override int GetHashCode()
            {
                return (this.strKey.GetHashCode() * 31) + this.strValue.GetHashCode();
            }

            public override bool Equals(object operandObj)
            {
                return this.Equals(operandObj as SqlKeyValueQueryBuilder);
            }

            public bool Equals(SqlKeyValueQueryBuilder other)
            {
                return other != null && other.strKey.Equals(this.strKey) && other.strValue.Equals(this.strValue);
            }
        }
    }
}
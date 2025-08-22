namespace Microsoft.Azure.Cosmos.Tests.Aot.Common
{
    using System;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class RestHelper
    {
        private readonly string accountEndpoint;
        private readonly string cosmosKey;
        private readonly string baseUrl;
        private readonly HttpClient httpClient;
        private readonly bool outputToConsole;

        public RestHelper(string accountEndpoint, string cosmosKey, bool outputToConsole = false)
        {
            this.accountEndpoint = accountEndpoint;
            this.cosmosKey = cosmosKey;
            this.baseUrl = $"{accountEndpoint}/";
            this.httpClient = new HttpClient();
            this.outputToConsole = outputToConsole;
        }

        #region Database Operations

        public async Task CreateDatabase(string databaseId, DatabaseThoughputMode mode)
        {
            HttpMethod method = HttpMethod.Post;

            ResourceType resourceType = ResourceType.dbs;
            string resourceLink = $"";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");

            if (mode == DatabaseThoughputMode.@fixed)
                this.httpClient.DefaultRequestHeaders.Add("x-ms-offer-throughput", "400");
            if (mode == DatabaseThoughputMode.autopilot)
                this.httpClient.DefaultRequestHeaders.Add("x-ms-cosmos-offer-autopilot-settings", "{\"maxThroughput\": 4000}");

            Uri requestUri = new Uri($"{this.baseUrl}/dbs");
            string requestBody = $"{{\"id\":\"{databaseId}\"}}";
            StringContent requestContent = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, Content = requestContent, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"Create Database with thoughput mode {mode}:", httpResponse);
        }

        public async Task ListDatabases()
        {
            HttpMethod method = HttpMethod.Get;

            ResourceType resourceType = ResourceType.dbs;
            string resourceLink = string.Empty;
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");

            Uri requestUri = new Uri($"{this.baseUrl}/dbs");
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"List Databases:", httpResponse);
        }

        public async Task GetDatabase(string databaseId)
        {
            HttpMethod method = HttpMethod.Get;

            ResourceType resourceType = ResourceType.dbs;
            string resourceLink = $"dbs/{databaseId}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}");
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"Get Database with id: '{databaseId}' :", httpResponse);
        }

        public async Task DeleteDatabase(string databaseId)
        {
            HttpMethod method = HttpMethod.Delete;

            ResourceType resourceType = ResourceType.dbs;
            string resourceLink = $"dbs/{databaseId}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}");
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput("Delete Database", httpResponse);
        }

        #endregion


        #region Container Operations

        public async Task CreateContainer(string databaseId, string containerId, DatabaseThoughputMode mode)
        {
            HttpMethod method = HttpMethod.Post;

            ResourceType resourceType = ResourceType.colls;
            string resourceLink = $"dbs/{databaseId}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");

            if (mode == DatabaseThoughputMode.@fixed)
                this.httpClient.DefaultRequestHeaders.Add("x-ms-offer-throughput", "400");
            if (mode == DatabaseThoughputMode.autopilot)
                this.httpClient.DefaultRequestHeaders.Add("x-ms-cosmos-offer-autopilot-settings", "{\"maxThroughput\": 4000}");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}/colls");
            string requestBody = $@"{{
        ""id"":""{containerId}"",
         ""partitionKey"": {{  
            ""paths"": [
              ""/pk""  
            ],  
            ""kind"": ""Hash"",
             ""Version"": 2
          }}  
        }}";
            StringContent requestContent = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, Content = requestContent, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"Create Container with thoughput mode {mode}:", httpResponse);
        }

        public async Task GetContainer(string databaseId, string containerId)
        {
            HttpMethod method = HttpMethod.Get;

            ResourceType resourceType = ResourceType.colls;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}");
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"Get Container with id: '{databaseId}' :", httpResponse);
        }


        public async Task DeleteContainer(string databaseId, string containerId)
        {
            HttpMethod method = HttpMethod.Delete;

            ResourceType resourceType = ResourceType.colls;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}");
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput("Delete Container", httpResponse);
        }

        public async Task GetContainerPartitionKeys(string databaseId, string containerId)
        {
            HttpMethod method = HttpMethod.Get;

            ResourceType resourceType = ResourceType.pkranges;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");
            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}/pkranges");

            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"Get Partition Key Ranges for collection '{containerId}':", httpResponse);
        }

        #endregion

        #region Stored Procedures

        public async Task CreateStoredProcedure(string databaseId, string containerId, string storedProcedureName)
        {
            HttpMethod method = HttpMethod.Post;

            ResourceType resourceType = ResourceType.sprocs;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}/sprocs");
            string requestBody = $@"{{
            ""body"": ""function (testParam) {{ var context = getContext(); var response = context.getResponse(); response.setBody(\""Hello, \""+testParam);}}"",
            ""id"":""{storedProcedureName}""
        }}";

            StringContent requestContent = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, Content = requestContent, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"Create Stored procedure '{storedProcedureName}' on container '{containerId}' :", httpResponse);
        }

        public async Task DeleteStoredProcedure(string databaseId, string containerId, string storedProcedureName)
        {
            HttpMethod method = HttpMethod.Delete;

            ResourceType resourceType = ResourceType.sprocs;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}/sprocs/{storedProcedureName}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}");
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"Delete Stored Procedure '{storedProcedureName}", httpResponse);
        }

        public async Task ExecuteStoredProcedure(string databaseId, string containerId, string storedProcedureName)
        {
            HttpMethod method = HttpMethod.Post;

            ResourceType resourceType = ResourceType.sprocs;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}/sprocs/{storedProcedureName}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}");
            string requestBody = $@"[""test param""]";

            StringContent requestContent = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, Content = requestContent, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"Executed Stored Procedure '{storedProcedureName}", httpResponse);
        }

        #endregion

        public async Task CreateDocument(string databaseId, string containerId, Item item)
        {
            HttpMethod method = HttpMethod.Post;

            ResourceType resourceType = ResourceType.docs;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-documentdb-is-upsert", "True");
            this.httpClient.DefaultRequestHeaders.Add("x-ms-documentdb-partitionkey", $"[\"{item.pk}\"]");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}/docs");
            StringContent requestContent = new StringContent(JsonSerializer.Serialize(item), System.Text.Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, Content = requestContent, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput("Create Document", httpResponse);
        }

        public async Task ListDocuments(string databaseId, string containerId, string partitionKey)
        {
            HttpMethod method = HttpMethod.Get;
            ResourceType resourceType = ResourceType.docs;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");
            this.httpClient.DefaultRequestHeaders.Add("x-ms-documentdb-partitionkey", $"[\"{partitionKey}\"]");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}/docs");
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"List Documents for partitionKey {partitionKey}", httpResponse);
        }

        public async Task GetDocument(string databaseId, string containerId, string id, string partitionKey)
        {
            HttpMethod method = HttpMethod.Get;
            ResourceType resourceType = ResourceType.docs;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}/docs/{id}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");
            this.httpClient.DefaultRequestHeaders.Add("x-ms-documentdb-partitionkey", $"[\"{partitionKey}\"]");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}");
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"Get Document by id: '{id}'", httpResponse);
        }

        public async Task ReplaceDocument(string databaseId, string containerId, string id, Item newItem)
        {
            HttpMethod method = HttpMethod.Put;

            ResourceType resourceType = ResourceType.docs;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}/docs/{id}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");
            this.httpClient.DefaultRequestHeaders.Add("x-ms-documentdb-partitionkey", $"[\"{newItem.pk}\"]");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}");
            StringContent requestContent = new StringContent(JsonSerializer.Serialize(newItem), System.Text.Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, Content = requestContent, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"Replace Document with id '{id}'", httpResponse);

        }

        public async Task PatchDocument(string databaseId, string containerId, string id, string partitionKey)
        {
            HttpMethod method = HttpMethod.Patch;

            ResourceType resourceType = ResourceType.docs;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}/docs/{id}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");
            this.httpClient.DefaultRequestHeaders.Add("x-ms-documentdb-partitionkey", $"[\"{partitionKey}\"]");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}");
            string requestBody = @"
        {
          ""operations"": [
            {
              ""op"": ""set"",
              ""path"": ""/someProperty"",
              ""value"": ""value-patched""
            }
          ]
        }  ";

            StringContent requestContent = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, Content = requestContent, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            await this.ReportOutput($"Patch Document with id '{id}'", httpResponse);
        }


        public async Task DeleteDocument(string databaseId, string containerId, string id, string partitionKey)
        {
            HttpMethod method = HttpMethod.Delete;
            ResourceType resourceType = ResourceType.docs;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}/docs/{id}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");
            this.httpClient.DefaultRequestHeaders.Add("x-ms-documentdb-partitionkey", $"[\"{partitionKey}\"]");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}");
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);

            await this.ReportOutput($"Deleted item with id '{id}':", httpResponse);
        }


        // Change var by explicit types in the entire file
        public async Task QueryDocuments(string databaseId, string containerId, string partitionKey)
        {
            HttpMethod method = HttpMethod.Post;
            ResourceType resourceType = ResourceType.docs;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");
            this.httpClient.DefaultRequestHeaders.Add("x-ms-documentdb-isquery", "True");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}/docs");
            string requestBody = @$"
        {{  
          ""query"": ""SELECT * FROM c WHERE c.pk = @pk"",  
          ""parameters"": [
            {{  
              ""name"": ""@pk"",
              ""value"": ""{partitionKey}""  
            }}
          ]  
        }}";
            StringContent requestContent = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/query+json");
            //NOTE -> this is important. CosmosDB expects a specific Content-Type with no CharSet on a query request.
            requestContent.Headers.ContentType.CharSet = "";
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, Content = requestContent, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);

            await this.ReportOutput("Query: ", httpResponse);
        }

        public async Task QueryDocumentsCrossPartition(string databaseId, string containerId)
        {
            HttpMethod method = HttpMethod.Post;
            ResourceType resourceType = ResourceType.docs;
            string resourceLink = $"dbs/{databaseId}/colls/{containerId}";
            string requestDateString = DateTime.UtcNow.ToString("r");
            string auth = this.GenerateMasterKeyAuthorizationSignature(method, resourceType, resourceLink, requestDateString, this.cosmosKey);

            this.httpClient.DefaultRequestHeaders.Clear();
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            this.httpClient.DefaultRequestHeaders.Add("authorization", auth);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-date", requestDateString);
            this.httpClient.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");
            this.httpClient.DefaultRequestHeaders.Add("x-ms-max-item-count", "2");
            this.httpClient.DefaultRequestHeaders.Add("x-ms-documentdb-query-enablecrosspartition", "True");
            this.httpClient.DefaultRequestHeaders.Add("x-ms-documentdb-isquery", "True");

            Uri requestUri = new Uri($"{this.baseUrl}/{resourceLink}/docs");
            string requestBody = @$"
        {{  
          ""query"": ""SELECT * FROM c"",  
          ""parameters"": []  
        }}";

            StringContent requestContent = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/query+json");
            //NOTE -> this is important. CosmosDB expects a specific Content-Type with no CharSet on a query request.
            requestContent.Headers.ContentType.CharSet = "";
            HttpRequestMessage httpRequest = new HttpRequestMessage { Method = method, Content = requestContent, RequestUri = requestUri };

            HttpResponseMessage httpResponse = await this.httpClient.SendAsync(httpRequest);
            //var continuation = httpResponse.Headers.GetValues("x-ms-continuation");

            await this.ReportOutput("Query: ", httpResponse);
        }

        private string GenerateMasterKeyAuthorizationSignature(HttpMethod verb, ResourceType resourceType, string resourceLink, string date, string key)
        {
            string keyType = "master";
            string tokenVersion = "1.0";
            string payload = $"{verb.ToString().ToLowerInvariant()}\n{resourceType.ToString().ToLowerInvariant()}\n{resourceLink}\n{date.ToLowerInvariant()}\n\n";

            HMACSHA256 hmacSha256 = new HMACSHA256 { Key = Convert.FromBase64String(key) };
            byte[] hashPayload = hmacSha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
            string signature = Convert.ToBase64String(hashPayload);
            string authSet = WebUtility.UrlEncode($"type={keyType}&ver={tokenVersion}&sig={signature}");

            return authSet;
        }

        private async Task ReportOutput(string methodName, HttpResponseMessage httpResponse)
        {
            string responseContent = await httpResponse.Content.ReadAsStringAsync();
            if (this.outputToConsole)
            {
                if (httpResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"{methodName}: SUCCESS\n    {responseContent}\n\n");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"{methodName}: FAILED -> {(int)httpResponse.StatusCode}: {httpResponse.ReasonPhrase}.\n    {responseContent}\n\n");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
        }

        public record Item(string id, string pk, string value);

        public enum ResourceType
        {
            dbs,
            colls,
            docs,
            sprocs,
            pkranges,
        }

        public enum DatabaseThoughputMode
        {
            none,
            @fixed,
            autopilot,
        }
    }
}

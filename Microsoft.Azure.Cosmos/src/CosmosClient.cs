﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Authorization;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Provides a client-side logical representation of the Azure Cosmos DB account.
    /// This client can be used to configure and execute requests in the Azure Cosmos DB database service.
    /// 
    /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
    /// of the application which enables efficient connection management and performance. Please refer to the
    /// <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">performance guide</see>.
    /// </summary>
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="Database"/>, and a <see cref="Container"/>.
    /// The CosmosClient is created with the connection string and configured to use "East US 2" region.
    /// <code language="c#">
    /// <![CDATA[
    /// using Microsoft.Azure.Cosmos;
    /// 
    /// CosmosClient cosmosClient = new CosmosClient(
    ///             "connection-string-from-portal", 
    ///             new CosmosClientOptions()
    ///             {
    ///                 ApplicationRegion = Regions.EastUS2,
    ///             });
    /// 
    /// Database db = await cosmosClient.CreateDatabaseAsync("database-id");
    /// Container container = await db.CreateContainerAsync("container-id");
    /// 
    /// // Dispose cosmosClient at application exit
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// This example creates a <see cref="CosmosClient"/>, <see cref="Database"/>, and a <see cref="Container"/>.
    /// The CosmosClient is created with the AccountEndpoint, AccountKey or ResourceToken and configured to use "East US 2" region.
    /// <code language="c#">
    /// <![CDATA[
    /// using Microsoft.Azure.Cosmos;
    /// 
    /// CosmosClient cosmosClient = new CosmosClient(
    ///             "account-endpoint-from-portal", 
    ///             "account-key-from-portal", 
    ///             new CosmosClientOptions()
    ///             {
    ///                 ApplicationRegion = Regions.EastUS2,
    ///             });
    /// 
    /// Database db = await cosmosClient.CreateDatabaseAsync("database-id");
    /// Container container = await db.CreateContainerAsync("container-id");
    /// 
    /// // Dispose cosmosClient at application exit
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// This example creates a <see cref="CosmosClient"/>, <see cref="Database"/>, and a <see cref="Container"/>.
    /// The CosmosClient is created through builder pattern using <see cref="Fluent.CosmosClientBuilder"/>.
    /// <code language="c#">
    /// <![CDATA[
    /// using Microsoft.Azure.Cosmos;
    /// using Microsoft.Azure.Cosmos.Fluent;
    /// 
    /// CosmosClient cosmosClient = new CosmosClientBuilder("connection-string-from-portal")
    ///     .WithApplicationRegion("East US 2")
    ///     .Build();
    /// 
    /// Database db = await cosmosClient.CreateDatabaseAsync("database-id")
    /// Container container = await db.CreateContainerAsync("container-id");
    /// 
    /// // Dispose cosmosClient at application exit
    /// ]]>
    /// </code>
    /// </example>
    /// <seealso cref="CosmosClientOptions"/>
    /// <seealso cref="Fluent.CosmosClientBuilder"/>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">Performance Tips</seealso>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk">Diagnose and troubleshoot issues</seealso>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/distribute-data-globally">Global data distribution</seealso>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/partitioning-overview">Partitioning and horizontal scaling</seealso>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
    public class CosmosClient : IDisposable
    {
        internal readonly string Id = Guid.NewGuid().ToString();
        private readonly object disposedLock = new object();

        private readonly string DatabaseRootUri = Paths.Databases_Root;
        private ConsistencyLevel? accountConsistencyLevel;
        private bool isDisposed = false;

        internal static int numberOfClientsCreated;
        internal static int NumberOfActiveClients;

        internal DateTime? DisposedDateTimeUtc { get; private set; } = null;

        static CosmosClient()
        {
#if PREVIEW
            HttpConstants.Versions.CurrentVersion = HttpConstants.Versions.v2020_07_15;
#else
            HttpConstants.Versions.CurrentVersion = HttpConstants.Versions.v2018_12_31;
#endif
            HttpConstants.Versions.CurrentVersionUTF8 = Encoding.UTF8.GetBytes(HttpConstants.Versions.CurrentVersion);

            ServiceInteropWrapper.AssembliesExist = new Lazy<bool>(() =>
            {
                // Attemp to create an instance of the ServiceInterop assembly
                TryCatch<IntPtr> tryCreateServiceProvider = QueryPartitionProvider.TryCreateServiceProvider("{}");
                if (tryCreateServiceProvider.Failed)
                {
                    // Failed, either the DLL is not present or one of its dependencies
                    return false;
                }

                // Assembly and dependencies are available
                // Release pointer
                Marshal.Release(tryCreateServiceProvider.Result);
                return true;
            });

            Microsoft.Azure.Cosmos.Core.Trace.DefaultTrace.InitEventListener();

            // If a debugger is not attached remove the DefaultTraceListener. 
            // DefaultTraceListener can cause lock contention leading to availability issues
            if (!Debugger.IsAttached)
            {
                CosmosClient.RemoveDefaultTraceListener();
            }
        }

        /// <summary>
        /// Create a new CosmosClient used for mock testing
        /// </summary>
        protected CosmosClient()
        {
        }

        /// <summary>
        /// Creates a new CosmosClient with the connection string.
        /// 
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">performance guide</see>.
        /// </summary>
        /// <param name="connectionString">The connection string to the cosmos account. ex: AccountEndpoint=https://XXXXX.documents.azure.com:443/;AccountKey=SuperSecretKey; </param>
        /// <param name="clientOptions">(Optional) client options</param>
        /// <example>
        /// The CosmosClient is created with the connection string and configured to use "East US 2" region.
        /// <code language="c#">
        /// <![CDATA[
        /// using Microsoft.Azure.Cosmos;
        /// 
        /// CosmosClient cosmosClient = new CosmosClient(
        ///             "account-endpoint-from-portal", 
        ///             "account-key-from-portal", 
        ///             new CosmosClientOptions()
        ///             {
        ///                 ApplicationRegion = Regions.EastUS2,
        ///             });
        /// 
        /// // Dispose cosmosClient at application exit
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="CosmosClientOptions"/>
        /// <seealso cref="Fluent.CosmosClientBuilder"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">Performance Tips</seealso>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk">Diagnose and troubleshoot issues</seealso>
        public CosmosClient(
            string connectionString,
            CosmosClientOptions clientOptions = null)
            : this(
                  CosmosClientOptions.GetAccountEndpoint(connectionString),
                  CosmosClientOptions.GetAccountKey(connectionString),
                  clientOptions)
        {
        }

        /// <summary>
        /// Creates a new CosmosClient with the account endpoint URI string and account key.
        /// 
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">performance guide</see>.
        /// </summary>
        /// <param name="accountEndpoint">The cosmos service endpoint to use</param>
        /// <param name="authKeyOrResourceToken">The cosmos account key or resource token to use to create the client.</param>
        /// <param name="clientOptions">(Optional) client options</param>
        /// <example>
        /// The CosmosClient is created with the AccountEndpoint, AccountKey or ResourceToken and configured to use "East US 2" region.
        /// <code language="c#">
        /// <![CDATA[
        /// using Microsoft.Azure.Cosmos;
        /// 
        /// CosmosClient cosmosClient = new CosmosClient(
        ///             "account-endpoint-from-portal", 
        ///             "account-key-from-portal", 
        ///             new CosmosClientOptions()
        ///             {
        ///                 ApplicationRegion = Regions.EastUS2,
        ///             });
        /// 
        /// // Dispose cosmosClient at application exit
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="CosmosClientOptions"/>
        /// <seealso cref="Fluent.CosmosClientBuilder"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">Performance Tips</seealso>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk">Diagnose and troubleshoot issues</seealso>
        public CosmosClient(
            string accountEndpoint,
            string authKeyOrResourceToken,
            CosmosClientOptions clientOptions = null)
             : this(accountEndpoint,
                     AuthorizationTokenProvider.CreateWithResourceTokenOrAuthKey(authKeyOrResourceToken),
                     clientOptions)
        {
        }

        /// <summary>
        /// Creates a new CosmosClient with the account endpoint URI string and AzureKeyCredential.
        /// AzureKeyCredential enables changing/updating master-key/ResourceToken while CosmosClient is still in use. 
        /// 
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">performance guide</see>.
        /// </summary>
        /// <param name="accountEndpoint">The cosmos service endpoint to use</param>
        /// <param name="authKeyOrResourceTokenCredential">AzureKeyCredential with master-key or resource token..</param>
        /// <param name="clientOptions">(Optional) client options</param>
        /// <example>
        /// The CosmosClient is created with the AccountEndpoint, AccountKey or ResourceToken and configured to use "East US 2" region.
        /// <code language="c#">
        /// <![CDATA[
        /// using Microsoft.Azure.Cosmos;
        /// 
        /// AzureKeyCredential keyCredential = new AzureKeyCredential("account-master-key/ResourceToken");
        /// CosmosClient cosmosClient = new CosmosClient(
        ///             "account-endpoint-from-portal", 
        ///             keyCredential, 
        ///             new CosmosClientOptions()
        ///             {
        ///                 ApplicationRegion = Regions.EastUS2,
        ///             });
        ///             
        /// ....
        /// 
        /// // To udpate key/credentials 
        /// keyCredential.Update("updated master-key/ResourceToken");
        /// 
        /// // Dispose cosmosClient at application exit
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="CosmosClientOptions"/>
        /// <seealso cref="Fluent.CosmosClientBuilder"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">Performance Tips</seealso>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk">Diagnose and troubleshoot issues</seealso>
        /// <remarks>AzureKeyCredential enables changing/updating master-key/ResourceToken whle CosmosClient is still in use.</remarks>
        public CosmosClient(
            string accountEndpoint,
            AzureKeyCredential authKeyOrResourceTokenCredential,
            CosmosClientOptions clientOptions = null)
             : this(accountEndpoint,
                     new AzureKeyCredentialAuthorizationTokenProvider(authKeyOrResourceTokenCredential),
                     clientOptions)
        {
        }

        /// <summary>
        /// Creates a new CosmosClient with the account endpoint URI string and TokenCredential.
        /// 
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">performance guide</see>.
        /// </summary>
        /// <param name="accountEndpoint">The cosmos service endpoint to use.</param>
        /// <param name="tokenCredential"><see cref="TokenCredential"/>The token to provide AAD token for authorization.</param>
        /// <param name="clientOptions">(Optional) client options</param>
        public CosmosClient(
            string accountEndpoint,
            TokenCredential tokenCredential,
            CosmosClientOptions clientOptions = null)
            : this(accountEndpoint,
                    new AuthorizationTokenProviderTokenCredential(
                        tokenCredential,
                        new Uri(accountEndpoint),
                        clientOptions?.TokenCredentialBackgroundRefreshInterval),
                    clientOptions)
        {
        }

        /// <summary>
        /// Used by Compute
        /// Creates a new CosmosClient with the AuthorizationTokenProvider
        /// </summary>
        internal CosmosClient(
             string accountEndpoint,
             AuthorizationTokenProvider authorizationTokenProvider,
             CosmosClientOptions clientOptions)
        {
            if (string.IsNullOrEmpty(accountEndpoint))
            {
                throw new ArgumentNullException(nameof(accountEndpoint));
            }

            this.Endpoint = new Uri(accountEndpoint);
            this.AuthorizationTokenProvider = authorizationTokenProvider ?? throw new ArgumentNullException(nameof(authorizationTokenProvider));

            clientOptions ??= new CosmosClientOptions();

            this.ClientId = this.IncrementNumberOfClientsCreated();
            
            this.ClientContext = ClientContextCore.Create(
                this,
                clientOptions);

            this.ClientConfigurationTraceDatum = new ClientConfigurationTraceDatum(this.ClientContext, DateTime.UtcNow);
        }

        /// <summary>
        /// Creates a new CosmosClient with the account endpoint URI string and TokenCredential.
        /// In addition to that it initializes the client with containers provided i.e The SDK warms up the caches and 
        /// connections before the first call to the service is made. Use this to obtain lower latency while startup of your application.
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">performance guide</see>.
        /// </summary>
        /// <param name="accountEndpoint">The cosmos service endpoint to use</param>
        /// <param name="authKeyOrResourceToken">The cosmos account key or resource token to use to create the client.</param>
        /// <param name="containers">Containers to be initialized identified by it's database name and container name.</param>
        /// <param name="cosmosClientOptions">(Optional) client options</param>
        /// <param name="cancellationToken">(Optional) Cancellation Token</param>
        /// <returns>
        /// A CosmosClient object.
        /// </returns>
        /// <example>
        /// The CosmosClient is created with the AccountEndpoint, AccountKey or ResourceToken and 2 containers in the account are initialized
        /// <code language="c#">
        /// <![CDATA[
        /// using Microsoft.Azure.Cosmos;
        /// List<(string, string)> containersToInitialize = new List<(string, string)>
        /// { ("DatabaseName1", "ContainerName1"), ("DatabaseName2", "ContainerName2") };
        /// 
        /// CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync("account-endpoint-from-portal", 
        ///                                                                         "account-key-from-portal",
        ///                                                                         containersToInitialize)
        /// 
        /// // Dispose cosmosClient at application exit
        /// ]]>
        /// </code>
        /// </example>
        public static async Task<CosmosClient> CreateAndInitializeAsync(string accountEndpoint,
                                                                        string authKeyOrResourceToken,
                                                                        IReadOnlyList<(string databaseId, string containerId)> containers,
                                                                        CosmosClientOptions cosmosClientOptions = null,
                                                                        CancellationToken cancellationToken = default)
        {
            if (containers == null)
            {
                throw new ArgumentNullException(nameof(containers));
            }

            CosmosClient cosmosClient = new CosmosClient(accountEndpoint,
                                                         authKeyOrResourceToken,
                                                         cosmosClientOptions);

            await cosmosClient.InitializeContainersAsync(containers, cancellationToken);
            return cosmosClient;
        }

        /// <summary>
        /// Creates a new CosmosClient with the account endpoint URI string and AzureKeyCredential.
        /// AzureKeyCredential enables changing/updating master-key/ResourceToken while CosmosClient is still in use. 
        /// 
        /// In addition to that it initializes the client with containers provided i.e The SDK warms up the caches and 
        /// connections before the first call to the service is made. Use this to obtain lower latency while startup of your application.
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">performance guide</see>.
        /// </summary>
        /// <param name="accountEndpoint">The cosmos service endpoint to use</param>
        /// <param name="authKeyOrResourceTokenCredential">AzureKeyCredential with master-key or resource token.</param>
        /// <param name="containers">Containers to be initialized identified by it's database name and container name.</param>
        /// <param name="cosmosClientOptions">(Optional) client options</param>
        /// <param name="cancellationToken">(Optional) Cancellation Token</param>
        /// <returns>
        /// A CosmosClient object.
        /// </returns>
        /// <example>
        /// The CosmosClient is created with the AccountEndpoint, AccountKey or ResourceToken and 2 containers in the account are initialized
        /// <code language="c#">
        /// <![CDATA[
        /// using Microsoft.Azure.Cosmos;
        /// List<(string, string)> containersToInitialize = new List<(string, string)>
        /// { ("DatabaseName1", "ContainerName1"), ("DatabaseName2", "ContainerName2") };
        /// 
        /// AzureKeyCredential keyCredential = new AzureKeyCredential("account-master-key/ResourceToken");
        /// CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync("account-endpoint-from-portal", 
        ///                                                                         keyCredential,
        ///                                                                         containersToInitialize)
        ///             
        /// ....
        /// 
        /// // To udpate key/credentials 
        /// keyCredential.Update("updated master-key/ResourceToken");
        /// 
        /// // Dispose cosmosClient at application exit
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>AzureKeyCredential enables changing/updating master-key/ResourceToken whle CosmosClient is still in use.</remarks> 
        public static async Task<CosmosClient> CreateAndInitializeAsync(string accountEndpoint,
                                                                        AzureKeyCredential authKeyOrResourceTokenCredential,
                                                                        IReadOnlyList<(string databaseId, string containerId)> containers,
                                                                        CosmosClientOptions cosmosClientOptions = null,
                                                                        CancellationToken cancellationToken = default)
        {
            if (containers == null)
            {
                throw new ArgumentNullException(nameof(containers));
            }

            CosmosClient cosmosClient = new CosmosClient(accountEndpoint,
                                                         authKeyOrResourceTokenCredential,
                                                         cosmosClientOptions);

            await cosmosClient.InitializeContainersAsync(containers, cancellationToken);
            return cosmosClient;
        }

        /// <summary>
        /// Creates a new CosmosClient with the account endpoint URI string and TokenCredential.
        /// In addition to that it initializes the client with containers provided i.e The SDK warms up the caches and 
        /// connections before the first call to the service is made. Use this to obtain lower latency while startup of your application.
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">performance guide</see>.
        /// </summary>
        /// <param name="connectionString">The connection string to the cosmos account. ex: AccountEndpoint=https://XXXXX.documents.azure.com:443/;AccountKey=SuperSecretKey; </param>
        /// <param name="containers">Containers to be initialized identified by it's database name and container name.</param>
        /// <param name="cosmosClientOptions">(Optional) client options</param>
        /// <param name="cancellationToken">(Optional) Cancellation Token</param>
        /// <returns>
        /// A CosmosClient object.
        /// </returns>
        /// <example>
        /// The CosmosClient is created with the ConnectionString and 2 containers in the account are initialized
        /// <code language="c#">
        /// <![CDATA[
        /// using Microsoft.Azure.Cosmos;
        /// List<(string, string)> containersToInitialize = new List<(string, string)>
        /// { ("DatabaseName1", "ContainerName1"), ("DatabaseName2", "ContainerName2") };
        /// 
        /// CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync("connection-string-from-portal",
        ///                                                                         containersToInitialize)
        /// 
        /// // Dispose cosmosClient at application exit
        /// ]]>
        /// </code>
        /// </example>
        public static async Task<CosmosClient> CreateAndInitializeAsync(string connectionString,
                                                                        IReadOnlyList<(string databaseId, string containerId)> containers,
                                                                        CosmosClientOptions cosmosClientOptions = null,
                                                                        CancellationToken cancellationToken = default)
        {
            if (containers == null)
            {
                throw new ArgumentNullException(nameof(containers));
            }

            CosmosClient cosmosClient = new CosmosClient(connectionString,
                                                         cosmosClientOptions);

            await cosmosClient.InitializeContainersAsync(containers, cancellationToken);
            return cosmosClient;
        }

        /// <summary>
        /// Creates a new CosmosClient with the account endpoint URI string and TokenCredential.
        /// In addition to that it initializes the client with containers provided i.e The SDK warms up the caches and 
        /// connections before the first call to the service is made. Use this to obtain lower latency while startup of your application.
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">performance guide</see>.
        /// </summary>
        /// <param name="accountEndpoint">The cosmos service endpoint to use.</param>
        /// <param name="tokenCredential"><see cref="TokenCredential"/>The token to provide AAD token for authorization.</param>
        /// <param name="containers">Containers to be initialized identified by it's database name and container name.</param>
        /// <param name="cosmosClientOptions">(Optional) client options</param>
        /// <param name="cancellationToken">(Optional) Cancellation Token</param>
        /// <returns>
        /// A CosmosClient object.
        /// </returns>
        public static async Task<CosmosClient> CreateAndInitializeAsync(string accountEndpoint,
                                                                        TokenCredential tokenCredential,
                                                                        IReadOnlyList<(string databaseId, string containerId)> containers,
                                                                        CosmosClientOptions cosmosClientOptions = null,
                                                                        CancellationToken cancellationToken = default)
        {
            if (containers == null)
            {
                throw new ArgumentNullException(nameof(containers));
            }

            CosmosClient cosmosClient = new CosmosClient(accountEndpoint,
                                                         tokenCredential,
                                                         cosmosClientOptions);

            await cosmosClient.InitializeContainersAsync(containers, cancellationToken);
            return cosmosClient;
        }

        /// <summary>
        /// Used for unit testing only.
        /// </summary>
        /// <remarks>This constructor should be removed at some point. The mocking should happen in a derived class.</remarks>
        internal CosmosClient(
            string accountEndpoint,
            string authKeyOrResourceToken,
            CosmosClientOptions cosmosClientOptions,
            DocumentClient documentClient)
        {
            if (string.IsNullOrEmpty(accountEndpoint))
            {
                throw new ArgumentNullException(nameof(accountEndpoint));
            }

            if (string.IsNullOrEmpty(authKeyOrResourceToken))
            {
                throw new ArgumentNullException(nameof(authKeyOrResourceToken));
            }

            if (cosmosClientOptions == null)
            {
                throw new ArgumentNullException(nameof(cosmosClientOptions));
            }

            if (documentClient == null)
            {
                throw new ArgumentNullException(nameof(documentClient));
            }

            this.Endpoint = new Uri(accountEndpoint);
            this.AccountKey = authKeyOrResourceToken;
            this.AuthorizationTokenProvider = AuthorizationTokenProvider.CreateWithResourceTokenOrAuthKey(authKeyOrResourceToken);

            this.ClientContext = ClientContextCore.Create(
                 this,
                 documentClient,
                 cosmosClientOptions);

            this.ClientConfigurationTraceDatum = new ClientConfigurationTraceDatum(this.ClientContext, DateTime.UtcNow);
        }

        /// <summary>
        /// The <see cref="Cosmos.CosmosClientOptions"/> used initialize CosmosClient.
        /// </summary>
        /// <remarks>This property is read-only. Modifying any options after the client has been created has no effect on the existing client instance.</remarks>
        public virtual CosmosClientOptions ClientOptions => this.ClientContext.ClientOptions;

        /// <summary>
        /// The response factory used to create CosmosClient response types.
        /// </summary>
        /// <remarks>
        /// This can be used for generating responses for tests, and allows users to create
        /// a custom container that modifies the response. For example the client encryption
        /// uses this to decrypt responses before returning to the caller.
        /// </remarks>
        public virtual CosmosResponseFactory ResponseFactory => this.ClientContext.ResponseFactory;

        /// <summary>
        /// Gets the endpoint Uri for the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Uri for the account endpoint.
        /// </value>
        /// <seealso cref="System.Uri"/>
        public virtual Uri Endpoint { get; }

        /// <summary>
        /// Gets the AuthKey or resource token used by the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The AuthKey used by the client.
        /// </value>
        internal string AccountKey { get; }

        /// <summary>
        /// Gets the AuthorizationTokenProvider used to generate the authorization token
        /// </summary>
        internal AuthorizationTokenProvider AuthorizationTokenProvider { get; }

        internal DocumentClient DocumentClient => this.ClientContext.DocumentClient;
        internal RequestInvokerHandler RequestHandler => this.ClientContext.RequestHandler;
        internal CosmosClientContext ClientContext { get; }
        internal ClientConfigurationTraceDatum ClientConfigurationTraceDatum { get; }
        internal int ClientId { get; }

        /// <summary>
        /// Reads the <see cref="Microsoft.Azure.Cosmos.AccountProperties"/> for the Azure Cosmos DB account.
        /// </summary>
        /// <returns>
        /// A <see cref="AccountProperties"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        public virtual Task<AccountProperties> ReadAccountAsync()
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadAccountAsync),
                null,
                (trace) => ((IDocumentClientInternal)this.DocumentClient).GetDatabaseAccountInternalAsync(this.Endpoint));
        }

        /// <summary>
        /// Returns a proxy reference to a database. 
        /// </summary>
        /// <param name="id">The Cosmos database id</param>
        /// <remarks>
        /// <see cref="Database"/> proxy reference doesn't guarantee existence.
        /// Please ensure database exists through <see cref="CosmosClient.CreateDatabaseAsync(string, int?, RequestOptions, CancellationToken)"/> 
        /// or <see cref="CosmosClient.CreateDatabaseIfNotExistsAsync(string, int?, RequestOptions, CancellationToken)"/>, before
        /// operating on it.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// Database db = cosmosClient.GetDatabase("myDatabaseId");
        /// DatabaseResponse response = await db.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>Cosmos database proxy</returns>
        public virtual Database GetDatabase(string id)
        {
            return new DatabaseInlineCore(this.ClientContext, id);
        }

        /// <summary>
        /// Returns a proxy reference to a container. 
        /// </summary>
        /// <remarks>
        /// <see cref="Container"/> proxy reference doesn't guarantee existence.
        /// Please ensure container exists through <see cref="Database.CreateContainerAsync(ContainerProperties, int?, RequestOptions, CancellationToken)"/> 
        /// or <see cref="Database.CreateContainerIfNotExistsAsync(ContainerProperties, int?, RequestOptions, CancellationToken)"/>, before
        /// operating on it.
        /// </remarks>
        /// <param name="databaseId">Cosmos database name</param>
        /// <param name="containerId">Cosmos container name</param>
        /// <returns>Cosmos container proxy</returns>
        public virtual Container GetContainer(string databaseId, string containerId)
        {
            if (string.IsNullOrEmpty(databaseId))
            {
                throw new ArgumentNullException(nameof(databaseId));
            }

            if (string.IsNullOrEmpty(containerId))
            {
                throw new ArgumentNullException(nameof(containerId));
            }

            return this.GetDatabase(databaseId).GetContainer(containerId);
        }

        /// <summary>
        /// Sends a request for creating a database.
        ///
        /// A database manages users, permissions and a set of containers.
        /// Each Azure Cosmos DB Database Account is able to support multiple independent named databases,
        /// with the database being the logical container for data.
        ///
        /// Each Database consists of one or more containers, each of which in turn contain one or more
        /// documents. Since databases are an administrative resource, the Service Master Key will be
        /// required in order to access and successfully complete any action using the User APIs.
        /// </summary>
        /// <param name="id">The database id.</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public virtual Task<DatabaseResponse> CreateDatabaseAsync(
                string id,
                int? throughput = null,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ClientContext.OperationHelperAsync(
                nameof(CreateDatabaseAsync),
                requestOptions,
                (trace) =>
                {
                    DatabaseProperties databaseProperties = this.PrepareDatabaseProperties(id);
                    ThroughputProperties throughputProperties = ThroughputProperties.CreateManualThroughput(throughput);

                    return this.CreateDatabaseInternalAsync(
                        databaseProperties: databaseProperties,
                        throughputProperties: throughputProperties,
                        requestOptions: requestOptions,
                        trace: trace,
                        cancellationToken: cancellationToken);
                },
                (response) => new OpenTelemetryResponse<DatabaseProperties>(response));
        }

        /// <summary>
        /// Sends a request for creating a database.
        ///
        /// A database manages users, permissions and a set of containers.
        /// Each Azure Cosmos DB Database Account is able to support multiple independent named databases,
        /// with the database being the logical container for data.
        ///
        /// Each Database consists of one or more containers, each of which in turn contain one or more
        /// documents. Since databases are an administrative resource, the Service Master Key will be
        /// required in order to access and successfully complete any action using the User APIs.
        /// </summary>
        /// <param name="id">The database id.</param>
        /// <param name="throughputProperties">(Optional) The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public virtual Task<DatabaseResponse> CreateDatabaseAsync(
                string id,
                ThroughputProperties throughputProperties,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ClientContext.OperationHelperAsync(
                nameof(CreateDatabaseAsync),
                requestOptions,
                (trace) =>
                {
                    DatabaseProperties databaseProperties = this.PrepareDatabaseProperties(id);
                    return this.CreateDatabaseInternalAsync(
                        databaseProperties: databaseProperties,
                        throughputProperties: throughputProperties,
                        requestOptions: requestOptions,
                        trace: trace,
                        cancellationToken: cancellationToken);
                },
                (response) => new OpenTelemetryResponse<DatabaseProperties>(response));
        }

        /// <summary>
        /// <para>Check if a database exists, and if it doesn't, create it.
        /// Only the database id is used to verify if there is an existing database. Other database properties 
        /// such as throughput are not validated and can be different then the passed properties.</para>
        /// 
        /// <para>A database manages users, permissions and a set of containers.
        /// Each Azure Cosmos DB Database Account is able to support multiple independent named databases,
        /// with the database being the logical container for data.</para>
        ///
        /// <para>Each Database consists of one or more containers, each of which in turn contain one or more
        /// documents. Since databases are an administrative resource, the Service Master Key will be
        /// required in order to access and successfully complete any action using the User APIs.</para>
        /// </summary>
        /// <param name="id">The database id.</param>
        /// <param name="throughputProperties">The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of additional options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Common success StatusCodes for the CreateDatabaseIfNotExistsAsync operation</description>
        ///     </listheader>
        ///     <item>
        ///         <term>201</term><description>Created - New database is created.</description>
        ///     </item>
        ///     <item>
        ///         <term>200</term><description>OK - This means the database already exists.</description>
        ///     </item>
        /// </list>
        /// </returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public virtual Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return string.IsNullOrEmpty(id)
                ? throw new ArgumentNullException(nameof(id))
                : this.ClientContext.OperationHelperAsync(
                nameof(CreateDatabaseIfNotExistsAsync),
                requestOptions,
                async (trace) =>
            {
                double totalRequestCharge = 0;
                // Doing a Read before Create will give us better latency for existing databases
                DatabaseProperties databaseProperties = this.PrepareDatabaseProperties(id);
                DatabaseCore database = (DatabaseCore)this.GetDatabase(id);
                using (ResponseMessage readResponse = await database.ReadStreamAsync(
                    requestOptions: requestOptions,
                    trace: trace,
                    cancellationToken: cancellationToken))
                {
                    totalRequestCharge = readResponse.Headers.RequestCharge;
                    if (readResponse.StatusCode != HttpStatusCode.NotFound)
                    {
                        return this.ClientContext.ResponseFactory.CreateDatabaseResponse(database, readResponse);
                    }
                }

                using (ResponseMessage createResponse = await this.CreateDatabaseStreamInternalAsync(
                    databaseProperties,
                    throughputProperties,
                    requestOptions,
                    trace,
                    cancellationToken))
                {
                    totalRequestCharge += createResponse.Headers.RequestCharge;
                    createResponse.Headers.RequestCharge = totalRequestCharge;

                    if (createResponse.StatusCode != HttpStatusCode.Conflict)
                    {
                        return this.ClientContext.ResponseFactory.CreateDatabaseResponse(this.GetDatabase(databaseProperties.Id), createResponse);
                    }
                }

                // This second Read is to handle the race condition when 2 or more threads have Read the database and only one succeeds with Create
                // so for the remaining ones we should do a Read instead of throwing Conflict exception
                using (ResponseMessage readResponseAfterConflict = await database.ReadStreamAsync(
                    requestOptions: requestOptions,
                    trace: trace,
                    cancellationToken: cancellationToken))
                {
                    totalRequestCharge += readResponseAfterConflict.Headers.RequestCharge;
                    readResponseAfterConflict.Headers.RequestCharge = totalRequestCharge;

                    return this.ClientContext.ResponseFactory.CreateDatabaseResponse(this.GetDatabase(databaseProperties.Id), readResponseAfterConflict);
                }
            },
                (response) => new OpenTelemetryResponse<DatabaseProperties>(response));
        }

        /// <summary>
        /// <para>Check if a database exists, and if it doesn't, create it.
        /// Only the database id is used to verify if there is an existing database. Other database properties 
        /// such as throughput are not validated and can be different then the passed properties.</para>
        /// 
        /// <para>A database manages users, permissions and a set of containers.
        /// Each Azure Cosmos DB Database Account is able to support multiple independent named databases,
        /// with the database being the logical container for data.</para>
        ///
        /// <para>Each Database consists of one or more containers, each of which in turn contain one or more
        /// documents. Since databases are an administrative resource, the Service Master Key will be
        /// required in order to access and successfully complete any action using the User APIs.</para>
        /// </summary>
        /// <param name="id">The database id.</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of additional options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Common success StatusCodes for the CreateDatabaseIfNotExistsAsync operation</description>
        ///     </listheader>
        ///     <item>
        ///         <term>201</term><description>Created - New database is created.</description>
        ///     </item>
        ///     <item>
        ///         <term>200</term><description>OK- This means the database already exists.</description>
        ///     </item>
        /// </list>
        /// </returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public virtual Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            ThroughputProperties throughputProperties = ThroughputProperties.CreateManualThroughput(throughput);

            return this.CreateDatabaseIfNotExistsAsync(
                id,
                throughputProperties,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement with parameterized values. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the databases.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Database.ReadAsync(RequestOptions, CancellationToken)" /> is recommended for single database look-up.
        /// </para>
        /// </remarks>
        /// <example>
        /// This create the type feed iterator for database with queryText as input,
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c where c.status like @status")
        ///     .WithParameter("@status", "start%");
        /// using (FeedIterator<DatabaseProperties> feedIterator = this.users.GetDatabaseQueryIterator<DatabaseProperties>(queryDefinition))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         FeedResponse<DatabaseProperties> response = await feedIterator.ReadNextAsync();
        ///         foreach (var database in response)
        ///         {
        ///             Console.WriteLine(database);
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual FeedIterator<T> GetDatabaseQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(
               this.GetDatabaseQueryIteratorHelper<T>(
                   queryDefinition,
                   continuationToken,
                   requestOptions),
               this.ClientContext);
        }

        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement with parameterized values. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the query request.</param>
        /// <returns>An iterator to go through the databases</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Database.ReadStreamAsync(RequestOptions, CancellationToken)" /> is recommended for single database look-up.
        /// </para>
        /// </remarks>
        /// <example>
        /// Example on how to fully drain the query results.
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition queryDefinition = new QueryDefinition("select * From c where c._rid = @rid")
        ///               .WithParameter("@rid", "TheRidValue");
        /// using (FeedIterator feedIterator = this.CosmosClient.GetDatabaseQueryStreamIterator(
        ///     queryDefinition)
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         // Stream iterator returns a response with status for errors
        ///         using(ResponseMessage response = await feedIterator.ReadNextAsync())
        ///         {
        ///             // Handle failure scenario. 
        ///             if(!response.IsSuccessStatusCode)
        ///             {
        ///                 // Log the response.Diagnostics and handle the error
        ///             }
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual FeedIterator GetDatabaseQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(
                this.GetDatabaseQueryStreamIteratorHelper(
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                this.ClientContext);
        }

        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement. It returns a FeedIterator.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the databases.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Database.ReadAsync(RequestOptions, CancellationToken)" /> is recommended for single database look-up.
        /// </para>
        /// </remarks>
        /// <example>
        /// This create the type feed iterator for database with queryText as input,
        /// <code language="c#">
        /// <![CDATA[
        /// string queryText = "SELECT * FROM c where c.status like 'start%'";
        /// using (FeedIterator<DatabaseProperties> feedIterator = this.users.GetDatabaseQueryIterator<DatabaseProperties>(queryText)
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         FeedResponse<DatabaseProperties> response = await feedIterator.ReadNextAsync();
        ///         foreach (var database in response)
        ///         {
        ///             Console.WriteLine(database);
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual FeedIterator<T> GetDatabaseQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return new FeedIteratorInlineCore<T>(
                this.GetDatabaseQueryIteratorHelper<T>(
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                this.ClientContext);
        }

        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement. It returns a FeedIterator.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the query request.</param>
        /// <returns>An iterator to go through the databases</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Database.ReadStreamAsync(RequestOptions, CancellationToken)" /> is recommended for single database look-up.
        /// </para>
        /// </remarks>
        /// <example>
        /// Example on how to fully drain the query results.
        /// <code language="c#">
        /// <![CDATA[
        /// using (FeedIterator feedIterator = this.CosmosClient.GetDatabaseQueryStreamIterator(
        ///     ("select * From c where c._rid = 'TheRidValue'")
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         // Stream iterator returns a response with status for errors
        ///         using(ResponseMessage response = await feedIterator.ReadNextAsync())
        ///         {
        ///             // Handle failure scenario. 
        ///             if(!response.IsSuccessStatusCode)
        ///             {
        ///                 // Log the response.Diagnostics and handle the error
        ///             }
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual FeedIterator GetDatabaseQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return new FeedIteratorInlineCore(
                this.GetDatabaseQueryStreamIterator(
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                this.ClientContext);
        }

        /// <summary>
        /// Send a request for creating a database.
        ///
        /// A database manages users, permissions and a set of containers.
        /// Each Azure Cosmos DB Database Account is able to support multiple independent named databases,
        /// with the database being the logical container for data.
        ///
        /// Each Database consists of one or more containers, each of which in turn contain one or more
        /// documents. Since databases are an administrative resource, the Service Master Key will be
        /// required in order to access and successfully complete any action using the User APIs.
        /// </summary>
        /// <param name="databaseProperties">The database properties</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public virtual Task<ResponseMessage> CreateDatabaseStreamAsync(
                DatabaseProperties databaseProperties,
                int? throughput = null,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            if (databaseProperties == null)
            {
                throw new ArgumentNullException(nameof(databaseProperties));
            }

            return this.ClientContext.OperationHelperAsync(
                 nameof(CreateDatabaseStreamAsync),
                 requestOptions,
                 (trace) =>
                 {
                     this.ClientContext.ValidateResource(databaseProperties.Id);
                     return this.CreateDatabaseStreamInternalAsync(
                         databaseProperties,
                         ThroughputProperties.CreateManualThroughput(throughput),
                         requestOptions,
                         trace,
                         cancellationToken);
                 },
                 (response) => new OpenTelemetryResponse(response));
        }

        /// <summary>
        /// Removes the DefaultTraceListener which causes locking issues which leads to avability problems. 
        /// </summary>
        private static void RemoveDefaultTraceListener()
        {
            if (Core.Trace.DefaultTrace.TraceSource.Listeners.Count > 0)
            {
                List<DefaultTraceListener> removeDefaultTraceListeners = new List<DefaultTraceListener>();
                foreach (object traceListnerObject in Core.Trace.DefaultTrace.TraceSource.Listeners)
                {
                    // The TraceSource already has the default trace listener
                    if (traceListnerObject is DefaultTraceListener defaultTraceListener)
                    {
                        removeDefaultTraceListeners.Add(defaultTraceListener);
                    }
                }

                // Remove all the default trace listeners
                foreach (DefaultTraceListener defaultTraceListener in removeDefaultTraceListeners)
                {
                    Core.Trace.DefaultTrace.TraceSource.Listeners.Remove(defaultTraceListener);
                }
            }
        }

        internal virtual async Task<ConsistencyLevel> GetAccountConsistencyLevelAsync()
        {
            if (!this.accountConsistencyLevel.HasValue)
            {
                this.accountConsistencyLevel = await this.DocumentClient.GetDefaultConsistencyLevelAsync();
            }

            return this.accountConsistencyLevel.Value;
        }

        internal DatabaseProperties PrepareDatabaseProperties(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            DatabaseProperties databaseProperties = new DatabaseProperties()
            {
                Id = id
            };

            this.ClientContext.ValidateResource(databaseProperties.Id);
            return databaseProperties;
        }

        /// <summary>
        /// Send a request for creating a database.
        ///
        /// A database manages users, permissions and a set of containers.
        /// Each Azure Cosmos DB Database Account is able to support multiple independent named databases,
        /// with the database being the logical container for data.
        ///
        /// Each Database consists of one or more containers, each of which in turn contain one or more
        /// documents. Since databases are an administrative resource, the Service Master Key will be
        /// required in order to access and successfully complete any action using the User APIs.
        /// </summary>
        /// <param name="databaseProperties">The database properties</param>
        /// <param name="throughputProperties">(Optional) The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.</returns>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        internal virtual Task<ResponseMessage> CreateDatabaseStreamAsync(
                DatabaseProperties databaseProperties,
                ThroughputProperties throughputProperties,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            if (databaseProperties == null)
            {
                throw new ArgumentNullException(nameof(databaseProperties));
            }

            return this.ClientContext.OperationHelperAsync(
                nameof(CreateDatabaseIfNotExistsAsync),
                requestOptions,
                (trace) =>
                {
                    this.ClientContext.ValidateResource(databaseProperties.Id);
                    return this.CreateDatabaseStreamInternalAsync(
                        databaseProperties,
                        throughputProperties,
                        requestOptions,
                        trace,
                        cancellationToken);
                },
                (response) => new OpenTelemetryResponse(response));
        }

        private async Task<DatabaseResponse> CreateDatabaseInternalAsync(
            DatabaseProperties databaseProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.DatabaseRootUri,
                resourceType: ResourceType.Database,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                cosmosContainerCore: null,
                feedRange: null,
                streamPayload: this.ClientContext.SerializerCore.ToStream<DatabaseProperties>(databaseProperties),
                requestEnricher: (httpRequestMessage) => httpRequestMessage.AddThroughputPropertiesHeader(throughputProperties),
                trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponse(this.GetDatabase(databaseProperties.Id), response);
        }

        private Task<ResponseMessage> CreateDatabaseStreamInternalAsync(
            DatabaseProperties databaseProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationAsync(
                resourceUri: this.DatabaseRootUri,
                resourceType: ResourceType.Database,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                containerInternal: null,
                feedRange: null,
                streamPayload: this.ClientContext.SerializerCore.ToStream<DatabaseProperties>(databaseProperties),
                requestEnricher: (httpRequestMessage) => httpRequestMessage.AddThroughputPropertiesHeader(throughputProperties),
                responseCreator: (response) => response,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        private FeedIteratorInternal<T> GetDatabaseQueryIteratorHelper<T>(
           QueryDefinition queryDefinition,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null)
        {
            if (!(this.GetDatabaseQueryStreamIteratorHelper(
                queryDefinition,
                continuationToken,
                requestOptions) is FeedIteratorInternal databaseStreamIterator))
            {
                throw new InvalidOperationException($"Expected a FeedIteratorInternal.");
            }

            return new FeedIteratorCore<T>(
                    databaseStreamIterator,
                    (response) => this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>(
                        responseMessage: response,
                        resourceType: ResourceType.Database));
        }

        private FeedIteratorInternal GetDatabaseQueryStreamIteratorHelper(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               clientContext: this.ClientContext,
               resourceLink: this.DatabaseRootUri,
               resourceType: ResourceType.Database,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions);
        }

        internal async Task InitializeContainersAsync(IReadOnlyList<(string databaseId, string containerId)> containers,
                                          CancellationToken cancellationToken)
        {
            try
            {
                List<Task> tasks = new List<Task>();
                foreach ((string databaseId, string containerId) in containers)
                {
                    tasks.Add(this.InitializeContainerAsync(databaseId, containerId, cancellationToken));
                }

                await Task.WhenAll(tasks);
            }
            catch
            {
                this.Dispose();
                throw;
            }
        }

        private int IncrementNumberOfClientsCreated()
        {
            this.IncrementNumberOfActiveClients();

            return Interlocked.Increment(ref numberOfClientsCreated);
        }

        private int IncrementNumberOfActiveClients()
        {
            return Interlocked.Increment(ref NumberOfActiveClients);
        }

        private int DecrementNumberOfActiveClients()
        {
            // In case dispose is called multiple times. Check if at least 1 active client is there
            if (NumberOfActiveClients > 0)
            {
                return Interlocked.Decrement(ref NumberOfActiveClients);
            }

            return 0;
        }

        private async Task InitializeContainerAsync(string databaseId, string containerId, CancellationToken cancellationToken = default)
        {
            ContainerInternal container = (ContainerInternal)this.GetContainer(databaseId, containerId);
            IReadOnlyList<FeedRange> feedRanges = await container.GetFeedRangesAsync(cancellationToken);
            List<Task> tasks = new List<Task>();
            foreach (FeedRange feedRange in feedRanges)
            {
                tasks.Add(CosmosClient.InitializeFeedRangeAsync(container, feedRange, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }

        private static async Task InitializeFeedRangeAsync(ContainerInternal container, FeedRange feedRange, CancellationToken cancellationToken = default)
        {
            // Do a dummy querry for each Partition Key Range to warm up the caches and connections
            string guidToCheck = Guid.NewGuid().ToString();
            QueryDefinition queryDefinition = new QueryDefinition($"select * from c where c.id = '{guidToCheck}'");
            using (FeedIterator feedIterator = container.GetItemQueryStreamIterator(feedRange,
                                                                                    queryDefinition,
                                                                                    continuationToken: null,
                                                                                    requestOptions: new QueryRequestOptions() { }))
            {
                while (feedIterator.HasMoreResults)
                {
                    using ResponseMessage response = await feedIterator.ReadNextAsync(cancellationToken);
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Dispose of cosmos client
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Dispose of cosmos client
        /// </summary>
        /// <param name="disposing">True if disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (this.disposedLock)
            {
                if (this.isDisposed == true)
                {
                    return;
                }
                this.isDisposed = true;
            }

            this.DisposedDateTimeUtc = DateTime.UtcNow;

            if (disposing)
            {
                this.ClientContext.Dispose();
                this.DecrementNumberOfActiveClients();
            }   
        }
    }
}

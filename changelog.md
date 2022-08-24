## <a name="recommended-version"></a> Recommended version

The **minimum recommended version is [3.25.0](#3.25.0)**.

Make sure that your applications, when using the .NET V3 SDK, are using at least the version described here to have all the critical fixes.

Any known issues detected on that version are listed in the [known issues](#known-issues) section.

## Release notes

Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### <a name="3.30.0-preview"/> [3.30.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.30.0-preview) - 2022-08-19

#### Added
- [#3394](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3394) Change Feed: Refactors Change Feed Contract to rename TimeToLiveExpired
- [#3331](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3331) Open Telemetry: Adds Client and other information in attributes
- [#3197](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3197) Change Feed: Adds SDK changes required for Full-Fidelity Change Feed

### <a name="3.30.0"/> [3.30.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.30.0) - 2022-08-19

#### Added
- [#3376](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3376) Client Telemetry : Refactors code to compute hash of VM ID and Process Name information
- [#3364](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3364) Integrated cache: Adds DedicatedGatewayRequestOptions for public release
- [#3273](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3273) Linq: Adds support constant evaluation of `Nullable<T>.HasValue`. (Thanks [@ccurrens](https://github.com/ccurrens))
- [#3268](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3268) Diagnostics: Adds GetStartTimeUtc and GetFailedRequestCount

#### Fixed
- [#3350](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3350) Diagnostics: Fixes Diagnostics for Query with FeedRanges
- [#3348](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3348) Documentation: Fixes DeleteItemAsync Example
- [#3338](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3338) Documentation: Fixes retry time to timespan
- [#3391](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3391) Diagnostics: Fixes Ordering of ClientConfiguration Initialization


### <a name="3.29.0-preview"/> [3.29.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.29.0-preview) - 2022-07-11

#### Added
- [#3277](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3277), [#3261](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3261) Open Telemetry: Adds Open Telemetry support

### <a name="3.29.0"/> [3.29.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.29.0) - 2022-07-11

#### Added
- [#3265](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3265), [#3285](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3285) Change Feed Processor: Adds Task.Delay check to prevent stalling
- [#3308](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3308) Dependencies: System.ConfigurationManager is upgraded to 6.0.0 tied to the .NET 6.0 release, which still supports .NET Standard 2.0, so it is not a breaking change.
- [#3308](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3308) Performance: Replaces DateTime.UtcNow with Rfc1123DateTimeCache.UtcNow()
- [#3320](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3320) Performance: Adds use of ValueStopwatch instead of Stopwatch
- [#3276](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3276) KeyRefresh: Adds AzureKeyCredential support to enable key refresh scenarios
- [#3322](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3322) Query: Adds new DLL dependencies for ServiceInterop.dll

#### Fixed
- [#3278](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3278), [#3310](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3310) CosmosClient: Fixes ObjectDisposedException during Background Refresh by adding Cancellation Token
- [#3309](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3309) Documentation: Fixes Container.PatchItemAsync example
- [#3313](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3313) Serialization: Fixes default JsonSerializerSettings for [GHSA-5crp-9r3c-p9vr](https://github.com/advisories/GHSA-5crp-9r3c-p9vr)
- [#3319](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3319) OperationCanceledException: Adds Exception Trace as Child to reduce noise on the top level of Diagnostics
- [#3308](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3308) ObjectDisposedException: Fixes ObjectDisposedException during Bounded Staleness/Strong barrier requests


### <a name="3.28.0"/> [3.28.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.28.0) - 2022-06-14
### <a name="3.28.0-preview"/> [3.28.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.28.0-preview) - 2022-06-14

#### Added
- [#3257](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3257) CosmosClientBuilder: Adds BuildAndInitializeAsync to match CosmosClient.CreateAndInitializeAsync
- [#3211](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3211) Client Encryption: Adds change to allow partition key path and id to be part of client encryption policy
- [#3236](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3236) MalformedContinuationTokenException: Adds the use of a new substatus code when throwing to programmatically determine the cause of the BadRequest
- [#3236](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3236) CosmosException: Adds custom messages for Service Unavailable scenarios to guide customer investigation

#### Fixed
- [#3253](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3253) CosmosOperationCanceledException: Fixes Closure on Cancellation Token status
- [#3252](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3252) Telemetry: Fixes Inconsistent behavior of VM Metadata Async Initialization
- [#3224](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3224) LINQ: Fixes preserve DateTime.Kind when passing value to custom JsonConverter (Thanks [@ccurrens](https://github.com/ccurrens))
- [#3236](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3236) Diagnostics: Fixes Exception caused when checking OS version of some Android Devices
- [#3236](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3236) Diagnostics: Fixes CPU NaN value causing broken json formatting on some devices


### <a name="3.27.2"/> [3.27.2](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.27.2) - 2022-06-02
### <a name="3.27.2-preview"/> [3.27.2-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.27.2-preview) - 2022-06-02

#### Added
- [#3231](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3231) Diagnostics: Adds Connection Mode to Client Configuration
- [#3234](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3234) CosmosOperationCanceledException: Adds short link and cancellation token status into message

#### Fixed
- [#3226](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3226) Query: Fixes DllNotFoundException when running on Windows/x64
- [#3227](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3227) Traces: Fixes message on SDK initialization when not running on Azure VM
- [#3242](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3242) CosmosClient Constructor: Fixes NullReferenceException when AzMetadata.Compute is null. Introduced in 3.27.0 PR [#3100](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3100)

### <a name="3.27.1"/> [3.27.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.27.1) - 2022-05-25
### <a name="3.27.1-preview"/> [3.27.1-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.27.1-preview) - 2022-05-25

#### Added
- [#3177](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3177) Performance: Adds optimized request headers
- [#3202](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3202) Diagnostics: Adds Application region into Client Configuration
- [#3194](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3194) Diagnostics: Adds Processor count
- [#3180](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3180) Diagnostics: Adds Continuation Token from partition key range cache
- [#3176](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3176) Diagnostics: Adds request session token

#### Fixed
- [#3190](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3190) LINQ: Fixes NullReferenceException when using a static field or property. Introduced in 3.27.0 PR [#2924](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2924)

### <a name="3.27.0"/> [3.27.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.27.0) - 2022-05-06
### <a name="3.27.0-preview"/> [3.27.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.27.0-preview) - 2022-05-06

#### Added
- [#3123](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3123) Availability: Adds optimization to reduce reduce metadata calls for addresses
- [#3127](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3127) Availability: Adds logic to reduce impact of replica failovers and upgrades
- [#3093](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3093) Patch: Adds Null support for Set operation
- [#3111](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3111) & [#3015](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3015) Merge support: Minimum SDK version that includes partition merge support.
- [#2924](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2924) Performance: Adds a LINQ optimization for member access in LINQ-to-SQL (Thanks [@notheotherben](https://github.com/notheotherben))
- [#3165](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3165) Diagnostics: Adds response serialization time
- [#3168](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3168) Performance: Adds aggressive prefetching for scalar aggregates for Query

#### Fixed
- [#3102](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3102) Upsert item: Fixes a bug causing session tokens for partition 0 to be overridden by session token for another partition when users don't pass the token as input
- [#3119](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3119) Session: Fixes NotFound/ReadSessionNotAvailable (404/1002) on collection-recreate scenario for query-only workloads
- [#3124](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3124) Change Feed Processor: Fixes noisy error notification when lease is stolen by other host
- [#3141](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3141) Diagnostics: Fixes contacted replica count
- [#3173](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3173) UserAgent: Optimized size by removing irrelevant information


### <a name="3.26.2"/> [3.26.2](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.26.2) - 2022-05-05
- [#3155](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3155) Query: Fixes "System.ArgumentException: Stream was not readable." when using WithParameterStream
- [#3154](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3154) Query: Fixes possible missing query results on Windows x64 when using a custom serializer
- [#3137](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3137) Query: Fixes exception message readability for invalid query text. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812)
- 
### <a name="3.26.1"/> [3.26.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.26.1) - 2022-03-16

#### Added
- [#3080](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3080) & [#3081](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3081) Availability: Adds optimization to partition key ranges cache to reduce number of gateway calls in exception scenarios
- [#3079](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3079) Availability: Adds a memory optimization to remove cached values when a not found is returned on a refresh
- [#3089](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3089) Availability: Adds retries to gateway calls for metadata operations on 408 responses

### <a name="3.26.0"/> [3.26.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.26.0) - 2022-03-10

#### Added
- [#3037](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3037) Diagnostics: Adds total number of active clients information
- [#3068](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3068) Query: Adds FeedRange API for Query to GA
- [#3035](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3035) Client Encryption: Adds Client Encryption APIs to GA SDK

### <a name="3.26.0-preview"/> [3.26.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.26.0-preview) - 2022-02-28

#### Added
- [#3037](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3037) Diagnostics: Adds total number of active clients information
- [#3049](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3049) ClientEncryption: Adds algorithm to EncryptionKeyWrapMetadata

### <a name="3.25.0-preview"/> [3.25.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.25.0-preview) - 2022-02-18

#### Added
- [#2948](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2948) Partition Key Delete: Adds DeleteAllItemsByPartitionKeyStreamAsync to container

### <a name="3.25.0"/> [3.25.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.25.0) - 2022-02-18

#### Added
- [#3029](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3029) Dependencies: Upgrades to Azure.Core 1.19.0.

#### Fixed
- [#3034](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3034), [#3024](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3024), [#3018](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3018), [#3000](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3000) Documentation: Improvements in code samples within xml documentation.
- [#3027](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3027) Initialization: Fixes the SDK to retry if the initialization fails due to transient errors.

### <a name="3.24.0-preview"/> [3.24.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.24.0-preview) - 2022-01-31

#### Added
- [#2960](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2960) UserAgent: Adds flag to user agent string to differentiate SDK version.

### <a name="3.24.0"/> [3.24.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.24.0) - 2022-01-31

> :warning: 3.24.0 removes the DefaultTraceListener from the SDK TraceSource for [performance reasons](https://docs.microsoft.com/azure/cosmos-db/sql/performance-tips?tabs=trace-net-core#logging-and-tracing) by default when not running in Debug mode.

#### Added
- [#2926](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2926) Performance: Removes DefaultTraceListener by default.
- [#3008](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3008) Performance: Adds buffer optimizations to Direct mode.
- [#2875](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2875) Query: Adds Index Metrics to Stream API.
- [#2401](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2401) Query: Adds Correlated ActivityId wiring through query.
- [#2917](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2917) LINQ: Adds support for `Nullable<T>.Value` or `Nullable<T>.HasValue` when using camelCase serialization.
- [#2893](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2893) Diagnostics: Adds address info to diagnostics on force cache refresh.
- [#3008](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3008) Diagnostics: Adds diagnostics for splits and timeouts.
- [#2907](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2907) Diagnostics: Adds performance improvement to GetContactedRegions().
- [#2988](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2988) Diagnostics: Adds performance improvement by reducing size and removing irrelevant information (caller info).
- [#3008](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3008) Diagnostics: Adds ServiceEndpoint and Connection Stats to the diagnostics.
- [#3008](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3008) Availability: Direct mode removes blocking call on broken connection exception.
- [#3008](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3008) Supportability: Adds SDK generated substatus codes for 503's to separate from server side 503.


#### Fixed
- [#3008](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3008) Availability: Fixes the SDK to ensure it does not retry on replica that previously failed with 410, 408 and >= 500 status codes.
- [#2869](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2869) Performance: Fixes query improvement to load values lazily. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812)
- [#2883](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2883) Change Feed Processor: Fixes diagnostics on Estimator and ChangeFeedProcessorContext. Introduced in 3.15.0 PR [#1933](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1933)
- [#2900](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2900), [#2899](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2899), [#2915](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2915), [#2912](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2912), [#2925](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2925), [#3000](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3000) Documentation: Improvements in code samples within xml documentation.
- [#2937](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2937), [#2975](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2975) Session: Improvements on session token handling for Gateway mode across splits and merges.
- [#2965](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2965) Session: Fixes operations sending the session token on Gateway mode when the operation reduced consistency to lower than Session.
- [#2975](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2975) Session: Fixes session token too large exception in Gateway mode if the client had no previous cache entry.

### <a name="3.23.0"/> [3.23.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.23.0) - 2021-11-12
### <a name="3.23.0-preview"/> [3.23.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.23.0-preview) - 2021-11-12

#### Added
- [#2868](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2868) Patch: Adds support for patch operations
- [#2822](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2822) Availability: Adds non-blocking async lazy cache to improve upgrade and scaling scenarios
- [#2807](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2807) ChangeFeedProcessor: Adds ChangeFeedProcessorUserException for detailed error notification
- [#2818](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2818) Diagnostics: Adds retry after time to diagnostics
- [#2818](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2818) Availability: Adds retry context to ensure requests on retries do not go to replicas that previously hit transport exceptions

#### Fixed
- [#2851](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2851) Query: Fixes stack overflow in SkipEmptyPageQueryPipelineStage. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812)
- [#2844](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2844) CosmosClientBuilder: Fixes accountEndpoint not being set when using TokenCredential. (Thanks [@levimatheri](https://github.com/levimatheri)) Introduced in 3.22.0 PR [#2753](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2753)  
- [#2793](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2793) Query: Fixes missing diagnostic from query pipeline. Introduced in 3.17.0 PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)
- [#2818](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2818) Diagnostics: Fixes memory calculations in diagnostics
- [#2818](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2818) Availability: Fixes blocking call on broken connection exception
- [#2861](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2861) Performance: Fixes query performance by avoiding ImmutableDictionary. Introduced in 3.17.0 PR [#2144](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2144)

### <a name="3.22.1-preview"/> [3.22.1-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.22.1-preview) - 2021-10-28
### <a name="3.22.1"/> [3.22.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.22.1) - 2021-10-28

#### Fixed
- [#2827](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2827) Query: Fixes a memory leak when on Windows x64 using ServiceInterop. Introduced in 3.22.0 [2777](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2777)
- [#2827](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2827) Availability: Adds keep alive to Linux systems for Direct mode connections to match the Windows keep alive interval

### <a name="3.22.0"/> [3.22.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.22.0) - 2021-10-18

#### Added
- [#2753](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2753) CosmosClientBuilder: Adds overload for passing TokenCredential (Thanks [@levimatheri](https://github.com/levimatheri))
- [#2732](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2732) Diagnostics: Adds request status code summary
- [#2777](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2777) RetryWith(449): Adds improved 449 retry logic with randomized seed and faster retries.
- [#2787](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2787) ChangeFeedProcessor: Adds Notification APIs

#### Fixed
- [#2776](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2776) Query: Fixes a bug where max page size is not being honored after the first 2 pages. Introduced in 3.17.0 [2144](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2144)
- [#2746](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2746) Bulk: Fixes validation to throw if ItemRequestOptions.Properties is set with bulk enabled.
- [#2712](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2712) Serialized types:  Fixes public types(Database Properties, ContainerProperties, etc..) to be forward compatible/future proof with service evolving.
We ourselves might struggle interpret in future after a while.
- [#2739](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2739) Bulk: Fixes item response to include SessionToken and ActivityId
- [#2764](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2764) ChangeFeedProcessor: Fixes log to remove expected 404 scenarios during lease release
- [#2777](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2777) InvalidOperationException: Fixes a race condition multiple threads try to modify exception header causing a InvalidOperationException
- [#2777](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2777) Diagnostics: Fix issue causing CPU usage to be NaN intermittently. Introduced in 3.21.0 PR [2687](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2687)
- [#2786](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2786) GlobalEndpointManager: Fixes noisy TraceCritical on GlobalEndpointManager dispose
- [#2793](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2793) Query Diagnostics: Fixes missing diagnostics from query pipeline
- [#2792](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2792) CosmosClient.ReadAccountAsync: Fixes it to throw CosmosException instead of DocumentClientException

### <a name="3.22.0-preview"/> [3.22.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.22.0-preview) - 2021-10-07

#### Added
- [#2753](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2753) CosmosClientBuilder: Adds overload for passing TokenCredential (Thanks [@levimatheri](https://github.com/levimatheri))
- [#2732](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2732) Diagnostics: Adds request summary
- [#2746](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2746) Bulk: Adds validation to throw if ItemRequestOptions.Properties is set with bulk enabled.
- [#2777](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2777) RetryWith(449): Adds improved 449 retry logic with randomized seed and faster retries.

#### Fixed
- [#2776](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2776) Query: Fixes a bug where max page size is not being honored after the first 2 pages. Introduced in 3.17.0 [2144](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2144)
- [#2712](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2712) Serialized types:  Fixes public types(Database Properties, ContainerProperties, etc..) to be upgrade safe so new content is not lost on deserialize and serialize paths
- [#2739](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2739) Bulk: Fixes item response to include SessionToken and ActivityId
- [#2764](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2764) ChangeFeedProcessor: Fixes log to remove expected 404 scenarios during lease release
- [#2777](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2777) InvalidOperationException: Fixes a race condition multiple threads try to modify exception header causing a InvalidOperationException
- [#2777](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2777) Diagnostics: Fix issue causing CPU usage to be NaN intermittently. Introduced in 3.21.0 PR [2687](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2687)

### <a name="3.21.0-preview"/> [3.21.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.21.0-preview) - 2021-09-10

#### Added
- [#2577](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2577) ResponseMessage : Adds setter for Diagnostics
- [#2613](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2613) Change Feed Processor: Adds notification APIs

#### Fixed
- [#2599](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2599) Diagnostics : Fixes CosmosResponseFactory.CreateItemResponse to use ResponseMessage.Diagnostics instead of creating a new instance

### <a name="3.21.0"/> [3.21.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.21.0) - 2021-09-10

#### Added

- [#2612](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2612) Query: Adds PopulateIndexMetrics request options
- [#2650](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2650) Change Feed Processor: Adds detailed delegate context, stream, and manual check pointing support
- [#2687](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2687) Diagnostics: Adds memory usage, thread starvation detection, optimizations to collection logic
- [#2719](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2719) CosmosException: Adds diagnostics to CosmosException.Message for status codes: 408, 500, 503, 404/1002
- [#2724](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2724) Availability: Adds EnableTcpConnectionEndpointRediscovery to true by default

#### Fixed
- [#2599](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2599) Diagnostics: Fixes duration for HttpResponseStatistics 
- [#2646](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2646) OperationCanceledException: Fixes lost CancellationToken on CosmosOperationCanceledException (Thanks to askazakov)
- [#2509](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2509) GlobalEndpointManager: Fixes exception handling to have the inner exception stacktrace
- [#2611](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2611) Query: Fixes c# query parser to handle Alias in from clause
- [#2661](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2661) ReadMany: Fixes ReadMany API for PartitionKey.None value
- [#2675](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2675) Change Feed: Fixes migration path from preview continuation
- [#2676](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2676) Performance: Fixes query performance regression. Introduced in 3.17.0 PR [2144](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2144)
- [#2687](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2687) Diagnostics: Fixes StoreResult start and end time to be accurate and removes duplicate CPU collector
- [#2697](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2697) Azure Active Directory(AAD): Fixes stuck requests when background refresh fails to refresh token
- [#2708](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2708) Query: Fixes order by logic to throw original exception instead of AggregateException (Thanks to askazakov)
- [#2710](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2710) Security: Fixes ServiceInterop.dll to be BinSkim compliant by adding /guard /Qspectre flags

### <a name="3.20.1"/> [3.20.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.20.1) - 2021-06-29
### <a name="3.20.1-preview"/> [3.20.1-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.20.1-preview) - 2021-06-29

#### Fixed
- [#2450](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2450) Query: Fixes c# parser grammar for recognizing string literal which will avoid falling back to gateway to get the query plan. 
- [#2574](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2574) UserAgent: Fixes race condition in user agent string creation and limits client id to 10. Introduced in 3.20.0 PR [2552](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2552)
- [#2580](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2580) LINQ : Fixes ArgumentNullException while calling ToQueryDefinition() when no filters are applied.

### <a name="3.20.0"/> [3.20.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.20.0) - 2021-06-21

#### Added
- [#2509](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2509) Change Feed: Adds change feed iterator APIs on containers
- [#2558](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2558) Diagnostics: Adds Duration field to HttpResponseStatistics in Diagnostics
- [#2502](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2502) Diagnostics: Adds Direct TransportRequestStats for tracking transport request timeline
- [#2491](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2491) Change Feed Processor: Adds support for Graph API accounts. Graph API accounts can now create lease containers with `/partitionKey` instead of `/id`.

#### Fixed
- [#2567](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2567) ReadMany: Fixes AddRequestHeaders request option and missing headers and message on failure scenarios
- [#2510](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2510) Query: Fixes InvalidOperationException when partitions are merged. Introduced in 3.17.0 PR [#2084](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2084).
- [#2510](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2510) Query: Fixes handling of pipeline execution on partition merge. Introduced in 3.17.0 PR [#2084](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2084).
- [#2547](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2547) Query: Fixes incorrect order by query when the field is an object or array
- [#2511](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2511) Availability: Fixes get account info retry logic to not go to secondary regions on 403(Forbidden)
- [#2512](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2512) Caches: Fixes the cache to remove values if generator throws an exception. Thanks @johngallardo.
- [#2516](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2516) Diagnostics: Fixes a race condition causing InvalidOperationException. Introduced in 3.17.0 PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)
- [#2530](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2530) Gateway: Fixes container recreate scenarios for Gateway Mode in session consistency. Introduced in 3.18.0 PR [#2165](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2165)
- [#2552](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2552) UserAgent: Fixes UserAgent to have the correct number of clients
- [#2562](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2562) Diagnostics: Fixes NullReferenceException in service unavailable scenarios. Introduced in 3.18.0 PR [#2312](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2312)
- [#2502](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2502) Tracing: Removes noisy trace in direct mode

### <a name="3.20.0-preview"/> [3.20.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.20.0-preview) - 2021-06-21

#### Added
- [#2509](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2509) Change Feed: Adds change feed iterator APIs on containers
- [#2558](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2558) Diagnostics: Adds Duration field to HttpResponseStatistics in Diagnostics
- [#2502](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2558) Diagnostics: Adds Direct TransportRequestStats for tracking transport request timeline
- [#2491](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2491) Change Feed Processor: Adds support for Graph API accounts. Graph API accounts can now create lease containers with `/partitionKey` instead of `/id`.
- [#2488](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2488) Change Feed Processor: Refactors checkpoint API to throw exception. Introduced in [#2331](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2331). This is considered a breaking change on the preview API as it evolves into GA.

#### Fixed
- [#2567](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2567) ReadMany: Fixes AddRequestHeaders request option and missing headers and message on failure scenarios
- [#2510](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2510) Query: Fixes InvalidOperationException when partitions are merged. Introduced in 3.17.0 PR [#2084](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2084).
- [#2510](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2510) Query: Fixes handling of pipeline execution on partition merge. Introduced in 3.17.0 PR [#2084](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2084).
- [#2547](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2547) Query: Fixes incorrect order by query when the field is an object or array
- [#2511](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2511) Availability: Fixes get account info retry logic to not go to secondary regions on 403(Forbidden)
- [#2512](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2512) Caches: Fixes the cache to remove values if generator throws an exception. Thanks @johngallardo.
- [#2516](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2516) Diagnostics: Fixes a race condition causing InvalidOperationException. Introduced in 3.17.0 PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)
- [#2530](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2530) Gateway: Fixes container recreate scenarios for Gateway Mode in session consistency. Introduced in 3.18.0 PR [#2165](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2165)
- [#2552](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2552) UserAgent: Fixes UserAgent to have the correct number of clients
- [#2562](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2562) Diagnostics: Fixes NullReferenceException in service unavailable scenarios. Introduced in 3.18.0 PR [#2312](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2312)
- [#2502](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2558) Tracing: Removes noisy trace in direct mode

### <a name="3.19.0"/> [3.19.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.19.0) - 2021-05-25

#### Added
- [#2482](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2482) Azure Active Directory: Adds CosmosClient TokenCredential APIs
- [#2440](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2440) Performance: Adds Bulk optimizations to reduce allocations and improves async task handling
- [#2447](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2447) Availability: Adds account refresh logic on gateway outage instead of waiting on 5min background refresh
- [#2493](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2493) NullReferenceException: Adds logic to append the CosmosDiagnostics to NullReferenceExceptions
- [#2465](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2465) ObjectDisposedException: Adds logic to append the CosmosDiagnostics to ObjectDisposedException
- [#2390](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2390) Bulk: Adds retry for patch operations when request is to large
- [#2487](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2487) UserAgent: Adds flag to user agent to show if region failover is configured

#### Fixed
- [#2451](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2451) Query: Fixes native c# parser not recognizing query with multiple IN statements. Introduced in 3.13.0 PR [#1743](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1743)
- [#2451](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2455) Bulk: Fixes diagnostic traces by removing redundant info and adding correct retry context. Introduced in 3.17.0 PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)
- [#2460](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2460) Permission: Fixes documentation on resource token range limit. (Thanks to arorainms)
- [#2490](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2490) Change Feed: Fixes CancellationToken to be honored. Introduced in 3.15.0 PR [#1933](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1933)
- [#2483](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2483) Availability: Fixes the get account information to stop the background refresh after CosmosClient is disposed. Introduced in 3.18.0 PR [#2355](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2355)
- [#2481](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2481) Azure Active Directory: Fixes token refresh interval, exception handling, and retry logic
- [#2474](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2474) Change Feed: Fixes exceptions generating "Change Feed should always have a next page". Introduced in 3.15.0 PR [#1933](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1933)
- [#2498](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2498) Diagnostics: Fixes default setting in Consistency Config Serialization. Introduced in 3.18.0 PR [#2250](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2250)

### <a name="3.19.0-preview1"/> [3.19.0-preview1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.19.0-preview1) - 2021-05-17

#### Added
- [#2398](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2398) Patch : Adds TrySerializeValueParameter method for PatchOperation
- [#2440](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2440) Performance: Adds Bulk optimizations to reduce allocations and improves async task handling
- [#2447](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2447) Availability: Adds account refresh logic on gateway outage instead of waiting on 5min background refresh
- [#2449](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2449) Client encryption: Adds PolicyFormatVersion and validation that partition key paths are not encrypted

#### Fixed
- [#2451](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2451) Query: Fixes native c# parser not recognizing query with multiple IN statements. Introduced in 3.13.0 PR [#1743](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1743)
- [#2451](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2455) Bulk: Fixes diagnostic traces by removing redundant info and adding correct retry context. Introduced in 3.17.0 PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)
- [#2460](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2460) Permission: Fixes documentation on resource token range limit. (Thanks to arorainms)

### <a name="3.19.0-preview"/> [3.19.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.19.0-preview) - 2021-04-27

#### Added
- [#2308](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2308) & [#2425](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2425) Dedicated Gateway: Adds MaxIntegratedCacheStaleness to Item and Query Request Options  
- [#2371](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2371) Request Options : Adds delegate on request options to access and add headers
- [#2398](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2398) Patch : Adds TrySerializeValueParameter and makes `PatchOperation<T>` internal since it is not used in any public API
- [#2331](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2331) ChangeFeedProcessor: Adds support for manual checkpoint, context, and stream

### <a name="3.18.1-preview"/> [3.18.1-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.18.1-preview) - 2021-06-14

#### Fixed
- [#2510](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2510) Query: Fixes InvalidOperationException on merge to a single partition
- [#2531](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2531) Query: Fixes invalid query results on partition merge

### <a name="3.18.0"/> [3.18.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.18.0) - 2021-04-26

#### Added
- [#2324](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2324) Diagnostics: Adds all http requests to diagnostics
- [#2400](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2400) Performance: Adds optimizations to reduce allocations for Direct + TCP operations
- [#2353](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2353) Query: Adds support to c# query parser for LIKE statement and INT system functions to avoid gateway query plan call when service interop is not available
- [#2397](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2397) Diagnostics: Adds optimizations and BELatency which is the Cosmos DB Backend Request Latency In Milliseconds
- [#2355](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2355) Availability: Adds concurrent requests to secondary region if the initial get account information takes longer than 5 seconds which reduces SDK startup time if primary region is down.
- [#2352](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2352) ReadManyApi: Adds new API designed to efficiently read a list of items using the item id and partition key value
- [#2250](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2250) Diagnostics: Adds client configuration information needed to root cause issues
- [#2241](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2241) ContainerBuilder: Adds public constructor to create ContainerBuilder instance
- [#2222](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2222) Query: Adds WithParameterStream to QueryDefinition to pass in serialized values
- [#2165](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2165) & [#2408](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2408)  Performance: Adds optimization to reduce header size for gateway mode with session consistency. It now only send specific partition session token like direct mode.

#### Fixed
- [#2282](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2282) Query: Fixes COUNT(DISTINCT) to always compute correct value. Any query with more than 1 page of results could produce incorrect values.
- [#2405](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2405) Change Feed: Fixes pull model to avoid additional NotModified call
- [#2368](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2368) Query: Fixes BadRequest with "Failed to parse ... as ResourceId" for gateway mode on splits. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812)
- [#2357](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2357) Query: Fixes incorrect RequestCharge and missing headers in FeedResponse for ordered cross-partition queries. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812) (Thanks to ccurrens)
- [#2409](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2409) Query: Fixes race condition in diagnostics causes missing information and Index out of bound exceptions. Introduced in 3.17.0 PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)
- [#2400](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2400) Availability: Fixes race condition in direct + tcp mode causing SDK generated internal server errors and invalid operation exceptions
- [#2400](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2400) Availability: Fixes race condition in direct + tcp mode causing unnecessary connections to be created by concurrent requests
- [#2392](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2392) Change Feed Estimator: Fixes exception propagation to throw on StartAsync for container/lease not found scenarios. Introduced in 3.17.0 PR [#1830](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1830)
- [#2383](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2383) Availability: Fixes CancellationToken evaluation during failover which could prevent necessary SDK refreshes to occur
- [#2376](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2376) Diagnostics: Fixes invalid nesting and handler names in ITrace. Introduced in 3.17.0 PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)
- [#2286](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2286) Diagnostics: Fixes regression in ITrace where direct operation diagnostics were not included in exception scenarios. Introduced in 3.17.0 PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)
- [#2424](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2424) Query: Fixes TaskCanceledException being converted to InternalServerError and not including diagnostics on most exceptions. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812)

### <a name="3.18.0-preview"/> [3.18.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.18.0-preview) - 2021-03-18

#### Added
- [#2308](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2308) Patch: Adds preview support for Patch API
- [#2312](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2312) Diagnostics: Adds Api for getting all regions contacted by a request

#### Fixed
- [#2314](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2314) Diagnostics: Fixes InvalidOperationException caused by concurrently modifing a dictionary in TraceWriter. Introduced in 3.17.0 PR [#2242](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2242)
- [#2303](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2303) CosmosException : Fixes exception messages to remove JSON formatting
- [#2311](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2311) Spatial: Fixes deserialization when Json does not represent a Spatial type
- [#2284](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2284) Diagnostics: Adds traces for cache operations. Introduced in 3.17.0 PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)
- [#2278](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2278) Documentation: Fixes typos in comment examples (Thanks to paulomorgado)
- [#2279](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2279) Availability: Fixes region failover logic on control plane hot path when gateway hangs. Introduced in 3.16.0 PR [#1954](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1954)
- [#2286](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2286) Diagnostics: Fixes regression which caused ActivityId to not get included. Introduced in 3.17.0 PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)

### <a name="3.17.1"/> [3.17.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.17.1) - 2021-03-19

#### Fixed
- [#2314](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2314) Diagnostics: Fixes InvalidOperationException caused by concurrently modifying a dictionary in TraceWriter. Introduced in 3.17.0 PR [#2242](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2242)
- [#2303](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2303) CosmosException : Fixes exception messages to remove JSON formatting
- [#2311](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2311) Spatial: Fixes deserialization when Json does not represent a Spatial type
- [#2284](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2284) Diagnostics: Adds traces for cache operations. Introduced in 3.17.0 PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)
- [#2278](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2278) Documentation: Fixes typos in comment examples (Thanks to paulomorgado)
- [#2279](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2279) Availability: Fixes region failover logic on control plane hot path when gateway hangs. Introduced in 3.16.0 PR [#1954](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1954)
- [#2286](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2286) Diagnostics: Fixes regression which caused ActivityId to not get included. Introduced in 3.17.0 PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)

### <a name="3.17.0"/> [3.17.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.17.0) - 2021-03-02

#### Added
- [#1870](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1870) Batch API: Adds Session token support
- [#2145](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2145) EnableContentResponseOnWrite: Adds client level support via CosmosClientOptions and CosmosClientBuilder 
- [#2166](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2166) QueryRequestOption: Adds optimization to avoid duplicating QueryRequestOption
- [#1830](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1830) & [#2170](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2170) Change Feed Estimator: Adds support for detailed estimation per lease
- [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097) & [#2204](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2204) & [#2213](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2213) & [#2235](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2235) & [#2236](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2236) & [#2242](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2242) & [#2246](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2246) CosmosDiagnostics: Refactored to use ITrace as the default implementation
- [#2206](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2206) LINQ : Adds User Defined Function Translation Support (Thanks to dpiessens)
- [#2210](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2210) QueryDefinition: Adds API to get query parameters (Thanks to thomaslevesque)
- [#2197](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2197) CosmosClient: Adds CreateAndInitializeAsync method which can be used to avoid latency of warming caches on first operation.
- [#2220](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2220) LINQ: Adds camelCase support to GetItemLinqQueryable() as optional parameter
- [#2249](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2249) Performance: Adds HTTP optimization to disable Nagle Algorithm for .NET Framework applications

#### Fixed
- [#2168](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2168) Query: Fixes a regression in Take operator where it drains the entire query instead of stopping a the take count. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812) and reported in issue [#1979](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/1979)
- [#2129](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2129) CosmosDiagnostics: Fixes memory leak caused by pagination library holding on to all diagnostics. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1933) and reported in issue [#2087](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/2087)
- [#2103](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2103) Query: Fixes ORDER BY undefined (and mixed type primitives) continuation token support. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812)
- [#2124](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2124) Bulk: Fixes retry logic to handle RequestEntityTooLarge exceptions caused by the underlying batch request being to large. Introduced in [#741](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/741)
- [#2198](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2198) CosmosClientOptions: Fixes a bug causing ConsistentPrefix to be convert to BoundedStaleness. Introduced in 3.1.0 PR [#541](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/541) and reported in issue [#2196](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/2196)
- [#2262](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2262) CosmosException: Fixes the headers not matching CosmosException property values and incorrect SubStatusCode values on client initialization failures
- [#2269](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2269) PermissionProperties: Fixes PermissionProperties to not take dependency on internal type to fix mocking 
- [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097) Diagnostics: Fixes regression in query, change feed, and read feed that causes diagnostics to be empty after first page. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812)

### <a name="3.17.0-Preview1"/> [3.17.0-preview1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.17.0-preview1) - 2021-03-02

#### Added
- [#2197](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2197) CosmosClient: Adds CreateAndInitializeAsync Method

#### Fixed
- [#2235](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2235) CosmosDiagnostics: Fixes ITrace JsonTraceWriter to include address resolution and store response stats. Introduced in 3.17.0-preview in PR [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097)
- [#2236](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2236) CosmosDiagnostics: Fixes missing POCO deserialization for query operations.
- [#2218](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2218) (Preview) ChangeFeed pull model: Fixes missing headers on failure path. Introduced in 3.15.0 in PR [#1933](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1933)

### <a name="3.17.0-Preview"/> [3.17.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.17.0-preview) - 2021-02-15

#### Added
- [#1870](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1870) Batch API: Adds Session token support
- [#1952](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1952) & [#1648](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1658) (Preview) Subpartitioning: Adds support for subpartitioning
- [#2122](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2122) (Preview) Change Feed: Adds Full Fidelity support
- [#2145](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2145) EnableContentResponseOnWrite: Adds client level support via CosmosClientOptions and CosmosClientBuilder 
- [#2166](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2166) QueryRequestOption: Adds optimization to avoid duplicating QueryRequestOption
- [#2097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2097) & [#2204](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2204) & [#2213](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2213) CosmosDiagnostics: Refactored to use ITrace as the default implementation
- [#2206](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2206) LINQ : Adds User Defined Function Translation Support (Thanks to dpiessens)
- [#2210](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2210) QueryDefinition: Adds API to get query parameters (Thanks to thomaslevesque)

#### Fixed
- [#2168](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2168) Query: Fixes a regression in Take operator where it drains the entire query instead of stopping a the take count. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812) and reported in issue [#1979](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/1979)
- [#2129](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2129) CosmosDiagnostics: Fixes memory leak caused by pagination library holding on to all diagnostics. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812) and reported in issue [#2087](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/2087)
- [#2103](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2103) Query: Fixes ORDER BY undefined (and mixed type primitives) continuation token support. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812)
- [#2124](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2124) Bulk: Fixes retry logic to handle RequestEntityTooLarge exceptions caused by the underlying batch request being to large. Introduced in [#741](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/741)
- [#2198](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2198) CosmosClientOptions: Fixes a bug causing ConsistentPrefix to be convert to BoundedStaleness. Introduced in 3.1.0 PR [#541](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/541) and reported in issue [#2196](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/2196)

### <a name="3.16.0"/> [3.16.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.16.0) - 2021-01-12

#### Added
- [#2098](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2098) Performance: Adds gateway header optimization
- [#1954](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1954) & [#2094](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2094) Control Plane Hot Path: Adds more aggressive timeout and retry logic for getting caches and query plan from gateway
- [#2013](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2013) Change Feed Processor: Adds support for EPK leases
- [#2016](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2016) Performance: Fixes lock contentions and reduce allocations on TCP requests
- [#2000](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2000) Performance: Adds Authorization Helper improvements

#### Fixed
- [#2110](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2110) CosmosException: Fixes substatuscode to get the correct value instead of 0 when it is not in the enum
- [#2092](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2092) Query: Fixes cancellation token support for the lazy + buffering path
- [#2099](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2099) & [#2116](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2116) CosmosDiagnostics: Fixes IndexOutOfRangeException by adding concurrent operation support
- [#2096](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2096) AggregateException: Fixes some cache calls to throw original exception instead of AggregateException
- [#2044](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2044) Query: Fixes Equals method on SqlParameter class
- [#2077](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2077) Availability: Fixes retry behavior on HttpException where SDK will retry on same region instead of secondary region
- [#2056](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2056) Performance: Fixes encoded strings performance for query operations
- [#2060](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2060) Query: Fixes high CPU usage caused by FeedRange comparison used in LINQ order by operation. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812)
- [#2041](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2041) Request Charge: Fixes request charges for offers and CreateIfNotExists APIs


### <a name="3.15.1"/> [3.15.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.15.1) - 2020-12-16

#### Fixed
- [#2069](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2069) Bulk: Fixes incorrect routing on split
- [#2047](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2047) Diagnostics: Adds operation name to summary
- [#2042](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2042) Change Feed Processor: Fixes StartTime not being correctly applied. Introduced in 3.13.0-preview PR [#1725](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1725)
- [#2071](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2071) Diagnostics: Fixes substatuscode when recording internal DocumentClientException

### <a name="3.15.0"/> [3.15.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.15.0) - 2020-11-17

#### Added
- [#1926](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1926) Query: Adds multiple arguments in IN clause support to c# query parser when service interop is not available.
- [#1933](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1933) ChangeFeed: Adds adoption of pagination library
- [#1943](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1943) Performance: Adds query optimization by LazyCosmosElement Cache Improvements
- [#1944](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1944) Performance: Adds direct version to get response header improvement
- [#1947](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1947) ReadFeed: Adds pagination library adoption
- [#1949](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1949) Performance: Adds optimized request headers
- [#1974](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1974) Performance: Adds Bulk optimization by reducing lock contention in TimerWheel
- [#1977](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1977) Performance diagnostics: Adds static timer and caches handler name

#### Fixed
- [#1930](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1930) Change Feed: Fixes estimator diagnostics
- [#1939](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1939) LINQ: Fixes ArgumentNullException with StringComparison sensitive case (Thanks to ylabade)
- [#1940](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1940) LINQ: Fixes CancellationToken bug in CosmosLinqQuery.AggregateResultAsync (Thanks to ylabade)
- [#1960](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1960) CosmosClientOptions and ClientBuilder: Fixes ArgumentException when setting null value on HttpClientFactory or WebProxy
- [#1961](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1961) RequestOption.Properties: Fixes RequestOption.Properties for CreateContainerIfNotExistsAsync
- [#1967](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1967) Query: Fixes CancellationToken logic in pagination library
- [#1988](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1988) Query: Fixes split proofing logic for queries with logical partition key
- [#1999](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1999) Performance: Fixes exception serialization when tracing is not enabled
- [#2004](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2004) Query: Fixes SplitHandling bug caused by caches not getting refreshed. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812)

###  <a name="3.15.2-preview"/> [3.15.2-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.15.2-preview) - 2020-11-17

#### Fixed
- [#2004](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2004) Query: Fixes SplitHandling bug caused by caches not getting refreshed. Introduced in 3.14.0 PR [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812)

###  Unlisted see [#2004](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2004) - <a name="3.15.1-preview"/> [3.15.1-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.15.1-preview) - 2020-11-05 

#### Fixed
- [#1972](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1972) Private preview Azure Active Directory: Fixes TokenCredentialCache timeout logic and ports tests from master
- [#1984](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1984) Private preview Azure Active Directory: Fixes issue with using wrong scope value

### Unlisted see [#2004](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2004) -  <a name="3.15.0-preview"/> [3.15.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.15.0-preview) - 2020-10-21

#### Added
- [#1944](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1944) Performance: Adds direct version to get response header improvement
- [#1933](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1933) Change Feed: Adds new continuation token format which can be migrated via new EmitOldContinuationToken.
- [#1933](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1933) Change Feed: Adds the ability to retry on 304s and no longer modifies HasMoreResults
- [#1926](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1926) Query: Adds multiple arguments in IN clause support to c# query parser when service interop is not available.
- [#1798](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1798) Private Preview Azure Active Directory: Adds Azure Active Directory support to the SDK

#### Fixed
- [#1933](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1933) Change Feed: Fixes StartFrom bug where the value was not honored

### Unlisted see [#2004](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2004) - <a name="3.14.0-preview"/> [3.14.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.14.0-preview) - 2020-10-09

#### Added
- [#1830](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1830) Change Feed Estimator: Adds support for detailed estimation per lease

### Unlisted see [#2004](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2004) -  <a name="3.14.0"/> [3.14.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.14.0) - 2020-10-09

#### Added
- [#1876](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1876) Performance: Adds session token optimization
- [#1879](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1879) Performance: Adds AuthorizationHelper improvements
- [#1882](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1882) Performance: Adds SessionContainer optimizations and style fixes
- [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812) Query: Adds adoption of pagination library
- [#1920](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1920) Query: Adds RegexMatch system function support

#### Fixed
- [#1875](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1875) HttpClient: Fixes HttpResponseMessage.RequestMessage is null in WASM
- [#1886](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1886) Change Feed Processor: Fixes failures during initialization
- [#1892](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1892) GatewayAddressCache: Fixes high CPU from HashSet usage on Address refresh path
- [#1909](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1909) Authorization: Fixes DocumentClientException being thrown on write operations
- [#1812](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1812) Query: Fixes MalformedContinuationTokenException.  Introduced in 3.7.0 PR [#1260](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1260) and reported in issue [#1364](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/1364)

### <a name="3.13.0"/> [3.13.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.13.0) - 2020-09-21

#### Added

- [#1743](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1743) Query Performance: Adds skipping getting query plan for non-aggregate single partition queries on non-Windows x64 systems when FeedOptions.PartitionKey is set
- [#1768](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1768) Performance: Adds SessionToken optimization to reduce header size by removing session token for CRUD on stored procedure, triggers, and UDFs
- [#1781](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1781) Performance: Adds headers optimization which can reduce response allocation by 10 KB per a request. 
- [#1825](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1825) RequestOptions.Properties: Adds the ability for applications to specify request context
- [#1835](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1835) Performance: Add HttpClient optimization to avoid double buffering gateway responses
- [#1837](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1837) Query SystemFunctions : Adds DateTime System Functions
- [#1842](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1842) Query Performance: Adds Singleton QueryPartitionProvider. Helps when Container is getting recreated.
- [#1857](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1857) Performance: Adds finalizer optimizations in a few places (Thanks to pentp)
- [#1843](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1843) Performance: Adds Transport serialization, SessionTokenMismatchRetryPolicy, and store response dictionary optimizations

#### Fixed

- [#1757](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1757) Batch API: Fixes the size limit to reduce timeouts
- [#1758](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1758) Connectivity: Fixes address resolution calls when using EnableTcpConnectionEndpointRediscovery
- [#1788](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1788) Transient HTTP exceptions: Adds retry logic to all http requests
- [#1863](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1863) HttpClientHandler: Fixes HttpClientHandler PlatformNotSupportedException

### <a name="3.13.0-preview"/> [3.13.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.13.0-preview) - 2020-08-12

#### Added
- [#1725](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1725) ChangeFeed : Adds ChangeFeedStartFrom to support StartTimes x FeedRanges. WARNING: This is breaking change for preview SDK
- [#1764](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1764) Performance: Adds compiler optimize flag
- [#1768](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1768) SessionToken: Adds optimization to reduce header size by removing session token for CRUD on stored procedure, triggers, and UDFs

#### Fixed

- [#1757](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1757) Batch API: Fixes the size limit to reduce timeouts
- [#1758](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1758) Connectivity: Fixes address resolution calls when using EnableTcpConnectionEndpointRediscovery

### <a name="3.12.0"/> [3.12.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.12.0) - 2020-08-06

#### Added 

- [#1548](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1548) Transport: Adds an optimization to unify HttpClient usage across Gateway classes
- [#1569](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1569) Batch API: Adds support of request options for transactional batch
- [#1693](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1693) Performance: Reduces lock contention on GlobalAddress Resolver
- [#1712](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1712) Performance: Adds optimization to reduce AuthorizationHelper memory allocations
- [#1715](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1715) Availability: Adds cross-region retry mechanism on transient connectivity issues
- [#1721](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1721) LINQ : Adds support for case-insensitive searches (Thanks to jeffpardy)
- [#1733](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1733) Change Feed Processor: Adds backward compatibility of lease store

#### Fixed

- [#1548](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1548) Availability: Fixes SDK failover logic. An HttpClient used the user configured request timeout on metadata request causing an ambiguous OperationCanceledException instead of the HttpRequestException which is used to trigger failovers.
- [#1720](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1720) Gateway Trace: Fixes a bug where the ActivityId is being set to Guid.Empty
- [#1728](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1728) Diagnostics: Fixes ActivityScope by moving it to operation level
- [#1740](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1740) Connection limits: Fixes .NET core to honor gateway connection limit
- [#1744](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1744) Transport: Fixes use of PortReuseMode and other Direct configuration settings

### <a name="3.11.1-preview"/> [3.11.1-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.11.1-preview) - 2020-10-01

- [#1892](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1892) Performance: Fixes High CPU caused by EnableTcpConnectionEndpointRediscovery by reducing HashSet lock contention

### <a name="3.11.0"/> [3.11.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.11.0) - 2020-07-07
### <a name="3.11.0-preview"/> [3.11.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.11.0-preview) - 2020-07-07
#### Added 

- [#1587](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1587) & [1643](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1643) & [1667](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1667)  Diagnostics: Adds synchronization context tracing to all request
- [#1617](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1617) Performance: Fixes Object Model hierarchy to use strings for relative paths instead of URI
- [#1639](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1639) CosmosClient: Adds argument check for empty key to prevent ambiguous 401 not authorized exception
- [#1640](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1640) Bulk: Adds TimerWheel to Bulk to improve latency
- [#1678](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1678) Autoscale: Adds to container builder

#### Fixed

- [#1638](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1638) Documentation : Fixes all examples to add using statement to FeedIterator
- [#1666](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1666) CosmosOperationCanceledException: Fixes handler to catch all operation cancelled exceptions
- [#1682](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1682) Performance: Fixes high CPU consumption caused by EnableTcpConnectionEndpointRediscovery


### <a name="3.10.1"/> [3.10.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.10.1) - 2020-06-18

- [#1637](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1637) TransportHandler : Removes stack trace print. Introduced in 3.10.0 PR 1587 

### <a name="3.10.0"/> [3.10.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.10.0) - 2020-06-18

#### Added

- [#1613](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1613) Query FeedIterator: Adds IDisposable to fix memory leak. WARNING: This will require changes to fix static anlysis tools checking for dispose.
- [#1550](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1550) CosmosOperationCanceledException: This enables users to access the diagnsotics when an operation is canceled via the cancellation token. The new type extends OperationCanceledException so it does not break current exception handling and includes the CosmosDiagnostic in the ToString().
- [#1578](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1578) Query: Adds memory optimization to prevent coping the buffer
- [#1578](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1578) Query: Adds support for ignore case for Contains and StartsWith functions.
- [#1602](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1602) Diagnostics: Adds CPU usage to all operations
- [#1603](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1603) Documentation: Adds new exception handling documentation


#### Fixed

- [#1530](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1530) ContainerDefinition : Fixes WithDefaultTimeToLive argument spelling (Thanks to tony-xia)
- [#1547](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1547) & [#1582](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1582) Query and Readfeed: Fix exceptions caused by not properly handling splits
- [#1578](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1578) ApplicationRegion: Fixes ApplicationRegion to ensure the correct order is being used for failover scenarios
- [#1585](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1585) Query : Fixes Multi- ORDER BY continuation token support with QueryExecutionInfo response headers

### <a name="3.9.1"/> [3.9.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.9.1) - 2020-05-19
### <a name="3.9.1-preview"/> [3.9.1-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.9.1-preview) - 2020-05-19

#### Fixed

- [#1539](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1539) CosmosException and Diagnostics: Fixes ToString() to not grow exponentially with retries. Introduced in 3.7.0 in PR [#1189](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1189).

### <a name="3.9.0"/> [3.9.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.9.0) - 2020-05-18

#### Added

- [#1356](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1356) & [#1407](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1407) & [#1428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1428) & [#1407](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1407) Adds autoscale support.
- [#1398](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1398) Diagnostics: Adds CPU monitoring for .NET Core.
- [#1441](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1441) Transport: Adds `HttpClientFactory` support on `CosmosClientOptions`.
- [#1457](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1457)  Container: Adds database reference to the container.
- [#1455](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1454) Serializer: Adds SDK serializer to `Client.ClientOptions.Serializer`.
- [#1397](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1397) CosmosClientBuilder: Adds preferred regions and `WithConnectionModeDirect()`.
- [#1439](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1439) No content on Response: Adds the ability to have operations return no content from Azure Cosmos DB. 
- [#1398](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1398) & [#1516](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1516) Read feed and change feed: Adds serialization optimization to reduce memory and CPU utilization up to 90%. Objects are now passed as an array to the serializer. 
- [#1516](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1516) Query: Adds serialization optimization to reduce memory up to %50 and CPU utilization up to 25%. Objects are now passed as an array to the serializer.

#### Fixed

- [#1401](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1401) & [#1437](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1437): Response type: Fix deadlock on scenarios with `SynchronizationContext` when using `Response.Container`.
- [#1445](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1445) Transport: Fix `ServicePoint` for `WebAssembly`.
- [#1462](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1462) UserAgent: Fix feature usage tracking.
- [#1469](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1469) Diagnostics: Fix `InvalidOperationException` and converts elapsed time to millisecond.
- [#1512](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1512) PartitionRoutingHelper: Fix ReadFeed `ArgumentNullException` due to container cache miss.
- [#1530](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1530) CosmosClientBuilder: Fix WithDefaultTimeToLive parameter spelling.

### <a name="3.9.0-preview3"/> [3.9.0-preview3](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.9.0-preview3) - 2020-05-11

#### Added

- [#1356](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1356) & [#1407](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1407) & [#1428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1428) Autoscale preview release.
- [#1407](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1407) Autoscale: Adds `CreateDatabaseIfNotExistsAsync` and `CreateContainerIfNotExistsAsync` methods.
- [#1410](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1410) FeedRange: Adds Json serialization support.
- [#1398](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1398) Diagnostics: Adds CPU monitoring for .NET Core.
- [#1441](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1441) Transport: Adds `HttpClientFactory` support on `CosmosClientOptions`.
- [#1457](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1457) Container: Adds database reference to the container.
- [#1453](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1453) Response factory: Adds a response factory to the public API.
- [#1455](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1454) Serializer: Adds SDK serializer to `Client.ClientOptions.Serializer`.
- [#1397](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1397) CosmosClientBuilder: Adds preferred regions and public internal func `WithConnectionModeDirect()`.
- [#1439](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1439) No content on response: Adds the ability to have operation return no content from Azure Cosmos DB.
- [#1469](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1469) Diagnostics: Fixes the `InvalidOperationException` and converts elapsed time to millisecond.

#### Fixed

- [#1398](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1398) Reduced memory allocations on query deserialization.
- [#1401](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1401) & [#1437](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1437): Response type: Fixes deadlock on scenarios with `SynchronizationContext` when using `Response.Container`.
- [#1445](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1445) Transport: Fixes `ServicePoint` for WebAssembly.
- [#1462](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1462) UserAgent: Fixes feature usage tracking.

### <a name="3.9.0-preview"/> [3.9.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.9.0-preview) - 2020-04-17

#### Added

- [#1356](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1356) Autoscale: Adds to public preview release

### <a name="3.8.0-preview"/> [3.8.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.8.0-preview) - 2020-04-16

#### Added

- [#1331](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1331) Enabled client encryption / decryption for transactional batch requests

#### Fixed

- [#1369](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1369) Fixes decryption for 'order by' query results

### <a name="3.8.0"/> [3.8.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.8.0) - 2020-04-07

#### Added

- [#1314](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1314) Adds configuration for proactive TCP end-of-connection detection.
- [#1305](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1305) Adds support for preferred region customization.

#### Fixed

- [#1312](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1312) Fixes null reference when using default(PartitionKey).
- [#1296](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1296) Decrypts the encrypted properties before returning query result.
- [#1345](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1345) Fixes get query plan diagnostics.

### <a name="3.7.1-preview"/> [3.7.1-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.7.1-preview) - 2020-03-30

- [#1210](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1210) Adds change feed pull model.
- [#1242](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1242) Client encryption - fixes bug in read path without encrypted properties.
- [#1314](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1314) Adds configuration for proactive TCP end-of-connection detection.
- [#1312](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1312) Fixes null reference when using default(PartitionKey).
- [#1296](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1296) Decrypts the encrypted properties before returning query result.

### <a name="3.7.0"/> [3.7.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.7.0) - 2020-03-26

#### Added

- [#1268](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1268) Adds `GetElapsedClientLatency` to `CosmosDiagnostics`.
- [#1239](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1239) Made `MultiPolygon` and `PolygonCoordinates` classes public.
- [#1233](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1265) Partition key now supports operators `==, !=` for equality comparison.
- [#1285](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1285) Add query plan retrieval to diagnostics.
- [#1289](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1289) Query `ORDER BY` resume optimization.
- [#1074](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1074) Bulk API congestion control.

#### Fixed

- [#1213](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1213) `CosmosException` now returns the original stack trace.
- [#1213](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1213) `ResponseMessage.ErrorMessage` is now always correctly populated. There was bug in some scenarios where the error message was left in the content stream.
- [#1298](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1298) `CosmosException.Message` contains the same information as `CosmosException.ToString()` to ensure all the information is being tracked.
- [#1242](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1242) Client encryption - Fixes bug in read path without encrypted properties.
- [#1189](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1189) Query diagnostics shows correct overall time.
- [#1189](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1189) Fixes a bug that caused duplicate information in diagnostic context.
- [#1263](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1263) Fixes a bug where retry after interval did not get set on query stream responses.
- [#1198](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1198) Fixes null reference exception when calling a disposed `CosmosClient`.
- [#1274](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1274) `ObjectDisposedException` is thrown when calling all SDK objects like Database and Container that reference a disposed client.
- [#1268](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1268) Fixes bug where Request Options was getting lost for `Database.ReadStreamAsync` and `Database.DeleteStreamAsync` methods.
- [#1304](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1304) Fixes XML documentation so it now is visible in Visual Studio.

### <a name="3.7.0-preview2"/> [3.7.0-preview2](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.7.0-preview2) - 2020-03-09

- [#1210](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1210) Change feed pull model.
- [#1242](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1242) Client encryption - fixes bug in read path without encrypted properties.

### <a name="3.7.0-preview"/> [3.7.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.7.0-preview) - 2020-02-25

- [#1074](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1074) Bulk API congestion control.
- [#1210](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1210) Change feed pull model.

### <a name="3.6.0"/> [3.6.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.6.0) - 2020-01-23

- [#1105](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1198) CosmosClient Immutability + Disposable Fixes

#### Added

- [#1097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1097) `GeospatialConfig` to `ContainerProperties`, `BoundingBoxProperties` to `SpatialPath`.
- [#1061](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1061) Stream payload to `ExecuteStoredProcedureStreamAsync`.
- [#1062](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1062) Additional diagnostic information including the ability to track time through the different SDK layers.
- [#1107](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1107) Source Link support.
- [#1121](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1121) `StandByFeedIterator` breath-first read strategy.

#### Fixed

- [#1105](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1105) Custom serializer no longer calls SDK owned types that would cause serialization exceptions.
- [#1112](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1112) Fixes SDK properties like `DatabaseProperties` to have the same JSON attributes.
- [#1116](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1116) Fixes a deadlock on scenarios with `SynchronizationContext` while executing async query operations.
- [#1143](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1143) Fixes permission resource link and authorization issue when doing a query with resource token for a specific partition key.
- [#1150](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1150) Fixes `NullReferenceException` when using a non-existent Lease Container.

### <a name="3.5.1"/> [3.5.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.5.1) - 2019-12-11

#### Fixed

- [#1060](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1060) Fixes unicode encoding bug in DISTINCT queries.
- [#1070](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1070) `CreateItem` will only retry for auto-extracted partition key in-case of collection re-creation.
- [#1075](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1075) Including header size details for bad request with large headers.
- [#1078](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1078) Fixes a deadlock on scenarios with `SynchronizationContext` while executing async SDK API.
- [#1081](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1081) Fixes race condition in serializer caused by null reference exception.
- [#1086](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1086) Fix possible `NullReferenceException` on a `TransactionalBatch` code path.
- [#1091](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1091) Fixes a bug in query when a partition split occurs that causes a `NotImplementedException` to be thrown.
- [#1089](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1089) Fixes a `NullReferenceException` when using Bulk with items without partition key.

### <a name="3.5.0"/> [3.5.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.5.0) - 2019-12-03

#### Added

- [#979](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/979) Make `SessionToken` on `QueryRequestOptions` public.
- [#995](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/995) Included session token in diagnostics.
- [#1000](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1000) Adds `PortReuseMode` to `CosmosClientOptions`.
- [#1017](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1017) Adds `ClientSideRequestStatistics` to gateway calls and making end time nullable.
- [#1038](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1038) Adds self-link to resource properties.

#### Fixed

- [#921](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/921) Fixes error handling to preserve stack trace in certain scenarios.
- [#944](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/944) Change feed processor won't use user serializer for internal operations.
- [#988](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/988) Fixes query mutating due to retry of gone / name cache is stale.
- [#954](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/954) Support start from beginning for change feed processor in multi master accounts.
- [#999](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/999) Fixes grabbing extra page, updated continuation token on exception path, and non-Ascii character in order by continuation token.
- [#1013](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1013) Gateway `OperationCanceledExceptions` are now returned as request timeouts.
- [#1020](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1020) Direct package update removes debug statements.
- [#1023](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1023) Fixes `ThroughputResponse.IsReplacePending` header mapping.
- [#1036](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1036) Fixes query responses to return null Content if it is a failure.
- [#1045](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1045) Adds stack trace and inner exception to `CosmosException`.
- [#1050](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1050) Adds mocking constructors to `TransactionalBatchOperationResult`.

### <a name="3.4.1"/> [3.4.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.4.1) - 2019-11-06

#### Fixed

- [#978](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/978) Fixes mocking for `FeedIterator` and response classes.

### <a name="3.4.0"/> [3.4.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.4.0) - 2019-11-04

#### Added

- [#853](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/853) ORDER BY arrays and object support.
- [#877](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/877) Query diagnostics now contains client-side request diagnostics information.
- [#923](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/923) Bulk support is now public.
- [#922](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/922) Included information of bulk support usage in user agent.
- [#934](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/934) Preserved the ordering of projections in a `GROUP BY` query.
- [#952](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/952) ORDER BY undefined and mixed type `ORDER BY` support.
- [#965](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/965) Batch API is now public.

#### Fixed

- [#901](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/901) Fixes a bug causing query response to create a new stream for each content call.
- [#918](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/918) Fixes serializer being used for scripts, permissions, and conflict-related iterators.
- [#936](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/936) Fixes bulk requests with large resources to have natural exception.


### <a name="3.3.3"/> [3.3.3](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.3.3) - 2019-10-30

- [#837](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/837) Fixes group by bug for non-Windows platforms.
- [#927](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/927) Fixes query returning partial results instead of error.

### <a name="3.3.2"/> [3.3.2](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.3.2) - 2019-10-16

#### Fixed

- [#905](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/909) Fixes lINQ camel case bug.

### <a name="3.3.1"/> [3.3.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.3.1) - 2019-10-11

#### Fixed

- [#895](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/895) Fixes user agent bug that caused format exceptions on non-Windows platforms.


### <a name="3.3.0"/> [3.3.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.3.0) - 2019-10-09

#### Added

- [#801](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/801) Enabled LINQ `ThenBy` operator after `OrderBy`.
- [#814](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/814) Ability to limit to configured endpoint only.
- [#822](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/822) `GROUP BY` query support.
- [#844](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/844) Adds `PartitionKeyDefinitionVersion` to container builder.

#### Fixed

- [#835](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/835) Fixes a bug that caused sorted ranges exceptions.
- [#846](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/846) Statistics not getting populated correctly on `CosmosException`.
- [#857](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/857) Fixes reusability of the bulk support across container instances.
- [#860](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/860) Fixes base user agent string.
- [#876](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/876) Default connection time out reduced from 60 s to 10 s.

### <a name="3.2.0"/> [3.2.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.2.0) - 2019-09-17

#### Added

- [#100](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/100) Configurable Tcp settings to `CosmosClientOptions`.
- [#615](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/615), [#775](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/775) Adds request diagnostics to response.
- [#622](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/622) Adds CRUD and query operations for users and permissions, which enables ResourceToken support.
- [#716](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/716) Added camel case serialization on LINQ query generation.
- [#729](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/729), [#776](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/776) Adds aggregate (CountAsync/SumAsync etc.) extensions for LINQ query.
- [#743](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/743) Adds `WebProxy` to `CosmosClientOptions`.

#### Fixed

- [#726](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/726) Query iterator `HasMoreResults` now returns false if an exception is hit.
- [#705](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/705) User agent suffix gets truncated.
- [#753](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/753) Reason was not being propagated for conflict exceptions.
- [#756](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/756) Change feed processor with `WithStartTime` would execute the delegate the first time with no items.
- [#761](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/761) `CosmosClient` deadlocks when using a custom task scheduler like Orleans.
- [#769](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/769) Session consistency and gateway mode session-token bug fix- under few rare non-success cases session token might be in-correct.
- [#772](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/772) Fixes throughput throwing when custom serializer used or offer doesn't exists.
- [#785](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/785) Incorrect key to throw `CosmosExceptions` with `HttpStatusCode.Unauthorized` status code.


### <a name="3.2.0-preview2"/> [3.2.0-preview2](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.2.0-preview2) - 2019-09-10

- [#585](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/585), [#741](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/741) Bulk execution support.
- [#427](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/427) Transactional batch support (Item CRUD).


### <a name="3.2.0-preview"/> [3.2.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.2.0-preview) - 2019-08-09

- [#427](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/427) Transactional batch support (Item CRUD).


### <a name="3.1.1"/> [3.1.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.1.1) - 2019-08-12

#### Added

- [#650](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/650) `CosmosSerializerOptions` to customize serialization.

#### Fixed

- [#612](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/612) Bug fix for `ReadFeed` with partition key.
- [#614](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/614) Fixes spatial path serialization and compatibility with older index versions.
- [#619](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/619) Fixes `PInvokeStackImbalance` exception for .NET framework.
- [#626](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/626) `FeedResponse<T>` status codes now return OK for success instead of the invalid status code 0 or Accepted.
- [#629](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/629) Fixes `CreateContainerIfNotExistsAsync` validation to limited to partition key path only.
- [#630](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/630) Fixes user agent to contain environment and package information.


### <a name="3.1.0"/> 3.1.0 - 2019-07-29 - Unlisted

#### Added

- [#541](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/541) Adds consistency level to client and query options.
- [#544](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/544) Adds continuation token support for LINQ.
- [#557](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/557) Adds trigger options to item request options.
- [#572](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/572) Adds partition key validation on `CreateContainerIfNotExistsAsync`.
- [#581](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/581) Adds LINQ to `QueryDefinition` API.
- [#592](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/592) Adds `CreateIfNotExistsAsync` to container builder.
- [#597](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/597) Adds continuation token property to `ResponseMessage`.
- [#604](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/604) Adds LINQ `ToStreamIterator` extension method.

#### Fixed

- [#548](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/548) Fixes mis-typed message in `CosmosException.ToString()`.
- [#558](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/558) Location cache `ConcurrentDict` lock contentions fix.
- [#561](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/561) `GetItemLinqQueryable` now works with null query.
- [#567](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/567) Query correctly handles different language cultures.
- [#574](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/574) Fixes empty error message if query parsing fails from unexpected exception.
- [#576](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/576) Query correctly serializes the input into a stream.


### <a name="3.0.0"/> [3.0.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.0.0) - 2019-07-15

- General availability of [Version 3.0.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/) of the .NET SDK.
- Targets .NET Standard 2.0, which supports .NET framework 4.6.1+ and .NET Core 2.0+.
- New object model, with top level `CosmosClient` and methods split across relevant database and container classes.
- New stream APIs that have high performance.
- Built-in support for change feed processor APIs.
- Fluent builder APIs for `CosmosClient`, container, and change feed processor.
- Idiomatic throughput management APIs.
- Granular `RequestOptions` and `ResponseTypes` for database, container, item, query, and throughput requests.
- Ability to scale non-partitioned containers.
- Extensible and customizable serializer.
- Extensible request pipeline with support for custom handlers.

## <a name="known-issues"></a> Known issues

Below is a list of any know issues affecting the [recommended minimum version](#recommended-version):

| Issue | Impact | Mitigation | Tracking link |
| --- | --- | --- | --- |

## Release & Retirement dates

Microsoft provides notification at least **12 months** in advance of retiring an SDK in order to smooth the transition to a newer/supported version. New features and functionality and optimizations are only added to the current SDK, as such it is recommended that you always upgrade to the latest SDK version as early as possible.

After **31 August 2022**, Azure Cosmos DB will no longer make bug fixes, add new features, and provide support for versions 1.x of the Azure Cosmos DB .NET or .NET Core SDK for SQL API. If you prefer not to upgrade, requests sent from version 1.x of the SDK will continue to be served by the Azure Cosmos DB service.

| Version | Release Date | Retirement Date |
| --- | --- | --- |
| [3.6.0](#3.6.0) |January 23, 2020 |--- |
| [3.5.1](#3.5.1) |December 11, 2019 |--- |
| [3.5.0](#3.5.0) |December 03, 2019 |--- |
| [3.4.1](#3.4.1) |November 06, 2019 |--- |
| [3.4.0](#3.4.0) |November 04, 2019 |--- |
| [3.3.3](#3.3.3) |October 30, 2019 |--- |
| [3.3.2](#3.3.2) |October 16, 2019 |--- |
| [3.3.1](#3.3.1) |October 11, 2019 |--- |
| [3.3.0](#3.3.0) |October 8, 2019 |--- |
| [3.2.0](#3.2.0) |September 18, 2019 |--- |
| [3.1.1](#3.1.1) |August 12, 2019 |--- |
| [3.1.0](#3.1.0) |July 29, 2019 |--- |
| [3.0.0](#3.0.0) |July 15, 2019 |--- |

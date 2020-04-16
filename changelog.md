# Changelog

Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

### Added

- [#1331](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1331) Enable client encryption / decryption for transactional batch requests

### Fixed

- [#1369](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1369) Fix decryption for 'order by' query results

## <a name="3.8.0"/> [3.8.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.8.0) - 2020-04-07

### Added

- [#1314](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1314) Added configuration for proactive TCP end-of-connection detection
- [#1305](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1305) Added support for preferred region customization

### Fixed

- [#1312](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1312) Fixed null reference when using default(PartitionKey)
- [#1296](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1296) Decrypt the encrypted properties before returning query result
- [#1345](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1345) Fixed get query plan diagnostics

## <a name="3.7.1-preview"/> [3.7.1-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.7.1-preview) - 2020-03-30

- [#1210](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1210) Change Feed pull model
- [#1242](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1242) Client encryption - Fix bug in read path without encrypted properties
- [#1314](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1314) Added configuration for proactive TCP end-of-connection detection
- [#1312](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1312) Fixed null reference when using default(PartitionKey)
- [#1296](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1296) Decrypt the encrypted properties before returning query result

## <a name="3.7.0"/> [3.7.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.7.0) - 2020-03-26

### Added
- [#1268](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1268) Add GetElapsedClientLatency to CosmosDiagnostics
- [#1239](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1239) Made MultiPolygon and PolygonCoordinates classes public.
- [#1233](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1265) PartitionKey now supports operators ==, != for equality comparison.
- [#1285](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1285) Add query plan retrevial to diagnostics
- [#1289](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1289) Query ORDER BY Resume Optimization
- [#1074](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1074) Bulk API congestion control

### Fixed

- [#1213](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1213) CosmosException now returns the original stack trace.
- [#1213](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1213) ResponseMessage.ErrorMessage is now always correctly populated. There was bug in some scenarios where the error message was left in the content stream.
- [#1298](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1298) CosmosException.Message contains the same information as CosmosException.ToString() to ensure all the information is being tracked
- [#1242](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1242) Client encryption - Fix bug in read path without encrypted properties
- [#1189](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1189) Query diagnostics shows correct overall time.
- [#1189](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1189) Fixed a bug that caused duplicate information in diagnostic context.
- [#1263](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1263) Fix a bug where retry after internval did not get set on query stream responses
- [#1198](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1198) Fixes null reference exception when calling a disposed CosmosClient
- [#1274](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1274) ObjectDisposedException is thrown when calling all SDK objects like Database and Container that reference a disposed client 
- [#1268](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1268) Fix bug where Request Options was getting lost for Database.ReadStreamAsync and Database.DeleteStreamAsync
- [#1304](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1304) Fixed XML documentation so it now is visible in Visual Studio

## <a name="3.7.0-preview2"/> [3.7.0-preview2](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.7.0-preview2) - 2020-03-09

- [#1210](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1210) Change Feed pull model
- [#1242](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1242) Client encryption - Fix bug in read path without encrypted properties

## <a name="3.7.0-preview"/> [3.7.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.7.0-preview) - 2020-02-25

- [#1074](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1074) Bulk API congestion control
- [#1210](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1210) Change Feed pull model

## <a name="3.6.0"/> [3.6.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.6.0) - 2020-01-23

- [#1105](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1198) CosmosClient Immutability + Disposable Fixes

### Added

- [#1097](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1097) Add GeospatialConfig to ContainerProperties, BoundingBoxProperties to SpatialPath
- [#1061](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1061) Add Stream payload to ExecuteStoredProcedureStreamAsync
- [#1062](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1062) Add additional diagnostic information including the ability to track time through the different SDK layers
- [#1107](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1107) Add Source Link support
- [#1121](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1121) StandByFeedIterator breath-first read strategy

### Fixed

- [#1105](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1105) Custom serializer no longer calls SDK owned types that would cause serialization exceptions
- [#1112](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1112) Fixed SDK properties like DatabaseProperties to have same JSON attributes
- [#1116](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1116) Fixed a deadlock on scenarios with SynchronizationContext while executing async query operations
- [#1143](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1143) Fixed permission resource link and authorization issue when doing a query with resource token for a specific partition key
- [#1150](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1150) Fixed NullReferenceException when using a non-existent Lease Container.

## <a name="3.5.1"/> [3.5.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.5.1) - 2019-12-11

### Fixed

- [#1060](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1060) Fixed unicode encoding bug in DISTINCT queries.
- [#1070](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1070) CreateItem will only retry for auto-extracted partition key in-case of collection re-creation
- [#1075](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1075) Including header size details for BadRequest with large headers
- [#1078](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1078) Fixed a deadlock on scenarios with SynchronizationContext while executing async SDK API
- [#1081](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1081) Fixed race condition in serializer caused null reference exception.
- [#1086](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1086) Fix possible NullReferenceException on a TransactionalBatch code path
- [#1091](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1091) Fixed a bug in query when a partition split occurs that causes a NotImplementedException to be thrown.
- [#1089](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1089) Fixes a NullReferenceException when using Bulk with items with no PK

## <a name="3.5.0"/> [3.5.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.5.0) - 2019-12-03

### Added

- [#979](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/979) Make SessionToken on QueryRequestOptions public.
- [#995](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/995) Included session token in diagnostics.
- [#1000](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1000) Add PortReuseMode to CosmosClientOptions.
- [#1017](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1017) Adding ClientSideRequestStatistics to gateway calls and making endtime nullable
- [#1038](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1038) Add Selflink to resource properties

### Fixed

- [#921](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/921) Fixed error handling to preserve stack trace in certain scenarios
- [#944](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/944) Change Feed Processor won't use user serializer for internal operations
- [#988](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/988) Fixed query mutating due to retry of gone / name cache is stale.
- [#954](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/954) Support "Start from Beginning" for Change Feed Processor in multi master accounts
- [#999](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/999) Fixed grabbing extra page, updated continuation token on exception path, and non ascii character in order by continuation token.
- [#1013](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1013) Gateway OperationCanceledException are now returned as request timeouts
- [#1020](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1020) Direct package update removes debug statements
- [#1023](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1023) Fixed ThroughputResponse.IsReplacePending header mapping
- [#1036](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1036) Fixed query responses to return null Content if it is a failure
- [#1045](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1045) Added stack trace and innner exception to CosmosException
- [#1050](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1050) Add mocking constructors to TransactionalBatchOperationResult

## <a name="3.4.1"/> [3.4.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.4.1) - 2019-11-06

### Fixed

- [#978](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/978) Fixed mocking for FeedIterator and Response classes

## <a name="3.4.0"/> [3.4.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.4.0) - 2019-11-04

### Added

- [#853](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/853) ORDER BY Arrays and Object support.
- [#877](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/877) Query diagnostics now contains client side request diagnostics information
- [#923](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/923) Bulk Support is now public
- [#922](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/922) Included information of bulk support usage in user agent
- [#934](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/934) Preserved the ordering of projections in a GROUP BY query.
- [#952](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/952) ORDER BY Undefined and Mixed Type ORDER BY support
- [#965](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/965) Batch API is now public

### Fixed
- [#901](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/901) Fixed a bug causing query response to create a new stream for each content call
- [#918](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/918) Fixed serializer being used for Scripts, Permissions, and Conflict related iterators
- [#936](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/936) Fixed bulk requests with large resources to have natural exception

## <a name="3.3.3"/> [3.3.3](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.3.3) - 2019-10-30

- [#837](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/837) Fixed group by bug for non-Windows platforms
- [#927](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/927) Fixed query returning partial results instead of error

## <a name="3.3.2"/> [3.3.2](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.3.2) - 2019-10-16

### Fixed

- [#905](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/909) Fixed linq camel case bug

## <a name="3.3.1"/> [3.3.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.3.1) - 2019-10-11

### Fixed

- [#895](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/895) Fixed user agent bug that caused format exceptions on non-Windows platforms


## <a name="3.3.0"/> [3.3.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.3.0) - 2019-10-09

### Added

- [#801](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/801) Enabled LINQ ThenBy operator after OrderBy
- [#814](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/814) Ability to limit to configured endpoint only
- [#822](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/822) GROUP BY query support.
- [#844](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/844) Added PartitionKeyDefinitionVersion to container builder

### Fixed

- [#835](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/835) Fixed a bug that caused sortedRanges exceptions
- [#846](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/846) Statistics not getting populated correctly on CosmosException.
- [#857](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/857) Fixed reusability of the Bulk support across Container instances
- [#860](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/860) Fixed base user agent string
- [#876](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/876) Default connection timeout reduced from 60s to 10s


## <a name="3.2.0"/> [3.2.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.2.0) - 2019-09-17

### Added

- [#100](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/100) Configurable Tcp settings to CosmosClientOptions
- [#615](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/615), [#775](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/775)  Added request diagnostics to Response's
- [#622](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/622) Added CRUD and query operations for Users and Permissions which enables [ResourceToken](https://docs.microsoft.com/azure/cosmos-db/secure-access-to-data#resource-tokens) support
- [#716](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/716) Added camel case serialization on LINQ query generation
- [#729](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/729), [#776](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/776) Added aggregate(CountAsync/SumAsync etc.) extensions for LINQ query
- [#743](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/743) Added WebProxy to CosmosClientOptions

### Fixed

- [#726](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/726) Query iterator HasMoreResults now returns false if an exception is hit
- [#705](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/705) User agent suffix gets truncated
- [#753](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/753) Reason was not being propagated for Conflict exceptions
- [#756](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/756) Change Feed Processor with WithStartTime would execute the delegate the first time with no items.
- [#761](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/761) CosmosClient deadlocks when using a custom Task Scheduler like Orleans (Thanks to jkonecki)
- [#769](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/769) Session Consistency + Gateway mode session-token bug fix: Under few rare non-success cases session token might be in-correct
- [#772](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/772) Fixed Throughput throwing when custom serializer used or offer doesn't exists
- [#785](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/785) Incorrect key to throw CosmosExceptions with HttpStatusCode.Unauthorized status code


## <a name="3.2.0-preview2"/> [3.2.0-preview2](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.2.0-preview2) - 2019-09-10

- [#585](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/585), [#741](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/741) Bulk execution support
- [#427](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/427) Transactional batch support (Item CRUD)


## <a name="3.2.0-preview"/> [3.2.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.2.0-preview) - 2019-08-09

- [#427](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/427) Transactional batch support (Item CRUD)


## <a name="3.1.1"/> [3.1.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.1.1) - 2019-08-12

### Added

- [#650](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/650) CosmosSerializerOptions to customize serialization

### Fixed

- [#612](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/612) Bug fix for ReadFeed with partition-key
- [#614](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/614) Fixed SpatialPath serialization and compatibility with older index versions
- [#619](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/619) Fixed PInvokeStackImbalance exception for .NET Framework
- [#626](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/626) FeedResponse<T> status code now return OK for success instead of the invalid status code 0 or Accepted
- [#629](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/629) Fixed CreateContainerIfNotExistsAsync validation to limited to partitionKeyPath only
- [#630](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/630) Fixed User Agent to contain environment and package information


## <a name="3.1.0"/> 3.1.0 - 2019-07-29 - Unlisted

### Added

- [#541](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/541) Added consistency level to client and query options
- [#544](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/544) Added continuation token support for LINQ
- [#557](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/557) Added trigger options to item request options
- [#572](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/572) Added partition key validation on CreateContainerIfNotExistsAsync
- [#581](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/581) Added LINQ to QueryDefinition API
- [#592](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/592) Added CreateIfNotExistsAsync to container builder
- [#597](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/597) Added continuation token property to ResponseMessage
- [#604](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/604) Added LINQ ToStreamIterator extension method

### Fixed

- [#548](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/548) Fixed mis-typed message in CosmosException.ToString();
- [#558](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/558) LocationCache ConcurrentDict lock contention fix
- [#561](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/561) GetItemLinqQueryable now works with null query
- [#567](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/567) Query correctly handles different language cultures
- [#574](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/574) Fixed empty error message if query parsing fails from unexpected exception
- [#576](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/576) Query correctly serializes the input into a stream


## <a name="3.0.0"/> [3.0.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.0.0) - 2019-07-15

- General availability of [Version 3.0.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/) of the .NET SDK
- Targets .NET Standard 2.0, which supports .NET framework 4.6.1+ and .NET Core 2.0+
- New object model, with top-level CosmosClient and methods split across relevant Database and Container classes
- New highly performant stream APIs
- Built-in support for Change Feed processor APIs
- Fluent builder APIs for CosmosClient, Container, and Change Feed processor
- Idiomatic throughput management APIs
- Granular RequestOptions and ResponseTypes for database, container, item, query and throughput requests
- Ability to scale non-partitioned containers
- Extensible and customizable serializer
- Extensible request pipeline with support for custom handlers

## Release & Retirement dates
Microsoft provides notification at least **12 months** in advance of retiring an SDK in order to smooth the transition to a newer/supported version.

New features and functionality and optimizations are only added to the current SDK, as such it is recommended that you always upgrade to the latest SDK version as early as possible.

Any requests to Azure Cosmos DB using a retired SDK are rejected by the service.

<br/>

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

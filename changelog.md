# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0] - 2019-07-15

### Added

- General availability of [Version 3.0.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/) of the .NET SDK
- StandByFeedIterator with no StartTime (#503)
- Making LINQ extension classes public (#507)

### Changed

- Stored procedure updated to take dynamic[] for input (#515)
- The non-stream APIs will throw 404 NotFound exceptions (#519)

### Fixed

- Remove GC.SuppressFinalize from response message (#512)
- Query fix to fully drain all items when it contains empty pages (#500)
- Query fix to return 429 responses. Bug caused argument exceptions (#520)

## [3.0.0-rc1] - 2019-07-01

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

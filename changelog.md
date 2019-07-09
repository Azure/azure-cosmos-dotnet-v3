# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

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

### Added

- Initial release candidate

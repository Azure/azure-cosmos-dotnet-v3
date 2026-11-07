# Change feed query filtering (preview)

Preview builds expose `Container.GetChangeFeedIteratorWithQuery<T>`,
`Container.GetChangeFeedStreamIteratorWithQuery`, and
`ChangeFeedProcessorBuilder.WithQuery`. They use a SQL predicate to filter
changes by their current document image.

Use `SELECT * FROM c` with an optional `WHERE` predicate. Results retain the
existing change feed format; they are not projected query results. Parameters,
projections, `SELECT VALUE`, `TOP`, `DISTINCT`, alternate root aliases, joins,
subqueries, ordering, grouping, aggregations, and `OFFSET LIMIT` are not part
of the supported contract. Computed-property and UDF predicates require
corresponding service support.

Filtering scans candidate changes without using the index. It can increase
latency and request charges and does not reduce the physical range scanned.
Partition-key and effective-partition-key feed ranges still constrain that
scan. Both latest-version and all-versions-and-deletes modes retain their
existing start-position requirements and routing; all-versions-and-deletes
retains Gateway routing and requires the appropriate retention policy.

All four arguments are required and must be non-null. Empty or whitespace-only
SQL is rejected. Supply `new ChangeFeedRequestOptions()` to use default options.
For unfiltered GET reads, use the existing `GetChangeFeedIterator<T>` or
`GetChangeFeedStreamIterator` methods instead.

Nonblank SQL is preserved exactly and sent as a POST query payload using the
SDK's internal serializer, not a customer document serializer.

## Reading and resuming

An empty successful page can advance the continuation. Continue draining until
`NotModified`, not merely until a page contains no documents. The iterator
remains pollable after `NotModified`; `HasMoreResults` alone is not a signal
that another batch is currently available.

```csharp
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

public static class FilteredChangeFeed
{
    public static async Task<string> DrainAsync(
        Container container,
        string continuation,
        CancellationToken cancellationToken)
    {
        ChangeFeedStartFrom start = continuation == null
            ? ChangeFeedStartFrom.Beginning()
            : ChangeFeedStartFrom.ContinuationToken(continuation);

        using FeedIterator<Item> iterator =
            container.GetChangeFeedIteratorWithQuery<Item>(
                start,
                ChangeFeedMode.LatestVersion,
                "SELECT * FROM c WHERE c.state = \"WA\"",
                new ChangeFeedRequestOptions());

        while (iterator.HasMoreResults)
        {
            FeedResponse<Item> response =
                await iterator.ReadNextAsync(cancellationToken);
            continuation = response.ContinuationToken;

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                break;
            }

            foreach (Item item in response)
            {
                Console.WriteLine(item.id);
            }
        }

        return continuation;
    }

    public sealed class Item
    {
        public string id { get; set; }
        public string state { get; set; }
    }
}
```

The stream API accepts the same arguments and returns `ResponseMessage`.
Dispose each response after consuming its content. Service validation failures
remain error responses for stream reads and `CosmosException` for typed reads;
the SDK does not convert them into empty successful pages.

Continuation tokens represent physical progress, not the SQL definition.
Resuming with different SQL is allowed, but earlier changes are not replayed.
Only current-image evaluation is exposed. A nonmatching current image does not
notify consumers that a previously matching document stopped matching.

## Processor lease compatibility

In a preview build, call `WithQuery(string)` on a change feed processor builder
before `Build()`. It applies to typed and stream callbacks, automatic and manual
checkpointing, and both change feed modes. Null, empty, and whitespace-only
SQL are rejected. The processor implementation binds leases to the exact SQL text;
even whitespace changes represent a different query identity. Legacy leases
without query metadata remain compatible with unfiltered processors.

Startup checks every existing lease before starting partition workers,
including restored in-memory leases and already-initialized stores. During
initialization, it checks again after creating missing leases so that leases
created or skipped because of another host cannot bypass validation. Lease
operations also enforce compatibility if the store changes after the startup
check; the startup snapshot itself is not a transaction across all leases.

An incompatible query requires a separate processor name, lease prefix, or
lease container. Stopping and restarting a processor does not allow it to
change its configured query identity or reuse incompatible saved leases.

If renewal detects an incompatible query identity after startup, the affected
partition worker stops with lease loss and reports the incompatibility through
`WithErrorNotification`. This does not change recovery for transient renewal
failures or make live query changes supported.

Do not share a filtered namespace with SDK versions that do not support this
feature. Such hosts cannot enforce query identity and can discard the fingerprint
when replacing a lease document. Upgrade all participants before creating a
dedicated filtered namespace. A processor name/prefix prevents accidental
sharing, not unauthorized access; use a separate lease container and access
restrictions when exclusion must be enforced. Do not downgrade against filtered
lease state or remove its metadata to bypass compatibility checks.

The preview fingerprint is Base64-encoded SHA-256 of exact UTF-8 SQL.
Cross-SDK agreement on this persisted format remains a preview-release gate.

For example, build a processor for a dedicated filtered namespace:

```csharp
using System;
using Microsoft.Azure.Cosmos;

public static class FilteredProcessor
{
    public static ChangeFeedProcessor Build<T>(
        Container source,
        Container leases,
        string instanceName,
        Container.ChangeFeedHandler<T> onChanges,
        Container.ChangeFeedMonitorErrorDelegate onError)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (leases == null) throw new ArgumentNullException(nameof(leases));
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentException("An instance name is required.", nameof(instanceName));
        if (onChanges == null) throw new ArgumentNullException(nameof(onChanges));
        if (onError == null) throw new ArgumentNullException(nameof(onError));

        return source.GetChangeFeedProcessorBuilder<T>("wa-filter-v1", onChanges)
            .WithQuery("SELECT * FROM c WHERE c.state = \"WA\"")
            .WithInstanceName(instanceName)
            .WithLeaseContainer(leases)
            .WithErrorNotification(onError)
            .Build();
    }
}
```

Call `StartAsync()` on the returned processor and always await `StopAsync()`
when shutting it down. Use a unique instance name for each participating host.
To change the predicate, choose a new processor name/namespace and explicitly
choose its start position; existing filtered progress is not automatically
migrated. This sample retains the default start-from-now behavior.

## Empty pages, checkpoints, and diagnostics

A successful filtered page can contain no matches while advancing the physical
continuation. Keep reading successful pages; an empty page is not end-of-feed.
HTTP 304 ends the current drain, and later polling resumes from its position.
Failed or canceled reads do not adopt an error response's ETag.

Automatic processor checkpointing advances after successful pages, including
empty pages. Typed callbacks skip empty batches; stream callbacks can receive
them. Manual checkpointing does not automatically acknowledge an earlier batch
when later pages are empty. Calling a later checkpoint explicitly acknowledges
all physical progress up to that position. HTTP 304 advances the live iterator,
but does not by itself persist a processor checkpoint.

Existing status, substatus, activity ID, request charge, and timing diagnostics
remain available. No raw SQL, query fingerprint, or document body is added to
diagnostic traces by this feature. The SDK does not expose backend evaluation
counts or evaluator latency, and returned document counts must not be used to
infer them. The estimator reports physical lag, not the number of matching
changes. An empty result can still cost RUs for scanning nonmatching changes.

## Service and emulator requirements

Processor read failures retain the existing recovery behavior. Register
`WithErrorNotification` to receive the service exception. A failed partition
worker can release its lease and be acquired again, so invalid immutable SQL
can produce repeated errors; HTTP 400 does not automatically stop the whole
processor. Stop the processor when intervention is needed. A restart retries
the same configuration and does not permit changing a query-bound namespace.

The SDK does not parse SQL or compensate for service contract differences.
Preview release remains gated on correct service validation of unsupported
shapes and computed-property/UDF support. An emulator accepting or ignoring
unsupported clauses does not make those clauses supported.

The emulator tests use the existing preview setup in
`templates\emulator-setup.yml`, including `EMULATORMSIURL`, `/EnablePreview`,
and its existing previous-delete-image override. No additional emulator flags
or CI changes are required for these pull-model and processor tests. All tests
use the existing test client's emulator configuration and create/delete their
own databases; they do not require new environment variables.

## Acceptance evidence and remaining release gates

The following distinguishes .NET implementation coverage from service release
acceptance for [issue #6145](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/6145).
Unit tests live in `Microsoft.Azure.Cosmos.Tests`; emulator tests below are in
`QueryFilteringTests` in `Microsoft.Azure.Cosmos.EmulatorTests`. Passing an
emulator case does not certify every cloud storage engine or transport.

| Requirement | Named local evidence | Remaining release evidence |
| --- | --- | --- |
| POST body and required mode/range headers | `QueryReadsPreserveProtocolAndOwnEachPayload`, `QueryRetainsStartPositionHeaders` | HTTP/RNTBD/Gateway service parity, including the issue's `HeadersValidationTests`. |
| Change-feed-specific SQL validation | `ServiceErrorsAreNotConvertedToEmptyPages` covers invalid syntax and JOIN | Backend `ChangeFeedQueryValidator` / `FilterQueryValidator` acceptance. No SDK parser is introduced. |
| Supported predicates | `SupportedPredicateCategoriesFilterChanges`: select-all, equality, range, array expressions, `IS_DEFINED`, no matches | Computed properties, UDF behavior/limits, and complete supported-function/operator matrix. |
| PK and EPK composition | `PullFiltersBothModesAndComposesWithPartitionKey`, `QueryComposesWithEffectivePartitionRanges`, protocol PK/EPK tests | Atomic/multi-LSN backend tests and live physical-range splits. |
| No-query compatibility | `ExistingApisRetainUnfilteredReads`, `MissingOrEmptyInternalQueryRetainsGet`, `WithoutQueryRetainsUnfilteredConfiguration` | Backend feature-disabled parity. Public query APIs deliberately reject blank SQL; use existing APIs for GET. |
| Malformed/unsupported request errors | Invalid syntax/JOIN emulator cases; `QueryApisValidateRequiredArguments`, `QueryApisRejectBlankText` | Malformed JSON, unknown properties, empty body, invalid mode/option combinations, and exact substatus/error contracts are service-owned. |
| Unsupported SQL categories | JOIN and invalid syntax surface as 400 in `ServiceErrorsAreNotConvertedToEmptyPages` | Separate rejection cases for projections/SELECT VALUE, TOP, DISTINCT, alternate root/source, subqueries, ORDER BY, GROUP BY, OFFSET LIMIT, SKIP/TAKE, aggregates, parameters, and multi-document evaluation. Some emulator shapes have been accepted/ignored; this is a release blocker, not SDK support. |
| Synchronous/asynchronous backend reads | SDK async pull and processor emulator cases | `ChangeFeedContextWithAsyncReadTest` parity with native synchronous reads. |
| Operation Log / Log Store / Local Store / HybridRow | Not selectable or certified by these SDK tests | All named `DocumentResourceManagerTest` and `DocumentResourceManagerTestWithLogStore` combinations in the issue, including switch points and disabled support. |
| Latest version and all versions/deletes | `PullFiltersBothModesAndComposesWithPartitionKey`, `QueryReadsPreserveProtocolAndOwnEachPayload`, `AllContainerBuilderShapesAcceptQuery`; `AllVersionsAndDeletesStartsAndRestartsWithFreshInMemoryLeases` verifies filtered/unfiltered public-builder startup and restart through the local request pipeline; `StartAsyncRejectsExistingLeaseInDifferentMode` preserves mode-mismatch rejection | Native full-fidelity storage parity and full-fidelity processor delivery/restart acceptance. Local lifecycle coverage is not backend full-fidelity processor E2E. |
| Empty pages and continuation progress | `EmptyPagesAdvanceReadsWithoutAcknowledgingManualBatches`, `FailedReadRetainsContinuation`, `CancellationDisposesPayloadWithoutAdvancing` | Backend evaluator loop/cancellation limits. |
| Response-size preemption/order | `LargeFilteredResponsesDrainWithoutLoss` drains a 6 MB single-partition backlog, with a large page-size hint, in multiple pages without loss, duplication, or reordering | Native `TestReadFeedIfNoneMatchWithQueryPreemptionByResponseSize`, including nonmatching records and both image modes. |
| Pull query changes | `PullContinuationAllowsChangingQueryWithoutReplayingEarlierChanges` | Cloud parity with existing continuation formats. |
| Processor identity/lifecycle | `ProcessorFiltersCheckpointsAndRestarts` covers typed/stream, auto/manual checkpoints, persisted identity, new compatible host and incompatible startup; `ReadErrorsNotifyAndRecoverAcrossAcquisitionCycles` covers 400/429/503 and restart; `ChangedQueryDuringRenewalStopsWorkerAndNotifiesWithoutCheckpoint` covers changed stored identity with the same or a new owner; `TransientRenewalFailureDoesNotCancelWorker` preserves filtered/unfiltered renewal recovery | Cross-SDK fingerprint agreement. `PreFeatureLeaseSchemaDropsQueryIdentityOnReplacement` models the verified old serializer schema, not a complete old-SDK process test; mixed-version filtered cohosting is unsupported. |
| Concurrent startup and range changes | `ValidatesRacingLeasesBeforeStartingWorkers`, `BootstrapRechecksLeasesSkippedDuringCreation`, `PartitionChangesPreserveConfiguredQuery`, `IncompatibleHostCannotDeliverFromActiveQueryNamespace` | Native concurrent split/merge and mixed-language deployment acceptance. |
| Current/previous/missing images | Current-image create filtering in emulator pull/processor cases; request payload always sets previous-image option false | Create with no previous image, update matching/nonmatching transitions, delete/TTL with no current image, and internal either-image match flags need explicit backend semantics and `FilterEvaluatorTest` cases. No public previous-image option is added. |
| Existing ordinary queries | `OrdinaryParameterizedPostQueriesRemainIndependent` verifies parameterized SELECT VALUE queries, no A-IM header, and correct results in Gateway and Direct configurations | Full backend non-change-feed query regression suite. |
| Diagnostics and performance | `QueryReadDiagnosticsDoNotContainSqlHashOrDocumentBody`; existing RU/activity/timing data preserved | Metrics/measurement gates below; no claim that returned counts equal evaluated counts. |
| SDK integration and customer guidance | All three preview APIs, generated preview API contract, stable API/telemetry checks, examples and limitations in this document | Other SDK implementations and cross-SDK lease-format decisions; no publication or cross-repository signoff is implied. |

### Observability ownership and performance acceptance

The SDK can observe response status/substatus, activity ID, RU charge, elapsed
read time, returned documents, and empty successful pages. Applications can
count filtered requests because they own the configuration. This change adds
no new metric schema or SQL/shape logging. Dedicated filtered-request and
empty-page metric signals, and integration with `SQLQueryExecMetrics`, remain
an explicit product/telemetry follow-up.

The backend owns validation success/failure categories, unsupported-shape
counts, documents evaluated, evaluator CPU/latency, query execution failures,
and internal current/previous-image match counts. These must be measured at
the evaluator; they cannot be reconstructed from response counts or SDK time.

Before release, run identical fixed backlogs with an unfiltered baseline and
no-match (0%), sparse-match (1%), and all-match (100%) predicates. Use at least
10,000 candidate changes per run, both small and near-limit documents,
single/multiple ranges, both modes, and pull/processor consumers. Repeat warm
and cold runs with the same account capacity, region, consistency, data, and
page-size hints; record service/SDK versions and the workload seed.

Record total drain time, per-page p50/p95/p99 latency, RUs, returned bytes and
documents, empty-page count, allocations/CPU on the client, cancellation
latency, and backend evaluator counts/CPU/latency. Zero-match cost is measured
per fixed backlog, not per returned document. Do not mix emulator timings
with cloud release measurements.

Functional acceptance requires exact expected results and per-partition
ordering, no lost/duplicated progress when resuming, bounded completion of a
fixed backlog, preserved manual acknowledgment boundaries, and effective
cancellation/timeouts. Service/product owners must approve numeric budgets
for latency, RU, CPU, allocation and cancellation overhead before collecting
release results. Those budgets and measurements are not yet available:
performance acceptance is blocked, not implicitly passed by local tests.

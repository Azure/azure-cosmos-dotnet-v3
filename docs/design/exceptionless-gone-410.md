# Exceptionless GoneException (410) for Direct/TCP Mode — Deep Research & Implementation Plan

## Executive Summary

In the Azure Cosmos DB .NET SDK's Direct/TCP connectivity mode, **GoneException (HTTP 410)** with various sub-status codes is thrown as a CLR exception across multiple layers of the call stack. These exceptions are caught and re-thrown repeatedly through the `StoreReader`, `ConsistencyReader/Writer`, `ReplicatedResourceClient`, and `GoneAndRetryWithRetryPolicy` stack. Since 410 responses are **expected operational signals** (partition splits, server failovers, stale caches, transport errors) — not true application errors — the high volume of exception creation, throwing, catching, and stack trace capture imposes significant CPU overhead. The codebase already has a proven "exceptionless" pattern (used for 404/1002, 409, 412, 403, 429, and 400) that returns status codes in `StoreResponse` instead of throwing. This report proposes extending that pattern to GoneException (410) with all sub-status codes, using a new `UseStatusCodeForGone` flight flag on `DocumentServiceRequest`, enabling safe side-by-side validation.

---

## Table of Contents

1. [Problem Analysis: Why GoneExceptions Cause High CPU](#1-problem-analysis)
2. [Current Exception Flow Architecture](#2-current-exception-flow)
3. [Sources of GoneException (410) by Sub-Status](#3-sources-of-goneexception)
4. [Existing Exceptionless Pattern (Prior Art)](#4-existing-exceptionless-pattern)
5. [Proposed Solution: Exceptionless 410](#5-proposed-solution)
6. [Detailed Implementation Plan](#6-detailed-implementation-plan)
7. [Side-by-Side Flighting Strategy](#7-side-by-side-flighting)
8. [Validation Plan](#8-validation-plan)
9. [Risk Assessment](#9-risk-assessment)
10. [Confidence Assessment](#10-confidence-assessment)
11. [Footnotes](#11-footnotes)

---

## 1. Problem Analysis: Why GoneExceptions Cause High CPU {#1-problem-analysis}

### The Cost of .NET Exceptions

Each `throw` of a .NET exception triggers:
- **Stack trace capture** — walks the entire call stack, resolves symbols (~10-50μs per throw depending on stack depth)
- **Object allocation** — `GoneException` extends `DocumentClientException` → `Exception`, includes string formatting, headers, inner exceptions
- **Exception dispatch** — CLR exception dispatch tables, filter evaluation, stack unwinding
- **First-chance exception handlers** — debuggers, ETW listeners, and diagnostics infrastructure all fire

### The Scale Problem

In Direct/TCP mode, a single user-level operation can produce **multiple GoneExceptions**:

1. **Transport layer** (`rntbd2/TransportClient.cs`): Each failed connection to a replica throws `GoneException`[^1]
2. **Backend response** (`TransportClient.ThrowServerException`): Server-returned 410 status creates exception[^2]
3. **Timeout** (`TimeoutHelper.ThrowGoneIfElapsed`): Each elapsed timeout checkpoint throws `GoneException`[^3]
4. **StoreReader** (`ReadMultipleReplicasInternalAsync`): Aggregates exceptions then re-throws[^4]
5. **ConsistencyReader/Writer**: Catches and re-throws or wraps[^5]
6. **GoneAndRetryWithRetryPolicy**: Catches, decides retry, then the cycle repeats for each retry attempt[^6]

For a typical read operation touching 4 replicas where 3 return Gone, with 3 retry attempts over 30 seconds, this can produce **9-12 GoneException objects** — each with full stack trace capture. Under high partition-split or failover scenarios, this multiplies across all concurrent requests.

### Why 410 Is Unique

Unlike 404 or 429 which typically happen once per request, **410 is the most retry-intensive status code**:
- The `GoneAndRetryWithRetryPolicy` retries for up to **30 seconds** (60 for Strong consistency)[^7]
- Each retry iteration contacts multiple replicas, each potentially returning 410
- Transport failures (connection reset, channel closed) are all converted to 410[^8]

---

## 2. Current Exception Flow Architecture {#2-current-exception-flow}

```
                                        User Application
                                              │
                                    ┌─────────▼──────────┐
                                    │    CosmosClient     │
                                    │  (RequestMessage)   │
                                    │ UseStatusCodeFor*   │ ◄── flags set here
                                    └─────────┬──────────┘
                                              │
                                    ┌─────────▼──────────┐
                                    │    StoreClient      │
                                    │ ProcessMessageAsync │
                                    │ BackoffRetryUtility │ ◄── catches GoneException from lower layers
                                    └─────────┬──────────┘
                                              │
                              ┌───────────────▼────────────────┐
                              │  ReplicatedResourceClient      │
                              │  InvokeAsync                   │
                              │  RequestRetryUtility +         │
                              │  GoneAndRetryWithRequestRetry  │ ◄── MAIN RETRY LOOP
                              │  Policy<StoreResponse>         │     catches GoneException, decides retry
                              └───────────────┬────────────────┘
                                              │
                         ┌────────────────────┼────────────────────┐
                         │                    │                    │
              ┌──────────▼─────────┐ ┌───────▼────────┐ ┌────────▼────────┐
              │ ConsistencyReader  │ │ConsistencyWriter│ │  QuorumReader   │
              │ ReadAsync          │ │ WriteAsync      │ │ ReadStrongAsync │
              └──────────┬─────────┘ └───────┬────────┘ └────────┬────────┘
                         │                   │                   │
                    ┌────▼─────────────────────────────────────────┐
                    │              StoreReader                      │
                    │  ReadMultipleReplicasInternalAsync            │
                    │  ReadPrimaryInternalAsync                     │
                    │  ┌──────────────────────────────────┐        │
                    │  │ Per-replica: catches Exception,   │        │
                    │  │ wraps in StoreResult with         │        │
                    │  │ Exception property                │ ◄── Exception stored, not thrown yet
                    │  └──────────────────────────────────┘        │
                    │  Then: throw new GoneException(...)           │ ◄── RE-THROWS at end
                    └──────────────────────────────────────────────┘
                         │
              ┌──────────▼───────────┐
              │ TransportClient      │
              │ ThrowServerException │ ◄── ORIGIN: creates & throws GoneException
              └──────────┬───────────┘
                         │
              ┌──────────▼───────────┐
              │ rntbd2/TransportClient│
              │ InvokeStoreAsync      │ ◄── ORIGIN: TransportException → GoneException
              └──────────────────────┘
```

### Key Throw Points

| Location | File:Line | Sub-Status | When |
|----------|-----------|------------|------|
| RNTBD transport failure (read) | `rntbd2/TransportClient.cs:199-204` | `TransportGenerated410` | Connection failure on read request[^1] |
| RNTBD transport failure (write, not sent) | `rntbd2/TransportClient.cs:209-214` | `TransportGenerated410` | Connection failure, write not delivered[^1] |
| Server response 410 | `TransportClient.cs:911-999` | `ServerGenerated410` + many others | Backend returns HTTP 410[^2] |
| Timeout elapsed | `TimeoutHelper.cs:46-51` | `TimeoutGenerated410` | Retry budget exhausted[^3] |
| No valid store responses | `StoreReader.cs:139-144` | `Server_NoValidStoreResponse` | All replicas failed[^9] |
| Gone during read-all | `StoreReader.cs:382` | From exception | Quorum not met, has Gone[^4] |
| Primary gone | `StoreReader.cs:457` | From StoreResult | Primary returned 410[^10] |
| ReadAny no responses | `ConsistencyReader.cs:293` | `Server_NoValidStoreResponse` | Eventual read failed[^5] |
| Write barrier not met | `ConsistencyWriter.cs:432` | `ServerGenerated410` | Strong write global barrier[^11] |
| Global strong barrier | `ConsistencyWriter.cs:478` | `Server_GlobalStrongWriteBarrierNotMet` | Strong consistency barrier[^11] |
| N-Region barrier | `ConsistencyWriter.cs:483` | `Server_NRegionCommitWriteBarrierNotMet` | Multi-region write barrier[^11] |

---

## 3. Sources of GoneException (410) by Sub-Status {#3-sources-of-goneexception}

| SubStatusCode | Constant | Meaning | Origin Layer |
|---------------|----------|---------|-------------|
| 0 / Unknown | `SubStatusCodes.Unknown` | Generic 410 | Various |
| (SDK-generated) | `SubStatusCodes.ServerGenerated410` | Server returned 410 with no sub-status | `TransportClient.ThrowServerException`[^2] |
| (SDK-generated) | `SubStatusCodes.TransportGenerated410` | RNTBD transport failure converted to 410 | `rntbd2/TransportClient`, `TransportExceptions`[^1][^8] |
| (SDK-generated) | `SubStatusCodes.TimeoutGenerated410` | Internal timeout converted to 410 | `TimeoutHelper.ThrowGoneIfElapsed`[^3] |
| 1002 | `SubStatusCodes.NameCacheIsStale` | Collection cache stale → `InvalidPartitionException` | `TransportClient.ThrowServerException`[^2] |
| 1002 | `SubStatusCodes.PartitionKeyRangeGone` | PKRange split → `PartitionKeyRangeGoneException` | `TransportClient.ThrowServerException`[^2] |
| 1002 | `SubStatusCodes.CompletingSplit` | Split in progress → `PartitionKeyRangeIsSplittingException` | `TransportClient.ThrowServerException`[^2] |
| 1003 | `SubStatusCodes.CompletingPartitionMigration` | Migration → `PartitionIsMigratingException` | `TransportClient.ThrowServerException`[^2] |
| (custom) | `SubStatusCodes.LeaseNotFound` | Lease gone → `LeaseNotFoundException` | `TransportClient.ThrowServerException`[^2] |
| (custom) | `SubStatusCodes.ArchivalPartitionNotPresent` | Archive partition gone | `TransportClient.ThrowServerException`[^2] |
| (custom) | `SubStatusCodes.Server_NoValidStoreResponse` | All replicas gave invalid responses | `StoreReader.cs`[^9] |
| (custom) | `SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet` | Strong write barrier failed | `ConsistencyWriter.cs`[^11] |
| (custom) | `SubStatusCodes.Server_NRegionCommitWriteBarrierNotMet` | N-region barrier failed | `ConsistencyWriter.cs`[^11] |

---

## 4. Existing Exceptionless Pattern (Prior Art) {#4-existing-exceptionless-pattern}

The SDK already implements the "exceptionless" pattern for several status codes. This is the **proven blueprint** for the GoneException change.

### How It Works

1. **Flag on `DocumentServiceRequest`**: A boolean property (e.g., `UseStatusCodeForFailures`, `UseStatusCodeFor4041002`, `UseStatusCodeFor429`) signals that specific status codes should NOT throw exceptions[^12]

2. **`TransportClient.ThrowServerException` early-return**: If `request.IsValidStatusCodeForExceptionlessRetry(statusCode, subStatusCode)` returns true, the method returns without throwing — the `StoreResponse` flows back with the error status code intact[^13]

3. **`StoreResult.CreateStoreResult` handles both paths**: When a `StoreResponse` has status code 410 and no exception, `StoreResult` already stores `StatusCode` and `SubStatusCode` from the response headers[^14]

4. **`GoneAndRetryWithRequestRetryPolicy.TryHandleResponseSynchronously`** already supports evaluating `TResponse response` (not just `Exception`). The `IsBaseGone`, `IsPartitionIsMigrating`, etc. helper methods all check **both** `response` and `exception`[^15]:
   ```csharp
   private static bool IsBaseGone(TResponse response, Exception exception)
   {
       return exception is GoneException 
           || (response?.StatusCode == HttpStatusCode.Gone &&
              (response?.SubStatusCode == SubStatusCodes.Unknown 
               || (response != null && response.SubStatusCode.IsSDKGeneratedSubStatus())));
   }
   ```

5. **`RequestRetryUtility.ProcessRequestAsync`** drives the retry loop using `IRequestRetryPolicy.TryHandleResponseSynchronously(request, response, exception, out shouldRetryResult)` — it passes the **response** when no exception occurs[^16]

### What's Already Exceptionless

| Status Code | Flag | Since |
|-------------|------|-------|
| 404 (not 1002), 409, 412 | `UseStatusCodeForFailures` | Early[^12] |
| 404/1002 | `UseStatusCodeFor4041002` | Later, scoped to non-master[^17] |
| 403 | `UseStatusCodeFor403` | Incremental[^12] |
| 429 | `UseStatusCodeFor429` | Incremental[^12] |
| 400 (not PartitionKeyMismatch) | `UseStatusCodeForBadRequest` | Incremental[^12] |

### What's NOT Exceptionless Yet

**410 (Gone) — all sub-status codes.** This is the one that hurts the most because of its retry-intensive nature.

---

## 5. Proposed Solution: Exceptionless 410 {#5-proposed-solution}

### Core Principle

Instead of throwing `GoneException` (and its sub-types like `InvalidPartitionException`, `PartitionKeyRangeGoneException`, etc.), return the `StoreResponse` with status code 410 and appropriate sub-status headers. The existing retry policies already understand how to evaluate responses without exceptions.

### Architecture Changes

```
BEFORE (Exception-based):
  TransportClient.ThrowServerException → throw GoneException 
    → caught by StoreReader → StoreResult.Exception = ex 
    → re-throw GoneException 
    → caught by GoneAndRetryWithRequestRetryPolicy via catch block

AFTER (Exceptionless):
  TransportClient.ThrowServerException → return (status=410 in StoreResponse)
    → StoreReader sees storeResponse with Status=410 
    → StoreResult has StatusCode=410, SubStatusCode=X, Exception=null, IsValid depends on context
    → GoneAndRetryWithRequestRetryPolicy.TryHandleResponseSynchronously evaluates response
    → retry decision made WITHOUT exception overhead
```

---

## 6. Detailed Implementation Plan {#6-detailed-implementation-plan}

### Phase 1: Add Flight Flag

**File: `direct/DocumentServiceRequest.cs`**

Add a new flag:
```csharp
/// <summary>
/// When true, GoneException (410) status codes are returned as StoreResponse
/// status codes rather than thrown as exceptions.
/// </summary>
public bool UseStatusCodeForGone { get; set; }
```

Add a static default (same pattern as `DefaultUseStatusCodeFor4041002`):
```csharp
internal static bool DefaultUseStatusCodeForGone = false;
```

Update `IsAnyExceptionLessEnabled()` to include the new flag[^18].

Update `Clone()` to copy the new flag[^19].

### Phase 2: Update `IsValidStatusCodeForExceptionlessRetry`

**File: `direct/DocumentServiceRequestExtensions.cs`**

Add a check for 410:
```csharp
// 410 (Gone) with any sub-status
if (request.UseStatusCodeForGone
    && statusCode == (int)System.Net.HttpStatusCode.Gone)
{
    return true;
}
```

This is the key gate: when the flag is `true`, `TransportClient.ThrowServerException` will `return` instead of `throw` for ALL 410 sub-statuses[^13].

### Phase 3: Update `TransportClient.ThrowServerException`

**File: `direct/TransportClient.cs`**

The existing early-return check at line 851 already calls `IsValidStatusCodeForExceptionlessRetry`[^13]. Once Phase 2 is done, 410 will be returned without throwing when the flag is set. **No change needed here** — the generic mechanism handles it.

However, the 410 `case` block currently creates different exception types based on sub-status (lines 911-999). When exceptionless, we need to ensure the `StoreResponse` preserves the sub-status code in headers. This is already the case since `ThrowServerException` reads sub-status from `storeResponse` headers — the response flows back as-is.

### Phase 4: Update `rntbd2/TransportClient` (Transport Layer)

**File: `direct/rntbd2/TransportClient.cs`**

This is the most complex change. The RNTBD transport client converts `TransportException` to `GoneException` on catch[^1]. For the exceptionless path, instead of throwing, we need to construct a synthetic `StoreResponse` with:
- `Status = 410`
- Sub-status header = `TransportGenerated410`
- Relevant transport diagnostics headers

```csharp
if (request.IsReadOnlyRequest)
{
    if (request.UseStatusCodeForGone)
    {
        // Return a synthetic StoreResponse with 410 status
        storeResponse = CreateGoneStoreResponse(
            SubStatusCodes.TransportGenerated410, 
            activityId, 
            transportRequestStats);
        // Fall through to return storeResponse
    }
    else
    {
        GoneException goneException = TransportExceptions.GetGoneException(...);
        throw goneException;
    }
}
```

A helper method `CreateGoneStoreResponse` constructs a `StoreResponse` object with the appropriate status code, sub-status, and diagnostics headers.

### Phase 5: Update `TimeoutHelper.ThrowGoneIfElapsed`

**File: `direct/TimeoutHelper.cs`**

This method is called from many places (`StoreReader`, `ConsistencyReader`)[^3]. It currently throws `GoneException`. For the exceptionless path, we have two options:

**Option A (Recommended)**: Change the callers rather than `TimeoutHelper` itself. The callers (e.g., `StoreReader.ReadMultipleReplicasInternalAsync`) should check `TimeoutHelper.IsElapsed()` and handle the timeout via status code path.

**Option B**: Add a `TryThrowGoneIfElapsed` that returns a bool, and callers check the return value.

```csharp
// New method
public bool IsGoneElapsed()
{
    return this.isElapsed || this.IsElapsed();
}
```

Callers then:
```csharp
if (entity.RequestContext.TimeoutHelper.IsGoneElapsed())
{
    if (entity.UseStatusCodeForGone)
    {
        return CreateTimeoutGoneResult(SubStatusCodes.TimeoutGenerated410);
    }
    entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();
}
```

### Phase 6: Update `StoreReader`

**File: `direct/StoreReader.cs`**

#### 6a. `ReadMultipleReplicasInternalAsync`

The key change is in how GoneException from replicas are handled:

**Current (line 324-351)**: `StoreResult.Exception != null` triggers `VerifyCanContinueOnException`. For exceptionless 410, the `StoreResult.Exception` will be `null`, but `StoreResult.StatusCode == StatusCodes.Gone`[^4].

The existing line at 351 already handles the exceptionless path:
```csharp
hasGoneException |= storeResult.StatusCode == StatusCodes.Gone 
    && storeResult.SubStatusCode != SubStatusCodes.NameCacheIsStale;
```

**But** the `StoreResult.VerifyCanContinueOnException` call at line 326 needs updating — it currently only checks for `PartitionKeyRangeGoneException`, `PartitionKeyRangeIsSplittingException`, `PartitionIsMigratingException`[^20]. For the exceptionless path, equivalent status code checks are needed:

```csharp
// Exceptionless path for VerifyCanContinueOnException equivalent
if (storeResult.Exception == null 
    && storeResult.StatusCode == StatusCodes.Gone)
{
    // For split/migration sub-statuses, propagate as exception (they need cache refresh)
    if (storeResult.SubStatusCode == SubStatusCodes.PartitionKeyRangeGone
        || storeResult.SubStatusCode == SubStatusCodes.CompletingSplit
        || storeResult.SubStatusCode == SubStatusCodes.CompletingPartitionMigration)
    {
        // These need to bubble up to trigger cache refreshes
        // Convert to exception only at this point
        throw CreateGoneExceptionFromStoreResult(storeResult);
    }
}
```

**However**, a better approach is to NOT throw at all — instead, let the `GoneAndRetryWithRequestRetryPolicy` handle these sub-statuses from the response, since it already has `IsPartitionIsMigrating(response, exception)`, `IsPartitionKeySplitting(response, exception)`, etc.[^15] that check the **response** path.

#### 6b. `ReadPrimaryInternalAsync` (line 450-461)

Currently throws `GoneException(RMResources.Gone, storeResult.Target.SubStatusCode)` when primary returns 410[^10]. For exceptionless:

```csharp
if (storeResult.Target.StatusCode == StatusCodes.Gone 
    && storeResult.Target.SubStatusCode != SubStatusCodes.NameCacheIsStale)
{
    if (entity.UseStatusCodeForGone)
    {
        // Return as response, let retry policy handle it
        return new ReadReplicaResult(true, new List<...>());
    }
    else
    {
        throw new GoneException(RMResources.Gone, storeResult.Target.SubStatusCode);
    }
}
```

#### 6c. `GetStoreResultOrThrowGoneException` (line 139-148)

Currently throws when no valid results. For exceptionless, return a synthetic StoreResult with 410 status:
```csharp
private static ReferenceCountedDisposable<StoreResult> GetStoreResultOrThrowGoneException(
    ReadReplicaResult readReplicaResult, 
    bool useStatusCodeForGone)
{
    if (storeResultList.Count == 0)
    {
        if (useStatusCodeForGone)
        {
            return StoreResult.CreateGoneResult(SubStatusCodes.Server_NoValidStoreResponse);
        }
        throw new GoneException(RMResources.Gone, SubStatusCodes.Server_NoValidStoreResponse);
    }
    return storeResultList.GetFirstStoreResultAndDereference();
}
```

### Phase 7: Update `StoreResult`

**File: `direct/StoreResult.cs`**

#### 7a. `CreateStoreResult` with non-exception 410 responses

When `responseException == null` and `storeResponse.Status == 410`, `CreateStoreResult` already handles this correctly — it creates a `StoreResult` with `StatusCode = 410` from the `StoreResponse`[^14]. No change needed for this path.

#### 7b. `ToResponse` method (line 465-508)

The `ToResponse` method currently throws `this.Exception` when `IsValid == false` (line 481) or when `Exception != null` (line 505)[^21]. For the exceptionless path, when `Exception == null` but `StatusCode == 410`:
- The `IsValid` flag will be set based on the logic at line 239: for 410 without NameCacheIsStale, `isValid` is `false` when `requiresValidLsn == true`[^14]
- We need a new code path in `ToResponse` for exceptionless 410: return the `StoreResponse` even if `IsValid == false`, letting the caller handle the status code

```csharp
public StoreResponse ToResponse(RequestChargeTracker requestChargeTracker = null)
{
    if (!this.IsValid)
    {
        if (this.Exception == null && this.StatusCode == StatusCodes.Gone)
        {
            // Exceptionless 410 — return the response with status code
            // The caller (retry policy) will handle it
            return this.storeResponse ?? CreateSyntheticGoneStoreResponse(this.SubStatusCode);
        }
        
        // ... existing exception path
    }
    // ... rest of method
}
```

#### 7c. `VerifyCanContinueOnException` (line 585-605)

Add a status-code-based equivalent:
```csharp
internal static bool ShouldPropagateAsException(StatusCodes statusCode, SubStatusCodes subStatusCode)
{
    if (statusCode == StatusCodes.Gone)
    {
        return subStatusCode == SubStatusCodes.PartitionKeyRangeGone
            || subStatusCode == SubStatusCodes.CompletingSplit
            || subStatusCode == SubStatusCodes.CompletingPartitionMigration;
    }
    return false;
}
```

### Phase 8: Update `ConsistencyReader`

**File: `direct/ConsistencyReader.cs`**

#### `ReadAnyAsync` (line 279-297)

Currently throws `GoneException` when `responses.Count == 0`[^5]. For exceptionless, return a response with 410:

```csharp
if (responses.Count == 0)
{
    if (entity.UseStatusCodeForGone)
    {
        return StoreResponse.CreateGone(SubStatusCodes.Server_NoValidStoreResponse);
    }
    throw new GoneException(RMResources.Gone, SubStatusCodes.Server_NoValidStoreResponse);
}
```

### Phase 9: Update `ConsistencyWriter`

**File: `direct/ConsistencyWriter.cs`**

The write barrier throw points (lines 432, 478, 483) need similar treatment[^11]:

```csharp
if (entity.UseStatusCodeForGone)
{
    return CreateGoneStoreResponse(SubStatusCodes.ServerGenerated410);
}
throw new GoneException(RMResources.Gone, SubStatusCodes.ServerGenerated410);
```

### Phase 10: Enable the Flag

**File: `Handler/RequestMessage.cs`**

Add the flag alongside existing ones (line 296-297):
```csharp
serviceRequest.UseStatusCodeForFailures = true;
serviceRequest.UseStatusCodeFor429 = true;
serviceRequest.UseStatusCodeForGone = true;  // NEW - gated by flight
```

Initially, this should be controlled by a flight/configuration flag from `CosmosClientOptions`.

### Phase 11: Add `StoreResponse` Helper

Add a static factory method on `StoreResponse` to create synthetic 410 responses:

```csharp
internal static StoreResponse CreateGone(
    SubStatusCodes subStatusCode,
    string activityId = null,
    TransportRequestStats transportRequestStats = null)
{
    var headers = new StoreResponseNameValueCollection();
    headers[WFConstants.BackendHeaders.SubStatus] = ((int)subStatusCode).ToString();
    if (activityId != null)
    {
        headers[HttpConstants.HttpHeaders.ActivityId] = activityId;
    }
    
    return new StoreResponse()
    {
        Status = (int)HttpStatusCode.Gone,
        Headers = headers,
        ResponseBody = Stream.Null,
        TransportRequestStats = transportRequestStats
    };
}
```

---

## 7. Side-by-Side Flighting Strategy {#7-side-by-side-flighting}

### Flight Flag Design

```csharp
// In CosmosClientOptions or an internal options class
public class CosmosClientOptionsFeatures
{
    /// <summary>
    /// When enabled, GoneException (410) responses in Direct/TCP mode 
    /// are handled via status codes instead of CLR exceptions.
    /// </summary>
    internal bool EnableExceptionlessGone { get; set; } = false;
}
```

### Propagation Chain

```
CosmosClientOptions.EnableExceptionlessGone
  → DocumentClient constructor
    → sets DocumentServiceRequest.DefaultUseStatusCodeForGone = true/false
      → RequestMessage.GetDocumentServiceRequest() 
        → serviceRequest.UseStatusCodeForGone = DefaultUseStatusCodeForGone
```

This follows the exact pattern used for `UseStatusCodeFor4041002`[^17]:
```csharp
// DocumentServiceRequest.cs
internal static bool DefaultUseStatusCodeForGone = false;
public bool UseStatusCodeForGone { get; set; } = DefaultUseStatusCodeForGone;
```

### Side-by-Side Execution

The beauty of this approach is that **both paths coexist simultaneously**:

1. **Flag OFF (default)**: 410 throws `GoneException` as before — zero behavior change
2. **Flag ON (flight)**: 410 returns status code in `StoreResponse`

The `GoneAndRetryWithRequestRetryPolicy.TryHandleResponseSynchronously` already handles **both** paths through its dual `response` + `exception` checking[^15]:
```csharp
private static bool IsBaseGone(TResponse response, Exception exception)
{
    return exception is GoneException   // <-- exception path (flag OFF)
        || (response?.StatusCode == HttpStatusCode.Gone && ...);  // <-- response path (flag ON)
}
```

This means the retry policy requires **minimal changes** — it already supports both paths.

### Rollout Plan

| Phase | Scope | Duration | Validation |
|-------|-------|----------|------------|
| **Alpha** | Internal dogfood, specific test accounts | 2 weeks | CPU profiling, correctness |
| **Beta** | Opt-in via `CosmosClientOptions` (preview) | 4 weeks | Community feedback, telemetry |
| **GA Default OFF** | SDK release with flag available but off | Release cycle | Wide compatibility |
| **GA Default ON** | Change `DefaultUseStatusCodeForGone = true` | Next release | Final validation |

---

## 8. Validation Plan {#8-validation-plan}

### Unit Tests

1. **`TransportClient.ThrowServerException` with `UseStatusCodeForGone = true`**: Verify no exception thrown for 410 responses, verify `StoreResponse` status preserved
2. **`StoreResult.CreateStoreResult` with 410 `StoreResponse`**: Verify `StatusCode`, `SubStatusCode`, `IsValid` are correctly set
3. **`GoneAndRetryWithRequestRetryPolicy` with response-only 410**: Verify retry decisions match exception-based path for all sub-status codes
4. **All sub-status code variations**: Each of the ~12 sub-statuses must produce the same retry behavior via response as via exception
5. **`TimeoutHelper` exceptionless path**: Verify callers handle timeout without exception
6. **`StoreReader.ReadMultipleReplicasInternalAsync`**: Verify the entire multi-replica read flow works with 410 responses

### Integration Tests

1. **Emulator partition split**: Trigger partition split, verify reads/writes succeed with exceptionless 410
2. **Server failover**: Verify address refresh still triggers on 410 response
3. **Strong consistency barrier**: Verify write barrier retries work with exceptionless 410
4. **Timeout escalation**: Verify 30-second timeout budget works identically

### Performance Benchmarks

1. **CPU profiling**: Compare CPU usage under sustained 410 load (simulated partition split)
   - Metric: % CPU reduction in `System.Exception..ctor` and stack trace capture
2. **Allocation profiling**: Compare GC pressure (exception objects avoided)
3. **Latency percentiles**: P50, P95, P99 for operations during partition movements
4. **Throughput**: Requests/second under 410-heavy scenarios

### A/B Comparison Strategy

```csharp
// In test harness, run identical workload with both paths:
var clientExceptionBased = new CosmosClient(connStr, new CosmosClientOptions 
{ 
    /* UseStatusCodeForGone = false (default) */ 
});
var clientExceptionless = new CosmosClient(connStr, new CosmosClientOptions 
{ 
    /* UseStatusCodeForGone = true */ 
});

// Run identical operations, compare:
// - Wall-clock time
// - CPU profiling traces
// - Exception counts (should be ~0 for exceptionless)
// - Final result correctness (must be identical)
```

---

## 9. Risk Assessment {#9-risk-assessment}

### High Risk Areas

1. **`StoreResult.VerifyCanContinueOnException` bypass**: Currently, `PartitionKeyRangeGoneException` and `PartitionIsMigratingException` are handled by immediately re-throwing from `StoreReader`[^20]. In the exceptionless path, this signal must be preserved as a status code that gets the same treatment (cache refresh + retry). The `GoneAndRetryWithRequestRetryPolicy` already handles this via response checking, but the `StoreReader` internal logic may need adjustment.

2. **`rntbd2/TransportClient` synthetic response construction**: Creating a `StoreResponse` for a transport-level failure (where there is no actual HTTP response) requires careful construction to ensure all downstream consumers see consistent metadata.

3. **`StoreResult.ToResponse()` behavior change**: This method is called to convert a `StoreResult` to a `StoreResponse` for return to callers. When `IsValid == false` and `Exception == null` (the exceptionless 410 case), the method currently has no path for this — it will hit the `"Exception not set for invalid response"` assertion[^21]. This MUST be fixed.

4. **Session token handling**: `StoreClient.CaptureSessionToken` is called differently for exception vs. response paths. The exceptionless 410 path needs to ensure session tokens are not incorrectly captured for 410 responses.

### Medium Risk Areas

1. **Diagnostics**: `ClientSideRequestStatisticsTraceDatum` records exceptions with stack traces. The exceptionless path should still record 410 events but without exception overhead.

2. **Telemetry/OpenTelemetry**: `OpenTelemetryResponse` extracts sub-status codes from exceptions. The response path needs equivalent extraction.

### Low Risk Areas

1. **`GoneAndRetryWithRequestRetryPolicy`**: Already fully supports the dual response+exception evaluation pattern[^15]. Minimal changes needed.

2. **`RequestRetryUtility`**: Already orchestrates the response-based retry flow. No changes needed.

---

## 10. Confidence Assessment {#10-confidence-assessment}

| Finding | Confidence | Notes |
|---------|-----------|-------|
| GoneException causes high CPU due to exception overhead | **High** | Well-known .NET exception cost; code clearly shows multiple throw/catch cycles |
| Existing exceptionless pattern is the right model | **High** | Already proven for 404, 409, 412, 403, 429, 400 with identical mechanism |
| `GoneAndRetryWithRequestRetryPolicy` already supports response-based 410 | **High** | Verified in source: `IsBaseGone` checks both response and exception[^15] |
| `UseStatusCodeForGone` flag approach enables safe flighting | **High** | Follows established pattern with `UseStatusCodeFor4041002`, `UseStatusCodeForFailures` |
| Transport layer needs synthetic StoreResponse construction | **High** | `rntbd2/TransportClient` catches `TransportException` and creates `GoneException`; exceptionless path needs equivalent `StoreResponse` |
| `StoreResult.ToResponse()` needs new path for exceptionless 410 | **High** | Current code asserts/throws when Exception is null and IsValid is false |
| Session token handling is safe | **Medium** | Need to verify `CaptureSessionToken` behavior for 410 response (not exception) path |
| All sub-status codes handled correctly | **Medium** | Need comprehensive unit tests for each of the ~12 sub-statuses |
| Performance improvement is significant | **Medium-High** | Depends on frequency of 410 in workload; highest impact during partition splits/failovers |

---

## 11. Footnotes {#11-footnotes}

[^1]: `direct/rntbd2/TransportClient.cs:155-214` — TransportException caught, converted to GoneException for reads and unsent writes
[^2]: `direct/TransportClient.cs:842-1124` — `ThrowServerException` method, 410 case at lines 911-999 creates GoneException and derivatives
[^3]: `direct/TimeoutHelper.cs:46-51` — `ThrowGoneIfElapsed()` throws `GoneException(SubStatusCodes.TimeoutGenerated410)`
[^4]: `direct/StoreReader.cs:371-382` — When `hasGoneException` is true and quorum not met, throws `GoneException`
[^5]: `direct/ConsistencyReader.cs:291-293` — `ReadAnyAsync` throws `GoneException` when zero responses
[^6]: `direct/GoneAndRetryWithRetryPolicy.cs:137-321` — Old retry policy catches exception, decides retry with backoff
[^7]: `direct/ReplicatedResourceClient.cs:19-20` — `GoneAndRetryWithRetryTimeoutInSeconds = 30`, `StrongGoneAndRetryWithRetryTimeoutInSeconds = 60`
[^8]: `direct/TransportExceptions.cs:39-97` — `GetGoneException()` factory creates GoneException with `SubStatusCodes.TransportGenerated410`
[^9]: `direct/StoreReader.cs:139-144` — `GetStoreResultOrThrowGoneException` throws when store result list is empty
[^10]: `direct/StoreReader.cs:450-457` — Primary returned 410, throws GoneException
[^11]: `direct/ConsistencyWriter.cs:432,478,483` — Write barrier failures throw GoneException
[^12]: `direct/DocumentServiceRequest.cs:234-266` — `UseStatusCodeForFailures`, `UseStatusCodeFor403`, `UseStatusCodeFor4041002`, `UseStatusCodeFor429`, `UseStatusCodeForBadRequest`
[^13]: `direct/TransportClient.cs:847-854` — Early return in `ThrowServerException` when `IsValidStatusCodeForExceptionlessRetry` returns true
[^14]: `direct/StoreResult.cs:23-145` — `CreateStoreResult` handles both StoreResponse and Exception paths, sets `StatusCode` and `SubStatusCode`
[^15]: `direct/GoneAndRetryWithRequestRetryPolicy.cs:440-483` — `IsBaseGone`, `IsPartitionIsMigrating`, `IsInvalidPartition`, `IsPartitionKeySplitting`, `IsPartitionKeyRangeGone` all check both `response` and `exception`
[^16]: `direct/RequestRetryUtility.cs` — `ProcessRequestAsync` drives retry with `IRequestRetryPolicy.TryHandleResponseSynchronously`
[^17]: `direct/DocumentServiceRequest.cs:22,252` — `DefaultUseStatusCodeFor4041002 = false; UseStatusCodeFor4041002 { get; set; } = DefaultUseStatusCodeFor4041002`
[^18]: `direct/DocumentServiceRequest.cs:419-425` — `IsAnyExceptionLessEnabled()` method
[^19]: `direct/DocumentServiceRequest.cs:1215-1221` — Clone method copies exceptionless flags
[^20]: `direct/StoreResult.cs:585-605` — `VerifyCanContinueOnException` re-throws partition-related exceptions
[^21]: `direct/StoreResult.cs:465-508` — `ToResponse()` throws `Exception` when `IsValid == false` or `Exception != null`

---

# Addendum: Monadic Approach Using `OperationResult<T>`

## Motivation

The original plan (Phases 1–11 above) uses scattered `if (entity.UseStatusCodeForGone)` checks at every throw site in `StoreReader`, `ConsistencyReader`, and `ConsistencyWriter`. This works but produces verbose branching throughout the code.

The Query pipeline already uses a **monadic `TryCatch<T>` pattern** (see `Query/Core/Monads/`) built on `Either<Exception, TResult>`. Every `IMonadic*` interface returns `Task<TryCatch<TPage>>` instead of throwing. The `CrossPartitionRangePageAsyncEnumerator` inspects `.Failed` to handle splits without re-throwing.

This addendum proposes adapting that pattern for the Direct mode Gone path — with a critical optimization.

## Why `TryCatch<T>` Cannot Be Used Directly

`TryCatch<T>.FromException()` (`TryCatch{TResult}.cs:243-254`) **always captures a stack trace**:

```csharp
public static TryCatch<TResult> FromException(Exception exception)
{
    StackTrace stackTrace = new StackTrace(skipFrames: 1);  // EXPENSIVE
    return new TryCatch<TResult>(
        new ExceptionWithStackTraceException(
            message: $"...",
            innerException: exception,   // also expensive if GoneException
            stackTrace: stackTrace));
}
```

This creates **two** expensive objects (the `GoneException` argument + the `ExceptionWithStackTraceException` wrapper). For our hot retry path (9–12 times per operation during splits), this is worse than the current single throw.

The Query pipeline can afford this because `FromException` is used for low-frequency parsing/token errors, not hot retry loops.

## Proposed: `OperationResult<T>` — Zero-Cost Error Variant

Reuse the existing `Either<TLeft, TRight>` discriminated union but with a **lightweight error struct** instead of `Exception`:

### `GoneError` — stack-allocated error descriptor

```csharp
/// <summary>
/// Lightweight error descriptor for Gone (410) responses.
/// No exception creation, no stack trace capture.
/// Cost: ~32 bytes on the stack vs ~2–10KB for a GoneException.
/// </summary>
internal readonly struct GoneError
{
    public GoneError(
        SubStatusCodes subStatusCode,
        string message = null,
        TransportRequestStats transportRequestStats = null)
    {
        this.SubStatusCode = subStatusCode;
        this.Message = message;
        this.TransportRequestStats = transportRequestStats;
    }

    public SubStatusCodes SubStatusCode { get; }
    public string Message { get; }
    public TransportRequestStats TransportRequestStats { get; }

    /// <summary>
    /// Deferred exception creation — only called when crossing into
    /// exception-based layers (old retry policy, final throw to caller).
    /// </summary>
    public GoneException ToException()
    {
        return new GoneException(
            this.Message ?? RMResources.Gone,
            this.SubStatusCode);
    }
}
```

### `OperationResult<T>` — the monad

```csharp
/// <summary>
/// Monadic result type for store operations. Built on the same Either
/// foundation as TryCatch but with a lightweight error type to avoid
/// CLR exception creation on the hot retry path.
///
/// Composes with TryCatch via .ToTryCatch() when crossing into layers
/// that expect TryCatch (query pipeline, pagination).
/// </summary>
internal readonly struct OperationResult<TResult>
{
    private readonly Either<GoneError, TResult> either;

    private OperationResult(Either<GoneError, TResult> either)
    {
        this.either = either;
    }

    public bool Succeeded => this.either.IsRight;
    public bool IsGone => this.either.IsLeft;

    public TResult Result
    {
        get
        {
            Debug.Assert(this.Succeeded);
            return this.either.FromRight(default);
        }
    }

    public GoneError Error
    {
        get
        {
            Debug.Assert(this.IsGone);
            return this.either.FromLeft(default);
        }
    }

    // --- Monadic combinators (same API shape as TryCatch) ---

    public OperationResult<T> Try<T>(Func<TResult, T> onSuccess)
    {
        if (this.Succeeded)
        {
            return OperationResult<T>.FromResult(
                onSuccess(this.either.FromRight(default)));
        }
        return OperationResult<T>.FromGone(this.either.FromLeft(default));
    }

    public OperationResult<T> Try<T>(Func<TResult, OperationResult<T>> onSuccess)
    {
        if (this.Succeeded)
        {
            return onSuccess(this.either.FromRight(default));
        }
        return OperationResult<T>.FromGone(this.either.FromLeft(default));
    }

    public async Task<OperationResult<T>> TryAsync<T>(
        Func<TResult, Task<OperationResult<T>>> onSuccess)
    {
        if (this.Succeeded)
        {
            return await onSuccess(this.either.FromRight(default));
        }
        return OperationResult<T>.FromGone(this.either.FromLeft(default));
    }

    public void Match(Action<TResult> onSuccess, Action<GoneError> onGone)
    {
        this.either.Match(onLeft: onGone, onRight: onSuccess);
    }

    public T Match<T>(Func<TResult, T> onSuccess, Func<GoneError, T> onGone)
    {
        return this.either.Match(onLeft: onGone, onRight: onSuccess);
    }

    /// <summary>
    /// Bridge to TryCatch for layers that need it.
    /// Only materializes the exception if converting to error path.
    /// </summary>
    public TryCatch<TResult> ToTryCatch()
    {
        if (this.Succeeded)
        {
            return TryCatch<TResult>.FromResult(this.Result);
        }
        return TryCatch<TResult>.FromException(this.Error.ToException());
    }

    /// <summary>
    /// Deferred throw — only creates exception when actually needed.
    /// Used at the flighting bridge when the flag is OFF.
    /// </summary>
    public void ThrowIfGone()
    {
        if (this.IsGone)
        {
            throw this.Error.ToException();
        }
    }

    // --- Factory methods ---

    public static OperationResult<TResult> FromResult(TResult result)
        => new OperationResult<TResult>(result);

    public static OperationResult<TResult> FromGone(GoneError error)
        => new OperationResult<TResult>(error);

    public static OperationResult<TResult> FromGone(SubStatusCodes subStatus)
        => new OperationResult<TResult>(new GoneError(subStatus));

    // Implicit conversions for ergonomics
    public static implicit operator OperationResult<TResult>(TResult result)
        => FromResult(result);

    public static implicit operator OperationResult<TResult>(GoneError error)
        => FromGone(error);
}
```

## StoreReader Changes With the Monad

### Key Principle: Flight Once at the Public Boundary

The internal methods (`ReadMultipleReplicasInternalAsync`, `ReadPrimaryInternalAsync`, etc.) use the monadic path **unconditionally**. The flighting decision is made **once** at the public entry point:

```csharp
// ReadMultipleReplicaAsync (public entry):
public async Task<List<ReferenceCountedDisposable<StoreResult>>>
    ReadMultipleReplicaAsync(DocumentServiceRequest entity, ...)
{
    // Internal method always returns OperationResult
    OperationResult<List<ReferenceCountedDisposable<StoreResult>>> result =
        await this.ReadMultipleReplicaMonadicAsync(entity, ...);

    if (entity.UseStatusCodeForGone)
    {
        // Exceptionless: convert Gone error to synthetic StoreResult
        return result.Match(
            onSuccess: r => r,
            onGone: error => new List<ReferenceCountedDisposable<StoreResult>>
            {
                StoreResult.CreateForGone(error.SubStatusCode)
            });
    }

    // Legacy: throw if gone (deferred exception creation)
    result.ThrowIfGone();
    return result.Result;
}
```

### `GetStoreResult` (was `GetStoreResultOrThrowGoneException`)

```csharp
// BEFORE: throws
private static ReferenceCountedDisposable<StoreResult>
    GetStoreResultOrThrowGoneException(ReadReplicaResult readReplicaResult)
{
    if (storeResultList.Count == 0)
    {
        throw new GoneException(RMResources.Gone,
            SubStatusCodes.Server_NoValidStoreResponse);
    }
    return storeResultList.GetFirstStoreResultAndDereference();
}

// AFTER: monadic — no exception created on empty results
private static OperationResult<ReferenceCountedDisposable<StoreResult>>
    GetStoreResult(ReadReplicaResult readReplicaResult)
{
    StoreResultList storeResultList = readReplicaResult.StoreResultList;
    if (storeResultList.Count == 0)
    {
        return new GoneError(SubStatusCodes.Server_NoValidStoreResponse);
    }
    return storeResultList.GetFirstStoreResultAndDereference();
}
```

### `ReadMultipleReplicasInternalAsync` — timeout

```csharp
// BEFORE: throws
entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

// AFTER: monadic, zero-cost
if (entity.RequestContext.TimeoutHelper.IsElapsed())
{
    return OperationResult<ReadReplicaResult>.FromGone(
        SubStatusCodes.TimeoutGenerated410);
}
```

### `ReadMultipleReplicasInternalAsync` — terminal gone (was `throw new GoneException`)

```csharp
// BEFORE:
if (hasGoneException && !entity.RequestContext.PerformLocalRefreshOnGoneException)
{
    throw new GoneException(exceptionToThrow, subStatusCodeForException);
}

// AFTER:
if (hasGoneException && !entity.RequestContext.PerformLocalRefreshOnGoneException)
{
    return OperationResult<ReadReplicaResult>.FromGone(
        subStatusCodeForException);
}
```

### `ReadPrimaryAsync` — composing with `.Try()`

```csharp
// BEFORE:
ReadReplicaResult result = await this.ReadPrimaryInternalAsync(...);
return GetStoreResultOrThrowGoneException(result);

// AFTER: monadic flat-map
OperationResult<ReadReplicaResult> readResult =
    await this.ReadPrimaryInternalMonadicAsync(entity, ...);

return readResult.Try(r => GetStoreResult(r));
```

### `VerifyCanContinueOnException` — exceptionless equivalent

```csharp
// The VerifyCanContinueOnException check (re-throws partition sub-statuses)
// becomes a simple status code inspection:
if (storeResult.Exception != null)
{
    StoreResult.VerifyCanContinueOnException(storeResult.Exception);
}
else if (storeResult.StatusCode == StatusCodes.Gone)
{
    // For partition-level sub-statuses, the
    // GoneAndRetryWithRequestRetryPolicy already evaluates
    // these via response.SubStatusCode (IsPartitionIsMigrating,
    // IsPartitionKeySplitting, IsInvalidPartition).
    //
    // Only RequestValidationFailure header still needs propagation:
    StoreResult.VerifyCanContinueOnStatusCode(storeResult.StoreResponse);
}
```

Where `VerifyCanContinueOnStatusCode` is a lightweight check:
```csharp
internal static void VerifyCanContinueOnStatusCode(StoreResponse response)
{
    if (response == null) return;

    if (response.TryGetHeaderValue(
            HttpConstants.HttpHeaders.RequestValidationFailure,
            out string value)
        && !string.IsNullOrWhiteSpace(value)
        && int.TryParse(value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int result)
        && result == 1)
    {
        throw new GoneException(RMResources.Gone, response.SubStatusCode);
    }
}
```

## Cost Comparison

| Metric | Exception Path | TryCatch.FromException | OperationResult |
|--------|---------------|----------------------|-----------------|
| Object creation | ~2–10KB (Exception + stacktrace) | ~4–20KB (2 exceptions + 2 stack traces) | **~32 bytes** (stack struct) |
| Stack trace capture | Yes (CLR forced) | Yes (explicit `new StackTrace()`) | **None** |
| Per-retry cost (10 retries) | ~100KB + 10 throw/catch | ~200KB + 0 throws | **~320 bytes** + 0 throws |
| Composability | try/catch blocks | `.Try()` / `.Catch()` | `.Try()` / `.Match()` |
| Type safety | Runtime exception type checks | Same | **Exhaustive `.Match()`** |
| Flight branching | N/A | N/A | **Once at public boundary** |

## Summary of Change Sites in StoreReader

| # | Method | Current | Monadic |
|---|--------|---------|---------|
| 1 | `ReadMultipleReplicaAsync` | `ThrowGoneIfElapsed()` at lines 67, 78 | `IsElapsed()` → `FromGone(TimeoutGenerated410)` |
| 2 | `ReadPrimaryAsync` | `ThrowGoneIfElapsed()` at line 107 | Same pattern |
| 3 | `ReadPrimaryAsync` | `GetStoreResultOrThrowGoneException` at lines 128, 131 | `.Try(r => GetStoreResult(r))` |
| 4 | `GetStoreResultOrThrowGoneException` | `throw new GoneException` at line 144 | `return new GoneError(...)` |
| 5 | `ReadMultipleReplicasInternalAsync` | `ThrowGoneIfElapsed()` at lines 171, 228 | `IsElapsed()` → `FromGone(...)` |
| 6 | `ReadMultipleReplicasInternalAsync` | `VerifyCanContinueOnException` at lines 324–327 | Add `VerifyCanContinueOnStatusCode` branch |
| 7 | `ReadMultipleReplicasInternalAsync` | `throw new GoneException` at line 382 | `return FromGone(subStatusCode)` |
| 8 | `ReadPrimaryInternalAsync` | `ThrowGoneIfElapsed()` at line 416 | `IsElapsed()` → `FromGone(...)` |
| 9 | `ReadPrimaryInternalAsync` | `VerifyCanContinueOnException` at lines 445–448 | Add status-code branch |
| 10 | `ReadPrimaryInternalAsync` | `throw new GoneException` at line 457 | `return FromGone(subStatusCode)` |
| 11 | `ReadFromStoreAsync` | `ThrowGoneIfElapsed()` at line 512 | `IsElapsed()` → synthetic `StoreResponse.CreateGone()` |

## Advantages Over Scattered If/Else Approach

1. **Single flighting point** — `UseStatusCodeForGone` checked once at public entry, not at every throw site
2. **Zero-cost error representation** — `GoneError` is a readonly struct (~32 bytes vs ~10KB exception)
3. **Reuses existing `Either<,>`** — no new discriminated union plumbing needed
4. **Composable** — `.Try()`, `.Match()` instead of nested if/else
5. **Deferred exception creation** — `.ThrowIfGone()` and `.ToException()` only materialize the expensive object when the legacy path needs it
6. **Bridge to TryCatch** — `.ToTryCatch()` integrates with Query pipeline layers if needed
7. **Type-safe exhaustiveness** — `.Match()` forces handling both success and gone paths

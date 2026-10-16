//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///  Contains the cosmos diagnostic information for the current request to Azure Cosmos DB service.
    /// </summary>
    public abstract class CosmosDiagnostics
    {
        /// <summary>
        /// This represent the end to end elapsed time of the request.
        /// If the request is still in progress it will return the current
        /// elapsed time since the start of the request.
        /// </summary>
        /// <returns>The clients end to end elapsed time of the request.</returns>
        public virtual TimeSpan GetClientElapsedTime()
        {
            // Default implementation avoids breaking change for users upgrading.
            throw new NotImplementedException($"CosmosDiagnostics.GetElapsedTime");
        }

        /// <summary>
        /// This represents the start time of the request.
        /// </summary>
        /// <returns>This returns the start time of the request.</returns>
        public virtual DateTime? GetStartTimeUtc()
        {
            // Default implementation avoids breaking change for users upgrading.
            throw new NotImplementedException($"{nameof(CosmosDiagnostics)}.{nameof(GetStartTimeUtc)})");
        }

        /// <summary>
        ///  This represents the count of failed requests.
        /// </summary>
        /// <returns>The count of failed requests with cosmos service.</returns>
        public virtual int GetFailedRequestCount()
        {
            // Default implementation avoids breaking change for users upgrading.
            throw new NotImplementedException($"{nameof(CosmosDiagnostics)}.{nameof(GetFailedRequestCount)}");
        }

        /// <summary>
        /// This represents the backend query metrics for the request.
        /// </summary>
        /// <remarks>
        /// This is only applicable for query operations. For all other operations this will return null.
        /// </remarks>
        /// <returns>The accumulated backend metrics for the request.</returns>
        public virtual ServerSideCumulativeMetrics GetQueryMetrics()
        {
            // Default implementation avoids breaking change for users upgrading.
            throw new NotImplementedException($"{nameof(CosmosDiagnostics)}.{nameof(GetQueryMetrics)}");
        }

        /// <summary>
        /// Gets the string field <see cref="CosmosDiagnostics"/> instance in the Azure Cosmos DB database service.
        /// </summary>
        /// <returns>The string field <see cref="CosmosDiagnostics"/> instance in the Azure Cosmos DB database service.</returns>
        /// <remarks>
        /// <see cref="CosmosDiagnostics"/> implements lazy materialization and is only materialized when <see cref="CosmosDiagnostics.ToString"/> is called.
        /// </remarks>
        /// <example>
        /// Do not eagerly materialize the diagnostics until the moment of consumption to avoid unnecessary allocations, let the ToString be called only when needed.
        /// You can capture diagnostics conditionally, based on latency or errors:
        /// <code language="c#">
        /// <![CDATA[
        /// try
        /// {
        ///     ItemResponse<Book> response = await container.CreateItemAsync<Book>(item: testItem);
        ///     if (response.Diagnostics.GetClientElapsedTime() > ConfigurableSlowRequestTimeSpan)
        ///     {
        ///         // Log the diagnostics and add any additional info necessary to correlate to other logs 
        ///         logger.LogInformation("Operation took longer than expected, Diagnostics: {Diagnostics}");
        ///     }
        /// }
        /// catch (CosmosException cosmosException)
        /// {
        ///     // Log the full exception including the stack trace 
        ///     logger.LogError(cosmosException);
        ///     // The Diagnostics can be logged separately if required.
        ///     logger.LogError("Cosmos DB call failed with {StatusCode}, {SubStatusCode}, Diagnostics: {Diagnostics}", cosmosException.StatusCode, cosmosException.SubStatusCode, cosmosException.Diagnostics);
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract override string ToString();

        /// <summary>
        /// Gets the list of all regions that were contacted for a request
        /// </summary>
        /// <returns>The list of tuples containing the Region name and the URI</returns>
        /// <remarks>
        /// The returned list contains unique regions and doesn't guarantee ordering of the regions contacted from the first to the last
        /// </remarks>
        public abstract IReadOnlyList<(string regionName, Uri uri)> GetContactedRegions();

        /// <summary>
        /// Returns <c>true</c> if the SDK actually dispatched this operation to a hedge
        /// region as part of a cross-region availability-strategy fan-out. Returns
        /// <c>false</c> for non-hedged operations and for hedged operations whose primary
        /// responded under the configured threshold (hedge tasks were registered but never
        /// awaited; no fan-out occurred).
        /// </summary>
        /// <returns>
        /// <c>true</c> if at least one hedge arm was actually dispatched; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>A return value of <c>false</c> does NOT mean hedging was disabled or
        /// misconfigured.</b> To check whether hedging was <i>configured</i> on the client,
        /// inspect <c>CosmosClientOptions.AvailabilityStrategy</c> or
        /// <c>RequestOptions.AvailabilityStrategy</c> directly.
        /// </para>
        /// <para>
        /// O(1). Safe to call on both the success path (<c>ItemResponse&lt;T&gt;.Diagnostics</c>)
        /// and the error path (<c>CosmosException.Diagnostics</c>). Returns <c>false</c> for
        /// diagnostics objects created outside the SDK pipeline (e.g. customer-authored
        /// subclasses of <see cref="CosmosDiagnostics"/>).
        /// </para>
        /// </remarks>
        public virtual bool HedgingStarted()
        {
            return false;
        }

        /// <summary>
        /// Returns the regions the SDK actually dispatched this operation to, in observed
        /// dispatch order, each tagged with the reason the SDK chose it. Includes the
        /// initial attempt.
        /// </summary>
        /// <returns>
        /// A read-only list of <see cref="RequestedRegion"/> entries. Never <c>null</c>;
        /// returns an empty list when there is no dispatch history (for example, when a
        /// pre-flight client-side validation failed before any dispatch was attempted, or
        /// for diagnostics objects created outside the SDK pipeline).
        /// </returns>
        /// <remarks>
        /// <para>
        /// The append site is the actual dispatch point; registered-but-never-awaited
        /// hedge tasks do <b>not</b> appear here. <see cref="RequestedRegionReason"/> is
        /// non-exhaustive — callers MUST include a <c>default</c> arm when switching on
        /// the enum to handle future values.
        /// </para>
        /// <para>
        /// <b>Contract is "dispatched, not necessarily wire-issued".</b> An entry reflects
        /// the SDK's decision to commit to dispatching — for hedge arms specifically, this
        /// means the per-arm threshold delay elapsed without cancellation. A cancellation
        /// arriving in the microsecond-wide window between that dispatch decision and the
        /// underlying HTTP/RNTBD send leaving the process still leaves the entry in this
        /// list. Callers should treat the list as a record of intent-to-dispatch, not a
        /// record of wire-issued requests.
        /// </para>
        /// <para>
        /// Duplicates are allowed (the same region can be tried multiple times across
        /// retries, fail-overs and probes). Returns empty for SDK clients constructed
        /// before this version's diagnostics plumbing populated it. Available on success
        /// and error paths.
        /// </para>
        /// </remarks>
        public virtual IReadOnlyList<RequestedRegion> GetRequestedRegions()
        {
            return Array.Empty<RequestedRegion>();
        }

        /// <summary>
        /// Returns the regions that responded to this operation (success or failure), in
        /// arrival order as observed by the SDK orchestrator.
        /// </summary>
        /// <returns>
        /// A read-only list of region names. Never <c>null</c>; returns an empty list when
        /// no response arrived (for example, when all dispatches were cancelled or timed
        /// out) or for diagnostics objects created outside the SDK pipeline.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>Duplicates are allowed and expected.</b> The same region may appear more
        /// than once if it produced multiple responses (e.g., a late response after a
        /// hedge winner). <c>Count &gt; 1</c> does <b>not</b> imply that more than one
        /// distinct region responded. For unique regions, call
        /// <c>.Distinct(StringComparer.OrdinalIgnoreCase)</c>.
        /// </para>
        /// <para>
        /// Returns empty for SDK clients constructed before this version's diagnostics
        /// plumbing populated it. Available on success and error paths.
        /// </para>
        /// </remarks>
        public virtual IReadOnlyList<string> GetRespondedRegions()
        {
            return Array.Empty<string>();
        }
    }
}

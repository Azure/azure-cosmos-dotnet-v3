//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Represents a single region the SDK dispatched a request to as part of an operation,
    /// tagged with the reason the orchestrator chose to send it.
    /// </summary>
    /// <remarks>
    /// <para>Used by <see cref="CosmosDiagnostics.GetRequestedRegions"/> to enumerate every
    /// dispatched attempt in observed dispatch order, including the initial attempt and any
    /// retries, region fail-overs, hedge arms, or circuit-breaker probes.</para>
    /// <para>This value is immutable. Equality is case-insensitive on
    /// <see cref="RegionName"/> and exact on <see cref="Reason"/>.</para>
    /// </remarks>
    public readonly struct RequestedRegion : IEquatable<RequestedRegion>
    {
        /// <summary>
        /// Initializes a new <see cref="RequestedRegion"/>.
        /// </summary>
        /// <param name="regionName">
        /// The name of the region the SDK dispatched to (e.g. "East US"). Must not be null.
        /// </param>
        /// <param name="reason">The reason the SDK chose this region for this dispatch.</param>
        /// <exception cref="ArgumentNullException"><paramref name="regionName"/> is null.</exception>
        public RequestedRegion(string regionName, RequestedRegionReason reason)
        {
            this.RegionName = regionName ?? throw new ArgumentNullException(nameof(regionName));
            this.Reason = reason;
        }

        /// <summary>
        /// Gets the name of the region the SDK dispatched to.
        /// </summary>
        public string RegionName { get; }

        /// <summary>
        /// Gets the reason the SDK chose this region for this particular dispatch attempt.
        /// </summary>
        public RequestedRegionReason Reason { get; }

        /// <summary>
        /// Determines whether this instance equals another <see cref="RequestedRegion"/> by
        /// comparing <see cref="RegionName"/> case-insensitively and <see cref="Reason"/> exactly.
        /// </summary>
        /// <param name="other">The other <see cref="RequestedRegion"/> to compare against.</param>
        /// <returns><c>true</c> when both region names match (case-insensitive) and the reasons match exactly; otherwise <c>false</c>.</returns>
        public bool Equals(RequestedRegion other)
        {
            return string.Equals(this.RegionName, other.RegionName, StringComparison.OrdinalIgnoreCase)
                && this.Reason == other.Reason;
        }

        /// <summary>
        /// Returns a human-readable representation of this <see cref="RequestedRegion"/> in the
        /// form <c>"{regionName}:{reason}"</c>.
        /// </summary>
        /// <returns>A string of the form <c>"{regionName}:{reason}"</c>.</returns>
        public override string ToString()
        {
            return $"{this.RegionName}:{this.Reason}";
        }
    }
}

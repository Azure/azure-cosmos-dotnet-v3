//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Diagnostics.Tracing;

    /// <summary>
    /// EventSource for the Microsoft.Azure.Cosmos.Encryption.Custom package.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Source name: <c>Azure-Cosmos-Encryption-Custom</c> — follows the <c>Azure-{Service}-{Subsystem}</c>
    /// naming convention used by other Azure SDK EventSources so that it is auto-discovered by
    /// <c>Azure.Core.Diagnostics.AzureEventSourceListener</c> and tools like <c>dotnet-trace</c>
    /// (e.g. <c>dotnet-trace --providers Azure-Cosmos-Encryption-Custom</c>).
    /// </para>
    /// <para>
    /// Use this for Release-visible diagnostics that are best-effort: failures from the optional
    /// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> integration
    /// (read / write / remove / background write) which are intentionally swallowed because the
    /// in-memory cache or source-of-truth still satisfy the operation. The event payload is a
    /// short message; do not log key material, wrapped key bytes, or full exception strings.
    /// </para>
    /// <para>
    /// <see cref="System.Diagnostics.Activity"/> tags on the surrounding cache scopes remain the
    /// primary correlation channel; this EventSource exists so the same failures are observable
    /// in environments where no <see cref="System.Diagnostics.ActivityListener"/> is attached.
    /// </para>
    /// </remarks>
    [EventSource(Name = EventSourceName)]
    internal sealed class EncryptionCustomEventSource : EventSource
    {
        internal const string EventSourceName = "Azure-Cosmos-Encryption-Custom";

        private static EncryptionCustomEventSource Singleton { get; } = new EncryptionCustomEventSource();

        private EncryptionCustomEventSource()
            : base(EventSourceName)
        {
        }

        /// <summary>
        /// Records that a distributed cache read failed and the caller fell back to the source.
        /// Operation correctness is preserved.
        /// </summary>
        [NonEvent]
        public static void DistributedCacheReadFailed(string dekId, Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            if (Singleton.IsEnabled(EventLevel.Warning, EventKeywords.None))
            {
                Singleton.DistributedCacheReadFailedCore(dekId ?? string.Empty, exception.GetType().FullName, exception.Message);
            }
        }

        /// <summary>
        /// Records that a synchronous distributed cache write (cold-path / refresh population)
        /// failed. The in-memory cache still holds the value; subsequent fetches will retry.
        /// </summary>
        [NonEvent]
        public static void DistributedCacheWriteFailed(string dekId, Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            if (Singleton.IsEnabled(EventLevel.Warning, EventKeywords.None))
            {
                Singleton.DistributedCacheWriteFailedCore(dekId ?? string.Empty, exception.GetType().FullName, exception.Message);
            }
        }

        /// <summary>
        /// Records that a fire-and-forget distributed cache write (e.g. from <c>SetDekProperties</c>)
        /// failed. The in-memory cache is authoritative for the current process; peers will repopulate
        /// L2 on their next miss.
        /// </summary>
        [NonEvent]
        public static void DistributedCacheBackgroundWriteFailed(string dekId, Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            if (Singleton.IsEnabled(EventLevel.Warning, EventKeywords.None))
            {
                Singleton.DistributedCacheBackgroundWriteFailedCore(dekId ?? string.Empty, exception.GetType().FullName, exception.Message);
            }
        }

        /// <summary>
        /// Records that a distributed cache <c>RemoveAsync</c> failed. The L1 entry has already
        /// been invalidated; the stale L2 entry will be superseded by the next write or expire on
        /// its own AbsoluteExpiration.
        /// </summary>
        [NonEvent]
        public static void DistributedCacheRemoveFailed(string dekId, Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            if (Singleton.IsEnabled(EventLevel.Warning, EventKeywords.None))
            {
                Singleton.DistributedCacheRemoveFailedCore(dekId ?? string.Empty, exception.GetType().FullName, exception.Message);
            }
        }

        [Event(1, Level = EventLevel.Warning, Message = "DekCache distributed cache read failed for DEK '{0}': {1}: {2}")]
        private void DistributedCacheReadFailedCore(string dekId, string exceptionType, string message)
            => this.WriteEvent(1, dekId, exceptionType, message);

        [Event(2, Level = EventLevel.Warning, Message = "DekCache distributed cache write failed for DEK '{0}': {1}: {2}")]
        private void DistributedCacheWriteFailedCore(string dekId, string exceptionType, string message)
            => this.WriteEvent(2, dekId, exceptionType, message);

        [Event(3, Level = EventLevel.Warning, Message = "DekCache background distributed cache write failed for DEK '{0}': {1}: {2}")]
        private void DistributedCacheBackgroundWriteFailedCore(string dekId, string exceptionType, string message)
            => this.WriteEvent(3, dekId, exceptionType, message);

        [Event(4, Level = EventLevel.Warning, Message = "DekCache distributed cache remove failed for DEK '{0}': {1}: {2}")]
        private void DistributedCacheRemoveFailedCore(string dekId, string exceptionType, string message)
            => this.WriteEvent(4, dekId, exceptionType, message);
    }
}

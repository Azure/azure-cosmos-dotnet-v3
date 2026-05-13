//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Diagnostics.Tracing;

    /// <summary>
    /// EventSource for the Microsoft.Azure.Cosmos.Encryption.Custom package. Source name
    /// <c>Azure-Cosmos-Encryption-Custom</c> follows the <c>Azure-{Service}-{Subsystem}</c>
    /// convention, so it is auto-discovered by <c>AzureEventSourceListener</c> and
    /// <c>dotnet-trace --providers Azure-Cosmos-Encryption-Custom</c>.
    /// </summary>
    /// <remarks>
    /// Used for Release-visible best-effort diagnostics on the optional
    /// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> integration
    /// (read / write / background write / remove). Activity tags on the surrounding cache
    /// scopes remain the primary correlation channel; this EventSource exists so the same
    /// failures are observable when no <see cref="System.Diagnostics.ActivityListener"/> is
    /// attached. Payloads must not contain key material, wrapped key bytes, or full exception
    /// strings.
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
        /// Distributed cache read failed; caller fell back to source.
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
        /// Synchronous distributed cache write (cold-path / refresh population) failed.
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
        /// Fire-and-forget distributed cache write failed.
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
        /// Distributed cache <c>RemoveAsync</c> failed. The L1 entry has already been invalidated.
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

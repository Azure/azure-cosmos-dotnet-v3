//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.IO;
    using System.Net.Http;
    using System.Net.Sockets;
    using Newtonsoft.Json;

    /// <summary>
    /// EventSource for the Microsoft.Azure.Cosmos.Encryption.Custom package. Source name
    /// <c>Azure-Cosmos-Encryption-Custom</c> follows the <c>Azure-{Service}-{Subsystem}</c>
    /// convention, so it is auto-discovered by <c>AzureEventSourceListener</c> and
    /// <c>dotnet-trace --providers Azure-Cosmos-Encryption-Custom</c>.
    /// </summary>
    /// <remarks>
    /// Used for Release-visible best-effort diagnostics on the optional
    /// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> integration
    /// (read / write / background write / remove / Id mismatch). Activity tags on the
    /// surrounding cache scopes remain the primary correlation channel; this EventSource
    /// exists so the same failures are observable when no
    /// <see cref="System.Diagnostics.ActivityListener"/> is attached. Payload contract:
    /// payloads MUST NOT contain key material, wrapped key bytes, or unstructured exception
    /// messages (which could leak Redis endpoints, SQL parameter values, or HTTP URLs with
    /// embedded credentials). Exception detail is reduced to a stable category string
    /// ('timeout' / 'connection' / 'deserialize' / 'other'); exception type FullName is
    /// included for diagnostic narrowing. The dekId IS emitted verbatim — treat dekId as
    /// customer-correlatable, not as a secret. The <c>observedDekId</c> payload of the
    /// Id-mismatch event originates from an untrusted distributed-cache peer; it is truncated
    /// to 128 characters and otherwise emitted verbatim. EventSource subscribers must not
    /// assume any structure or bound beyond that.
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
                Singleton.DistributedCacheReadFailedCore(dekId ?? string.Empty, exception.GetType().FullName, CategorizeException(exception));
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
                Singleton.DistributedCacheWriteFailedCore(dekId ?? string.Empty, exception.GetType().FullName, CategorizeException(exception));
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
                Singleton.DistributedCacheBackgroundWriteFailedCore(dekId ?? string.Empty, exception.GetType().FullName, CategorizeException(exception));
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
                Singleton.DistributedCacheRemoveFailedCore(dekId ?? string.Empty, exception.GetType().FullName, CategorizeException(exception));
            }
        }

        /// <summary>
        /// Distributed cache payload Id did not match the requested DEK.
        /// </summary>
        [NonEvent]
        public static void DistributedCacheIdMismatch(string requestedDekId, string observedDekId)
        {
            if (Singleton.IsEnabled(EventLevel.Warning, EventKeywords.None))
            {
                Singleton.DistributedCacheIdMismatchCore(requestedDekId ?? string.Empty, TruncateForTelemetry(observedDekId));
            }
        }

        // StackExchange.Redis-specific exceptions (for example RedisConnectionException and
        // RedisTimeoutException) intentionally fall through to "other" because this package does
        // not take a direct dependency on StackExchange.Redis. Operators still get the concrete
        // exception type via the payload's exceptionType FullName field.
        private static string CategorizeException(Exception ex)
        {
            if (ex is OperationCanceledException || ex is TimeoutException)
            {
                return "timeout";
            }

            if (ex is SocketException || ex is HttpRequestException)
            {
                return "connection";
            }

            if (ex is JsonException || ex is InvalidDataException)
            {
                return "deserialize";
            }

            return "other";
        }

        internal static string TruncateForTelemetry(string value, int maxLength = 128)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return string.Concat(
                value.Substring(0, maxLength),
                "…(truncated, ",
                value.Length.ToString(CultureInfo.InvariantCulture),
                " chars)");
        }

        [Event(1, Level = EventLevel.Warning, Message = "DekCache distributed cache read failed for DEK '{0}': {1} category: {2}")]
        private void DistributedCacheReadFailedCore(string dekId, string exceptionType, string category)
            => this.WriteEvent(1, dekId, exceptionType, category);

        [Event(2, Level = EventLevel.Warning, Message = "DekCache distributed cache write failed for DEK '{0}': {1} category: {2}")]
        private void DistributedCacheWriteFailedCore(string dekId, string exceptionType, string category)
            => this.WriteEvent(2, dekId, exceptionType, category);

        [Event(3, Level = EventLevel.Warning, Message = "DekCache background distributed cache write failed for DEK '{0}': {1} category: {2}")]
        private void DistributedCacheBackgroundWriteFailedCore(string dekId, string exceptionType, string category)
            => this.WriteEvent(3, dekId, exceptionType, category);

        [Event(4, Level = EventLevel.Warning, Message = "DekCache distributed cache remove failed for DEK '{0}': {1} category: {2}")]
        private void DistributedCacheRemoveFailedCore(string dekId, string exceptionType, string category)
            => this.WriteEvent(4, dekId, exceptionType, category);

        [Event(5, Level = EventLevel.Warning, Message = "DekCache distributed cache entry Id mismatch: requested '{0}', payload Id was '{1}'.")]
        private void DistributedCacheIdMismatchCore(string requestedDekId, string observedDekId)
            => this.WriteEvent(5, requestedDekId, observedDekId);
    }
}

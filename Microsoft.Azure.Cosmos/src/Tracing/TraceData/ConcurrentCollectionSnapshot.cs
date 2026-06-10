// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Helper for taking a defensive snapshot of a collection that may be mutated
    /// concurrently by other threads while it is being read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ClientSideRequestStatisticsTraceDatum"/> exposes a few collections
    /// (<c>ContactedReplicas</c>, <c>FailedReplicas</c>, <c>RegionsContacted</c>) as raw
    /// <see cref="List{T}"/> / <see cref="HashSet{T}"/> instances on its public surface
    /// because the <c>IClientSideRequestStatistics</c> contract from the Direct package
    /// requires those exact types. The Direct package mutates them without any locking,
    /// while the diagnostic serializers walk them at the same time — for example when
    /// cross-region read hedging fires off parallel store-reader paths and OpenTelemetry
    /// logs the in-flight diagnostics tree on a different thread.
    /// </para>
    /// <para>
    /// We cannot wrap the writers with a lock because the writers live in a separate
    /// package. Instead, every read site uses <see cref="SnapshotList{T}"/> /
    /// <see cref="SnapshotCollection{T}"/> to copy the collection into a private array
    /// before iterating. The snapshot retries a small number of times if the source
    /// collection is observed to mutate mid-copy; if every attempt fails we fall back to
    /// an empty snapshot so diagnostics serialization can never throw and bubble out as
    /// an <see cref="InvalidOperationException"/> to customer code.
    /// </para>
    /// </remarks>
    internal static class ConcurrentCollectionSnapshot
    {
        private const int MaxAttempts = 5;

        /// <summary>
        /// Returns a defensive snapshot of <paramref name="source"/>.
        /// Safe to call while another thread mutates the underlying collection.
        /// </summary>
        public static IReadOnlyList<T> SnapshotList<T>(IReadOnlyList<T> source)
        {
            if (source == null)
            {
                return Array.Empty<T>();
            }

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                try
                {
                    int count = source.Count;
                    if (count == 0)
                    {
                        return Array.Empty<T>();
                    }

                    T[] buffer = new T[count];
                    int written = 0;
                    for (int i = 0; i < count && i < buffer.Length; i++)
                    {
                        buffer[written++] = source[i];
                    }

                    if (written == buffer.Length)
                    {
                        return buffer;
                    }

                    T[] trimmed = new T[written];
                    Array.Copy(buffer, trimmed, written);
                    return trimmed;
                }
                catch (Exception ex) when (IsTransientConcurrencyException(ex))
                {
                    // The source collection mutated mid-copy. Retry.
                }
            }

            return Array.Empty<T>();
        }

        /// <summary>
        /// Returns a defensive snapshot of <paramref name="source"/> by enumerating it.
        /// Safe to call while another thread mutates the underlying collection.
        /// </summary>
        public static IReadOnlyList<T> SnapshotCollection<T>(IEnumerable<T> source)
        {
            if (source == null)
            {
                return Array.Empty<T>();
            }

            if (source is IReadOnlyList<T> readOnlyList)
            {
                return SnapshotList(readOnlyList);
            }

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                try
                {
                    List<T> snapshot = source is ICollection<T> collection
                        ? new List<T>(collection.Count)
                        : new List<T>();

                    foreach (T item in source)
                    {
                        snapshot.Add(item);
                    }

                    return snapshot;
                }
                catch (Exception ex) when (IsTransientConcurrencyException(ex))
                {
                    // The source collection mutated mid-copy. Retry.
                }
            }

            return Array.Empty<T>();
        }

        private static bool IsTransientConcurrencyException(Exception ex)
        {
            // List<T> / HashSet<T> / Dictionary<,> throw one of these when their backing
            // store mutates during enumeration or CopyTo.
            return ex is InvalidOperationException
                || ex is ArgumentException
                || ex is IndexOutOfRangeException;
        }
    }
}

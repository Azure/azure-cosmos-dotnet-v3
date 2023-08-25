//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// Cache which keeps a copy of the current time formatted in RFC1123 (that is, ToString("r")-style)
    /// available.
    /// </summary>
    internal static class Rfc1123DateTimeCache
    {
        private static FormattedTriple Current = new FormattedTriple(DateTime.UtcNow);
        private static long Timestamp = Stopwatch.GetTimestamp();

        /// <summary>
        /// Approximates DateTime.UtcNow using a cache instance updated once a second.
        /// </summary>
        internal static DateTime Raw() => GetCacheFormattedTriple().Date;

        /// <summary>
        /// Equivalent to DateTime.UtcNow.ToString("r"), but re-uses a cached instance.
        /// </summary>
        internal static string UtcNow() => GetCacheFormattedTriple().Formatted;

        private static FormattedTriple GetCacheFormattedTriple()
        {
            FormattedTriple snapshot = Volatile.Read(ref Current);
            long nowTimestamp = Stopwatch.GetTimestamp();
            long delta = nowTimestamp - Volatile.Read(ref Timestamp);

            // Frequency == ticks per second, so this is equivalent to >= 1s
            if (delta >= Stopwatch.Frequency)
            {
                FormattedTriple candidate = new FormattedTriple(DateTime.UtcNow);
                FormattedTriple updatedSnapshot = Interlocked.CompareExchange(ref Current, candidate, snapshot);
                if (updatedSnapshot == snapshot)
                {
                    Volatile.Write(ref Timestamp, nowTimestamp);
                    // this means we replaced Current, so return our new one
                    return candidate;
                }
                // we failed to replace Current, because somebody else did - return what they shoved up there
                return updatedSnapshot;
            }
            else
            {
                // current cache value is still good
                return snapshot;
            }
        }

        /// <summary>
        /// Triple of a DateTime, it's RFC1123 equivalent, and it's .ToLowerInvariant() RFC1123 equivalent.
        /// </summary>
        private sealed class FormattedTriple
        {
            internal string Formatted { get; }
            internal DateTime Date { get; }
            internal FormattedTriple(DateTime date)
            {
                this.Date = date;
                this.Formatted = date.ToString("r");
            }
        }
    }
}

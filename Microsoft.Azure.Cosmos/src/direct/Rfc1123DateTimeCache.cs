//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Threading;

    /// <summary>
    /// Cache which keeps a copy of the current time formatted in RFC1123 (that is, ToString("r")-style)
    /// available.
    /// </summary>
    internal static class Rfc1123DateTimeCache
    {
#pragma warning disable CA1823 // Remove unread private members, this is needed to keep the Timer alive
        private static readonly Timer Timer =
            new Timer(
                callback: static _ => Value = DateTime.UtcNow.ToString("r"),
                state: null,
                dueTime: TimeSpan.Zero,
                period: TimeSpan.FromSeconds(1));
#pragma warning restore CA1823 // Remove unread private members

        private static string Value = DateTime.UtcNow.ToString("r");

        /// <summary>
        /// Equivalent to DateTime.UtcNow.ToString("r"), but re-uses a cached instance.
        /// 
        /// This is updated approximately once a second using a <see cref="Timer"/>.
        /// </summary>
        internal static string UtcNow() => Value;
    }
}

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Linq;

    internal sealed class MemoryLoadHistory
    {
        private readonly TimeSpan monitoringInterval;

        public ReadOnlyCollection<MemoryLoad> MemoryLoad { get; }
        private readonly Lazy<string> memoryLoadHistory;

        public MemoryLoadHistory(ReadOnlyCollection<MemoryLoad> memoryLoad, TimeSpan monitoringInterval)
        {
            this.MemoryLoad = memoryLoad ?? throw new ArgumentNullException(nameof(memoryLoad));
            this.memoryLoadHistory = new Lazy<string>(() => { return this.MemoryLoad?.Count == 0 ? "empty" : string.Join(", ", this.MemoryLoad); });
            if (monitoringInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monitoringInterval),
                    monitoringInterval,
                    string.Format("{0} must be strictly positive", nameof(monitoringInterval)));
            }
            this.monitoringInterval = monitoringInterval;
        }

        // Currently used only for unit tests.
        internal DateTime LastTimestamp { get { return this.MemoryLoad.Last().Timestamp; } }

        public override string ToString()
        {
            return this.memoryLoadHistory.Value;
        }
    }
}
//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// Testable wrapper for CosmosDiagnosticsContext that records scope information for test verification.
    /// </summary>
    internal class TestableCosmosDiagnosticsContext
    {
        private readonly CosmosDiagnosticsContext inner;
        private readonly List<ScopeRecord> records = new List<ScopeRecord>(4);
        private readonly object recordsLock = new object();
        private int nextScopeId = 0;
        private readonly HashSet<int> disposedScopeIds = new HashSet<int>();

        public TestableCosmosDiagnosticsContext()
        {
            this.inner = CosmosDiagnosticsContext.Create(null);
        }

        /// <summary>
        /// Recorded scope metadata.
        /// </summary>
        public readonly struct ScopeRecord
        {
            public ScopeRecord(string name, long startTimestamp, long elapsedTicks)
            {
                this.Name = name;
                this.StartTimestamp = startTimestamp;
                this.ElapsedTicks = elapsedTicks;
            }

            public string Name { get; }

            public long StartTimestamp { get; }

            public long ElapsedTicks { get; }

            public TimeSpan Elapsed => TimeSpan.FromTicks(this.ElapsedTicks);
        }

        /// <summary>
        /// Gets recorded scope names for test verification.
        /// </summary>
        public IReadOnlyList<string> Scopes
        {
            get
            {
                lock (this.recordsLock)
                {
                    if (this.records.Count == 0)
                    {
                        return Array.Empty<string>();
                    }

                    string[] names = new string[this.records.Count];
                    for (int i = 0; i < this.records.Count; i++)
                    {
                        names[i] = this.records[i].Name;
                    }

                    return names;
                }
            }
        }

        /// <summary>
        /// Creates a testable scope that records timing information.
        /// </summary>
        public TestableScope CreateScope(string scope)
        {
            int scopeId = System.Threading.Interlocked.Increment(ref this.nextScopeId);
            CosmosDiagnosticsContext.Scope innerScope = this.inner.CreateScope(scope);
            return new TestableScope(this, scope, innerScope, scopeId);
        }

        private bool TryMarkDisposed(int scopeId)
        {
            lock (this.recordsLock)
            {
                return this.disposedScopeIds.Add(scopeId);
            }
        }

        private void Record(string name, long startTicks, long elapsedTicks)
        {
            lock (this.recordsLock)
            {
                this.records.Add(new ScopeRecord(name, startTicks, elapsedTicks));
            }
        }

        /// <summary>
        /// Testable scope wrapper that records timing information for tests.
        /// </summary>
        public readonly struct TestableScope : IDisposable
        {
            private readonly TestableCosmosDiagnosticsContext owner;
            private readonly string name;
            private readonly long startTicks;
            private readonly CosmosDiagnosticsContext.Scope innerScope;
            private readonly int scopeId;

            internal TestableScope(TestableCosmosDiagnosticsContext owner, string name, CosmosDiagnosticsContext.Scope innerScope, int scopeId)
            {
                this.owner = owner;
                this.name = name;
                this.startTicks = Stopwatch.GetTimestamp();
                this.innerScope = innerScope;
                this.scopeId = scopeId;
            }

            public void Dispose()
            {
                // Only record the first dispose call (idempotent)
                if (this.owner.TryMarkDisposed(this.scopeId))
                {
                    long elapsedTicks = Stopwatch.GetTimestamp() - this.startTicks;
                    this.owner.Record(this.name, this.startTicks, elapsedTicks);
                }
                
                this.innerScope.Dispose();
            }
        }
    }
}

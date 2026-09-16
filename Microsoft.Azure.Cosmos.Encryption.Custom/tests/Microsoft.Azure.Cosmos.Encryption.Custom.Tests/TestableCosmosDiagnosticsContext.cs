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

        public TestableCosmosDiagnosticsContext()
        {
            this.inner = CosmosDiagnosticsContext.Create(null);
        }

        /// <summary>
        /// Gets the inner CosmosDiagnosticsContext for passing to APIs.
        /// </summary>
        public CosmosDiagnosticsContext Inner => this.inner;

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
            CosmosDiagnosticsContext.Scope innerScope = this.inner.CreateScope(scope);
            return new TestableScope(this, scope, innerScope);
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
        public sealed class TestableScope : IDisposable
        {
            private readonly TestableCosmosDiagnosticsContext owner;
            private readonly string name;
            private readonly long startTicks;
            private readonly CosmosDiagnosticsContext.Scope innerScope;
            private bool isDisposed;

            internal TestableScope(TestableCosmosDiagnosticsContext owner, string name, CosmosDiagnosticsContext.Scope innerScope)
            {
                this.owner = owner;
                this.name = name;
                this.startTicks = Stopwatch.GetTimestamp();
                this.innerScope = innerScope;
            }

            public void Dispose()
            {
                // Only record the first dispose call (idempotent)
                if (!this.isDisposed)
                {
                    this.isDisposed = true;
                    long elapsedTicks = Stopwatch.GetTimestamp() - this.startTicks;
                    this.owner.Record(this.name, this.startTicks, elapsedTicks);
                    this.innerScope.Dispose();
                }
            }
        }
    }
}

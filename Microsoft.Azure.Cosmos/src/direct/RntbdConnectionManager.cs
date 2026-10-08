//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.FaultInjection;

    /// <summary>
    /// Per-endpoint connection manager that decouples connection
    /// establishment from request dispatch. Owned by
    /// <see cref="CMLoadBalancingPartition"/> on the connection-manager
    /// parallel path (activated by the CMTransportClient fork).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Versioned-slot producer/consumer design:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Fixed array of <see cref="ConnectionEntry"/>
    ///   (<c>MaxChannels</c> entries). Each entry has a lifecycle state
    ///   (<see cref="ConnectionEntryState.Closed"/> → <c>Opening</c> →
    ///   <c>Open</c>) and a monotonically-increasing <c>Version</c> bumped
    ///   on every Close transition.</description></item>
    ///   <item><description>An <see cref="ISlotQueue{Slot}"/> carries
    ///   slot tokens (one per dispatchable request slot). Each token
    ///   stamps the entry's <c>Version</c> at write time; readers detect
    ///   stale tokens by comparing the stamp against the current entry
    ///   version and skip them.</description></item>
    ///   <item><description>Single in-flight open at a time across the
    ///   manager, gated by an <c>isConnectionOpenPending</c> flag under
    ///   <see cref="connectionsLock"/>. Same pattern as <c>Proxy</c> and
    ///   <c>HttpClient</c> (HTTP/2 connection pool).</description></item>
    ///   <item><description>Open failures are NOT observed by acquirers.
    ///   A failed open produces no slot; the open attempt runs to its
    ///   natural success or failure (the manager imposes no open timeout).
    ///   Each acquirer keeps waiting for a slot, bounded only by the
    ///   caller's cancellation token — any open-timeout policy belongs to
    ///   the requester. The opener does not retry on its own; new acquirers
    ///   arriving after a failure trigger a fresh open attempt.
    ///   <see cref="WarmAsync"/> is the one path that still observes the
    ///   open task directly.</description></item>
    /// </list>
    /// <para>
    /// Stale slots disappear naturally — closing an entry bumps its version,
    /// so any in-flight tokens with the old version are filtered out by
    /// <see cref="TryGetActiveConnectionFromSlot"/>. A channel that dies
    /// mid-life (after a successful open) is recycled at acquire time by
    /// <see cref="RecycleIfUnhealthy"/>: the entry is closed (version bumped)
    /// and re-opened, so a dead connection never permanently occupies a spot
    /// in the finite <see cref="connections"/> array. No phantom-debt
    /// accounting needed.
    /// </para>
    /// </remarks>
    internal sealed class RntbdConnectionManager : IDisposable
    {
        private readonly Uri serverUri;
        private readonly string serverEndpointDisplay;
        private readonly ChannelProperties channelProperties;
        private readonly ConnectionManagerOptions options;
        private readonly bool localRegionRequest;
        private readonly IChaosInterceptor chaosInterceptor;
        private readonly IRntbdConnectionManagerTelemetry telemetry;

        // Gates emission of the per-acquire connection-acquisition rows
        // (RntbdConnectionManagerConnectionAcquisitionEvent). Sourced from a
        // resolver threaded down from CMTransportClient so the manager never
        // depends on the (now pristine) ChannelProperties for feature flags.
        private readonly Func<bool> debugLogsResolver;

        private readonly DateTime createdAtUtc;
        private readonly ConnectionEntry[] connections;

        // Protects connection entry transitions and the
        // isConnectionOpenPending flag. Held only briefly; never held
        // across awaits.
        private readonly object connectionsLock = new object();

        // Periodic heartbeat that emits a PoolSnapshot event with the
        // current pool composition and lifetime counters. Lets ops see
        // "current state per endpoint" in Kusto / log files without
        // having to reconstruct it from per-event deltas.
        //
        // The emit cadence is sourced live from snapshotIntervalSecondsResolver
        // (host/tenant config) so the knob applies to already-alive, long-lived
        // managers, not just newly-created ones. A resolved value <= 0 disables
        // emission. The timer is a self-rescheduling one-shot: it wakes at most
        // every SnapshotPollInterval to re-read the config (so a runtime enable
        // takes effect within that bound), and when enabled reschedules at the
        // configured interval. The timer is not created at all when no resolver
        // is wired (e.g. SDK / Direct callers), so those paths keep zero
        // snapshot overhead. Disposed in Dispose.
        private static readonly TimeSpan SnapshotPollInterval = TimeSpan.FromSeconds(600);

        // Resolves the PoolSnapshot emit interval in seconds (<= 0 disables).
        // Null when no caller wired the knob, in which case no snapshot timer
        // is created.
        private readonly Func<int> snapshotIntervalSecondsResolver;
        private readonly ISlotQueue<Slot> requestSlots = new SlotQueue<Slot>();

        private long lifetimeAcquires;
        private long lifetimeOpens;
        private long outstandingAcquires;
        private Timer snapshotTimer;

        // True iff an opener task is currently in flight. Gates
        // EnsureConnectionOpening so only one open runs at a time. Read
        // and written only under connectionsLock, together with
        // currentOpenTask, so the two always present a consistent snapshot.
        private bool isConnectionOpenPending;

        // Reference to the currently-pending opener task. Set under
        // connectionsLock alongside isConnectionOpenPending; cleared by the
        // opener's finally (also under connectionsLock) before it transitions
        // isConnectionOpenPending back to false. Consumed by WarmAsync so
        // pre-warm / replica-validation callers can await the open. Acquirers
        // do not observe this task — they wait for a slot bounded only by the
        // caller's cancellation token.
        private Task currentOpenTask;

        private int acceptsNewAcquires = 1;
        private int disposed;

        public RntbdConnectionManager(
            Uri serverUri,
            ChannelProperties channelProperties,
            bool localRegionRequest,
            IChaosInterceptor chaosInterceptor = null,
            IRntbdConnectionManagerTelemetry telemetry = null,
            Func<bool> debugLogsResolver = null,
            Func<int> snapshotIntervalSecondsResolver = null)
        {
            if (serverUri == null)
            {
                throw new ArgumentNullException(nameof(serverUri));
            }

            if (channelProperties == null)
            {
                throw new ArgumentNullException(nameof(channelProperties));
            }

            this.serverUri = serverUri;
            this.serverEndpointDisplay = serverUri.ToString();
            this.channelProperties = channelProperties;
            this.localRegionRequest = localRegionRequest;
            this.chaosInterceptor = chaosInterceptor;
            this.telemetry = telemetry ?? DefaultTraceConnectionManagerTelemetry.Instance;
            this.debugLogsResolver = debugLogsResolver;
            this.snapshotIntervalSecondsResolver = snapshotIntervalSecondsResolver;
            this.options = ConnectionManagerOptions.FromChannelProperties(channelProperties);

            this.connections = new ConnectionEntry[this.options.MaxChannels];
            for (int i = 0; i < this.connections.Length; i++)
            {
                this.connections[i] = new ConnectionEntry(i);
            }

            this.ConnectionManagerId = Guid.NewGuid();
            this.createdAtUtc = DateTime.UtcNow;

            this.telemetry.LogEvent(RntbdConnectionManagerEvent.ManagerCreated(
                this.ConnectionManagerId,
                this.serverEndpointDisplay,
                this.options.MaxChannels,
                this.options.MaxRequestsPerChannel));

            if (this.snapshotIntervalSecondsResolver != null)
            {
                // Self-rescheduling one-shot timer (period = Infinite). Each
                // tick re-reads the configured interval so the cadence tracks
                // runtime config changes on this long-lived manager. The first
                // tick fires after SnapshotPollInterval.
                this.snapshotTimer = new Timer(
                    state => ((RntbdConnectionManager)state).OnSnapshotTimer(),
                    state: this,
                    dueTime: SnapshotPollInterval,
                    period: Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Stable identifier for this manager instance.
        /// </summary>
        public Guid ConnectionManagerId { get; }

        internal bool AcceptsNewAcquires => Volatile.Read(ref this.acceptsNewAcquires) != 0;

        internal bool Disposed => this.IsDisposed();

        internal long OutstandingAcquires => Interlocked.Read(ref this.outstandingAcquires);

        internal void StopAcceptingNewAcquires()
        {
            Interlocked.Exchange(ref this.acceptsNewAcquires, 0);
        }

        /// <summary>
        /// Result of <see cref="AcquireAsync"/>. Carries the
        /// <see cref="IChannel"/> the caller dispatches the request on.
        /// Disposing returns the slot to the pool exactly once; callers
        /// should wrap the acquire in a <c>using</c> block.
        /// </summary>
        internal sealed class AcquiredConnection : IDisposable
        {
            private readonly RntbdConnectionManager owner;
            private readonly Slot slot;
            private int disposed;

            internal AcquiredConnection(IChannel channel, Slot slot, RntbdConnectionManager owner)
            {
                this.Channel = channel;
                this.slot = slot;
                this.owner = owner;
            }

            public IChannel Channel { get; }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref this.disposed, 1) != 0)
                {
                    return;
                }

                try
                {
                    this.owner.ReturnSlot(this.slot);
                }
                finally
                {
                    Interlocked.Decrement(ref this.owner.outstandingAcquires);
                }
            }
        }

        /// <summary>
        /// Acquire a request slot on an open connection. Caller MUST
        /// dispose the returned <see cref="AcquiredConnection"/> exactly
        /// once (use a <c>using</c> block) so the slot is returned to
        /// the pool.
        /// </summary>
        public async Task<AcquiredConnection> AcquireAsync(
            Guid activityId,
            CancellationToken cancellationToken)
        {
            // Throwing wrapper retained for callers and tests that rely on the
            // exception contract (and for WarmAsync's sibling guards). The
            // exceptionless core lives in TryAcquireAsync; ValueOrThrow rethrows
            // preserving the original stack.
            Res<AcquiredConnection> result =
                await this.TryAcquireAsync(activityId, cancellationToken).ConfigureAwait(false);
            return result.ValueOrThrow();
        }

        /// <summary>
        /// Exceptionless variant of <see cref="AcquireAsync"/>. Returns a
        /// <see cref="Res{T}"/> carrying either the acquired connection or the
        /// exception <see cref="AcquireAsync"/> would have thrown (manager
        /// disposed, caller cancelled, or open failure). Lets the
        /// connection-manager request path stay exceptionless end-to-end,
        /// mirroring the legacy <c>LoadBalancingPartition.TryRequestAsync</c>
        /// shape so <see cref="CMLoadBalancingPartition"/> needs no
        /// acquire-site try/catch.
        /// </summary>
        internal async Task<Res<AcquiredConnection>> TryAcquireAsync(
            Guid activityId,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException guardException =
                this.GetDisposedException() ?? this.GetNotAcceptingAcquiresException();
            if (guardException != null)
            {
                return Res.FromException<AcquiredConnection>(guardException);
            }

            Interlocked.Increment(ref this.lifetimeAcquires);

            ValueStopwatch acquireSw = ValueStopwatch.StartNew();
            bool joinedExistingOpener = false;
            bool triggeredOpen = false;
            int ensureOpenCalls = 0;
            int staleSlotsConsumed = 0;
            PoolState poolStateAtEntry = this.requestSlots.TryPeek(out _) ? PoolState.Open : PoolState.Empty;

            // The manager imposes NO open timeout. It waits for a slot to
            // become available, bounded only by the caller's cancellation
            // token, and never observes an opener's exception. The open
            // attempt runs to its natural success or failure; a failed open
            // simply produces no slot, so the acquire keeps waiting until a
            // connection succeeds or the caller (the requester, which owns any
            // open-timeout policy) cancels.
            while (true)
            {
                // Enqueue the wait FIRST, then trigger an open if the
                // task wasn't already completed by an item in the queue.
                // Order matters: kicking EnsureConnectionOpening before
                // joining the queue would race with a fast opener that
                // completed between our kick and our wait — we could end
                // up waiting on a queue with no producer in flight.
                Task<Slot> slotTask;
                try
                {
                    slotTask = this.requestSlots.ReadAsync(cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    // Queue completed by Dispose.
                    this.EmitAcquireFailure(
                        activityId, acquireSw.ElapsedMilliseconds, poolStateAtEntry,
                        joinedExistingOpener, triggeredOpen, ensureOpenCalls,
                        staleSlotsConsumed,
                        AcquireOutcome.ManagerDisposed,
                        errorCode: null);
                    return Res.FromException<AcquiredConnection>(
                        new ObjectDisposedException(nameof(RntbdConnectionManager)));
                }

                if (!slotTask.IsCompleted)
                {
                    OpenJoinResult attempt = this.EnsureConnectionOpening();
                    switch (attempt.Kind)
                    {
                        case OpenJoinKind.Kicked: triggeredOpen = true; break;
                        case OpenJoinKind.Joined: joinedExistingOpener = true; break;
                    }
                    ensureOpenCalls++;
                }

                Slot slot;
                try
                {
                    // The acquirer does NOT observe the opener's exception. A
                    // failed open simply produces no slot; this acquire keeps
                    // waiting for a slot (from this or a subsequent open, or a
                    // returned slot) until a connection succeeds or the caller
                    // cancels. A stale slot (handled below) loops back and
                    // kicks a fresh open.
                    slot = await slotTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException oce)
                {
                    this.EmitAcquireFailure(
                        activityId, acquireSw.ElapsedMilliseconds, poolStateAtEntry,
                        joinedExistingOpener, triggeredOpen, ensureOpenCalls,
                        staleSlotsConsumed,
                        AcquireOutcome.CallerCancelled,
                        errorCode: null);
                    return Res.FromException<AcquiredConnection>(oce);
                }
                catch (InvalidOperationException)
                {
                    // Queue completed by Dispose while we were waiting.
                    this.EmitAcquireFailure(
                        activityId, acquireSw.ElapsedMilliseconds, poolStateAtEntry,
                        joinedExistingOpener, triggeredOpen, ensureOpenCalls,
                        staleSlotsConsumed,
                        AcquireOutcome.ManagerDisposed,
                        errorCode: null);
                    return Res.FromException<AcquiredConnection>(
                        new ObjectDisposedException(nameof(RntbdConnectionManager)));
                }

                IChannel conn = this.TryGetActiveConnectionFromSlot(slot);
                if (conn != null)
                {
                    // We claimed the last available request slot. There
                    // may or may not still be a waiter, but either way
                    // preemptively open another connection if we can —
                    // this doubly-solves any acquire-then-open race and
                    // keeps the pool warm under sustained load. Worst
                    // case we open a connection we don't end up needing.
                    if (!this.requestSlots.TryPeek(out _))
                    {
                        if (this.EnsureConnectionOpening().Kind == OpenJoinKind.Kicked)
                        {
                            triggeredOpen = true;
                        }
                        ensureOpenCalls++;
                    }
                    this.EmitAcquireSuccess(
                        activityId, acquireSw.ElapsedMilliseconds, poolStateAtEntry,
                        joinedExistingOpener, triggeredOpen, ensureOpenCalls,
                        staleSlotsConsumed);
                    Interlocked.Increment(ref this.outstandingAcquires);
                    return Res.Success(new AcquiredConnection(conn, slot, this));
                }

                // No active connection for this slot. If the entry is still
                // Open but its channel died mid-life, recycle it back to Closed
                // so it can be re-opened — otherwise it would hold a permanent
                // spot in the finite connections array and the pool would
                // eventually exhaust. (On a version mismatch the token is just
                // stale and RecycleIfUnhealthy no-ops.)
                this.RecycleIfUnhealthy(slot);

                // Stale slot consumed — loop back; the next ReadAsync's
                // IsCompleted check will kick a fresh EnsureConnectionOpening
                // if the queue is empty.
                staleSlotsConsumed++;
            }
        }

        /// <summary>
        /// Internal callback from <see cref="AcquiredConnection.Dispose"/>.
        /// Returns a slot acquired via <see cref="AcquireAsync"/> to the
        /// pool. If the underlying connection has been closed since
        /// acquisition (version mismatch), the slot is dropped on the
        /// floor — it's stale and would just be filtered by future readers.
        /// </summary>
        private void ReturnSlot(Slot slot)
        {
            ConnectionEntry entry = this.connections[slot.Index];
            // Read entry.Version without the lock. Versions are bumped
            // under the lock but reads are racy-safe because Slot.Version
            // is monotonic — if Volatile.Read returns the version that
            // matches our slot, the connection identity hasn't rotated.
            // If it returns a higher version, our slot is stale.
            if (Volatile.Read(ref entry.Version) == slot.Version)
            {
                this.requestSlots.Write(slot);
            }
        }

        /// <summary>
        /// Slot identity passed through the request-slot queue. Carries
        /// the entry index and the version stamp from the open that
        /// produced it. Readers compare the stamp against the entry's
        /// current Version to detect stale tokens.
        /// </summary>
        internal readonly struct Slot : IEquatable<Slot>
        {
            public Slot(int index, long version)
            {
                this.Index = index;
                this.Version = version;
            }
            public int Index { get; }
            public long Version { get; }
            public bool Equals(Slot other) => this.Index == other.Index && this.Version == other.Version;
            public override bool Equals(object obj) => obj is Slot s && this.Equals(s);
            public override int GetHashCode() => (this.Index * 397) ^ this.Version.GetHashCode();
        }

        /// <summary>
        /// Outcome of an <see cref="EnsureConnectionOpening"/> call. Carries
        /// the in-flight open task (if any) so the caller can race it
        /// against its slot wait and surface open failures.
        /// </summary>
        internal readonly struct OpenJoinResult
        {
            public OpenJoinResult(OpenJoinKind kind, Task pendingOpen)
            {
                this.Kind = kind;
                this.PendingOpen = pendingOpen;
            }
            /// <summary>Kicked / Joined / NoOp — see <see cref="OpenJoinKind"/>.</summary>
            public OpenJoinKind Kind { get; }
            /// <summary>
            /// Snapshot of the currently-pending opener task. Non-null when
            /// <see cref="Kind"/> is <c>Kicked</c> or <c>Joined</c>. Null
            /// when <c>NoOp</c> (manager disposed or all entries already
            /// non-Closed and no in-flight open).
            /// </summary>
            public Task PendingOpen { get; }
        }

        /// <summary>
        /// Outcome categories for <see cref="EnsureConnectionOpening"/>.
        /// </summary>
        internal enum OpenJoinKind
        {
            /// <summary>This call kicked a new opener task.</summary>
            Kicked,
            /// <summary>An opener was already in flight; this call joined it.</summary>
            Joined,
            /// <summary>Nothing to do — manager disposed or no Closed entry to fill.</summary>
            NoOp,
        }

        // Cached completed task — Task.CompletedTask isn't available on
        // net45 (added in .NET 4.6).
        private static readonly Task CompletedTask = Task.FromResult(0);

        /// <summary>
        /// Pre-warm / replica-validation entry point. Joins or starts an
        /// open and returns a task that completes when that open resolves
        /// (success or failure). If no open is needed — e.g. all entries
        /// are already Open or Opening with no Closed slot available —
        /// returns a completed task immediately so callers don't block on
        /// a no-op.
        /// </summary>
        /// <remarks>
        /// Matches the legacy <c>LoadBalancingPartition.OpenChannelAsync</c>
        /// contract: the returned task is awaitable for "warmup is done".
        /// Replica-validation callers (e.g. <c>RntbdOpenConnectionHandler</c>)
        /// rely on this to gate downstream traffic.
        /// </remarks>
        public Task WarmAsync(Guid activityId)
        {
            // Exceptionless, best-effort warmup: nothing to warm on a disposed
            // or draining manager, so return a completed no-op task rather than
            // throwing. Mirrors the legacy OpenChannelAsync contract, whose
            // guard failures are caught and swallowed by the replica-validation
            // (RntbdOpenConnectionHandler) caller anyway.
            if (this.IsDisposed() || !this.AcceptsNewAcquires)
            {
                return RntbdConnectionManager.CompletedTask;
            }

            OpenJoinResult attempt = this.EnsureConnectionOpening();

            this.telemetry.LogEvent(RntbdConnectionManagerEvent.PreWarmTriggered(
                this.ConnectionManagerId, this.serverEndpointDisplay,
                OpenTrigger.PreWarm,
                targetSlotIndex: null,
                activityId: activityId));

            return attempt.PendingOpen ?? RntbdConnectionManager.CompletedTask;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) == 1)
            {
                return;
            }

            try
            {
                this.snapshotTimer?.Dispose();
            }
            catch
            { /* best-effort */
            }

            // Complete the queue first so any blocked ReadAsync calls
            // observe the disposal.
            try
            {
                this.requestSlots.Complete();
            }
            catch
            { /* best-effort */
            }

            lock (this.connectionsLock)
            {
                for (int i = 0; i < this.connections.Length; i++)
                {
                    this.CloseEntryLocked(i, SlotFreeReason.Disposal);
                }
            }

            try
            {
                long lifetimeMs = (long)(DateTime.UtcNow - this.createdAtUtc).TotalMilliseconds;
                this.telemetry.LogEvent(RntbdConnectionManagerEvent.ManagerDisposed(
                    this.ConnectionManagerId, this.serverEndpointDisplay,
                    lifetimeMs,
                    Interlocked.Read(ref this.lifetimeAcquires),
                    Interlocked.Read(ref this.lifetimeOpens)));
            }
            catch
            {
                // Telemetry must never throw out of Dispose.
            }
        }

        /// <summary>
        /// Timer callback. Re-reads the configured snapshot interval, emits a
        /// PoolSnapshot when enabled (&gt; 0 seconds), then reschedules the next
        /// tick. When disabled it reschedules at <see cref="SnapshotPollInterval"/>
        /// so a runtime enable is picked up within that bound. Never allowed to
        /// throw out of the timer.
        /// </summary>
        private void OnSnapshotTimer()
        {
            if (this.IsDisposed())
            {
                return;
            }

            TimeSpan nextDelay = SnapshotPollInterval;
            try
            {
                int intervalSeconds = this.ResolveSnapshotIntervalSeconds();
                if (intervalSeconds > 0)
                {
                    this.EmitPoolSnapshot();
                    nextDelay = TimeSpan.FromSeconds(intervalSeconds);
                }
            }
            catch
            {
                // Telemetry / config must never kill the heartbeat timer; fall
                // back to the poll cadence and try again on the next tick.
            }
            finally
            {
                try
                {
                    this.snapshotTimer?.Change(nextDelay, Timeout.InfiniteTimeSpan);
                }
                catch (ObjectDisposedException)
                {
                    // Raced with Dispose; nothing left to reschedule.
                }
            }
        }

        /// <summary>
        /// Resolves the configured PoolSnapshot emit interval in seconds. A
        /// value &lt;= 0 (or a throwing / unwired resolver) disables emission.
        /// </summary>
        private int ResolveSnapshotIntervalSeconds()
        {
            if (this.snapshotIntervalSecondsResolver == null)
            {
                return 0;
            }

            try
            {
                return this.snapshotIntervalSecondsResolver();
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning(
                    "RntbdConnectionManager snapshotIntervalSecondsResolver threw; treating as disabled: {0}",
                    ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Snapshot current pool composition + lifetime counters and emit a
        /// <see cref="RntbdConnectionManagerEventType.PoolSnapshot"/> row.
        /// Invoked by <see cref="OnSnapshotTimer"/> when snapshots are enabled.
        /// Skipped if disposed or if the telemetry call throws (telemetry must
        /// never crash the timer).
        /// </summary>
        private void EmitPoolSnapshot()
        {
            if (this.IsDisposed())
            {
                return;
            }

            int openCount = 0;
            int openingCount = 0;
            int closedCount = 0;
            lock (this.connectionsLock)
            {
                for (int i = 0; i < this.connections.Length; i++)
                {
                    switch (this.connections[i].State)
                    {
                        case ConnectionEntryState.Open: openCount++; break;
                        case ConnectionEntryState.Opening: openingCount++; break;
                        case ConnectionEntryState.Closed: closedCount++; break;
                    }
                }
            }

            int slotsAvailable = this.requestSlots.TryPeek(out _) ? 1 : 0;
            long lifetimeAcquiresSnap = Interlocked.Read(ref this.lifetimeAcquires);
            long lifetimeOpensSnap = Interlocked.Read(ref this.lifetimeOpens);

            try
            {
                this.telemetry.LogEvent(RntbdConnectionManagerEvent.PoolSnapshot(
                    this.ConnectionManagerId, this.serverEndpointDisplay,
                    openCount, openingCount, closedCount, slotsAvailable,
                    lifetimeAcquiresSnap, lifetimeOpensSnap));
            }
            catch
            {
                // Telemetry must never crash the heartbeat timer.
            }
        }

        private IChannel TryGetActiveConnectionFromSlot(Slot slot)
        {
            if (slot.Index < 0 || slot.Index >= this.connections.Length)
            {
                return null;
            }
            ConnectionEntry entry = this.connections[slot.Index];
            if (Volatile.Read(ref entry.Version) != slot.Version)
            {
                return null;
            }
            IChannel channel = entry.Channel;
            if (channel == null || !channel.Healthy)
            {
                return null;
            }
            return channel;
        }

        /// <summary>
        /// Recycle an entry whose channel died mid-life. If the slot's entry
        /// is still <see cref="ConnectionEntryState.Open"/> but its channel is
        /// no longer healthy, transition it back to
        /// <see cref="ConnectionEntryState.Closed"/> (which bumps the version,
        /// invalidating this entry's outstanding slot tokens, closes the dead
        /// channel, and makes the entry eligible for a fresh open on the next
        /// <see cref="EnsureConnectionOpening"/>).
        /// </summary>
        /// <remarks>
        /// Without this, a connection that disconnects after opening would
        /// never leave the Open state: <see cref="PickConnectionToOpenLocked"/>
        /// only re-opens Closed entries, so the dead entry would hold a
        /// permanent spot in the finite <see cref="connections"/> array. Enough
        /// mid-life disconnects would exhaust the array and wedge the pool in a
        /// permanently failed state. Called from the acquire loop whenever a
        /// consumed slot yields no active connection.
        /// </remarks>
        private void RecycleIfUnhealthy(Slot slot)
        {
            if (slot.Index < 0 || slot.Index >= this.connections.Length)
            {
                return;
            }

            ConnectionEntry entry = this.connections[slot.Index];

            // Fast lock-free skip: a version mismatch means the entry was
            // already recycled/closed and this token is simply stale.
            if (Volatile.Read(ref entry.Version) != slot.Version)
            {
                return;
            }

            lock (this.connectionsLock)
            {
                // Re-check under the lock — version, state, and channel are all
                // mutated here, so another acquirer may have recycled it first.
                if (Volatile.Read(ref entry.Version) != slot.Version)
                {
                    return;
                }
                if (entry.State != ConnectionEntryState.Open)
                {
                    return;
                }
                IChannel channel = entry.Channel;
                if (channel != null && channel.Healthy)
                {
                    // Raced with a health flap or another observer — leave it.
                    return;
                }
                this.CloseEntryLocked(slot.Index, SlotFreeReason.Unhealthy);
            }
        }

        // ----------------------------------------------------------------
        // Open coordination
        // ----------------------------------------------------------------

        /// <summary>
        /// Kick a new open if one isn't already in flight and there is a
        /// Closed entry available. The returned <see cref="OpenJoinResult"/>
        /// carries the currently-pending open task (whether started by this
        /// call or joined from a prior call). <see cref="WarmAsync"/> awaits
        /// this task; the acquire path ignores it and waits for a slot
        /// bounded only by the caller's cancellation token.
        /// </summary>
        /// <remarks>
        /// All state mutations and the read of <c>currentOpenTask</c> happen
        /// under <see cref="connectionsLock"/>; the lock is the single
        /// barrier coordinating opener start with the opener's own finally
        /// that clears the flag and the task reference.
        /// </remarks>
        private OpenJoinResult EnsureConnectionOpening()
        {
            if (this.IsDisposed())
            {
                return new OpenJoinResult(OpenJoinKind.NoOp, null);
            }

            // Both isConnectionOpenPending and currentOpenTask are read and
            // mutated only under connectionsLock. Reading them together under
            // the lock yields a consistent snapshot: a pending=true
            // observation is always paired with the matching non-null task,
            // and the opener's finally (which clears both fields under the
            // same lock) can never interleave between the two reads.
            int chosenIndex;
            Task opener;
            lock (this.connectionsLock)
            {
                if (this.isConnectionOpenPending)
                {
                    return new OpenJoinResult(OpenJoinKind.Joined, this.currentOpenTask);
                }
                chosenIndex = this.PickConnectionToOpenLocked();
                if (chosenIndex == -1)
                {
                    return new OpenJoinResult(OpenJoinKind.NoOp, null);
                }
                this.connections[chosenIndex].SetStateLocked(ConnectionEntryState.Opening);

                opener = Task.Run(() => this.OpenAndInjectAsync(chosenIndex));
                // Mark exceptions observed so any failure not raced by an
                // acquirer (e.g. WarmAsync path) does not raise
                // UnobservedTaskException.
                Task ignored = opener.ContinueWith(
                    t => { _ = t.Exception; },
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

                this.currentOpenTask = opener;
                this.isConnectionOpenPending = true;
            }
            return new OpenJoinResult(OpenJoinKind.Kicked, opener);
        }

        /// <summary>
        /// Scan for a Closed entry. Caller must hold
        /// <see cref="connectionsLock"/>. Returns -1 if no Closed entry
        /// exists (all entries are Opening or Open — i.e., the maximum
        /// number of connections is already established or being established).
        /// </summary>
        private int PickConnectionToOpenLocked()
        {
            Debug.Assert(Monitor.IsEntered(this.connectionsLock));
            for (int i = 0; i < this.connections.Length; i++)
            {
                if (this.connections[i].State == ConnectionEntryState.Closed)
                {
                    return i;
                }
            }
            return -1;
        }

        private async Task OpenAndInjectAsync(int index)
        {
            try
            {
                await this.OpenConnectionAsync(index).ConfigureAwait(false);
            }
            // OpenConnectionAsync is exceptionless on disposal (it returns
            // quietly), so no ObjectDisposedException reaches here. All other
            // exceptions propagate to the wrapping Task so any acquirer racing
            // this task via Task.WhenAny observes the failure and re-throws to
            // its caller. The ContinueWith hook installed in
            // EnsureConnectionOpening prevents the exception from surfacing as
            // UnobservedTaskException if no acquirer is currently racing.
            finally
            {
                lock (this.connectionsLock)
                {
                    // Clear both fields under connectionsLock so callers in
                    // EnsureConnectionOpening only ever observe a consistent
                    // (pending, task) pair: either an in-flight open with its
                    // non-null task, or no open pending with a null task.
                    this.currentOpenTask = null;
                    this.isConnectionOpenPending = false;
                }
            }
        }

        /// <summary>
        /// Single-attempt open. On failure, the exception propagates to the
        /// caller (the wrapping Task) so any acquirer racing this open via
        /// Task.WhenAny sees it. Retry policy lives at the request layer
        /// above the transport client, mirroring the legacy path.
        /// </summary>
        private async Task OpenConnectionAsync(int index)
        {
            if (this.IsDisposed())
            {
                // Manager disposed before this open started. Nothing to open;
                // return quietly. Disposal is a normal lifecycle event, not a
                // failure — an exceptionless early-out avoids throwing an
                // ObjectDisposedException only for OpenAndInjectAsync to swallow
                // it. Acquirers still observe disposal via the completed slot
                // queue, so this is behavior-equivalent.
                return;
            }

            Guid channelId = Guid.NewGuid();
            Guid openActivityId = Guid.NewGuid();
            CMChannel channel = null;

            this.telemetry.LogEvent(RntbdConnectionManagerEvent.OpenStarted(
                this.ConnectionManagerId, this.serverEndpointDisplay,
                index, channelId,
                OpenTrigger.OnDemandAcquire,
                openActivityId: openActivityId));

            ValueStopwatch openSw = ValueStopwatch.StartNew();
            try
            {
                channel = new CMChannel(
                    openActivityId,
                    this.serverUri,
                    this.channelProperties,
                    this.localRegionRequest,
                    openingSlim: null,
                    chaosInterceptor: this.chaosInterceptor,
                    onChannelOpen: null,
                    deferInitialize: true);

                await channel.OpenWithoutTimerAsync(
                    openActivityId,
                    CancellationToken.None,
                    onChannelOpen: this.BuildOnChannelOpenForOpenerTask())
                    .ConfigureAwait(false);

                // Success. Publish the connection and write its slot tokens.
                long publishedVersion;
                DateTime openedAt = DateTime.UtcNow;
                lock (this.connectionsLock)
                {
                    ConnectionEntry entry = this.connections[index];
                    entry.Channel = channel;
                    entry.ChannelId = channelId;
                    entry.OpenedAtUtc = openedAt;
                    publishedVersion = entry.SetStateLocked(ConnectionEntryState.Open);
                }

                Interlocked.Increment(ref this.lifetimeOpens);
                (long? tcpMs, long? tlsMs, long? rntbdMs, _) = ReadOpenTimings(channel);
                this.telemetry.LogEvent(RntbdConnectionManagerEvent.OpenCompleted(
                    this.ConnectionManagerId, this.serverEndpointDisplay,
                    index, channelId, openSw.ElapsedMilliseconds,
                    tcpMs: tcpMs, tlsMs: tlsMs, rntbdMs: rntbdMs,
                    openActivityId: openActivityId));

                for (int i = 0; i < this.options.MaxRequestsPerChannel; i++)
                {
                    this.requestSlots.Write(new Slot(index, publishedVersion));
                }
            }
            catch (ObjectDisposedException)
            {
                // Manager (or a resource it depends on) was disposed mid-open.
                // Disposal is a normal lifecycle event, not an acquirer-visible
                // failure: close the half-open channel and return without
                // faulting the opener task. Acquirers observe disposal via the
                // completed slot queue instead. Returning (rather than
                // rethrowing only for OpenAndInjectAsync to swallow) keeps the
                // disposal path exceptionless. No entry rollback is needed —
                // Dispose closes all entries under connectionsLock.
                if (channel != null)
                {
                    try
                    {
                        channel.Close();
                    }
                    catch
                    {
                    }
                }
                return;
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning(
                    "RntbdConnectionManager [{0}]: open for slot {1} failed: {2}",
                    this.ConnectionManagerId, index, ex.Message);

                (long? tcpMs, long? tlsMs, long? rntbdMs, OpenFailureStage failedStage) = ReadOpenTimings(channel);
                this.telemetry.LogEvent(RntbdConnectionManagerEvent.OpenFailed(
                    this.ConnectionManagerId, this.serverEndpointDisplay,
                    index, channelId, openSw.ElapsedMilliseconds,
                    failedStage,
                    ex.GetType().Name, ex.Message,
                    tcpMs: tcpMs, tlsMs: tlsMs, rntbdMs: rntbdMs,
                    openActivityId: openActivityId));

                if (channel != null)
                {
                    try
                    {
                        channel.Close();
                    }
                    catch
                    {
                    }
                }

                // Roll the entry back to Closed so a subsequent acquirer can
                // pick this slot for a fresh open. SetStateLocked bumps the
                // version on the ->Closed transition; no slot tokens were ever
                // published for this open, so the bump is harmless.
                lock (this.connectionsLock)
                {
                    ConnectionEntry entry = this.connections[index];
                    entry.Channel = null;
                    entry.SetStateLocked(ConnectionEntryState.Closed);
                }

                throw;
            }
        }

        /// <summary>
        /// Snapshot per-stage durations + derived <see cref="OpenFailureStage"/>
        /// from a Channel's <see cref="ChannelOpenTimeline"/>. Used by both
        /// the success and failure paths of <see cref="OpenConnectionAsync"/>.
        /// Failure stage is inferred from which stage timestamps are missing:
        /// no TCP connect → TcpConnect; TCP but no TLS → Tls; TLS but no
        /// RNTBD context → RntbdContext; everything present → Unknown
        /// (failure after all stages, e.g. a post-handshake write error).
        /// </summary>
        private static (long? tcpMs, long? tlsMs, long? rntbdMs, OpenFailureStage failedStage) ReadOpenTimings(CMChannel channel)
        {
            if (channel?.OpenTimeline == null)
            {
                return (null, null, null, OpenFailureStage.Unknown);
            }

            ChannelOpenTimeline t = channel.OpenTimeline;
            long? tcpMs = t.TcpConnectDuration is TimeSpan tcp ? (long?)tcp.TotalMilliseconds : null;
            long? tlsMs = t.SslHandshakeDuration is TimeSpan tls ? (long?)tls.TotalMilliseconds : null;
            long? rntbdMs = t.RntbdHandshakeDuration is TimeSpan rntbd ? (long?)rntbd.TotalMilliseconds : null;

            OpenFailureStage stage;
            if (t.ConnectTime == null) stage = OpenFailureStage.TcpConnect;
            else if (t.SslHandshakeTime == null) stage = OpenFailureStage.Tls;
            else if (t.RntbdHandshakeTime == null) stage = OpenFailureStage.RntbdContext;
            else stage = OpenFailureStage.Unknown;

            return (tcpMs, tlsMs, rntbdMs, stage);
        }

        // Chaos-interceptor connection-open hook.
        //
        // The interceptor's OnChannelOpenAsync(...) is typed to the legacy
        // concrete Rntbd.Channel (it stashes the channel in its fault-injection
        // channel store for later connection-error injection). The connection
        // manager owns a standalone CMChannel, which is deliberately NOT a
        // Rntbd.Channel, so it cannot participate in that Channel-typed store.
        //
        // Connection-OPEN fault injection is therefore not wired on the manager
        // path (a documented, feature-flag-gated limitation). Per-request and
        // channel-dispose chaos hooks still flow: CMChannel forwards the
        // interceptor to its Dispatcher (request send/receive rules) and calls
        // OnChannelDispose on close.
        private Func<Guid, Guid, Uri, CMChannel, Task> BuildOnChannelOpenForOpenerTask()
        {
            return null;
        }

        // ----------------------------------------------------------------
        // Close
        // ----------------------------------------------------------------

        private void CloseEntryLocked(int index, SlotFreeReason reason)
        {
            Debug.Assert(Monitor.IsEntered(this.connectionsLock));
            ConnectionEntry entry = this.connections[index];
            if (entry.State == ConnectionEntryState.Closed)
            {
                return;
            }

            IChannel channel = entry.Channel;
            Guid channelId = entry.ChannelId;
            DateTime openedAt = entry.OpenedAtUtc;
            entry.Channel = null;
            // SetStateLocked → Closed bumps the version, invalidating any
            // outstanding slot tokens for this entry.
            entry.SetStateLocked(ConnectionEntryState.Closed);

            if (channel != null)
            {
                try
                {
                    channel.Close();
                }
                catch
                { /* best-effort */
                }
            }

            try
            {
                long lifetimeMs = openedAt == default(DateTime)
                    ? 0
                    : (long)(DateTime.UtcNow - openedAt).TotalMilliseconds;
                this.telemetry.LogEvent(RntbdConnectionManagerEvent.SlotFreed(
                    this.ConnectionManagerId, this.serverEndpointDisplay,
                    index, channelId,
                    reason,
                    channelLifetimeMs: lifetimeMs,
                    requestsServed: 0));
            }
            catch
            {
                // Telemetry must never throw.
            }
        }

        // ----------------------------------------------------------------
        // Telemetry helpers
        // ----------------------------------------------------------------

        private void EmitAcquireSuccess(
            Guid activityId, long waitMs, PoolState poolStateAtEntry,
            bool joinedExistingOpener, bool triggeredOpen,
            int ensureOpenCalls, int staleSlotsConsumed)
        {
            if (!(this.debugLogsResolver?.Invoke() ?? false))
            {
                return;
            }

            this.telemetry.LogAcquisition(new RntbdConnectionManagerConnectionAcquisitionEvent(
                connectionManagerId: this.ConnectionManagerId,
                activityId: activityId,
                serverEndpoint: this.serverEndpointDisplay,
                localRegion: this.localRegionRequest,
                waitForConnectionMs: waitMs,
                poolStateAtEntry: poolStateAtEntry,
                joinedExistingOpenerTask: joinedExistingOpener,
                triggeredOpen: triggeredOpen,
                ensureOpenCalls: ensureOpenCalls,
                staleSlotsConsumed: staleSlotsConsumed,
                outcome: AcquireOutcome.Acquired));
        }

        private void EmitAcquireFailure(
            Guid activityId, long waitMs, PoolState poolStateAtEntry,
            bool joined, bool triggered,
            int ensureOpenCalls, int staleSlotsConsumed,
            AcquireOutcome outcome, string errorCode)
        {
            if (!(this.debugLogsResolver?.Invoke() ?? false))
            {
                return;
            }

            this.telemetry.LogAcquisition(new RntbdConnectionManagerConnectionAcquisitionEvent(
                connectionManagerId: this.ConnectionManagerId,
                activityId: activityId,
                serverEndpoint: this.serverEndpointDisplay,
                localRegion: this.localRegionRequest,
                waitForConnectionMs: waitMs,
                poolStateAtEntry: poolStateAtEntry,
                joinedExistingOpenerTask: joined,
                triggeredOpen: triggered,
                ensureOpenCalls: ensureOpenCalls,
                staleSlotsConsumed: staleSlotsConsumed,
                outcome: outcome,
                errorCode: errorCode));
        }

        private bool IsDisposed()
        {
            return Volatile.Read(ref this.disposed) != 0;
        }

        /// <summary>
        /// Exceptionless disposed check: returns the exception the acquire
        /// path surfaces via <see cref="Res{T}"/>, or null when live.
        /// </summary>
        private ObjectDisposedException GetDisposedException()
        {
            return this.IsDisposed()
                ? new ObjectDisposedException(nameof(RntbdConnectionManager))
                : null;
        }

        /// <summary>
        /// Exceptionless draining check: returns the exception the acquire
        /// path surfaces via <see cref="Res{T}"/> when the manager is no
        /// longer accepting new acquires, or null when still accepting.
        /// </summary>
        private ObjectDisposedException GetNotAcceptingAcquiresException()
        {
            // Reuse ObjectDisposedException for the drain state so callers
            // observe the same contract whether the manager is already
            // disposed or has been detached and is draining.
            return this.AcceptsNewAcquires
                ? null
                : new ObjectDisposedException(nameof(RntbdConnectionManager));
        }

        // ----------------------------------------------------------------
        // Entry type
        // ----------------------------------------------------------------

        private enum ConnectionEntryState
        {
            Closed = 0,   // No connection. Initial state.
            Opening = 1,  // Opener task in progress.
            Open = 2,     // Healthy, serving requests.
        }

        // Fields are mutated only under connectionsLock. Version field
        // additionally readable lock-free via Volatile.Read.
        private sealed class ConnectionEntry
        {
            public ConnectionEntry(int index)
            {
                this.Index = index;
                this.State = ConnectionEntryState.Closed;
            }

            public int Index { get; }
            public ConnectionEntryState State { get; private set; }
            public long Version;  // Volatile; bumped on any Close transition.
            public IChannel Channel;
            public Guid ChannelId;
            public DateTime OpenedAtUtc;

            /// <summary>
            /// Transition the entry's state and (on Closed transition)
            /// bump the version. Returns the new version. Caller must
            /// hold the manager's connectionsLock.
            /// </summary>
            public long SetStateLocked(ConnectionEntryState newState)
            {
                ConnectionEntryState previous = this.State;
                this.State = newState;
                if (newState == ConnectionEntryState.Closed && previous != ConnectionEntryState.Closed)
                {
                    // Bump version so any outstanding slot tokens are
                    // detected as stale by future readers.
                    Interlocked.Increment(ref this.Version);
                }
                return Volatile.Read(ref this.Version);
            }
        }
    }
}

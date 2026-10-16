//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net.Sockets;
    using Microsoft.Azure.Cosmos.Core.Trace;

    // User-mode implementation of a reusable port pool.
    // This class is thread safe.
    internal sealed class UserPortPool
    {
        private readonly int portReuseThreshold;
        private readonly int candidatePortCount;

        private readonly Pool ipv4Pool = new Pool();
        private readonly Pool ipv6Pool = new Pool();

        public UserPortPool(int portReuseThreshold, int candidatePortCount)
        {
            if (portReuseThreshold <= 0)
            {
                throw new ArgumentException("The port reuse threshold must be positive");
            }
            if (candidatePortCount <= 0)
            {
                throw new ArgumentException("The candidate port count must be positive");
            }
            if (candidatePortCount > portReuseThreshold)
            {
                throw new ArgumentException(
                    "The candidate port count must be less than or equal to the port reuse threshold");
            }
            this.portReuseThreshold = portReuseThreshold;
            this.candidatePortCount = candidatePortCount;
        }

        public ushort[] GetCandidatePorts(AddressFamily addressFamily)
        {
            Pool pool = this.GetPool(addressFamily);

            lock (pool.mutex)
            {
                Debug.Assert(pool.usablePortCount + pool.unusablePortCount == pool.ports.Count);
                if (pool.usablePortCount < this.portReuseThreshold)
                {
                    return null;
                }
                Debug.Assert(this.candidatePortCount <= pool.usablePortCount);
                // This approach is O(n) (for a bounded, but non-trivial n).
                // Replace with something faster if it's a problem.
                return UserPortPool.GetRandomSample(
                    pool.ports, this.candidatePortCount, pool.rand);
            }
        }

        public void AddReference(AddressFamily addressFamily, ushort port)
        {
            Pool pool = this.GetPool(addressFamily);

            lock (pool.mutex)
            {
                Debug.Assert(pool.usablePortCount + pool.unusablePortCount == pool.ports.Count);
                PortState state = null;
                if (pool.ports.TryGetValue(port, out state))
                {
                    state.referenceCount++;
                }
                else
                {
                    state = new PortState();
                    state.referenceCount++;
                    pool.ports.Add(port, state);
                    pool.usablePortCount++;

                    DefaultTrace.TraceInformation("PrivatePortPool: Port is added to port pool: {0}", port);
                }
                Debug.Assert(pool.usablePortCount + pool.unusablePortCount == pool.ports.Count);
            }
        }

        public void RemoveReference(AddressFamily addressFamily, ushort port)
        {
            Pool pool = this.GetPool(addressFamily);

            lock (pool.mutex)
            {
                Debug.Assert(pool.usablePortCount + pool.unusablePortCount == pool.ports.Count);

                PortState state = null;
                if (!pool.ports.TryGetValue(port, out state))
                {
                    Debug.Assert(false);
                    return;
                }
                Debug.Assert(state != null);
                Debug.Assert(state.referenceCount > 0);
                state.referenceCount--;
                if (state.referenceCount == 0)
                {
                    bool result = pool.ports.Remove(port);
                    Debug.Assert(result);
                    if (state.usable)
                    {
                        pool.usablePortCount--;
                    }
                    else
                    {
                        pool.unusablePortCount--;
                    }

                    DefaultTrace.TraceInformation("PrivatePortPool: Port is removed from port pool: {0}", port);
                }

                Debug.Assert(pool.usablePortCount + pool.unusablePortCount == pool.ports.Count);
            }
        }

        public void MarkUnusable(AddressFamily addressFamily, ushort port)
        {
            Pool pool = this.GetPool(addressFamily);

            lock (pool.mutex)
            {
                Debug.Assert(pool.usablePortCount + pool.unusablePortCount == pool.ports.Count);

                PortState state = null;
                if (!pool.ports.TryGetValue(port, out state)) {
                    return;
                }

                Debug.Assert(state != null);
                state.usable = false;
                pool.usablePortCount--;
                pool.unusablePortCount++;

                DefaultTrace.TraceInformation("PrivatePortPool: Port is marked as unusable: {0}", port);
                Debug.Assert(pool.usablePortCount + pool.unusablePortCount == pool.ports.Count);
            }
        }

        public string DumpStatus()
        {
            return string.Format("portReuseThreshold:{0};candidatePortCount:{1};ipv4Pool:{2};ipv6Pool:{3}",
                this.portReuseThreshold,
                this.candidatePortCount,
                this.DumpPoolStatus(this.ipv4Pool),
                this.DumpPoolStatus(this.ipv6Pool));
        }

        private string DumpPoolStatus(Pool pool)
        {
            lock (pool.mutex)
            {
                return string.Format("{0} totalPorts.{1} usablePorts.", pool.ports.Count, pool.usablePortCount);
            }
        }

        private Pool GetPool(AddressFamily af)
        {
            switch (af)
            {
                case AddressFamily.InterNetwork:
                    return this.ipv4Pool;

                case AddressFamily.InterNetworkV6:
                    return this.ipv6Pool;

                default:
                    throw new NotSupportedException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Address family {0} not supported", af));
            }
        }

        private static ushort[] GetRandomSample(
            Dictionary<ushort, PortState> pool,
            int candidatePortCount, Random rng)
        {
            ushort[] sample = UserPortPool.ReservoirSample(pool, candidatePortCount, rng);
            Debug.Assert(sample != null);
            Debug.Assert(sample.Length == candidatePortCount);
            UserPortPool.Shuffle(rng, sample);
            return sample;
        }

        private static ushort[] ReservoirSample(Dictionary<ushort, PortState> pool, int candidatePortCount, Random rng)
        {
            IEnumerable<ushort> keys = pool.Keys;
            ushort[] sample = new ushort[candidatePortCount];
            int i = 0;
            int j = 0;
            foreach (ushort port in keys)
            {
                if (!pool[port].usable)
                {
                    // Continue without incrementing i and j.
                    continue;
                }
                if (j < sample.Length)
                {
                    sample[j] = port;
                    j++;
                }
                else
                {
                    int k = rng.Next(i + 1);
                    if (k < sample.Length)
                    {
                        sample[k] = port;
                    }
                }
                i++;
            }
#if DEBUG
            Debug.Assert(j == sample.Length);
            foreach (ushort p in sample)
            {
                Debug.Assert(p != 0);
            }
#endif  // DEBUG
            return sample;
        }

        private static void Shuffle(Random rng, ushort[] sample)
        {
            // Fisher-Yates shuffle (aka Knuth shuffle).
            // For each i from i-1 down to 1, pick a random integer j such that
            // 0 <= j <= i; exchange sample[i] and sample[j].
            // This is necessary because ports are tried in the order returned.
            for (int i = sample.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                ushort temp = sample[j];
                sample[j] = sample[i];
                sample[i] = temp;
            }
        }

        private sealed class Pool
        {
            public readonly object mutex = new object();
            // All fields are guarded by mutex.
            public readonly Dictionary<ushort, PortState> ports =
                new Dictionary<ushort, PortState>(192);
            public readonly Random rand = new Random();
            // usablePortCount + unusablePortCount = ports.Count
            public int usablePortCount = 0;
            public int unusablePortCount = 0;
        }

        private sealed class PortState {
            public int referenceCount = 0;
            public bool usable = true;
        }
    }
}
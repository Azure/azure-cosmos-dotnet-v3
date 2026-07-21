//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Helpers that route contacted/failed replica and region mutations to the thread-safe
    /// <see cref="IClientSideRequestStatisticsExtension"/> implementation when the target supports it, and
    /// otherwise fall back to mutating the legacy collections directly.
    /// </summary>
    /// <remarks>
    /// This lets the Direct transport stack (StoreReader, ConsistencyWriter, FeedResponse, ...) use a single
    /// call site that works for both the Direct <see cref="ClientSideRequestStatistics"/> - which implements
    /// the thread-safe extension - and any implementation that only implements
    /// <see cref="IClientSideRequestStatistics"/>. Implementations that adopt
    /// <see cref="IClientSideRequestStatisticsExtension"/> transparently pick up the thread-safe path with no
    /// further change to these call sites.
    /// </remarks>
    internal static class ClientSideRequestStatisticsExtensions
    {
        public static void AppendContactedReplica(this IClientSideRequestStatistics statistics, TransportAddressUri contactedReplica)
        {
            if (statistics is IClientSideRequestStatisticsExtension extension)
            {
                extension.AppendContactedReplica(contactedReplica);
            }
            else
            {
                statistics.ContactedReplicas.Add(contactedReplica);
            }
        }

        public static void AppendContactedReplicas(this IClientSideRequestStatistics statistics, IReadOnlyList<TransportAddressUri> contactedReplicas)
        {
            if (contactedReplicas == null)
            {
                return;
            }

            if (statistics is IClientSideRequestStatisticsExtension extension)
            {
                extension.AppendContactedReplicas(contactedReplicas);
            }
            else
            {
                statistics.ContactedReplicas.AddRange(contactedReplicas);
            }
        }

        public static void RecordContactedReplicas(this IClientSideRequestStatistics statistics, IReadOnlyList<TransportAddressUri> contactedReplicas)
        {
            if (contactedReplicas == null)
            {
                return;
            }

            if (statistics is IClientSideRequestStatisticsExtension extension)
            {
                extension.RecordContactedReplicas(contactedReplicas);
            }
            else
            {
                // The write path replaces the full replica list before the request is issued.
                statistics.ContactedReplicas = new List<TransportAddressUri>(contactedReplicas);
            }
        }

        public static void AppendFailedReplica(this IClientSideRequestStatistics statistics, TransportAddressUri failedReplica)
        {
            if (statistics is IClientSideRequestStatisticsExtension extension)
            {
                extension.AppendFailedReplica(failedReplica);
            }
            else
            {
                statistics.FailedReplicas.Add(failedReplica);
            }
        }

        public static void AppendRegionContacted(this IClientSideRequestStatistics statistics, string regionName, Uri locationEndpoint)
        {
            if (statistics is IClientSideRequestStatisticsExtension extension)
            {
                extension.AppendRegionContacted(regionName, locationEndpoint);
            }
            else
            {
                statistics.RegionsContacted.Add((regionName, locationEndpoint));
            }
        }
    }
}

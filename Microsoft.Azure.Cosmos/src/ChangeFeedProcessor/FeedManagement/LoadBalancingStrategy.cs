//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;

    /// <summary>
    /// A strategy defines which leases should be taken by the current host in a certain moment.
    /// </summary>
    /// <remarks>
    /// It can set new <see cref="DocumentServiceLease.Properties"/> for all returned leases if needed, including currently owned leases.
    /// </remarks>
    /// <example>
    /// <code language="C#">
    /// <![CDATA[
    /// public class CustomStrategy : LoadBalancingStrategy
    /// {
    ///     private string hostName;
    ///     private string hostVersion;
    ///     private TimeSpan leaseExpirationInterval;
    ///
    ///     private const string VersionPropertyKey = "version";
    ///
    ///     public IEnumerable<DocumentServiceLease> SelectLeasesToTake(IEnumerable<DocumentServiceLease> allLeases)
    ///     {
    ///         var takenLeases = this.FindLeasesToTake(allLeases);
    ///         foreach (var lease in takenLeases)
    ///         {
    ///             lease.Properties[VersionPropertyKey] = this.hostVersion;
    ///         }
    ///
    ///         return takenLeases;
    ///     }
    ///
    ///     private IEnumerable<ILease> FindLeasesToTake(IEnumerable<DocumentServiceLease> allLeases)
    ///     {
    ///         List<DocumentServiceLease> takenLeases = new List<DocumentServiceLease>();
    ///         foreach (var lease in allLeases)
    ///         {
    ///             if (string.IsNullOrWhiteSpace(lease.Owner) || this.IsExpired(lease))
    ///             {
    ///                 takenLeases.Add(lease);
    ///             }
    ///
    ///             if (lease.Owner != this.hostName)
    ///             {
    ///                 var ownerVersion = lease.Properties[VersionPropertyKey];
    ///                 if (ownerVersion < this.hostVersion)
    ///                 {
    ///                     takenLeases.Add(lease);
    ///                 }
    ///
    ///                 // more logic for leases owned by other hosts
    ///             }
    ///         }
    ///
    ///         return takenLeases;
    ///     }
    ///
    ///     private bool IsExpired(DocumentServiceLease lease)
    ///     {
    ///         return lease.Timestamp.ToUniversalTime() + this.leaseExpirationInterval < DateTime.UtcNow;
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    internal abstract class LoadBalancingStrategy
    {
        /// <summary>
        /// Select leases that should be taken for processing.
        /// This method will be called periodically with <see cref="ChangeFeedLeaseOptions.LeaseAcquireInterval"/>
        /// </summary>
        /// <param name="allLeases">All leases</param>
        /// <returns>Leases that should be taken for processing by this host</returns>
        public abstract IEnumerable<DocumentServiceLease> SelectLeasesToTake(IEnumerable<DocumentServiceLease> allLeases);
    }
}
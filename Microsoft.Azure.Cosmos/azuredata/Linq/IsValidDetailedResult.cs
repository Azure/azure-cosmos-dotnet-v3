//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Linq
{
    using System.Runtime.Serialization;

    /// <summary>
    /// <para>
    /// Return value of <see cref="SpatialExtensions.IsValidDetailed"/> in the Azure Cosmos DB service.
    /// </para>
    /// <para>
    /// Contains detailed description why a geometyr is invalid.
    /// </para>
    /// </summary>
    [DataContract]
    internal sealed class IsValidDetailedResult
    {
        public IsValidDetailedResult(bool isValid, string reason)
        {
            this.IsValid = isValid;
            this.Reason = reason;
        }

        /// <summary>
        /// Returns a value indicating whether geometry for which <see cref="SpatialExtensions.IsValidDetailed"/>
        /// was called is valid or not in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// <c>true</c> if geometry for which <see cref="SpatialExtensions.IsValidDetailed"/> was called is valid. <c>false</c> otherwise.
        /// </value>
        [DataMember(Name = "valid")]
        public bool IsValid { get; }

        /// <summary>
        /// If geometry is invalid, returns detailed reason in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Description why a geometry is invalid.
        /// </value>
        [DataMember(Name = "reason")]
        public string Reason { get; }
    }
}

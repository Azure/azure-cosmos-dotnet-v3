// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System.Runtime.Serialization;
    using Newtonsoft.Json;

    /// <summary>
    /// <para>
    /// Return value of <see cref="GeoObjectExtensions.IsValidDetailed"/> in the Azure Cosmos DB service.
    /// </para>
    /// <para>
    /// Contains detailed description why a geometyr is invalid.
    /// </para>
    /// </summary>
    [DataContract]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class GeometryValidationResult
    {
        /// <summary>
        /// Returns a value indicating whether geometry for which <see cref="GeoObjectExtensions.IsValidDetailed"/>
        /// was called is valid or not in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// <c>true</c> if geometry for which <see cref="GeoObjectExtensions.IsValidDetailed"/> was called is valid. <c>false</c> otherwise.
        /// </value>
        [DataMember(Name = "valid")]
        [JsonProperty("valid", Required = Required.Always, Order = 0)]
        public bool IsValid { get; private set; }

        /// <summary>
        /// If geometry is invalid, returns detailed reason in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Description why a geometry is invalid.
        /// </value>
        [DataMember(Name = "reason")]
        [JsonProperty("reason", Order = 1)]
        public string Reason { get; private set; }
    }
}

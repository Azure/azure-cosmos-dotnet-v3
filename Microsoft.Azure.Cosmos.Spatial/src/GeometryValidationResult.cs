//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System.Runtime.Serialization;
    
    /// <summary>
    /// <para>
    /// Return value of <see cref="Geometry.IsValidDetailed"/> in the Azure Cosmos DB service.
    /// </para>
    /// <para>
    /// Contains detailed description why a geometyr is invalid.
    /// </para>
    /// </summary>
    [DataContract]
    [System.Text.Json.Serialization.JsonConverter(typeof(TextJsonGeometryConverterFactory))]
    [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public class GeometryValidationResult
    {
        /// <summary>
        /// Returns a value indicating whether geometry for which <see cref="Geometry.IsValidDetailed"/>
        /// was called is valid or not in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// <c>true</c> if geometry for which <see cref="Geometry.IsValidDetailed"/> was called is valid. <c>false</c> otherwise.
        /// </value>
        [DataMember(Name = "valid")]
        [Newtonsoft.Json.JsonProperty("valid", Required = Newtonsoft.Json.Required.Always, Order = 0)]
        public bool IsValid { get; internal set; }

        /// <summary>
        /// If geometry is invalid, returns detailed reason in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Description why a geometry is invalid.
        /// </value>
        [DataMember(Name = "reason")]
        [Newtonsoft.Json.JsonProperty("reason", Order = 1)]
        public string Reason { get; internal set; }
    }
}

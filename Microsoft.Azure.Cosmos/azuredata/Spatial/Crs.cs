//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents Coordinate Reference System in the Azure Cosmos DB service.
    /// </summary>
    [DataContract]
    public abstract class Crs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Crs" /> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="type">
        /// CRS type.
        /// </param>
        protected Crs(CrsType type)
        {
            this.Type = type;
        }

        /// <summary>
        /// Gets default CRS in the Azure Cosmos DB service. Default CRS is named CRS with the name "urn:ogc:def:crs:OGC:1.3:CRS84".
        /// </summary>
        public static Crs Default
        {
            get
            {
                return new NamedCrs("urn:ogc:def:crs:OGC:1.3:CRS84");
            }
        }

        /// <summary>
        /// Gets "Unspecified" CRS in the Azure Cosmos DB service. No CRS can be assumed for Geometries having "Unspecified" CRS.
        /// </summary>
        public static Crs Unspecified
        {
            get
            {
                return new UnspecifiedCrs();
            }
        }

        /// <summary>
        /// Gets CRS type in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Type of CRS.
        /// </value>
        [DataMember(Name = "type")]
        public CrsType Type { get; private set; }

        /// <summary>
        /// Creates named CRS with the name specified in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="name">CRS name.</param>
        /// <returns>Instance of <see cref="Crs" /> class.</returns>
        public static NamedCrs Named(string name)
        {
            return new NamedCrs(name);
        }

        /// <summary>
        /// Creates linked CRS in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="href">
        /// CRS link.
        /// </param>
        /// <returns>
        /// Instance of <see cref="Crs" /> class.
        /// </returns>
        public static LinkedCrs Linked(string href)
        {
            return new LinkedCrs(href, null);
        }

        /// <summary>
        /// Creates linked CRS with the optional type specified in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="href">
        /// CRS link.
        /// </param>
        /// <param name="type">
        /// CRS link type.
        /// </param>
        /// <returns>
        /// Instance of <see cref="Crs" /> class.
        /// </returns>
        public static LinkedCrs Linked(string href, string type)
        {
            return new LinkedCrs(href, type);
        }
    }
}

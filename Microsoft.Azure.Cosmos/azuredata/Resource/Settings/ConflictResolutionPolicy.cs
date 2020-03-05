//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    /// <summary>
    /// Represents the conflict resolution policy configuration for specifying how to resolve conflicts 
    /// in case writes from different regions result in conflicts on items in the container in the Azure Cosmos DB service.
    /// </summary>
    public class ConflictResolutionPolicy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConflictResolutionPolicy"/> class for the Azure Cosmos DB service.
        /// </summary>
        public ConflictResolutionPolicy()
        {
            // defaults
            this.Mode = ConflictResolutionMode.LastWriterWins;
        }

        /// <summary>
        /// Gets or sets the <see cref="ConflictResolutionMode"/> in the Azure Cosmos DB service. By default it is <see cref="ConflictResolutionMode.LastWriterWins"/>.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="ConflictResolutionMode"/> enumeration.
        /// </value>
        public ConflictResolutionMode Mode { get; set; }

        /// <summary>
        /// Gets or sets the path which is present in each item in the Azure Cosmos DB service for last writer wins conflict-resolution.
        /// This path must be present in each item and must be an integer value.
        /// In case of a conflict occurring on a item, the item with the higher integer value in the specified path will be picked.
        /// If the path is unspecified, by default the time stamp path will be used.
        /// </summary>
        /// <remarks>
        /// This value should only be set when using <see cref="ConflictResolutionMode.LastWriterWins"/>
        /// </remarks>
        /// <value>
        /// <![CDATA[The path to check values for last-writer wins conflict resolution. That path is a rooted path of the property in the item, such as "/name/first".]]>
        /// </value>
        /// <example>
        /// <![CDATA[
        /// conflictResolutionPolicy.ConflictResolutionPath = "/name/first";
        /// ]]>
        /// </example>
        public string ResolutionPath { get; set; }

        /// <summary>
        /// Gets or sets the Stored Procedure which is used for conflict resolution in the Azure Cosmos DB service.
        /// This stored procedure may be created after the <see cref="CosmosContainer"/> is created and can be changed as required. 
        /// </summary>
        /// <remarks>
        /// 1. This value should only be set when using <see cref="ConflictResolutionMode.Custom"/>
        /// 2. In case the stored procedure fails or throws an exception, the conflict resolution will default to registering conflicts in the conflicts feed"/>.
        /// 3. The user can provide the stored procedure id.
        /// </remarks>
        /// <value>
        /// <![CDATA[The stored procedure to perform conflict resolution.]]>
        /// </value>
        /// <example>
        /// <![CDATA[
        /// conflictResolutionPolicy.ConflictResolutionProcedure = "dbs/databaseName/colls/containerName/sprocs/storedProcedureName";
        /// ]]>
        /// </example>
        public string ResolutionProcedure { get; set; }
    }
}

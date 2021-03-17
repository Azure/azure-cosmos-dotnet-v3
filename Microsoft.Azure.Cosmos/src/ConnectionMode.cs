//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Represents the connection mode to be used by the client when connecting to the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Direct and Gateway connectivity modes are supported. Direct is the default. 
    /// </remarks>
    /// <example>
    /// <code language="c#">
    /// <![CDATA[
    /// CosmosClient client = new CosmosClient(connectionString, new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway });
    /// ]]>
    /// </code>
    /// </example>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/sql-sdk-connection-modes"/>
    public enum ConnectionMode
    {
        /// <summary>
        /// Use the Azure Cosmos DB gateway to route all requests to the Azure Cosmos DB service. The gateway proxies requests to the right data partition.
        /// </summary>
        /// <remarks>
        /// Use Gateway connectivity when within firewall settings do not allow Direct connectivity. All connections 
        /// are made to the database account's endpoint through the standard HTTPS port (443).
        /// </remarks>
        Gateway = 0,

        /// <summary>
        /// Uses direct connectivity to connect to the data nodes in the Azure Cosmos DB service. Use gateway only to initialize and cache logical addresses and refresh on updates
        /// </summary>
        /// <remarks>
        /// Use Direct connectivity for best performance. Connections are made to the data nodes on Azure Cosmos DB's clusters 
        /// on a range of port numbers either using HTTPS or TCP/SSL.
        /// </remarks>
        Direct
    }
}

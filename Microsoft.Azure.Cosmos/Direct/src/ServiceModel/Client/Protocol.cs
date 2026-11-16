//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Client
{
    /// <summary>
    /// Specifies the protocol to be used by DocumentClient for communicating to the Azure Cosmos DB service.
    /// </summary>
    /// <example>
    /// <code language="c#">
    /// <![CDATA[
    /// DocumentClient client = new DocumentClient(endpointUri, masterKey, new ConnectionPolicy 
    /// { 
    ///     ConnectionMode = ConnectionMode.Direct,
    ///     ConnectionProtocol = Protocol.Tcp
    /// }); 
    /// ]]>
    /// </code>
    /// </example>
    /// <seealso cref="ConnectionMode"/>
    /// <seealso cref="ConnectionPolicy"/>
    /// <seealso cref="DocumentClient"/>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum Protocol
    {
        /// <summary>
        /// Specifies the HTTPS protocol.
        /// </summary>
        /// <remarks>Default connectivity.</remarks>
        Https,

        /// <summary>
        /// Specifies a custom binary protocol on TCP.
        /// </summary>
        /// <remarks>Better for performance.</remarks>
        Tcp,
    }
}

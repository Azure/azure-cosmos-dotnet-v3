//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using StackExchange.Redis;

    internal class Connection
    {
        // TODO: Selectable database seems to be optimization. Explore more. 
        // https://redis.io/docs/latest/commands/select/

        private static readonly string connectionString = "10.8.0.19:6379,syncTimeout=100000,asyncTimeout=100000";
        internal static ConnectionMultiplexer CreateConnection(WriterConfig config)
        {
            ConnectionMultiplexer con = ConnectionMultiplexer.Connect(connectionString);

            return con;
        }
    }

}

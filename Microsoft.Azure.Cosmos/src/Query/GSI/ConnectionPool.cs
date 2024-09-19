//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using StackExchange.Redis;

    internal class ConnectionPool
    {
        private static ConnectionPool connectionPool;

        public ConnectionMultiplexer[] connections;

        private ConnectionPool(WriterConfig config)
        {
            this.connections = new ConnectionMultiplexer[config.ConnectionCount];
        }

        // TODO: Check if separate read & write threads gives better performance.
        // If yes, maintain two separate thread pools.
        internal static ConnectionPool getConnectionPool(WriterConfig config)
        {
            if (connectionPool != null)
                return connectionPool;

            connectionPool = new ConnectionPool(config);

            for (int i = 0; i < connectionPool.connections.Length; i++)
            {
                Console.WriteLine($" - Creating Connection #{i}");

                ConnectionMultiplexer con = Connection.CreateConnection(config);
                connectionPool.connections[i] = con;
            }

            Console.WriteLine($"Done Establishing Connection(s)");

            return connectionPool;
        }

        internal ConnectionMultiplexer ConnectionAt(int index)
        {
            return this.connections[index];
        }

        public IDatabase GetDatabase()
        {
            long leastPendingTasks = long.MaxValue;
            IDatabase leastPendingDatabase = this.connections[0].GetDatabase();

            for (int i = 0; i < this.connections.Length; i++)
            {
                ConnectionMultiplexer connection = this.connections[i];

                long pending = connection.GetCounters().TotalOutstanding;

                if (pending == 0)
                {
                    return connection.GetDatabase();
                }

                if (pending < leastPendingTasks)
                {
                    leastPendingTasks = pending;
                    leastPendingDatabase = connection.GetDatabase();
                }
            }

            return leastPendingDatabase;
        }
    }
}

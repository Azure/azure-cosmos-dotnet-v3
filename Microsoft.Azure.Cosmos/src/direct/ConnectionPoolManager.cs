//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Threading.Tasks;

    internal sealed class ConnectionPoolManager : IDisposable
    {
        private ConcurrentDictionary<string, ConnectionPool> connectionPools;
        private readonly IConnectionDispenser connectionDispenser;
        private int maxConcurrentConnectionOpenRequests;
        private bool isDisposed = false;
        public ConnectionPoolManager(IConnectionDispenser connectionDispenser, int maxConcurrentConnectionOpenRequests)
        {
            this.connectionPools = new ConcurrentDictionary<string, ConnectionPool>();
            this.connectionDispenser = connectionDispenser;
            this.maxConcurrentConnectionOpenRequests = maxConcurrentConnectionOpenRequests;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if(this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                foreach(KeyValuePair<string, ConnectionPool> kvp in connectionPools)
                {
                    kvp.Value.Dispose();
                }

                connectionPools = null;
                ((IDisposable)this.connectionDispenser).Dispose();
            }
            
            this.isDisposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException("ConnectionPoolManager");
            }
        }

        public Task<IConnection> GetOpenConnection(Guid activityId, Uri fullUri)
        {
            this.ThrowIfDisposed();
            string poolKey = string.Format(CultureInfo.InvariantCulture, "{0}:{1}", fullUri.Host, fullUri.Port);
            ConnectionPool pool = GetConnectionPool(poolKey);
            return pool.GetOpenConnection(activityId, fullUri, poolKey);
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Disposable object returned by method")]
        private ConnectionPool GetConnectionPool(string poolKey)
        {
            ConnectionPool connectionPool = null;
            if (!this.connectionPools.TryGetValue(poolKey, out connectionPool))
            {
                connectionPool = new ConnectionPool(poolKey, this.connectionDispenser, this.maxConcurrentConnectionOpenRequests);
                connectionPool = this.connectionPools.GetOrAdd(poolKey, connectionPool);
            }

            return connectionPool;
        }
        
        public void ReturnToPool(IConnection connection)
        {
            ConnectionPool connectionPool;
            if(!this.connectionPools.TryGetValue(connection.PoolKey, out connectionPool))
            {
                connection.Close();
                return;
            }

            connectionPool.ReturnConnection(connection);
        }
    }
}

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class ConnectionPool : IDisposable
    {
        private const int MaxPoolFailureRetries = 3;
        private readonly string address;
        private readonly IConnectionDispenser connectionDispenser;

        private ConcurrentStack<IConnection> connections;
        private int ConcurrentConnectionMaxOpen = 0;
        private SemaphoreSlim semaphore;
        private bool isDisposed = false;

        public ConnectionPool(string address, IConnectionDispenser connectionDispenser, int maxConcurrentConnectionOpenRequests)
        {
            this.address = address;
            this.connectionDispenser = connectionDispenser;

            this.connections = new ConcurrentStack<IConnection>();
            this.ConcurrentConnectionMaxOpen = maxConcurrentConnectionOpenRequests;
            this.semaphore = new SemaphoreSlim(maxConcurrentConnectionOpenRequests);
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
                this.DisposeAllConnections();

                this.connections = null;
                this.semaphore.Dispose();
                this.semaphore = null;

                DefaultTrace.TraceInformation("Connection Pool Disposed");
            }

            this.isDisposed = true;
        }

        private void DisposeAllConnections()
        {
            IConnection connection;
            while (this.connections.TryPop(out connection))
            {
                connection.Close();
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException("ConnectionPool");
            }
        }

        public async Task<IConnection> GetOpenConnection(Guid activityId, Uri fullUri, string poolKey)
        {
            this.ThrowIfDisposed();
            IConnection connection;
            int confirmOpenFailures = 0;
            while (true)
            {
                // If we hit a lot of pool check failures in a row, then the target process probably died or shutdown.
                // In that case, clear the pool and throw a GoneException.
                if (confirmOpenFailures > ConnectionPool.MaxPoolFailureRetries)
                {
                    this.DisposeAllConnections();
                    throw new GoneException();
                }

                if (this.connections.TryPop(out connection))
                {
                    if(connection.HasExpired())
                    {
                        connection.Close();
                        continue;
                    }
                    else if(!connection.ConfirmOpen())
                    {
                        confirmOpenFailures++;
                        connection.Close();
                        continue;
                    }
                    else
                    {
                        return connection;
                    }
                }
                else
                {
                    try
                    {
                        if(this.semaphore.CurrentCount == 0)
                        {
                            DefaultTrace.TraceWarning("Too Many Concurrent Connections being opened. Current Pending Count: {0}", this.ConcurrentConnectionMaxOpen);
                        }

                        await this.semaphore.WaitAsync();
                        return await this.connectionDispenser.OpenNewConnection(activityId, fullUri, poolKey);

                    }
                    finally
                    {
                        this.semaphore.Release();
                    }
                }
            }
        }

        public void ReturnConnection(IConnection connection)
        {
            this.connections.Push(connection);
        }
    }
}

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal interface ICTLResultProducer<T>
    {
        public Task<T> GetNextAsync();

        public bool HasMoreResults { get; }
    }

    /// <summary>
    /// Result producer that will invoke the result factory only once.
    /// </summary>
    internal class SingleExecutionResultProducer<T> : ICTLResultProducer<T>
    {
        private readonly Func<Task<T>> factory;
        private bool hasMoreResults = true;

        public SingleExecutionResultProducer(Func<Task<T>> factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public bool HasMoreResults 
        {
            get
            {
                if (this.hasMoreResults)
                {
                    this.hasMoreResults = false;
                    return true;
                }

                return this.hasMoreResults;
            }
        }

        public Task<T> GetNextAsync()
        {
            return this.factory();
        }
    }

    /// <summary>
    /// Result producer that will invoke the result factory through an iterator.
    /// </summary>
    internal class IteratorResultProducer<T> : ICTLResultProducer<FeedResponse<T>>
    {
        private readonly FeedIterator<T> iterator;
        public IteratorResultProducer(FeedIterator<T> iterator)
        {
            this.iterator = iterator ?? throw new ArgumentNullException(nameof(iterator));
        }

        public bool HasMoreResults => this.iterator.HasMoreResults;

        public Task<FeedResponse<T>> GetNextAsync()
        {
            return this.iterator.ReadNextAsync();
        }
    }
}

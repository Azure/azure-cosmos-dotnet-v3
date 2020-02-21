//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal sealed class BatchPartitionMetric
    {
        public BatchPartitionMetric()
            : this(0, 0, 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the OperationMetrics class (instance constructor).
        /// </summary>
        /// <param name="numberOfItemsOperatedOn">Number of documents operated on.</param>
        /// <param name="timeTakenInMilliseconds">Amount of time taken to insert the documents.</param>
        /// <param name="numberOfThrottles">The number of throttles encountered to insert the documents.</param>
        public BatchPartitionMetric(long numberOfItemsOperatedOn, long timeTakenInMilliseconds, long numberOfThrottles)
        {
            if (numberOfItemsOperatedOn < 0)
            {
                throw new ArgumentException("numberOfItemsOperatedOn must be non negative");
            }

            if (timeTakenInMilliseconds < 0)
            {
                throw new ArgumentException("timeTakenInMilliseconds must be non negative");
            }

            if (numberOfThrottles < 0)
            {
                throw new ArgumentException("numberOfThrottles must be non negative");
            }

            this.NumberOfItemsOperatedOn = numberOfItemsOperatedOn;
            this.TimeTakenInMilliseconds = timeTakenInMilliseconds;
            this.NumberOfThrottles = numberOfThrottles;
        }

        /// <summary>
        /// Gets the number of documents operated on.
        /// </summary>
        public long NumberOfItemsOperatedOn
        {
            get; private set;
        }

        /// <summary>
        /// Gets the time taken to operate on the documents.
        /// </summary>
        public long TimeTakenInMilliseconds
        {
            get; private set;
        }

        /// <summary>
        /// Gets the number of throttles incurred while operating on the documents.
        /// </summary>
        public long NumberOfThrottles
        {
            get; private set;
        }

        public void Add(long numberOfDocumentsOperatedOn, long timeTakenInMilliseconds, long numberOfThrottles)
        {
            if (numberOfDocumentsOperatedOn < 0)
            {
                throw new ArgumentException("numberOfDocumentsOperatedOn must be non negative");
            }

            if (timeTakenInMilliseconds < 0)
            {
                throw new ArgumentException("timeTakenInMilliseconds must be non negative");
            }

            if (numberOfThrottles < 0)
            {
                throw new ArgumentException("numberOfThrottles must be non negative");
            }

            this.NumberOfItemsOperatedOn += numberOfDocumentsOperatedOn;
            this.TimeTakenInMilliseconds += timeTakenInMilliseconds;
            this.NumberOfThrottles += numberOfThrottles;
        }
    }
}
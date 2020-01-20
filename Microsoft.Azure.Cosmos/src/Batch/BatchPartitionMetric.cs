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
        /// <param name="numberOfDocumentsOperatedOn">Number of documents operated on.</param>
        /// <param name="timeTaken">Amount of time taken to insert the documents.</param>
        /// <param name="numberOfThrottles">The number of throttles encountered to insert the documents.</param>
        public BatchPartitionMetric(long numberOfDocumentsOperatedOn, long timeTaken, long numberOfThrottles)
        {
            if (numberOfDocumentsOperatedOn < 0)
            {
                throw new ArgumentException("numberOfDocumentsOperatedOn must be non negative");
            }

            if (numberOfThrottles < 0)
            {
                throw new ArgumentException("numberOfThrottles must be non negative");
            }

            this.NumberOfDocumentsOperatedOn = numberOfDocumentsOperatedOn;
            this.TimeTaken = timeTaken;
            this.NumberOfThrottles = numberOfThrottles;
        }

        /// <summary>
        /// Gets the number of documents operated on.
        /// </summary>
        public long NumberOfDocumentsOperatedOn
        {
            get; private set;
        }

        /// <summary>
        /// Gets the time taken to operate on the documents.
        /// </summary>
        public long TimeTaken
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

        public void add(long numberOfDocumentsOperatedOn, long timeTaken, long numberOfThrottles)
        {
            this.NumberOfDocumentsOperatedOn += numberOfDocumentsOperatedOn;
            this.TimeTaken += timeTaken;
            this.NumberOfThrottles += numberOfThrottles;
        }
    }
}
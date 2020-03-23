//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// RetryOptions class defines the parameters an application can set to customize the
    /// built-in retry policies in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// The <see cref="DocumentClient"/> class supports retry
    /// on certain types of exceptions. This class provides options for applications to control the
    /// retry behavior.
    /// </remarks>
    internal sealed class RetryOptions
    {
        internal const int DefaultMaxRetryWaitTimeInSeconds = 30;
        internal const int DefaultMaxRetryAttemptsOnThrottledRequests = 9;

        private readonly RetryWithConfiguration retryWithConfiguration;
        private int maxRetryAttemptsOnThrottledRequests;
        private int maxRetryWaitTime;

        /// <summary>
        /// Creates a new instance of the RetryOptions class and intialize all properties
        /// to default values for the Azure Cosmos DB service.
        /// </summary>
        public RetryOptions()
        {
            this.maxRetryAttemptsOnThrottledRequests = RetryOptions.DefaultMaxRetryAttemptsOnThrottledRequests;
            this.maxRetryWaitTime = RetryOptions.DefaultMaxRetryWaitTimeInSeconds;
            this.retryWithConfiguration = new RetryWithConfiguration();
        }

        /// <summary>
        /// Gets or sets the maximum number of retries in the case where the request fails
        /// because the Azure Cosmos DB service has applied rate limiting on the client.
        /// </summary>
        /// <value>
        /// The default value is 9. This means in the case where the request is rate limited,
        /// the same request will be issued for a maximum of 10 times to the server before
        /// an error is returned to the application. If the value of this property is set to 0,
        /// there will be no automatic retry on rate limiting requests from the client and the exception
        /// needs to handled at the application level.
        /// For an example on how to set this value, please refer to <see cref="ConnectionPolicy.RetryOptions"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When a client is sending requests faster than the allowed rate,
        /// the service will return HttpStatusCode 429 (Too Many Request) to rate limit the client. The current
        /// implementation in the SDK will then wait for the amount of time the service tells it to wait and
        /// retry after the time has elapsed.
        /// </para>
        /// <para>
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/documentdb/documentdb-performance-tips#429">Handle rate limiting/request rate too large</see>.
        /// </para>
        /// </remarks>
        public int MaxRetryAttemptsOnThrottledRequests
        {
            get
            {
                return this.maxRetryAttemptsOnThrottledRequests;
            }

            set
            {
                if (value < 0)
                {
                    throw new ArgumentException("value must be a positive integer.");
                }

                this.maxRetryAttemptsOnThrottledRequests = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum retry time in seconds for the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The default value is 30 seconds. For an example on how to set this value, please refer to <see cref="ConnectionPolicy.RetryOptions"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When a request fails due to a rate limiting error, the service sends back a response that
        /// contains a value indicating the client should not retry before the <see cref="Microsoft.Azure.Documents.DocumentClientException.RetryAfter"/> time period has
        /// elapsed. This property allows the application to set a maximum wait time for all retry attempts.
        /// If the cumulative wait time exceeds the this value, the client will stop retrying and return the error to the application.
        /// </para>
        /// <para>
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/documentdb/documentdb-performance-tips#429">Handle rate limiting/request rate too large</see>.
        /// </para>
        /// </remarks>
        public int MaxRetryWaitTimeInSeconds
        {
            get
            {
                return this.maxRetryWaitTime;
            }

            set
            {
                if (value < 0 || value > int.MaxValue / 1000)
                {
                    throw new ArgumentException("value must be a positive integer between the range of 0 to " + (int.MaxValue / 1000));
                }

                this.maxRetryWaitTime = value;
            }
        }

        /// <summary>
        /// Gets or sets the initial delay retry time in milliseconds for the Azure Cosmos DB service
        /// for requests that hit RetryWithExceptions. This covers errors that occur due to concurrency errors in the store.
        /// </summary>
        /// <value>
        /// The default value is 1 second. For an example on how to set this value, please refer to <see cref="ConnectionPolicy.RetryOptions"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When a request fails due to a RetryWith error, the client delays and retries the request. This configures the client
        /// to delay the time specified before retrying the request.
        /// </para>
        /// </remarks>
        internal int? InitialRetryForRetryWithMilliseconds
        {
            get
            {
                return this.retryWithConfiguration.InitialRetryIntervalMilliseconds;
            }
            set
            {
                if (value != null && (value < 0 || value > int.MaxValue / 1000))
                {
                    throw new ArgumentException("value must be a positive integer between the range of 0 to " + (int.MaxValue / 1000));
                }

                this.retryWithConfiguration.InitialRetryIntervalMilliseconds = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum delay retry time in milliseconds for the Azure Cosmos DB service
        /// for requests that hit RetryWithExceptions. This covers errors that occur due to concurrency errors in the store.
        /// </summary>
        /// <value>
        /// The default value is 30 seconds. For an example on how to set this value, please refer to <see cref="ConnectionPolicy.RetryOptions"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When a request fails due to a RetryWith error, the client delays and retries the request. This configures the maximum time
        /// the client should delay before failing the request.
        /// </para>
        /// </remarks>
        internal int? MaximumRetryForRetryWithMilliseconds
        {
            get
            {
                return this.retryWithConfiguration.MaximumRetryIntervalMilliseconds;
            }
            set
            {
                if (value != null && (value < 0 || value > int.MaxValue / 1000))
                {
                    throw new ArgumentException("value must be a positive integer between the range of 0 to " + (int.MaxValue / 1000));
                }

                this.retryWithConfiguration.MaximumRetryIntervalMilliseconds = value;
            }
        }

        /// <summary>
        /// Gets or sets the interval to salt retrywith retries with. This will spread the retry values from 1..n from the exponential backoff
        /// subscribed.
        /// </summary>
        /// <value>
        /// The default value is to not salt.
        /// </value>
        /// <remarks>
        /// <para>
        /// When a request fails due to a RetryWith error, the client delays and retries the request. This configures the jitter on the retry attempted.
        /// </para>
        /// </remarks>
        internal int? RandomSaltForRetryWithMilliseconds
        {
            get
            {
                return this.retryWithConfiguration.RandomSaltMaxValueMilliseconds;
            }
            set
            {
                if (value != null && (value < 0 || value > int.MaxValue / 1000))
                {
                    throw new ArgumentException("value must be a positive integer between the range of 0 to " + (int.MaxValue / 1000));
                }

                this.retryWithConfiguration.RandomSaltMaxValueMilliseconds = value;
            }
        }

        /// <summary>
        /// Gets or sets the total time to wait before failing the request for retrywith failures.
        /// subscribed.
        /// </summary>
        /// <value>
        /// The default value 30 seconds.
        /// </value>
        /// <remarks>
        /// <para>
        /// When a request fails due to a RetryWith error, the client delays and retries the request. This configures total time spent waiting on the request.
        /// </para>
        /// </remarks>
        internal int? TotalWaitTimeForRetryWithMilliseconds
        {
            get
            {
                return this.retryWithConfiguration.TotalWaitTimeMilliseconds;
            }
            set
            {
                if (value != null && (value < 0 || value > int.MaxValue / 1000))
                {
                    throw new ArgumentException("value must be a positive integer between the range of 0 to " + (int.MaxValue / 1000));
                }

                this.retryWithConfiguration.TotalWaitTimeMilliseconds = value;
            }
        }

        internal RetryWithConfiguration GetRetryWithConfiguration()
        {
            return this.retryWithConfiguration;
        }
    }
}

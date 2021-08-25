//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Mirrored file.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "Mirrored file.")]
    internal class ShouldRetryResult
    {
        private static readonly ShouldRetryResult EmptyNoRetry = new ShouldRetryResult { ShouldRetry = false };

        protected ShouldRetryResult()
        {
        }

        public bool ShouldRetry { get; protected set; }

        /// <summary>
        /// Gets or sets how long to wait before next retry. 0 indicates retry immediately.
        /// </summary>
        public TimeSpan BackoffTime { get; protected set; }

        /// <summary>
        /// Gets or sets exception to throw.
        /// </summary>
        public Exception ExceptionToThrow { get; protected set; }

        public void ThrowIfDoneTrying(ExceptionDispatchInfo capturedException)
        {
            if (this.ShouldRetry)
            {
                return;
            }

            if (this.ExceptionToThrow == null)
            {
                capturedException.Throw();
            }

            if (capturedException != null && object.ReferenceEquals(
                this.ExceptionToThrow, capturedException.SourceException))
            {
                capturedException.Throw();
            }
            else
            {
                throw this.ExceptionToThrow;
            }
        }

        public static ShouldRetryResult NoRetry(Exception exception = null)
        {
            if (exception == null)
            {
                return ShouldRetryResult.EmptyNoRetry;
            }

            return new ShouldRetryResult { ShouldRetry = false, ExceptionToThrow = exception };
        }

        public static ShouldRetryResult RetryAfter(TimeSpan backoffTime)
        {
            return new ShouldRetryResult { ShouldRetry = true, BackoffTime = backoffTime };
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Mirrored file.")]
    internal class ShouldRetryResult<TPolicyArg1> : ShouldRetryResult
    {
        /// <summary>
        /// Gets argument to be passed to the callback method.
        /// </summary>
        public TPolicyArg1 PolicyArg1 { get; private set; }

        public static new ShouldRetryResult<TPolicyArg1> NoRetry(Exception exception = null)
        {
            return new ShouldRetryResult<TPolicyArg1> { ShouldRetry = false, ExceptionToThrow = exception };
        }

        public static ShouldRetryResult<TPolicyArg1> RetryAfter(TimeSpan backoffTime, TPolicyArg1 policyArg1)
        {
            return new ShouldRetryResult<TPolicyArg1> { ShouldRetry = true, BackoffTime = backoffTime, PolicyArg1 = policyArg1 };
        }
    }

    internal abstract class IRetryPolicy
    {
        /// <summary>
        /// Method that is called to determine from the policy that needs to retry on the exception
        /// </summary>
        /// <param name="exception">Exception during the callback method invocation</param>
        /// <param name="cancellationToken">Cancellation Token</param>
        /// <returns>If the retry needs to be attempted or not</returns>
        public abstract Task<ShouldRetryResult> ShouldRetryAsync(Exception exception, CancellationToken cancellationToken);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Mirrored file.")]
    internal abstract class IRetryPolicy<TPolicyArg1>
    {
        /// <summary>
        /// Method that is called to determine from the policy that needs to retry on the exception
        /// </summary>
        /// <param name="exception">Exception during the callback method invocation</param>
        /// <param name="cancellationToken">Cancelltion Token</param>
        /// <returns>If the retry needs to be attempted or not</returns>
        public abstract Task<ShouldRetryResult<TPolicyArg1>> ShouldRetryAsync(Exception exception, CancellationToken cancellationToken);

        /// <summary>
        /// Gets initial value of the template argument
        /// </summary>
        public abstract TPolicyArg1 InitialArgumentValue
        {
            get;
        }
    }
}
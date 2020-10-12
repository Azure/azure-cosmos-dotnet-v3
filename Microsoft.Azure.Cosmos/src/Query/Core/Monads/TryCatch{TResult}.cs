// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Monads
{
    using System;
    using System.Diagnostics;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    readonly struct TryCatch<TResult>
    {
        private readonly Either<Exception, TResult> either;

        private TryCatch(Either<Exception, TResult> either)
        {
            this.either = either;
        }

        public bool Succeeded => this.either.IsRight;

        public bool Failed => !this.Succeeded;

        public TResult Result
        {
            get
            {
                if (this.Succeeded)
                {
                    return this.either.FromRight(default);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Tried to get the result of a {nameof(TryCatch<TResult>)} that ended in an exception.");
                }
            }
        }

        public Exception Exception
        {
            get
            {
                if (!this.Succeeded)
                {
                    return this.either.FromLeft(default);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Tried to get the exception of a {nameof(TryCatch<TResult>)} that ended in a result.");
                }
            }
        }

        public Exception InnerMostException
        {
            get
            {
                Exception exception = this.Exception;
                while (exception.InnerException != null)
                {
                    exception = exception.InnerException;
                }

                return exception;
            }
        }

        public void Match(
            Action<TResult> onSuccess,
            Action<Exception> onError)
        {
            this.either.Match(onLeft: onError, onRight: onSuccess);
        }

        public TryCatch<TResult> Try(
            Action<TResult> onSuccess)
        {
            if (this.Succeeded)
            {
                onSuccess(this.either.FromRight(default));
            }

            return this;
        }

        public TryCatch<T> Try<T>(
            Func<TResult, T> onSuccess)
        {
            TryCatch<T> matchResult;
            if (this.Succeeded)
            {
                matchResult = TryCatch<T>.FromResult(onSuccess(this.either.FromRight(default)));
            }
            else
            {
                matchResult = TryCatch<T>.FromException(this.either.FromLeft(default));
            }

            return matchResult;
        }

        public async Task<TryCatch<T>> TryAsync<T>(
            Func<TResult, Task<T>> onSuccess)
        {
            TryCatch<T> matchResult;
            if (this.Succeeded)
            {
                matchResult = TryCatch<T>.FromResult(await onSuccess(this.either.FromRight(default)));
            }
            else
            {
                matchResult = TryCatch<T>.FromException(this.either.FromLeft(default));
            }

            return matchResult;
        }

        public TryCatch<TResult> Catch(
            Action<Exception> onError)
        {
            if (!this.Succeeded)
            {
                onError(this.either.FromLeft(default));
            }

            return this;
        }

        public TryCatch<TResult> Catch(
            Func<Exception, TryCatch<TResult>> onError)
        {
            if (!this.Succeeded)
            {
                return onError(this.either.FromLeft(default));
            }

            return this;
        }

        public async Task<TryCatch<TResult>> CatchAsync(
            Func<Exception, Task> onError)
        {
            if (!this.Succeeded)
            {
                await onError(this.either.FromLeft(default));
            }

            return this;
        }

        public async Task<TryCatch<TResult>> CatchAsync(
            Func<Exception, Task<TryCatch<TResult>>> onError)
        {
            if (!this.Succeeded)
            {
                return await onError(this.either.FromLeft(default));
            }

            return this;
        }

        public void ThrowIfFailed()
        {
            if (!this.Succeeded)
            {
                ExceptionDispatchInfo.Capture(this.Exception).Throw();
            }
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is TryCatch<TResult> other)
            {
                return this.Equals(other);
            }

            return false;
        }

        public bool Equals(TryCatch<TResult> other)
        {
            return this.either.Equals(other.either);
        }

        public override int GetHashCode()
        {
            return this.either.GetHashCode();
        }

        public static TryCatch<TResult> FromResult(TResult result)
        {
            return new TryCatch<TResult>(result);
        }

        public static TryCatch<TResult> FromException(Exception exception)
        {
            // Skipping a stack frame, since we don't want this method showing up in the stack trace.
            StackTrace stackTrace = new StackTrace(skipFrames: 1);
            return new TryCatch<TResult>(
                new ExceptionWithStackTraceException(
                    message: $"{nameof(TryCatch<TResult>)} resulted in an exception.",
                    innerException: exception,
                    stackTrace: stackTrace));
        }

        public static bool ConvertToTryGet<T>(TryCatch<T> tryCatch, out T result)
        {
            if (tryCatch.Failed)
            {
                result = default;
                return false;
            }

            result = tryCatch.Result;
            return true;
        }

        public static T UnsafeGetResult<T>(TryCatch<T> tryCatch)
        {
            tryCatch.ThrowIfFailed();
            return tryCatch.Result;
        }

        public static Task<T> UnsafeGetResultAsync<T>(Task<TryCatch<T>> tryCatch, CancellationToken cancellationToken)
        {
            return tryCatch
                .ContinueWith(antecedent =>
                {
                    antecedent.Result.ThrowIfFailed();
                    return antecedent.Result.Result;
                }, cancellationToken);
        }
    }
}

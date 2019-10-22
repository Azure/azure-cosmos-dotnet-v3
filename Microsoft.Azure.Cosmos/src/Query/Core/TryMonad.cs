// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Threading.Tasks;

    internal struct TryMonad<TResult, TException>
        where TException : Exception
    {
        private readonly TResult result;
        private readonly TException exception;

        private TryMonad(
            TResult result,
            TException exception,
            bool succeeded)
        {
            this.result = result;
            this.exception = exception;
            this.Succeeded = succeeded;
        }

        public bool Succeeded { get; }

        public TResult Result
        {
            get
            {
                if (this.Succeeded)
                {
                    return this.result;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Tried to get the result of a {nameof(TryMonad<TResult, TException>)} that ended in an exception.");
                }
            }
        }

        public TException Exception
        {
            get
            {
                if (!this.Succeeded)
                {
                    return this.exception;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Tried to get the exception of a {nameof(TryMonad<TResult, TException>)} that ended in a result.");
                }
            }
        }

        public static TryMonad<TResult, TException> FromResult(TResult result)
        {
            return new TryMonad<TResult, TException>(
                result: result,
                exception: default,
                succeeded: true);
        }

        public static TryMonad<TResult, TException> FromException(TException exception)
        {
            return new TryMonad<TResult, TException>(
                result: default,
                exception: exception,
                succeeded: false);
        }

        public void Match(
            Action<TResult> onSuccess,
            Action<TException> onError)
        {
            if (this.Succeeded)
            {
                onSuccess(this.result);
            }
            else
            {
                onError(this.exception);
            }
        }

        public TResult ThrowIfException
        {
            get
            {
                if (this.Succeeded)
                {
                    return this.result;
                }

                throw this.exception;
            }
        }

        public TryMonad<TResult, TException> Try(
            Action<TResult> onSuccess)
        {
            if (this.Succeeded)
            {
                onSuccess(this.result);
            }

            return this;
        }

        public TryMonad<T, TException> Try<T>(
            Func<TResult, T> onSuccess)
        {
            TryMonad<T, TException> matchResult;
            if (this.Succeeded)
            {
                try
                {
                    matchResult = TryMonad<T, TException>.FromResult(onSuccess(this.result));
                }
                catch (TException ex)
                {
                    matchResult = TryMonad<T, TException>.FromException(ex);
                }
            }
            else
            {
                matchResult = TryMonad<T, TException>.FromException(this.exception);
            }

            return matchResult;
        }

        public async Task<TryMonad<T, TException>> TryAsync<T>(
            Func<TResult, Task<T>> onSuccess)
        {
            TryMonad<T, TException> matchResult;
            if (this.Succeeded)
            {
                try
                {
                    matchResult = TryMonad<T, TException>.FromResult(await onSuccess(this.result));
                }
                catch (TException ex)
                {
                    matchResult = TryMonad<T, TException>.FromException(ex);
                }
            }
            else
            {
                matchResult = TryMonad<T, TException>.FromException(this.exception);
            }

            return matchResult;
        }

        public TryMonad<TResult, TException> Catch(
            Action<TException> onError)
        {
            if (!this.Succeeded)
            {
                onError(this.exception);
            }

            return this;
        }

        public TryMonad<TResult, TException> Catch(
            Func<TException, TryMonad<TResult, TException>> onError)
        {
            if (!this.Succeeded)
            {
                return onError(this.exception);
            }

            return this;
        }

        public async Task<TryMonad<TResult, TException>> CatchAsync(
            Func<TException, Task> onError)
        {
            if (!this.Succeeded)
            {
                await onError(this.exception);
            }

            return this;
        }

        public async Task<TryMonad<TResult, TException>> CatchAsync(
            Func<TException, Task<TryMonad<TResult, TException>>> onError)
        {
            if (!this.Succeeded)
            {
                return await onError(this.exception);
            }

            return this;
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is TryMonad<TResult, TException> other)
            {
                return this.Equals(other);
            }

            return false;
        }

        public bool Equals(TryMonad<TResult, TException> other)
        {
            return this.Equals(other, (result1, result2) => result1.Equals(result2));
        }

        public bool Equals(TryMonad<TResult, TException> other, Func<TResult, TResult, bool> equalsCallback)
        {
            if (this.Succeeded == other.Succeeded)
            {
                if (this.Succeeded)
                {
                    if (object.ReferenceEquals(this.result, other.result))
                    {
                        return true;
                    }

                    if (this.result == null || other.result == null)
                    {
                        return false;
                    }

                    return equalsCallback(this.result, other.result);
                }
                else
                {
                    if (object.ReferenceEquals(this.exception, other.exception))
                    {
                        return true;
                    }

                    if (this.exception == null || other.exception == null)
                    {
                        return false;
                    }

                    return this.exception.Equals(other.exception);
                }
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Succeeded.GetHashCode() ^
                (this.result != null ? this.result.GetHashCode() : 0) ^
                (this.exception != null ? this.exception.GetHashCode() : 0);
        }
    }
}

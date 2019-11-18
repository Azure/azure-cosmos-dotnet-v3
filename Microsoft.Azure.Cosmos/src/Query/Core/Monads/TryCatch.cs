// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Monads
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading.Tasks;

    internal readonly struct TryCatch<TResult>
    {
        private readonly Either<Exception, TResult> either;

        private TryCatch(Either<Exception, TResult> either)
        {
            this.either = either;
        }

        public bool Succeeded
        {
            get { return this.either.IsRight; }
        }

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
            if (exception.StackTrace != null)
            {
                // If the exception already has a stack trace, then let's preserve it
            }
        }

        /// <summary>
        /// https://stackoverflow.com/questions/37093261/attach-stacktrace-to-exception-without-throwing-in-c-sharp-net
        /// </summary>
        private static class ExceptionStackTraceModifer
        {
            private static readonly FieldInfo StrackTraceStringFieldInfo = typeof(Exception).GetField(
                "_stackTraceString",
                BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly Type TraceFormatTypeInfo = Type.GetType("System.Diagnostics.StackTrace").GetNestedType(
                "TraceFormat",
                BindingFlags.NonPublic);
            private static readonly MethodInfo TraceToStringMethodInfo = typeof(StackTrace).GetMethod(
                "ToString",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[]
                {
                    TraceFormatTypeInfo
                },
                null);

            public static void SetStackTrace(Exception target)
            {
                // Create stack trace, but skip 2 frames (SetStackTrace + TryCatch<TResult>.FromException)
                StackTrace stackTrace = new StackTrace(skipFrames: 2);

                object getStackTraceString = TraceToStringMethodInfo.Invoke(
                    stackTrace,
                    new object[]
                    {
                        Enum.GetValues(TraceFormatTypeInfo).GetValue(0)
                    });
                StrackTraceStringFieldInfo.SetValue(target, getStackTraceString);
            }
        }

        private sealed class ExceptionWithStackTrace : Exception
        {
            private readonly StackTrace stackTrace;

            public ExceptionWithStackTrace(Exception ex)
                : base(message: $"{nameof(TryCatch<object>)} monad ended in exception.", innerException: ex)
            {
                // Create stack trace, but skip 2 frames (constructor + TryCatch<TResult>.FromException)
                StackTrace stackTrace = new StackTrace(skipFrames: 2);
                this.stackTrace = stackTrace;
            }

            public override string StackTrace => this.stackTrace.ToString();
        }
    }
}

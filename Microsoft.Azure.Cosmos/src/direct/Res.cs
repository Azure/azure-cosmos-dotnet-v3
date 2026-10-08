//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;

    /// <summary>
    /// Generic value-type result for the exceptionless path.
    /// Either contains a successful <typeparamref name="T"/> value or an
    /// <see cref="Exception"/> describing what went wrong.
    /// </summary>
    internal readonly struct Res<T>
    {
        private readonly T value;

        internal Res(T value, Exception exception)
        {
            this.value = value;
            this.Exception = exception;
        }

        public T Value
        {
            get
            {
                if (this.Exception != null)
                {
                    throw new InvalidOperationException(
                        "Cannot access Value on a faulted Result. Check IsSuccess first.",
                        this.Exception);
                }

                return this.value;
            }
        }

        public Exception Exception { get; }

        public bool IsSuccess => this.Exception == null;

        /// <summary>
        /// Returns the value if successful, or rethrows the original exception
        /// preserving its stack trace.
        /// </summary>
        public T ValueOrThrow()
        {
            if (this.Exception != null)
            {
                ExceptionDispatchInfo.Capture(this.Exception).Throw();
            }

            return this.value;
        }
    }

    /// <summary>
    /// Static factory and helper methods for <see cref="Res{T}"/>.
    /// </summary>
    internal static class Res
    {
        private static readonly Task<Exception> CompletedNullTask = Task.FromResult<Exception>(null);

        public static Res<T> Success<T>(T value)
        {
            return new Res<T>(value, exception: null);
        }

        public static Res<T> FromException<T>(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return new Res<T>(default, exception: UnwrapAggregateException(exception));
        }

        public static Task<Res<T>> TaskFromException<T>(Exception exception)
        {
            return Task.FromResult(FromException<T>(exception));
        }

        public static Task<Res<T>> TaskSuccess<T>(T value)
        {
            return Task.FromResult(Success(value));
        }

        /// <summary>
        /// Wraps a <see cref="Task{T}"/> so that any exception is captured in a
        /// <see cref="Res{T}"/> instead of propagating through the caller's
        /// async state machine.
        /// </summary>
        public static Task<Res<T>> Wrap<T>(Task<T> task)
        {
            if (task.IsCompleted)
            {
                return Task.FromResult(ToRes(task));
            }

            TaskCompletionSource<Res<T>> completionSource = new TaskCompletionSource<Res<T>>();
            Task ignored = task.ContinueWith(
                static (completedTask, state) =>
                {
                    TaskCompletionSource<Res<T>> source = (TaskCompletionSource<Res<T>>)state;
                    source.SetResult(ToRes(completedTask));
                },
                completionSource);

            return completionSource.Task;

            static Res<T> ToRes(Task<T> completedTask)
            {
                (T value, Exception exception) = completedTask.GetValueOrException();
                return exception == null ? Res.Success(value) : Res.FromException<T>(exception);
            }
        }

        /// <summary>
        /// Wraps a non-generic <see cref="Task"/> so that any exception is captured
        /// instead of propagating through the caller's async state machine.
        /// Returns null on success, or the unwrapped exception on failure.
        /// </summary>
        public static Task<Exception> Wrap(Task task)
        {
            if (task.IsCompleted)
            {
                Exception ex = GetExceptionFromCompletedTask(task);
                return ex == null ? CompletedNullTask : Task.FromResult(UnwrapAggregateException(ex));
            }

            TaskCompletionSource<Exception> tcs = new();
            Task ignored = task.ContinueWith(static (t, state) =>
            {
                TaskCompletionSource<Exception> tcs = (TaskCompletionSource<Exception>)state;
                Exception ex = GetExceptionFromCompletedTask(t);
                tcs.SetResult(UnwrapAggregateException(ex));
            },
                tcs);

            return tcs.Task;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Exception UnwrapAggregateException(Exception ex)
        {
            return ex is AggregateException agg
                ? (agg.InnerException ?? agg)
                : ex;
        }

        private static Exception GetExceptionFromCompletedTask(Task task)
        {
            Debug.Assert(task.IsCompleted);

            if (task.Status == TaskStatus.RanToCompletion)
            {
                return null;
            }

            Exception ex = task.Exception;
            if (ex == null)
            {
                Debug.Assert(task.IsCanceled);
                ex = new OperationCanceledException();
            }

            return ex;
        }
    }
}

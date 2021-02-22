namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.AsyncEnumerable;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TraceWithAsyncEnumerableTests
    {
        [TestMethod]
        public async Task Aasdf()
        {
            {
                ITrace rootTrace;
                using (rootTrace = Trace.GetRootTrace("Root Trace"))
                {
                    OuterTraceableAsyncEnumerable blah = new OuterTraceableAsyncEnumerable(rootTrace);
                    await foreach (int value in blah)
                    {
                        Console.WriteLine(value);
                    }
                }

                string traceText = TraceWriter.TraceToText(rootTrace);
                Console.WriteLine(traceText);
            }

            {
                ITrace rootTrace;
                using (rootTrace = Trace.GetRootTrace("Root Trace"))
                {
                    OuterTraceableAsyncEnumerable enumerable = new OuterTraceableAsyncEnumerable(rootTrace);
                    ITraceableAsyncEnumerator<int> enumerator = enumerable.GetAsyncEnumerator(cancellationToken: default, rootTrace);
                    while (await enumerator.MoveNextAsync(rootTrace))
                    {
                        Console.WriteLine(enumerator.Current);
                    }
                }

                string traceText = TraceWriter.TraceToText(rootTrace);
                Console.WriteLine(traceText);
            }

            {
                static async IAsyncEnumerable<int> GetNumbers(ITrace trace)
                {
                    OuterTraceableAsyncEnumerable enumerable = new OuterTraceableAsyncEnumerable(trace);
                    ITraceableAsyncEnumerator<int> enumerator = enumerable.GetAsyncEnumerator(cancellationToken: default, trace);
                    while (await enumerator.MoveNextAsync(trace))
                    {
                        yield return enumerator.Current;
                    }
                }

                ITrace rootTrace;
                using (rootTrace = Trace.GetRootTrace("Root Trace"))
                {
                    IAsyncEnumerable<int> numbers = GetNumbers(rootTrace);
                    await foreach (int number in numbers)
                    {
                        Console.WriteLine(number);
                    }
                }

                string traceText = TraceWriter.TraceToText(rootTrace);
                Console.WriteLine(traceText);
            }
        }

        private sealed class OuterTraceableAsyncEnumerable : ITraceableAsyncEnumerable<int>
        {
            private readonly ITraceableAsyncEnumerable<int> source;
            private readonly ITrace trace;

            public OuterTraceableAsyncEnumerable(ITraceableAsyncEnumerable<int> source, ITrace trace)
            {
                this.source = source;
                this.trace = trace;
            }

            public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return this.GetAsyncEnumerator(cancellationToken, this.trace);
            }

            public ITraceableAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken, ITrace trace)
            {
                InnerTraceableAsyncEnumerable source = new InnerTraceableAsyncEnumerable(cancellationToken, trace);
                return new OuterTraceableAsyncEnumerator(
                    source.GetAsyncEnumerator(cancellationToken, trace), 
                    cancellationToken, 
                    trace);
            }
        }

        private sealed class OuterTraceableAsyncEnumerator : ITraceableAsyncEnumerator<int>
        {
            private readonly CancellationToken cancellationToken;
            private readonly ITrace trace;
            private readonly ITraceableAsyncEnumerator<int> source;

            public OuterTraceableAsyncEnumerator(ITraceableAsyncEnumerator<int> source, CancellationToken cancellationToken, ITrace trace)
            {
                this.cancellationToken = cancellationToken;
                this.trace = trace;
                this.source = source;
            }

            public int Current { get; set; }

            public ValueTask DisposeAsync()
            {
                return default;
            }

            public ValueTask<bool> MoveNextAsync()
            {
                return this.MoveNextAsync(this.trace);
            }

            public async ValueTask<bool> MoveNextAsync(ITrace trace)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                using (ITrace childTrace = trace.StartChild("Outer Move Next Async"))
                {
                    bool movedNext = await this.source.MoveNextAsync(childTrace);
                    this.Current = this.source.Current;

                    return movedNext;
                }
            }
        }

        private sealed class InnerTraceableAsyncEnumerable : ITraceableAsyncEnumerable<int>
        {
            private readonly CancellationToken cancellationToken;
            private readonly ITrace trace;

            public InnerTraceableAsyncEnumerable(CancellationToken cancellationToken, ITrace trace)
            {
                this.cancellationToken = cancellationToken;
                this.trace = trace;
            }

            public ITraceableAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken, ITrace trace)
            {
                throw new NotImplementedException();
            }

            public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class InnerTraceableAsyncEnumerator : ITraceableAsyncEnumerator<int>
        {
            private readonly CancellationToken cancellationToken;
            private readonly ITrace trace;
            private readonly IEnumerator<int> enumerator = new List<int>() { 1, 2, 3 }.GetEnumerator();

            public int Current { get; set; }

            public ValueTask DisposeAsync()
            {
                return default;
            }

            public ValueTask<bool> MoveNextAsync()
            {
                return this.MoveNextAsync(this.trace);
            }

            public ValueTask<bool> MoveNextAsync(ITrace trace)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                using (ITrace childTrace = trace.StartChild("Inner Move Next Async"))
                {
                    bool movedNext = this.enumerator.MoveNext();
                    this.Current = this.enumerator.Current;

                    return new ValueTask<bool>(movedNext);
                }
            }
        }

    }
}

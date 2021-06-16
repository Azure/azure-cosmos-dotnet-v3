// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    [Config(typeof(SdkBenchmarkConfiguration))]
    public class ITraceBenchmark
    {
        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public void CreateITrace()
        {
            this.CreateITraceTree();
        }

        private ITrace CreateITraceTree()
        {
            ITrace root;
            using (root = Tracing.Trace.GetRootTrace("Root Trace"))
            {
                using (ITrace firstlevel = root.StartChild("first"))
                {
                    using (ITrace secondLevel = firstlevel.StartChild("second"))
                    {
                        using (ITrace thirdLevel = secondLevel.StartChild("third"))
                        {
                            using (ITrace fourthLevel = thirdLevel.StartChild("fourth"))
                            {

                            }
                            using (ITrace fourthLevel = thirdLevel.StartChild("fourth"))
                            {

                            }
                            using (ITrace fourthLevel = thirdLevel.StartChild("fourth"))
                            {

                            }
                        }
                    }
                }
            }

            return root;
        }
    }
}
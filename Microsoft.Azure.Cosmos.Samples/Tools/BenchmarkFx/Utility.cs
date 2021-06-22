//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;

    internal static class Utility
    {
        public static void TeeTraceInformation(string payload)
        {
            Console.WriteLine(payload);
            Trace.TraceInformation(payload);
        }

        public static void TeePrint(string format, params object[] arg)
        {
            string payload = string.Format(format, arg);
            Utility.TeeTraceInformation(payload);
        }
    }
}

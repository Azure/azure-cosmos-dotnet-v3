//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

    public static class CosmosDiagnosticsLogger
    {
        private readonly static ConcurrentQueue<CosmosDiagnostics> CosmosDiagnosticsToLog = new ConcurrentQueue<CosmosDiagnostics>();
        private static TimeSpan maxTimeSpan = TimeSpan.Zero;
        private static readonly int MaxSize = 2;
        private static readonly TimeSpan minimumDelayBetweenDiagnostics = TimeSpan.FromSeconds(10);
        private static readonly Stopwatch stopwatch = new Stopwatch();

        public static void Log(CosmosDiagnostics cosmosDiagnostics)
        {
            TimeSpan elapsedTime = cosmosDiagnostics.GetClientElapsedTime();
            // Require the diagnostics to be at least 10 seconds apart to avoid getting the
            // diagnostics from the exact same time frame to avoid the same issue multiple times
            if (stopwatch.Elapsed > CosmosDiagnosticsLogger.minimumDelayBetweenDiagnostics &&
                elapsedTime > CosmosDiagnosticsLogger.maxTimeSpan)
            {
                stopwatch.Restart();
                maxTimeSpan = elapsedTime;
                CosmosDiagnosticsLogger.CosmosDiagnosticsToLog.Enqueue(cosmosDiagnostics);
                if(CosmosDiagnosticsToLog.Count > MaxSize)
                {
                    CosmosDiagnosticsToLog.TryDequeue(out _);
                }
            }
        }

        public static JArray GetDiagnostics()
        {
            if (!CosmosDiagnosticsToLog.Any())
            {
                return null;
            }

            JArray jArray = new JArray();
            foreach(CosmosDiagnostics cosmosDiagnostics in CosmosDiagnosticsLogger.CosmosDiagnosticsToLog)
            {
                try
                {
                    JObject jObject = JObject.Parse(cosmosDiagnostics.ToString());
                    jArray.Add(jObject);
                }
                catch(Exception)
                {
                    JObject jObject = new JObject
                    {
                        { "Exception", cosmosDiagnostics.ToString() }
                    };

                    jArray.Add(jObject);
                }
            }

            return jArray;
        }
    }
}

//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

    public static class CosmosDiagnosticsLogger
    {
        private readonly static ConcurrentQueue<CosmosDiagnostics> CosmosDiagnosticsToLog = new ConcurrentQueue<CosmosDiagnostics>();
        private static TimeSpan maxTimeSpan = TimeSpan.Zero;
        private static readonly int MaxSize = 2;

        public static void Log(CosmosDiagnostics cosmosDiagnostics)
        {
            TimeSpan elapsedTime = cosmosDiagnostics.GetClientElapsedTime();
            if (elapsedTime > CosmosDiagnosticsLogger.maxTimeSpan)
            {
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
            foreach(CosmosDiagnostics cosmosDiagnostics in CosmosDiagnosticsToLog)
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

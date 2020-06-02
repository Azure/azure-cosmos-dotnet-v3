//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    internal static class DefaultTrace
    {
        public static void Flush() { }
        public static void TraceCritical(string message) { }
        public static void TraceCritical(string format, params object[] args) { }
        public static void TraceError(string message) { }
        public static void TraceError(string format, params object[] args) { }
        public static void TraceInformation(string message) { }
        public static void TraceInformation(string format, params object[] args) { }
        public static void TraceVerbose(string message) { }
        public static void TraceVerbose(string format, params object[] args) { }
        public static void TraceWarning(string message) { }
        public static void TraceWarning(string format, params object[] args) { }
    }

}

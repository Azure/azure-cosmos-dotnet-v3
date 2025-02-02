namespace TestWorkloadV2
{
    using System;
    using System.Net;

    internal ref struct ResponseAttributes
    {
        public HttpStatusCode StatusCode;

        public double RequestCharge;

        // public object Diagnostics;

        // public TimeSpan RequestLatency;
    }
}

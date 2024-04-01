namespace TestWorkloadV2
{
    using System;
    using System.Net;

    internal ref struct ResponseAttributes
    {
        public HttpStatusCode StatusCode;

//        public TimeSpan RequestLatency;

        public double RequestCharge;
    }
}

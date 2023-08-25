//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;

    internal sealed class RuntimePerfCounters : IDisposable
    {
#pragma warning disable SA1401 // Fields should be private
        public static RuntimePerfCounters Counters = new RuntimePerfCounters("DocDB Frontend Runtime", "Counters for DocDB Frontend Runtime");
#pragma warning restore SA1401 // Fields should be private

        private string performanceCategory;

        private string performanceCategoryHelp;

        private PerformanceCounter requestsWithUserErrors;

        private PerformanceCounter requestsWithSystemErrors;

        private PerformanceCounter averageRequestSize;

        private PerformanceCounter averageRequestSizeBase;

        private PerformanceCounter averageResponseSize;

        private PerformanceCounter averageResponseSizeBase;

        private PerformanceCounter networkInBytesPerSec;

        private PerformanceCounter networkOutBytesPerSec;

        private PerformanceCounter deleteRequestsPerSec;

        private PerformanceCounter getRequestsPerSec;

        private PerformanceCounter headRequestsPerSec;

        private PerformanceCounter patchRequestsPerSec;

        private PerformanceCounter postRequestsPerSec;

        private PerformanceCounter putRequestsPerSec;

        private PerformanceCounter averageDeleteRequestDuration;

        private PerformanceCounter averageDeleteRequestDurationBase;

        private PerformanceCounter averageGetRequestDuration;

        private PerformanceCounter averageGetRequestDurationBase;

        private PerformanceCounter averageHeadRequestDuration;

        private PerformanceCounter averageHeadRequestDurationBase;

        private PerformanceCounter averagePatchRequestDuration;

        private PerformanceCounter averagePatchRequestDurationBase;

        private PerformanceCounter averagePostRequestDuration;

        private PerformanceCounter averagePostRequestDurationBase;

        private PerformanceCounter averagePutRequestDuration;

        private PerformanceCounter averagePutRequestDurationBase;

        private RuntimePerfCounters(string category, string categoryHelp)
        {
            this.performanceCategory = category;
            this.performanceCategoryHelp = categoryHelp;
        }

        public PerformanceCounter RequestsWithUserErrors
        {
            get
            {
                return this.requestsWithUserErrors;
            }
        }

        public PerformanceCounter RequestsWithSystemErrors
        {
            get
            {
                return this.requestsWithSystemErrors;
            }
        }

        public PerformanceCounter AverageRequestSize
        {
            get
            {
                return this.averageRequestSize;
            }
        }

        public PerformanceCounter AverageRequestSizeBase
        {
            get
            {
                return this.averageRequestSizeBase;
            }
        }

        public PerformanceCounter AverageResponseSize
        {
            get
            {
                return this.averageResponseSize;
            }
        }

        public PerformanceCounter AverageResponseSizeBase
        {
            get
            {
                return this.averageResponseSizeBase;
            }
        }

        public PerformanceCounter NetworkInBytesPerSec
        {
            get
            {
                return this.networkInBytesPerSec;
            }
        }

        public PerformanceCounter NetworkOutBytesPerSec
        {
            get
            {
                return this.networkOutBytesPerSec;
            }
        }

        public PerformanceCounter DeleteRequestsPerSec
        {
            get
            {
                return this.deleteRequestsPerSec;
            }
        }

        public PerformanceCounter GetRequestsPerSec
        {
            get
            {
                return this.getRequestsPerSec;
            }
        }

        public PerformanceCounter HeadRequestsPerSec
        {
            get
            {
                return this.headRequestsPerSec;
            }
        }

        public PerformanceCounter PatchRequestsPerSec
        {
            get
            {
                return this.patchRequestsPerSec;
            }
        }

        public PerformanceCounter PostRequestsPerSec
        {
            get
            {
                return this.postRequestsPerSec;
            }
        }

        public PerformanceCounter PutRequestsPerSec
        {
            get
            {
                return this.putRequestsPerSec;
            }
        }

        public PerformanceCounter AverageDeleteRequestDuration
        {
            get
            {
                return this.averageDeleteRequestDuration;
            }
        }

        public PerformanceCounter AverageDeleteRequestDurationBase
        {
            get
            {
                return this.averageDeleteRequestDurationBase;
            }
        }

        public PerformanceCounter AverageGetRequestDuration
        {
            get
            {
                return this.averageGetRequestDuration;
            }
        }

        public PerformanceCounter AverageGetRequestDurationBase
        {
            get
            {
                return this.averageGetRequestDurationBase;
            }
        }

        public PerformanceCounter AverageHeadRequestDuration
        {
            get
            {
                return this.averageHeadRequestDuration;
            }
        }

        public PerformanceCounter AverageHeadRequestDurationBase
        {
            get
            {
                return this.averageHeadRequestDurationBase;
            }
        }

        public PerformanceCounter AveragePatchRequestDuration
        {
            get
            {
                return this.averagePatchRequestDuration;
            }
        }

        public PerformanceCounter AveragePatchRequestDurationBase
        {
            get
            {
                return this.averagePatchRequestDurationBase;
            }
        }

        public PerformanceCounter AveragePostRequestDuration
        {
            get
            {
                return this.averagePostRequestDuration;
            }
        }

        public PerformanceCounter AveragePostRequestDurationBase
        {
            get
            {
                return this.averagePostRequestDurationBase;
            }
        }

        public PerformanceCounter AveragePutRequestDuration
        {
            get
            {
                return this.averagePutRequestDuration;
            }
        }

        public PerformanceCounter AveragePutRequestDurationBase
        {
            get
            {
                return this.averagePutRequestDurationBase;
            }
        }

        // Create a perf counter category with the provided name and creates the provided list of perf counters inside
        // the category.
        public static void CreatePerfCounterCategory(
            string category,
            string categoryHelp,
            PerformanceCounterCategoryType categoryType,
            CounterCreationDataCollection counters)
        {
            // If the performance counter category already exists, check if any counters have changed.
            if (PerformanceCounterCategory.Exists(category))
            {
                PerformanceCounterCategory perfCategory = new PerformanceCounterCategory(category);
                bool shouldReturn = true;
                foreach (CounterCreationData counter in counters)
                {
                    try
                    {
                        if (!perfCategory.CounterExists(counter.CounterName))
                        {
                            shouldReturn = false;
                            break;
                        }
                    }
                    catch
                    {
                        shouldReturn = false;
                        break;
                    }
                }

                if (shouldReturn)
                {
                    return;
                }
                else
                {
                    PerformanceCounterCategory.Delete(category);
                }
            }

            // Create the category.
            PerformanceCounterCategory.Create(category, categoryHelp, categoryType, counters);
        }

        public void InstallCounters()
        {
            CounterCreationDataCollection counters = new CounterCreationDataCollection();

            counters.Add(new CounterCreationData("Requests with User Errors", "Number of requests with a user error (4xx code)", PerformanceCounterType.CounterDelta32));

            counters.Add(new CounterCreationData("Requests with System Errors", "Number of requests with a user error (5xx code)", PerformanceCounterType.CounterDelta32));

            counters.Add(new CounterCreationData("Average Request Size", "Average Request Size", PerformanceCounterType.AverageCount64));

            counters.Add(new CounterCreationData("Average Request Size Base", "Average Request Size Base", PerformanceCounterType.AverageBase));

            counters.Add(new CounterCreationData("Average Response Size", "Average Response Size", PerformanceCounterType.AverageCount64));

            counters.Add(new CounterCreationData("Average Response Size Base", "Average Response Size Base", PerformanceCounterType.AverageBase));

            counters.Add(new CounterCreationData("Network In Bytes/sec", "Network In Bytes per second ", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("Network Out Bytes/sec", "Network Out Bytes per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("DELETE Requests/sec", "DELETE Requests per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("GET Requests/sec", "GET Requests per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("HEAD Requests/sec", "HEAD Requests per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("PATCH Requests/sec", "PATCH Requests per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("POST Requests/sec", "POST Requests per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("PUT Requests/sec", "PUT Requests per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("Average DELETE Request Duration", "Average Duration of a DELETE Request", PerformanceCounterType.AverageTimer32));

            counters.Add(new CounterCreationData("Average DELETE Request Duration Base", "Average Duration of a DELETE Request Base", PerformanceCounterType.AverageBase));

            counters.Add(new CounterCreationData("Average GET Request Duration", "Average Duration of a GET Request", PerformanceCounterType.AverageTimer32));

            counters.Add(new CounterCreationData("Average GET Request Duration Base", "Average Duration of a GET Request Base", PerformanceCounterType.AverageBase));

            counters.Add(new CounterCreationData("Average HEAD Request Duration", "Average Duration of a HEAD Request", PerformanceCounterType.AverageTimer32));

            counters.Add(new CounterCreationData("Average HEAD Request Duration Base", "Average Duration of a HEAD Request Base", PerformanceCounterType.AverageBase));

            counters.Add(new CounterCreationData("Average PATCH Request Duration", "Average Duration of a PUT Request", PerformanceCounterType.AverageTimer32));

            counters.Add(new CounterCreationData("Average PATCH Request Duration Base", "Average Duration of a PUT Request Base", PerformanceCounterType.AverageBase));

            counters.Add(new CounterCreationData("Average POST Request Duration", "Average Duration of a POST Request", PerformanceCounterType.AverageTimer32));

            counters.Add(new CounterCreationData("Average POST Request Duration Base", "Average Duration of a POst Base Request Base", PerformanceCounterType.AverageBase));

            counters.Add(new CounterCreationData("Average PUT Request Duration", "Average Duration of a PUT Request", PerformanceCounterType.AverageTimer32));

            counters.Add(new CounterCreationData("Average PUT Request Duration Base", "Average Duration of a PUT Request Base", PerformanceCounterType.AverageBase));

            RuntimePerfCounters.CreatePerfCounterCategory(this.performanceCategory, this.performanceCategoryHelp, PerformanceCounterCategoryType.SingleInstance, counters);
        }

        public void InitializePerfCounters()
        {
            this.requestsWithUserErrors = new PerformanceCounter(this.performanceCategory, "Requests with User Errors", false);
            this.requestsWithUserErrors.RawValue = 0;

            this.requestsWithSystemErrors = new PerformanceCounter(this.performanceCategory, "Requests with System Errors", false);
            this.requestsWithSystemErrors.RawValue = 0;

            this.averageRequestSize = new PerformanceCounter(this.performanceCategory, "Average Request Size", false);
            this.averageRequestSize.RawValue = 0;

            this.averageRequestSizeBase = new PerformanceCounter(this.performanceCategory, "Average Request Size Base", false);
            this.averageRequestSizeBase.RawValue = 0;

            this.averageResponseSize = new PerformanceCounter(this.performanceCategory, "Average Response Size", false);
            this.averageResponseSize.RawValue = 0;

            this.averageResponseSizeBase = new PerformanceCounter(this.performanceCategory, "Average Response Size Base", false);
            this.averageResponseSizeBase.RawValue = 0;

            this.networkInBytesPerSec = new PerformanceCounter(this.performanceCategory, "Network In Bytes/sec", false);
            this.networkInBytesPerSec.RawValue = 0;

            this.networkOutBytesPerSec = new PerformanceCounter(this.performanceCategory, "Network Out Bytes/sec", false);
            this.networkOutBytesPerSec.RawValue = 0;

            this.deleteRequestsPerSec = new PerformanceCounter(this.performanceCategory, "DELETE Requests/sec", false);
            this.deleteRequestsPerSec.RawValue = 0;

            this.getRequestsPerSec = new PerformanceCounter(this.performanceCategory, "GET Requests/sec", false);
            this.getRequestsPerSec.RawValue = 0;

            this.headRequestsPerSec = new PerformanceCounter(this.performanceCategory, "HEAD Requests/sec", false);
            this.headRequestsPerSec.RawValue = 0;

            this.patchRequestsPerSec = new PerformanceCounter(this.performanceCategory, "PATCH Requests/sec", false);
            this.patchRequestsPerSec.RawValue = 0;

            this.postRequestsPerSec = new PerformanceCounter(this.performanceCategory, "POST Requests/sec", false);
            this.postRequestsPerSec.RawValue = 0;

            this.putRequestsPerSec = new PerformanceCounter(this.performanceCategory, "PUT Requests/sec", false);
            this.putRequestsPerSec.RawValue = 0;

            this.averageDeleteRequestDuration = new PerformanceCounter(this.performanceCategory, "Average DELETE Request Duration", false);
            this.averageDeleteRequestDuration.RawValue = 0;

            this.averageDeleteRequestDurationBase = new PerformanceCounter(this.performanceCategory, "Average DELETE Request Duration Base", false);
            this.averageDeleteRequestDurationBase.RawValue = 0;

            this.averageGetRequestDuration = new PerformanceCounter(this.performanceCategory, "Average GET Request Duration", false);
            this.averageGetRequestDuration.RawValue = 0;

            this.averageGetRequestDurationBase = new PerformanceCounter(this.performanceCategory, "Average GET Request Duration Base", false);
            this.averageGetRequestDurationBase.RawValue = 0;

            this.averageHeadRequestDuration = new PerformanceCounter(this.performanceCategory, "Average HEAD Request Duration", false);
            this.averageHeadRequestDuration.RawValue = 0;

            this.averageHeadRequestDurationBase = new PerformanceCounter(this.performanceCategory, "Average HEAD Request Duration Base", false);
            this.averageHeadRequestDurationBase.RawValue = 0;

            this.averagePatchRequestDuration = new PerformanceCounter(this.performanceCategory, "Average PATCH Request Duration", false);
            this.averagePatchRequestDuration.RawValue = 0;

            this.averagePatchRequestDurationBase = new PerformanceCounter(this.performanceCategory, "Average PATCH Request Duration Base", false);
            this.averagePatchRequestDurationBase.RawValue = 0;

            this.averagePostRequestDuration = new PerformanceCounter(this.performanceCategory, "Average POST Request Duration", false);
            this.averagePostRequestDuration.RawValue = 0;

            this.averagePostRequestDurationBase = new PerformanceCounter(this.performanceCategory, "Average POST Request Duration Base", false);
            this.averagePostRequestDurationBase.RawValue = 0;

            this.averagePutRequestDuration = new PerformanceCounter(this.performanceCategory, "Average PUT Request Duration", false);
            this.averagePutRequestDuration.RawValue = 0;

            this.averagePutRequestDurationBase = new PerformanceCounter(this.performanceCategory, "Average PUT Request Duration Base", false);
            this.averagePutRequestDurationBase.RawValue = 0;
        }

        // Implement the dispose pattern.  The pattern is detailed at:
        //
        //  http://www.bluebytesoftware.com/blog/CategoryView,category,DesignGuideline.aspx
        public void Dispose()
        {
            using (this.requestsWithUserErrors)
            {
            }

            using (this.requestsWithSystemErrors)
            {
            }

            using (this.averageRequestSize)
            {
            }

            using (this.averageRequestSizeBase)
            {
            }

            using (this.averageResponseSize)
            {
            }

            using (this.averageResponseSizeBase)
            {
            }

            using (this.networkInBytesPerSec)
            {
            }

            using (this.networkOutBytesPerSec)
            {
            }

            using (this.deleteRequestsPerSec)
            {
            }

            using (this.getRequestsPerSec)
            {
            }

            using (this.headRequestsPerSec)
            {
            }

            using (this.patchRequestsPerSec)
            {
            }

            using (this.postRequestsPerSec)
            {
            }

            using (this.putRequestsPerSec)
            {
            }

            using (this.averageDeleteRequestDuration)
            {
            }

            using (this.averageDeleteRequestDurationBase)
            {
            }

            using (this.averageGetRequestDuration)
            {
            }

            using (this.averageGetRequestDurationBase)
            {
            }

            using (this.averageHeadRequestDuration)
            {
            }

            using (this.averageHeadRequestDurationBase)
            {
            }

            using (this.averagePatchRequestDuration)
            {
            }

            using (this.averagePatchRequestDurationBase)
            {
            }

            using (this.averagePostRequestDuration)
            {
            }

            using (this.averagePostRequestDurationBase)
            {
            }

            using (this.averagePutRequestDuration)
            {
            }

            using (this.averagePutRequestDurationBase)
            {
            }
        }
    }
}

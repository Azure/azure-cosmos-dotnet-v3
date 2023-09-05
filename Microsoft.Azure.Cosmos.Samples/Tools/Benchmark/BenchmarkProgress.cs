//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a class that is used as an item in CosmosDB to track benchmark progress.
    /// </summary>
    public class BenchmarkProgress
    {

        /// <summary>
        /// Record item id
        /// </summary>
        [JsonProperty]
        public string id { get; set; }

        /// <summary>
        /// Machine name
        /// </summary>
        [JsonProperty]
        public string MachineName { get; set; }

        /// <summary>
        /// Job status STARTED|COMPLETED
        /// </summary>
        [JsonProperty]
        public string JobStatus { get; set; }

        /// <summary>
        /// Job start time .
        /// </summary>
        [JsonProperty]
        public DateTime JobStartTime { get; set; }

        /// <summary>
        /// Job end time .
        /// </summary>
        [JsonProperty]
        public DateTime JobEndTime { get; set; }

    }
}

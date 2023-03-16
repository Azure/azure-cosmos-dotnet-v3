//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Sampler for client telemetry properties
    /// </summary>
    /// <typeparam name="T"> Type of Input data on which Sampling will be provided</typeparam>
    /// <typeparam name="V"> Type of Output data on which Sampling will be provided</typeparam>
    internal interface IClientTelemetrySampler<T, V>
    {
        /// <summary>
        /// Sample Logic for given input data and return dropped request count due to sampling and callback function to be executed on selected data
        /// </summary>
        /// <param name="inputData"></param>
        /// <param name="droppedRntbdRequestCount"></param>
        /// <param name="callback"></param>
        public void Sample(List<T> inputData, out int droppedRntbdRequestCount, Action<T, V> callback);
    }
}

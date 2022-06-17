//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///  Contains the cosmos diagnostic information for the current request to Azure Cosmos DB service.
    /// </summary>
    public abstract class CosmosDiagnostics
    {
        /// <summary>
        /// This represent the end to end elapsed time of the request.
        /// If the request is still in progress it will return the current
        /// elapsed time since the start of the request.
        /// </summary>
        /// <returns>The clients end to end elapsed time of the request.</returns>
        public virtual TimeSpan GetClientElapsedTime()
        {
            // Default implementation avoids breaking change for users upgrading.
            throw new NotImplementedException($"CosmosDiagnostics.GetElapsedTime");
        }

        /// <summary>
        /// This represent the start time of the request.
        /// </summary>
        /// <returns>This returns the start time of the request.</returns>
        public virtual DateTime GetStartTimeUtc()
        {
            // Default implementation avoids breaking change for users upgrading.
            throw new NotImplementedException($"CosmosDiagnostics.GetStartTimeUtc");
        }

        /// <summary>
        ///  This represent the count of failed requests.
        /// </summary>
        /// <returns>The count of failed requests with cosmos service.</returns>
        public virtual int GetFailedRequestCount()
        {
            // Default implementation avoids breaking change for users upgrading.
            throw new NotImplementedException($"CosmosDiagnostics.GetFailedRequestCount");
        }

        /// <summary>
        /// Gets the string field <see cref="CosmosDiagnostics"/> instance in the Azure CosmosDB database service.
        /// </summary>
        /// <returns>The string field <see cref="CosmosDiagnostics"/> instance in the Azure CosmosDB database service.</returns>
        /// <remarks>
        /// <see cref="CosmosDiagnostics"/> implements lazy materialization and is only materialized when <see cref="CosmosDiagnostics.ToString"/> is called.
        /// </remarks>
        /// <example>
        /// Do not eagerly materialize the diagnostics until the moment of consumption to avoid unnecessary allocations, let the ToString be called only when needed.
        /// You can capture diagnostics conditionally, based on latency or errors:
        /// <code language="c#">
        /// <![CDATA[
        /// try
        /// {
        ///     ItemResponse<Book> response = await container.CreateItemAsync<Book>(item: testItem);
        ///     if (response.Diagnostics.GetClientElapsedTime() > ConfigurableSlowRequestTimeSpan)
        ///     {
        ///         // Log the diagnostics and add any additional info necessary to correlate to other logs 
        ///         logger.LogInformation("Operation took longer than expected, Diagnostics: {Diagnostics}");
        ///     }
        /// }
        /// catch (CosmosException cosmosException)
        /// {
        ///     // Log the full exception including the stack trace 
        ///     logger.LogError(cosmosException);
        ///     // The Diagnostics can be logged separately if required.
        ///     logger.LogError("Cosmos DB call failed with {StatusCode}, {SubStatusCode}, Diagnostics: {Diagnostics}", cosmosException.StatusCode, cosmosException.SubStatusCode, cosmosException.Diagnostics);
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract override string ToString();

        /// <summary>
        /// Gets the list of all regions that were contacted for a request
        /// </summary>
        /// <returns>The list of tuples containing the Region name and the URI</returns>
        public abstract IReadOnlyList<(string regionName, Uri uri)> GetContactedRegions();
    }
}

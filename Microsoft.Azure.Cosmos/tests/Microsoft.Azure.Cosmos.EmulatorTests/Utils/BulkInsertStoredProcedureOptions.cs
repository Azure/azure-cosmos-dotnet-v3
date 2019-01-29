//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Interop.Mongo
{
    using Newtonsoft.Json;

    internal sealed class BulkInsertStoredProcedureOptions
    {
        [JsonProperty("disableAutomaticIdGeneration")]
        public bool DisableAutomaticIdGeneration { get; set; }

        [JsonProperty("softStopOnConflict")]
        public bool SoftStopOnConflict { get; set; }

        [JsonProperty("systemCollectionId")]
        public string SystemCollectionId { get; set; }

        [JsonProperty("enableBsonSchema")]
        public bool EnableBsonSchema { get; set; }

        [JsonProperty("enableUpsert")]
        public bool EnableUpsert { get; set; }

        public BulkInsertStoredProcedureOptions(
            bool disableAutomaticIdGeneration,
            bool softStopOnConflict,
            string systemCollectionId,
            bool enableBsonSchema,
            bool enableUpsert = false)
        {
            this.DisableAutomaticIdGeneration = disableAutomaticIdGeneration;
            this.SoftStopOnConflict = softStopOnConflict;
            this.SystemCollectionId = systemCollectionId;
            this.EnableBsonSchema = enableBsonSchema;
            this.EnableUpsert = enableUpsert;
        }
    }
}

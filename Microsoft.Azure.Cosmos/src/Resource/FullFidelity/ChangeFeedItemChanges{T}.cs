//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    /// <summary>
    /// The typed response that contains the current, previous, and metadata change feed resource when <see cref="ChangeFeedMode"/> is initialized to <see cref="ChangeFeedMode.FullFidelity"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif  
        class ChangeFeedItemChanges<T>
    {
        /// <summary>
        /// The full fidelity change feed current item.
        /// </summary>
        [JsonProperty(PropertyName = "current")]
        public T Current { get; set; }

        /// <summary>
        /// The full fidelity change feed metadata.
        /// </summary>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// //
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Remarks.</remarks>
        [JsonProperty(PropertyName = "metadata", NullValueHandling = NullValueHandling.Ignore)]
        public ChangeFeedMetadata Metadata { get; set; }

        /// <summary>
        /// The previous image on replace is not going to be exposed by default, this was done to address COGs concerns from PITR.
        /// For deletes previous image is always going to be provided. To opt-in for previous image for replaces, currently we would
        ///     need to set naming config for a customer(enablePreviousImageForReplaceInFFCF, that’s to for critical scenarios when
        ///     there is high business need, no billing is afffected). Later we will add explicit opt-in in Portal, and customer would
        ///     get a higher bill – these changes need to be developed.
        /// </summary>
        [JsonProperty(PropertyName = "previous", NullValueHandling = NullValueHandling.Ignore)]
        public T Previous { get; set; }
    }
}

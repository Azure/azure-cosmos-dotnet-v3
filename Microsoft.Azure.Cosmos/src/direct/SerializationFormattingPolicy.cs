using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// The formatting policy associated with JSON serialization/de-serialization in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum SerializationFormattingPolicy
    {
        /// <summary> 
        /// No additional formatting required.
        /// </summary>
        None,

        /// <summary>
        /// Indent the fields appropriately.
        /// </summary>
        Indented        
    }
}

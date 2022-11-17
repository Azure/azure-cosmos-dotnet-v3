namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public record Product(
    string Id,
    string Category,
    string Name,
    int Quantity,
    bool Sale
    );
}
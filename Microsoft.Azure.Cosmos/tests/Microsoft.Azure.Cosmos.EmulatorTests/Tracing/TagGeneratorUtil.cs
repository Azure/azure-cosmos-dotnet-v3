//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    
    internal static class TagGeneratorUtil
    {
        public static string GenerateTagForBaselineTest(Activity activity)
        {
            List<string> tagsWithStaticValue = new List<string>
            {
                "kind",
                "az.namespace",
                "db.operation",
                "db.system",
                "net.peer.name",
                "db.cosmosdb.connection_mode",
                "db.cosmosdb.operation_type",
                "db.cosmosdb.regions_contacted"
            };

            StringBuilder builder = new StringBuilder();
            builder.Append("<ACTIVITY>")
                   .Append("<OPERATION>")
                   .Append(activity.OperationName)
                   .Append("</OPERATION>");

            foreach (KeyValuePair<string, string> tag in activity.Tags)
            {
                builder
                .Append("<ATTRIBUTE-KEY>")
                .Append(tag.Key)
                .Append("</ATTRIBUTE-KEY>");

                if (tagsWithStaticValue.Contains(tag.Key))
                {
                    builder
                    .Append("<ATTRIBUTE-VALUE>")
                    .Append(tag.Value)
                    .Append("</ATTRIBUTE-VALUE>");
                }
            }

            builder.Append("</ACTIVITY>");

            return builder.ToString();
        }

    }
}

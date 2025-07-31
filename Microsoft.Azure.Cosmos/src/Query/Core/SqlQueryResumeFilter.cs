//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [DataContract]
    public sealed class SqlQueryResumeFilter
    {
        [DataMember(Name = "value")]
        [JsonPropertyName("value")]
        public IReadOnlyList<SqlQueryResumeValue> ResumeValues { get; }

        [DataMember(Name = "rid", EmitDefaultValue = false)]
        [JsonPropertyName("rid")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Rid { get; set;  }

        [DataMember(Name = "exclude")]
        [JsonPropertyName("exclude")]
        public bool Exclude { get; set; }

        public SqlQueryResumeFilter(
            IReadOnlyList<SqlQueryResumeValue> resumeValues,
            string rid,
            bool exclude)
        {
            if ((resumeValues == null) || (resumeValues.Count == 0))
            {
                throw new ArgumentException($"{nameof(resumeValues)} can not be empty.");
            }

            this.ResumeValues = resumeValues;
            this.Rid = rid;
            this.Exclude = exclude;
        }
    }
}

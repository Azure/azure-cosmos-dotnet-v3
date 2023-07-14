//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Cosmos.Core.Utf8;

    [DataContract]
    internal sealed class SqlQueryResumeFilter
    {
        [DataMember(Name = "value")]
        public IReadOnlyList<ResumeValue> ResumeValues { get; }

        [DataMember(Name = "rid", EmitDefaultValue = false)]
        public string Rid { get; }

        [DataMember(Name = "exclude")]
        public bool Exclude { get; }

        public SqlQueryResumeFilter(
            IReadOnlyList<ResumeValue> resumeValues,
            string rid,
            bool exclude)
        {
            if (resumeValues.Count == 0)
            {
                throw new ArgumentException($"{nameof(resumeValues)} can not be empty.");
            }

            this.ResumeValues = resumeValues;
            this.Rid = rid;
            this.Exclude = exclude;
        }

        public class ResumeValue
        {
            public static class PropertyNames
            {
                public const string Type = "type";
                public const string ArrayType = "array";
                public const string ObjectType = "object";
                public const string Low = "low";
                public const string High = "high";
            }
        }

        public class UndefinedResumeValue : ResumeValue
        {
        }

        public class NullResumeValue : ResumeValue
        {
        }

        public class BooleanResumeValue : ResumeValue 
        {
            public bool Value { get; }

            public BooleanResumeValue(bool value)
            {
                this.Value = value;
            }
        }

        public class NumberResumeValue : ResumeValue
        {
            public Number64 Value { get; }

            public NumberResumeValue(Number64 value)
            {
                this.Value = value;
            }
        }

        public class StringResumeValue : ResumeValue
        {
            public UtfAnyString Value { get; }

            public StringResumeValue(UtfAnyString value)
            {
                this.Value = value;
            }
        }

        public class ArrayResumeValue : ResumeValue
        {
            public UInt128 HashValue { get; }

            public ArrayResumeValue(UInt128 hashValue)
            {
                this.HashValue = hashValue;
            }
        }

        public class ObjectResumeValue : ResumeValue
        {
            public UInt128 HashValue { get; }

            public ObjectResumeValue(UInt128 hashValue)
            {
                this.HashValue = hashValue;
            }
        }
    }
}

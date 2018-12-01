//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Globalization;

    internal abstract class SqlSelectSpecVisitor
    {
        protected SqlSelectSpec Visit(SqlSelectSpec spec)
        {
            switch(spec.Kind)
            {
                case SqlObjectKind.SelectListSpec:
                    return Visit(spec as SqlSelectListSpec);
                case SqlObjectKind.SelectStarSpec:
                    return Visit(spec as SqlSelectStarSpec);
                case SqlObjectKind.SelectValueSpec:
                    return Visit(spec as SqlSelectValueSpec);
                default:
                    throw new InvalidProgramException(
                        string.Format(CultureInfo.InvariantCulture, "Unexpected SqlObjectKind {0}", spec.Kind));
            }
        }

        protected abstract SqlSelectSpec Visit(SqlSelectListSpec spec);
        protected abstract SqlSelectSpec Visit(SqlSelectStarSpec spec);
        protected abstract SqlSelectSpec Visit(SqlSelectValueSpec spec);
    }

}

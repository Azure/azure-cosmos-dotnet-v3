//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;

    internal abstract class CosmosElementException : Exception
    {
        public CosmosElementException(string message = null, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    internal sealed class CosmosElementWrongTypeException : CosmosElementException
    {
        public CosmosElementWrongTypeException(string message = null, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    internal sealed class CosmosElementEmptyBufferException : CosmosElementException
    {
        public CosmosElementEmptyBufferException(string message = null, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    internal sealed class CosmosElementNoPubliclyAccessibleConstructorException : CosmosElementException
    {
        public CosmosElementNoPubliclyAccessibleConstructorException(string message = null, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    internal sealed class CosmosElementCouldNotDetermineWhichConstructorToUseException : CosmosElementException
    {
        public CosmosElementCouldNotDetermineWhichConstructorToUseException(string message = "Could not determine which constructor to use", Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    internal sealed class CosmosElementFailedToFindPropertyException : CosmosElementException
    {
        public CosmosElementFailedToFindPropertyException(string message = null, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}

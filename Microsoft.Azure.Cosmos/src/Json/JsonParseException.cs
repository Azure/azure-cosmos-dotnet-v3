//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System.Net;
    using DocumentClientException = Documents.DocumentClientException;
    using RMResources = Documents.RMResources;

    /// <summary>
    /// Abstract class that all JsonParseExceptions will derive from.
    /// </summary>
    public abstract class JsonParseException : DocumentClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonParseException"/> class.
        /// </summary>
        /// <param name="message">The exception message for the JsonParseException</param>
        protected JsonParseException(string message)
            : base(message, null, HttpStatusCode.BadRequest)
        {
        }
    }

    /// <summary>
    /// JsonParseException for MissingClosingQuote 
    /// </summary>
    public sealed class JsonMissingClosingQuoteException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonMissingClosingQuoteException"/> class.
        /// </summary>
        public JsonMissingClosingQuoteException()
            : base(RMResources.JsonMissingClosingQuote)
        {
        }
    }

    /// <summary>
    /// JsonParseException for NotFieldnameToken 
    /// </summary>
    public sealed class JsonNotFieldnameTokenException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonNotFieldnameTokenException class.
        /// </summary>
        public JsonNotFieldnameTokenException()
            : base(RMResources.JsonNotFieldnameToken)
        {
        }
    }

    /// <summary>
    /// JsonParseException for InvalidParameter 
    /// </summary>
    public sealed class JsonInvalidParameterException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonInvalidParameterException class.
        /// </summary>
        public JsonInvalidParameterException()
            : base(RMResources.JsonInvalidParameter)
        {
        }
    }

    /// <summary>
    /// JsonParseException for NumberTooLong 
    /// </summary>
    public sealed class JsonNumberTooLongException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonNumberTooLongException class.
        /// </summary>
        public JsonNumberTooLongException()
            : base(RMResources.JsonNumberTooLong)
        {
        }
    }

    /// <summary>
    /// JsonParseException for MissingNameSeparator 
    /// </summary>
    public sealed class JsonMissingNameSeparatorException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonMissingNameSeparatorException class.
        /// </summary>
        public JsonMissingNameSeparatorException()
            : base(RMResources.JsonMissingNameSeparator)
        {
        }
    }

    /// <summary>
    /// JsonParseException for UnexpectedToken 
    /// </summary>
    public sealed class JsonUnexpectedTokenException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonUnexpectedTokenException class.
        /// </summary>
        public JsonUnexpectedTokenException()
            : base(RMResources.JsonUnexpectedToken)
        {
        }
    }

    /// <summary>
    /// JsonParseException for UnexpectedEndArray 
    /// </summary>
    public sealed class JsonUnexpectedEndArrayException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonUnexpectedEndArrayException class.
        /// </summary>
        public JsonUnexpectedEndArrayException()
            : base(RMResources.JsonUnexpectedEndArray)
        {
        }
    }

    /// <summary>
    /// JsonParseException for UnexpectedEndObject 
    /// </summary>
    public sealed class JsonUnexpectedEndObjectException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonUnexpectedEndObjectException class.
        /// </summary>
        public JsonUnexpectedEndObjectException()
            : base(RMResources.JsonUnexpectedEndObject)
        {
        }
    }

    /// <summary>
    /// JsonParseException for InvalidToken 
    /// </summary>
    public sealed class JsonInvalidTokenException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonInvalidTokenException class.
        /// </summary>
        public JsonInvalidTokenException()
            : base(RMResources.JsonInvalidToken)
        {
        }
    }

    /// <summary>
    /// JsonParseException for UnexpectedNameSeparator 
    /// </summary>
    public sealed class JsonUnexpectedNameSeparatorException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonUnexpectedNameSeparatorException class.
        /// </summary>
        public JsonUnexpectedNameSeparatorException()
            : base(RMResources.JsonUnexpectedNameSeparator)
        {
        }
    }

    /// <summary>
    /// JsonParseException for UnexpectedValueSeparator 
    /// </summary>
    public sealed class JsonUnexpectedValueSeparatorException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonUnexpectedValueSeparatorException class.
        /// </summary>
        public JsonUnexpectedValueSeparatorException()
            : base(RMResources.JsonUnexpectedValueSeparator)
        {
        }
    }

    /// <summary>
    /// JsonParseException for MissingEndObject 
    /// </summary>
    public sealed class JsonMissingEndObjectException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonMissingEndObjectException class.
        /// </summary>
        public JsonMissingEndObjectException()
            : base(RMResources.JsonMissingEndObject)
        {
        }
    }

    /// <summary>
    /// JsonParseException for MissingEndArray 
    /// </summary>
    internal sealed class JsonMissingEndArrayException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonMissingEndArrayException class.
        /// </summary>
        public JsonMissingEndArrayException()
            : base(RMResources.JsonMissingEndArray)
        {
        }
    }

    /// <summary>
    /// JsonParseException for NotStringToken 
    /// </summary>
    public sealed class JsonNotStringTokenException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonNotStringTokenException class.
        /// </summary>
        public JsonNotStringTokenException()
            : base(RMResources.JsonNotStringToken)
        {
        }
    }

    /// <summary>
    /// JsonParseException for MaxNestingExceeded 
    /// </summary>
    public sealed class JsonMaxNestingExceededException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonMaxNestingExceededException class.
        /// </summary>
        public JsonMaxNestingExceededException()
            : base(RMResources.JsonMaxNestingExceeded)
        {
        }
    }

    /// <summary>
    /// JsonParseException for InvalidEscapedCharacter 
    /// </summary>
    public sealed class JsonInvalidEscapedCharacterException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonInvalidEscapedCharacterException class.
        /// </summary>
        public JsonInvalidEscapedCharacterException()
            : base(RMResources.JsonInvalidEscapedCharacter)
        {
        }
    }

    /// <summary>
    /// JsonParseException for InvalidUnicodeEscape 
    /// </summary>
    public sealed class JsonInvalidUnicodeEscapeException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonInvalidUnicodeEscapeException class.
        /// </summary>
        public JsonInvalidUnicodeEscapeException()
            : base(RMResources.JsonInvalidUnicodeEscape)
        {
        }
    }

    /// <summary>
    /// JsonParseException for InvalidStringCharacter 
    /// </summary>
    public sealed class JsonInvalidStringCharacterException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonInvalidStringCharacterException class.
        /// </summary>
        public JsonInvalidStringCharacterException()
            : base(RMResources.JsonInvalidStringCharacter)
        {
        }
    }

    /// <summary>
    /// JsonParseException for InvalidNumber 
    /// </summary>
    public sealed class JsonInvalidNumberException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonInvalidNumberException class.
        /// </summary>
        public JsonInvalidNumberException()
            : base(RMResources.JsonInvalidNumber)
        {
        }
    }

    /// <summary>
    /// JsonParseException for NotNumberToken 
    /// </summary>
    internal sealed class JsonNotNumberTokenException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonNotNumberTokenException class.
        /// </summary>
        public JsonNotNumberTokenException()
            : base(RMResources.JsonNotNumberToken)
        {
        }
    }

    /// <summary>
    /// JsonParseException for NumberOutOfRange 
    /// </summary>
    public sealed class JsonNumberOutOfRangeException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonNumberOutOfRangeException class.
        /// </summary>
        public JsonNumberOutOfRangeException()
            : base(RMResources.JsonNumberOutOfRange)
        {
        }
    }

    /// <summary>
    /// JsonParseException for MissingProperty 
    /// </summary>
    internal sealed class JsonMissingPropertyException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonMissingPropertyException class.
        /// </summary>
        public JsonMissingPropertyException()
            : base(RMResources.JsonMissingProperty)
        {
        }
    }

    /// <summary>
    /// JsonParseException for PropertyAlreadyAdded 
    /// </summary>
    internal sealed class JsonPropertyAlreadyAddedException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonPropertyAlreadyAddedException class.
        /// </summary>
        public JsonPropertyAlreadyAddedException()
            : base(RMResources.JsonPropertyAlreadyAdded)
        {
        }
    }

    /// <summary>
    /// JsonParseException for ObjectNotStarted 
    /// </summary>
    public sealed class JsonObjectNotStartedException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonObjectNotStartedException class.
        /// </summary>
        public JsonObjectNotStartedException()
            : base(RMResources.JsonObjectNotStarted)
        {
        }
    }

    /// <summary>
    /// JsonParseException for ArrayNotStarted 
    /// </summary>
    internal sealed class JsonArrayNotStartedException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonArrayNotStartedException class.
        /// </summary>
        public JsonArrayNotStartedException()
            : base(RMResources.JsonArrayNotStarted)
        {
        }
    }

    /// <summary>
    /// JsonParseException for PropertyArrayOrObjectNotStarted 
    /// </summary>
    public sealed class JsonPropertyArrayOrObjectNotStartedException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonPropertyArrayOrObjectNotStartedException class.
        /// </summary>
        public JsonPropertyArrayOrObjectNotStartedException()
            : base(RMResources.JsonPropertyArrayOrObjectNotStarted)
        {
        }
    }

    /// <summary>
    /// JsonParseException for NotComplete 
    /// </summary>
    public sealed class JsonNotCompleteException : JsonParseException
    {
        /// <summary>
        /// Initializes a new instance of the JsonNotCompleteException class.
        /// </summary>
        public JsonNotCompleteException()
            : base(RMResources.JsonNotComplete)
        {
        }
    }
}

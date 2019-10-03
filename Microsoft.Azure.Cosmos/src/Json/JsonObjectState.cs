//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using RMResources = Documents.RMResources;

    /// <summary>
    /// This class maintains the current state of a JSON object/value while it is being read or written.
    /// </summary>
    internal sealed class JsonObjectState
    {
        /// <summary>
        /// This constant defines the maximum nesting depth that the parser supports.
        /// The JSON spec states that this is an implementation dependent thing, so we're just picking a value for now.
        /// FWIW .Net chose 100
        /// Note: This value needs to be a multiple of 8 and must be less than 2^15 (see asserts in the constructor)
        /// </summary>
        private const int JsonMaxNestingDepth = 128;

        /// <summary>
        /// Flag for determining whether to throw exceptions that connote a context at the end or not started / complete.
        /// </summary>
        private readonly bool readMode;

        /// <summary>
        /// Stores a bitmap for whether we are in an array or object context at a particular level (0 => array, 1 => object).
        /// </summary>
        private readonly byte[] nestingStackBitmap;

        /// <summary>
        /// The current nesting stack index.
        /// </summary>
        private int nestingStackIndex;

        /// <summary>
        /// The current JsonTokenType.
        /// </summary>
        private JsonTokenType currentTokenType;

        /// <summary>
        /// The current JsonObjectContext.
        /// </summary>
        private JsonObjectContext currentContext;

        /// <summary>
        /// Initializes a new instance of the JsonObjectState class.
        /// </summary>
        /// <param name="readMode">Flag for determining whether to throw exceptions that correspond to a JsonReader or JsonWriter.</param>
        public JsonObjectState(bool readMode)
        {
            Debug.Assert(JsonMaxNestingDepth % 8 == 0, "JsonMaxNestingDepth must be multiple of 8");
            Debug.Assert(JsonMaxNestingDepth < (1 << 15), "JsonMaxNestingDepth must be less than 2^15");

            this.readMode = readMode;
            this.nestingStackBitmap = new byte[JsonMaxNestingDepth / 8];
            this.nestingStackIndex = -1;
            this.currentTokenType = JsonTokenType.NotStarted;
            this.currentContext = JsonObjectContext.None;
        }

        /// <summary>
        /// JsonObjectContext enum
        /// </summary>
        private enum JsonObjectContext
        {
            /// <summary>
            /// Context at the start of the object state.
            /// </summary>
            None,

            /// <summary>
            /// Context when state is in an array.
            /// </summary>
            Array,

            /// <summary>
            /// Context when state is in an object.
            /// </summary>
            Object,
        }

        /// <summary>
        /// Gets the current depth (level of nesting).
        /// </summary>
        public int CurrentDepth
        {
            get
            {
                return this.nestingStackIndex + 1;
            }
        }

        /// <summary>
        /// Gets the current JsonTokenType.
        /// </summary>
        public JsonTokenType CurrentTokenType
        {
            get
            {
                return this.currentTokenType;
            }
        }

        /// <summary>
        /// Gets a value indicating whether a property is expected.
        /// </summary>
        public bool IsPropertyExpected
        {
            get
            {
                return (this.currentTokenType != JsonTokenType.FieldName) && (this.currentContext == JsonObjectContext.Object);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current context is an array.
        /// </summary>
        public bool InArrayContext
        {
            get
            {
                return this.currentContext == JsonObjectContext.Array;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current context in an object.
        /// </summary>
        public bool InObjectContext
        {
            get
            {
                return this.currentContext == JsonObjectContext.Object;
            }
        }

        /// <summary>
        /// Gets the current JsonObjectContext
        /// </summary>
        private JsonObjectContext RetrieveCurrentContext
        {
            get
            {
                if (this.nestingStackIndex < 0)
                {
                    return JsonObjectContext.None;
                }

                return (this.nestingStackBitmap[this.nestingStackIndex / 8] & this.Mask) == 0 ? JsonObjectContext.Array : JsonObjectContext.Object;
            }
        }

        /// <summary>
        /// Gets a mask to use to get the current context from the nesting stack
        /// </summary>
        private byte Mask
        {
            get
            {
                return (byte)(1 << (this.nestingStackIndex % 8));
            }
        }

        /// <summary>
        /// Registers a JsonTokenType.
        /// </summary>
        /// <param name="jsonTokenType">The JsonTokenType to register.</param>
        public void RegisterToken(JsonTokenType jsonTokenType)
        {
            switch (jsonTokenType)
            {
                case JsonTokenType.String:
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                case JsonTokenType.Float32:
                case JsonTokenType.Float64:
                case JsonTokenType.Int8:
                case JsonTokenType.Int16:
                case JsonTokenType.Int32:
                case JsonTokenType.Int64:
                case JsonTokenType.UInt32:
                case JsonTokenType.Binary:
                case JsonTokenType.Guid:
                    this.RegisterValue(jsonTokenType);
                    break;
                case JsonTokenType.BeginArray:
                    this.RegisterBeginArray();
                    break;
                case JsonTokenType.EndArray:
                    this.RegisterEndArray();
                    break;
                case JsonTokenType.BeginObject:
                    this.RegisterBeginObject();
                    break;
                case JsonTokenType.EndObject:
                    this.RegisterEndObject();
                    break;
                case JsonTokenType.FieldName:
                    this.RegisterFieldName();
                    break;
                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, RMResources.UnexpectedJsonTokenType, jsonTokenType));
            }
        }

        /// <summary>
        /// Pushes a JsonObjectContext onto the nesting stack.
        /// </summary>
        /// <param name="isArray">Whether the JsonObjectContext is an array.</param>
        private void Push(bool isArray)
        {
            if (this.nestingStackIndex + 1 >= JsonMaxNestingDepth)
            {
                throw new InvalidOperationException(RMResources.JsonMaxNestingExceeded);
            }

            this.nestingStackIndex++;

            if (isArray)
            {
                this.nestingStackBitmap[this.nestingStackIndex / 8] &= (byte)~this.Mask;
                this.currentContext = JsonObjectContext.Array;
            }
            else
            {
                this.nestingStackBitmap[this.nestingStackIndex / 8] |= this.Mask;
                this.currentContext = JsonObjectContext.Object;
            }
        }

        /// <summary>
        /// Registers any json token type.
        /// </summary>
        /// <param name="jsonTokenType">The jsonTokenType to register</param>
        private void RegisterValue(JsonTokenType jsonTokenType)
        {
            if ((this.currentContext == JsonObjectContext.Object) && (this.currentTokenType != JsonTokenType.FieldName))
            {
                throw new JsonMissingPropertyException();
            }

            if ((this.currentContext == JsonObjectContext.None) && (this.currentTokenType != JsonTokenType.NotStarted))
            {
                throw new JsonPropertyArrayOrObjectNotStartedException();
            }

            this.currentTokenType = jsonTokenType;
        }

        /// <summary>
        /// Registers a beginning of a json array ('[')
        /// </summary>
        private void RegisterBeginArray()
        {
            // An array start is also a value
            this.RegisterValue(JsonTokenType.BeginArray);
            this.Push(true);
        }

        /// <summary>
        /// Registers the end of a json array (']')
        /// </summary>
        private void RegisterEndArray()
        {
            if (this.currentContext != JsonObjectContext.Array)
            {
                if (this.readMode)
                {
                    throw new JsonUnexpectedEndArrayException();
                }
                else
                {
                    throw new JsonArrayNotStartedException();
                }
            }

            this.nestingStackIndex--;
            this.currentTokenType = JsonTokenType.EndArray;
            this.currentContext = this.RetrieveCurrentContext;
        }

        /// <summary>
        /// Registers a beginning of a json object ('{')
        /// </summary>
        private void RegisterBeginObject()
        {
            // An object start is also a value
            this.RegisterValue(JsonTokenType.BeginObject);
            this.Push(false);
        }

        /// <summary>
        /// Registers a end of a json object ('}')
        /// </summary>
        private void RegisterEndObject()
        {
            if (this.currentContext != JsonObjectContext.Object)
            {
                if (this.readMode)
                {
                    throw new JsonUnexpectedEndObjectException();
                }
                else
                {
                    throw new JsonObjectNotStartedException();
                }
            }

            // check if we have a property name but not a value
            if (this.currentTokenType == JsonTokenType.FieldName)
            {
                if (this.readMode)
                {
                    throw new JsonUnexpectedEndObjectException();
                }
                else
                {
                    throw new JsonNotCompleteException();
                }
            }

            this.nestingStackIndex--;
            this.currentTokenType = JsonTokenType.EndObject;
            this.currentContext = this.RetrieveCurrentContext;
        }

        /// <summary>
        /// Register a Json FieldName
        /// </summary>
        private void RegisterFieldName()
        {
            if (this.currentContext != JsonObjectContext.Object)
            {
                throw new JsonObjectNotStartedException();
            }

            if (this.currentTokenType == JsonTokenType.FieldName)
            {
                throw new JsonPropertyAlreadyAddedException();
            }

            this.currentTokenType = JsonTokenType.FieldName;
        }
    }
}
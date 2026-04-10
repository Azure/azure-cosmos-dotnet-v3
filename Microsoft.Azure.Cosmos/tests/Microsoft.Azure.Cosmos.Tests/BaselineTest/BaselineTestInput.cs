//-----------------------------------------------------------------------
// <copyright file="BaselineTestInput.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Test.BaselineTest
{
    using System;
    using System.Xml;

    /// <summary>
    /// Abstract base class for inputs.
    /// </summary>
    public abstract class BaselineTestInput
    {
        /// <summary>
        /// Initializes a new instance of the BaselineTestInput class.
        /// </summary>
        /// <param name="description">The description of the input.</param>
        protected BaselineTestInput(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException($"{nameof(description)} must not be null, empty, or whitespace.");
            }

            this.Description = description;
        }

        /// <summary>
        /// Gets The description of the input.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Serializes the input as xml using the provided writer.
        /// The derived class will have to override this, since every input is serialized a little differently.
        /// </summary>
        /// <param name="xmlWriter">The xml writer to use.</param>
        public abstract void SerializeAsXml(XmlWriter xmlWriter);
    }
}
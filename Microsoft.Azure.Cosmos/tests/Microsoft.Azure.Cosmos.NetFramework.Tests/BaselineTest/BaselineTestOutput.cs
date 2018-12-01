//-----------------------------------------------------------------------
// <copyright file="BaselineTestOutput.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Documents.Services.Management.Tests.BaselineTest
{
    using System.Xml;

    /// <summary>
    /// Abstract base class for BaselineTestOutput.
    /// </summary>
    public abstract class BaselineTestOutput
    {
        /// <summary>
        /// Serializes the output as xml using the provided writer.
        /// The derived class will have to override this, since every output is serialized a little differently.
        /// </summary>
        /// <param name="xmlWriter">The writer to use.</param>
        public abstract void SerializeAsXML(XmlWriter xmlWriter);
    }
}

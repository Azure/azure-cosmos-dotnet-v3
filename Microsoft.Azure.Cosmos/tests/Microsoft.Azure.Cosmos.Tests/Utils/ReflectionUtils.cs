//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Common utility class for reflaction related operations.
    /// </summary>
    internal static class ReflectionUtils
    {
        /// <summary>
        /// This helper method uses reflection to set the private and read only fields
        /// to the disered values to help the test cases mimic the expected behavior.
        /// </summary>
        /// <param name="objectName">An object where reflection will be applied to update the field.</param>
        /// <param name="fieldName">A string containing the internal field name.</param>
        /// <param name="delayInMinutes">An integer to add or substract the desired delay in minutes.</param>
        internal static void AddMinuteToDateTimeFieldUsingReflection(
            object objectName,
            string fieldName,
            int delayInMinutes)
        {
            FieldInfo fieldInfo = objectName
                .GetType()
                .GetField(
                    name: fieldName,
                    bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic);

            DateTime? fieldValue = (DateTime?)fieldInfo
                .GetValue(
                    obj: objectName);

            fieldInfo
                .SetValue(
                    obj: objectName,
                    value: ((DateTime)fieldValue).AddMinutes(delayInMinutes));
        }
    }
}
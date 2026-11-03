// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using System.Globalization;
    using System.Reflection;

    /// <summary>
    /// This class contains utility methods for the sole purpose of unit tests.
    /// </summary>
    public static class TestUtils
    {
        /// <summary>
        /// Helper method to parse a string into a date time field.
        /// </summary>
        /// <param name="dateTimeStr">A string containing the desired date time.</param>
        /// <returns>A <see cref="DateTime"/> field containing the parsed date time.</returns>
        public static DateTime ParseDateTime(string dateTimeStr) => DateTime.ParseExact(
            dateTimeStr,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture);

        /// <summary>
        /// Helper method to snapshot the connection timestamps.
        /// </summary>
        /// <param name="currentTime">A <see cref="DateTime"/> field containing the current time.</param>
        /// <param name="lastSendAttempt">A <see cref="DateTime"/> field containing the last send attempt time.</param>
        /// <param name="lastSend">A <see cref="DateTime"/> field containing the last send time.</param>
        /// <param name="lastReceive">A <see cref="DateTime"/> field containing the last receive time.</param>
        /// <param name="firstSendSinceLastReceive">A <see cref="DateTime"/> field containing the first send since last receive time.</param>
        /// <param name="sendDelayLimit">A <see cref="TimeSpan"/> field containing the send delay limit.</param>
        /// <param name="receiveDelayLimit">A <see cref="TimeSpan"/> field containing the receive delay limit.</param>
        /// <param name="idleConnectionTimeout">A <see cref="TimeSpan"/> field containing the connection idle timeout.</param>
        public static void SnapshotConnectionTimestamps(
            out DateTime currentTime,
            out DateTime lastSendAttempt,
            out DateTime lastSend,
            out DateTime lastReceive,
            out DateTime firstSendSinceLastReceive,
            out TimeSpan sendDelayLimit,
            out TimeSpan receiveDelayLimit,
            out TimeSpan idleConnectionTimeout)
        {
            currentTime = TestUtils.ParseDateTime("2023-05-12 14:15:05");
            lastSendAttempt = TestUtils.ParseDateTime("2023-05-12 14:14:00");
            lastSend = TestUtils.ParseDateTime("2023-05-12 14:15:00");
            lastReceive = TestUtils.ParseDateTime("2023-05-12 14:14:57");
            firstSendSinceLastReceive = TestUtils.ParseDateTime("2023-05-12 14:00:00");
            sendDelayLimit = TimeSpan.FromSeconds(5);
            receiveDelayLimit = TimeSpan.FromSeconds(15);
            idleConnectionTimeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// This helper method uses reflection to add the desired seconds to a private and read only date time
        /// field to help the test cases mimic the expected behavior.
        /// </summary>
        /// <param name="objectName">An object where reflection will be applied to update the field.</param>
        /// <param name="fieldName">A string containing the internal field name.</param>
        /// <param name="delayInSeconds">An integer to add or substract the desired delay in seconds.</param>
        internal static void AddSecondsToDateTimeFieldUsingReflection(
            object objectName,
            string fieldName,
            int delayInSeconds)
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
                    value: ((DateTime)fieldValue).AddSeconds(delayInSeconds));
        }

        /// <summary>
        /// This helper method uses reflection to set the private and read only fields
        /// to the desired values to help the test cases mimic the expected behavior.
        /// </summary>
        /// <param name="objectName">An object where reflection will be applied to update the field.</param>
        /// <param name="fieldName">A string containing the internal field name.</param>
        /// <param name="fieldValue">A generic value to be set to the provided field.</param>
        internal static void UpdateFieldUsingReflection<T1>(
            object objectName,
            string fieldName,
            T1 fieldValue)
            where T1 : struct
        {
            FieldInfo fieldInfo = objectName
                .GetType()
                .GetField(
                    name: fieldName,
                    bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic);

            T1 objectValue = (T1)(object)fieldValue;

            fieldInfo
                .SetValue(
                    obj: objectName,
                    value: objectValue);
        }
    }
}
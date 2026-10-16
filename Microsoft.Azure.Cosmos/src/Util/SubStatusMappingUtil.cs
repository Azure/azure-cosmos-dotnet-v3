//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Util
{
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Utility for correctly mapping duplicate SubStatus codes.
    /// </summary>
    internal class SubStatusMappingUtil
    {
        public static string GetSubStatusCodeString(StatusCodes statusCode, SubStatusCodes subStatusCode)
        {
            if ((int)subStatusCode == 1002)
            {
                return statusCode == StatusCodes.NotFound
                     ? nameof(SubStatusCodes.ReadSessionNotAvailable)
                     : nameof(SubStatusCodes.PartitionKeyRangeGone);
            }

            if ((int)subStatusCode == 2001)
            {
                return statusCode == StatusCodes.NoContent
                    ? nameof(SubStatusCodes.MissedTargetLsn)
                    : nameof(SubStatusCodes.SplitIsDisabled);
            }

            if ((int)subStatusCode == 2002)
            {
                return statusCode == StatusCodes.NoContent
                    ? nameof(SubStatusCodes.MissedTargetLsnOver100)
                    : nameof(SubStatusCodes.CollectionsInPartitionGotUpdated);
            }

            if ((int)subStatusCode == 2003)
            {
                return statusCode == StatusCodes.NoContent
                    ? nameof(SubStatusCodes.MissedTargetLsnOver1000)
                    : nameof(SubStatusCodes.CanNotAcquirePKRangesLock);
            }

            if ((int)subStatusCode == 2004)
            {
                return statusCode == StatusCodes.NoContent
                    ? nameof(SubStatusCodes.MissedTargetLsnOver10000)
                    : nameof(SubStatusCodes.ResourceNotFound);
            }

            if ((int)subStatusCode == 2011)
            {
                return statusCode == StatusCodes.NoContent
                    ? nameof(SubStatusCodes.MissedTargetGlobalCommittedLsn)
                    : nameof(SubStatusCodes.StorageSplitConflictingWithNWayThroughputSplit);
            }

            if ((int)subStatusCode == 2012)
            {
                return statusCode == StatusCodes.NoContent
                    ? nameof(SubStatusCodes.MissedTargetGlobalCommittedLsnOver100)
                    : nameof(SubStatusCodes.MergeIsDisabled);
            }

            if ((int)subStatusCode == 1004)
            {
                return statusCode == StatusCodes.BadRequest
                    ? nameof(SubStatusCodes.CrossPartitionQueryNotServable)
                    : nameof(SubStatusCodes.ConfigurationNameNotFound);
            }

            if ((int)subStatusCode == 1007)
            {
                return statusCode == StatusCodes.Gone
                    ? nameof(SubStatusCodes.CompletingSplit)
                    : nameof(SubStatusCodes.InsufficientBindablePartitions);
            }

            if ((int)subStatusCode == 1008)
            {
                return statusCode == StatusCodes.Gone
                    ? nameof(SubStatusCodes.CompletingPartitionMigration)
                    : nameof(SubStatusCodes.DatabaseAccountNotFound);
            }

            if ((int)subStatusCode == 1005)
            {
                return statusCode == StatusCodes.NotFound
                    ? nameof(SubStatusCodes.ConfigurationPropertyNotFound)
                    : nameof(SubStatusCodes.ProvisionLimitReached);
            }

            if ((int)subStatusCode == 3207)
            {
                return statusCode == StatusCodes.Conflict
                    ? nameof(SubStatusCodes.ConfigurationNameAlreadyExists)
                    : nameof(SubStatusCodes.PrepareTimeLimitExceeded);
            }

            if ((int)subStatusCode == 6001)
            {
                return statusCode == StatusCodes.ServiceUnavailable
                    ? nameof(SubStatusCodes.AggregatedHealthStateError)
                    : nameof(SubStatusCodes.PartitionMigrationWaitForFullSyncReceivedInternalServerErrorDuringCompleteMigrationFromBackend);
            }

            if ((int)subStatusCode == 6002)
            {
                return statusCode == StatusCodes.ServiceUnavailable
                    ? nameof(SubStatusCodes.ApplicationHealthStateError)
                    : nameof(SubStatusCodes.PartitionMigrationWaitForFullSyncReceivedInternalServerErrorDuringAbortMigrationFromBackend);
            }

            if ((int)subStatusCode == 6003)
            {
                return statusCode == StatusCodes.ServiceUnavailable
                    ? nameof(SubStatusCodes.HealthStateError)
                    : nameof(SubStatusCodes.PartitionMigrationFinalizeMigrationsDidNotCompleteInTenRetries);
            }

            return SubStatusMappingUtil.GetCorrectedSpelling(subStatusCode) ?? subStatusCode.ToString();
        }

        /// <summary>
        /// Returns the correctly spelled name of a <see cref="SubStatusCodes"/> member whose name is misspelled in
        /// Microsoft.Azure.Cosmos.Direct, or <c>null</c> when the name is spelled correctly.
        /// </summary>
        /// <remarks>
        /// The names surface in diagnostics and OpenTelemetry attributes, so they are corrected here until the enum
        /// itself is fixed (https://github.com/Azure/azure-cosmos-dotnet-v3/issues/6134). The cases reference the
        /// misspelled members on purpose: once Direct renames them, this method stops compiling and can be removed.
        /// Only values that no other member shares are listed, because Enum.ToString is not deterministic for
        /// shared values; 2020 (PartitionMigrationServiceTypeAndOperationTypeDoesnotMatch) shares its value with
        /// CollectionStateChanged and is therefore left out.
        /// </remarks>
        private static string GetCorrectedSpelling(SubStatusCodes subStatusCode)
        {
            switch (subStatusCode)
            {
                case SubStatusCodes.CanNotAquireMasterPartitionAccessLock:
                    return "CanNotAcquireMasterPartitionAccessLock";
                case SubStatusCodes.PartitionMigrationIsDisableOnTheGlobalDatabaseAccount:
                    return "PartitionMigrationIsDisabledOnTheGlobalDatabaseAccount";
                case SubStatusCodes.PartitionMigrationIsDisableOnTheRunnerAccount:
                    return "PartitionMigrationIsDisabledOnTheRunnerAccount";
                case SubStatusCodes.AccountAlreadyinTargetGateway:
                    return "AccountAlreadyInTargetGateway";
                case SubStatusCodes.FederationDoesnotExistOrIsLocked:
                    return "FederationDoesNotExistOrIsLocked";
                case SubStatusCodes.ThottleDueToSplit:
                    return "ThrottleDueToSplit";
                case SubStatusCodes.PartitionkeyHashCollisionForId:
                    return "PartitionKeyHashCollisionForId";
                case SubStatusCodes.FabricTokenValidatonFailed:
                    return "FabricTokenValidationFailed";
                case SubStatusCodes.InvalidFabricAritfactId:
                    return "InvalidFabricArtifactId";
                default:
                    return null;
            }
        }
    }
}

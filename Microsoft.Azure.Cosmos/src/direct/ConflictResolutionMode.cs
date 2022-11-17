//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary> 
    /// Specifies the supported conflict resolution modes, as specified in <see cref="ConflictResolutionPolicy"/>
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum ConflictResolutionMode
    {
        /// <summary>
        /// Last writer wins conflict resolution mode
        /// </summary>
        /// <remarks>
        /// Setting the ConflictResolutionMode to "LastWriterWins" indicates that conflict resolution should be done by inspecting a field in the conflicting documents
        /// and picking the document which has the higher value in that path. See <see cref="ConflictResolutionPolicy.ConflictResolutionPath"/> for details on how to specify the path
        /// to be checked for conflict resolution.
        /// </remarks>
        LastWriterWins,

        /// <summary>
        /// Custom conflict resolution mode
        /// </summary>
        /// <remarks>
        /// Setting the ConflictResolutionMode to "Custom" indicates that conflict resolution is custom handled by a user. 
        /// The user could elect to register a user specified <see cref="StoredProcedure"/> for handling conflicting resources.
        /// Should the user not register a user specified StoredProcedure, conflicts will default to being made available as <see cref="Conflict"/> resources, 
        /// which the user can inspect and manually resolve.
        /// See <see cref="ConflictResolutionPolicy.ConflictResolutionProcedure"/> for details on how to specify the stored procedure
        /// to run for conflict resolution.
        /// </remarks>
        Custom
    }
}
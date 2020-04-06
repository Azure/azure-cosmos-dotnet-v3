# Pull Request Template

## Pull Request Title
1. Changelog will be generated from PR titles.
2. PR titles will be formatted with the following layout.
   1. Internal is optional and represents changes with no public facing changes such as test only changes
   2. Category represents the area of the change like batch, changefeed, point operation, or query
   3. Add or Fix identifies if a new feature is being added or if a bug is being fixed
   4. Description is a user friendly explanation of the change

### Format
`[Internal] Category: (Add|Fix) Description`

### Example
`Diagnostics: Add GetElapsedClientLatency to CosmosDiagnostics`<br/>
`PartitionKey: Fix null reference when using default(PartitionKey)`<br/>
`[Internal] Query: Add code generator for CosmosNumbers for easy additions in the future.`<br/>

## Description

Please include a summary of the change and which issue is fixed. Please also include relevant motivation and context. List any dependencies that are required for this change.

## Type of change

Please delete options that are not relevant.

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] This change requires a documentation update

## Closing issues

Put closes #XXXX in your comment to auto-close the issue that your PR fixes (if such).

## Assignee

Please add yourself as the assignee

## Projects

Please add relevant projects so this issue can be properly tracked.


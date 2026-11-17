# Token-only change feed end-to-end tests

`ChangeFeedTokenOnlyE2ETests.TokenOnlyNowWithConcurrentIngestion` exercises
`LatestVersion` and `AllVersionsAndDeletes`, through Direct and Gateway connections.
Ingestion commits to every physical range while a `Now` token-only read is in
progress. The test verifies an empty one-call result, complete continuation
coverage, a second explicit cycle through nonempty service 200 responses while
ingestion continues, and normal delivery of writes made after capture.

The live service can return documents despite a zero page size. Token-only mode
suppresses those payloads in the SDK response, retains status and continuation,
and keeps cycling through all ranges instead of failing or stopping at a 200.
Explicit reads remain supported after `HasMoreResults` becomes false. Suppression
is client-side: payload transfer and request charges can still occur, and the token
advances past suppressed changes.

The fixture belongs to the existing `ChangeFeed` CI category in
[`templates/build-test.yml`](../../../../templates/build-test.yml). CI runs it
against the Windows emulator configured by
[`templates/emulator-setup.yml`](../../../../templates/emulator-setup.yml);
`EMULATORMSIURL` supplies the CI installer. No additional CI secrets are required.

To run against an authorized live account, set the **process-scoped**
`COSMOSDB_CHANGE_FEED_E2E` environment variable to its connection string using your
secret store, then run from the repository root:

```powershell
dotnet test .\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.EmulatorTests\Microsoft.Azure.Cosmos.EmulatorTests.csproj -c Release -f net6.0 --filter "FullyQualifiedName~ChangeFeedTokenOnlyE2ETests"
```

Never commit or log the connection string. Remove the environment variable after
the run. An unset variable uses the normal emulator configuration; an invalid live
configuration fails rather than falling back to the emulator.

The live account must support all-versions-and-deletes change feed, including
continuous backup. Live change-feed retention is inherited from continuous backup;
only emulator runs set a container-level retention duration.
Each test creates a uniquely named `cf-token-only-e2e-*`
database, provisions a 20,000 RU/s multi-partition container, and deletes only that
database in `finally`. It does not change account settings or reuse existing data.
The run needs permission to create/read/write/delete its own database and items.

Do not redirect fixtures inheriting `BaseCosmosClientHelper` to a shared live
account: their initialization deletes all account databases. This standalone
fixture deliberately does not inherit that helper.

# Versioning

The Azure Cosmos DB SDK ships two [Nuget package](https://www.nuget.org/packages/Microsoft.Azure.Cosmos) versions:

* GA SDK: Versioned with `3.X.Y`, it contains APIs and features that are considered General Availability
* Preview SDK: Versioned with `3.(X + 1).0-preview.Y`, it contains APIs and features that are in Preview. These APIs or features might be service features that are in Preview (such as a new Cosmos DB Service feature or operation) or SDK client specific features that are unrelated to a service feature (such as improvements in connection handling).

> :information_source: General Availability of an API or feature present in a Preview SDK is not guaranteed on the next GA SDK release following the Preview SDK release when it was introduced. Each Preview API can have an independent GA cycle that can be dependent on the Azure Cosmos DB Service.

## Minor releases

Minor releases are defined by: 

* Changes in the public API surface of the SDK, such as new features, or GAing APIs that were in preview
* Changes in the version of one of the dependencies

In these cases, the **GA SDK** should **increase the minor version** and **Preview SDK** should **increase the minor version to be one more than GA SDK**.

For example, if `3.10.0` is being released for **GA SDK**, then `3.11.0-preview.0` should be released for **Preview SDK**.

## Patch releases

Patch releases are defined by:

* No Public API changes
* Includes a subset bug fixes reported after the last major release

In these cases, the **GA SDK** should **increase the patch version** and **Preview SDK** should **increase the preview suffix version**.

For example, if `3.10.0` is being patched, for **GA SDK** we would release `3.10.1` and for **Preview SDK** we would release `3.11.0-preview1`.

If `3.10.1` is being patched, for **GA SDK** we would release `3.10.2` and for **Preview SDK** we would release `3.11.0-preview.2`.

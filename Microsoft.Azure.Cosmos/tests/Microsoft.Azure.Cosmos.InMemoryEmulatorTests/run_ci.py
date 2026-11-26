#!/usr/bin/env python3
# Copyright (c) Microsoft Corporation. All rights reserved.

import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import time
import xml.etree.ElementTree as ET


HERE = Path(__file__).resolve().parent
PROJECTS = {
    "host": HERE,
    "shared": HERE.parent / "Microsoft.Azure.Cosmos.EmulatorTests",
}
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
HOST_CONTRACT = Counter({
    "Microsoft.Azure.Cosmos.InMemoryEmulatorTests.EmulatorLifecycleTests.ReadsDynamicallyAssignedEndpoints": 1,
    "Microsoft.Azure.Cosmos.InMemoryEmulatorTests.EmulatorLifecycleTests.RejectsUnexpectedEndpoints": 4,
    "Microsoft.Azure.Cosmos.InMemoryEmulatorTests.EmulatorLifecycleTests.ReportsExitBeforeReadiness": 1,
    "Microsoft.Azure.Cosmos.InMemoryEmulatorTests.EmulatorLifecycleTests.MissingExecutableIsAnError": 1,
    "Microsoft.Azure.Cosmos.InMemoryEmulatorTests.ItemTests.ItemCrudRoundTripsThroughGateway": 1,
    "Microsoft.Azure.Cosmos.InMemoryEmulatorTests.ItemTests.PartitionKeysIsolateIdenticalIds": 1,
    "Microsoft.Azure.Cosmos.InMemoryEmulatorTests.ItemTests.DuplicateAndStaleEtagReturnServiceErrors": 1,
    "Microsoft.Azure.Cosmos.InMemoryEmulatorTests.ItemTests.PartitionScopedReadFeedPaginates": 1,
    "Microsoft.Azure.Cosmos.InMemoryEmulatorTests.ItemTests.PatchPersistsUpdatedValue": 1,
    "Microsoft.Azure.Cosmos.InMemoryEmulatorTests.ItemTests.InvalidConfigurationReportsStartupFailure": 1,
    "Microsoft.Azure.Cosmos.InMemoryEmulatorTests.ItemTests.HostsHaveIsolatedStateAndStopOnDisposal": 1,
})


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate manifest key: {key}")
        result[key] = value
    return result


def load_scenarios(path):
    manifest = json.loads(path.read_text(), object_pairs_hook=unique_object)
    if set(manifest) != {"Items", "Reads"}:
        raise ValueError("The manifest must cover both CI jobs: Items and Reads")
    groups = {}
    assigned = set()
    for group, suites in manifest.items():
        if not suites or "shared" not in suites or set(suites) - PROJECTS.keys():
            raise ValueError(f"Invalid or empty suites in {group}")
        groups[group] = {}
        for suite, classes in suites.items():
            expected = Counter()
            for class_name, methods in classes.items():
                for method, count in methods.items():
                    if type(count) is not int or count < 1:
                        raise ValueError(f"Invalid case count: {class_name}.{method}")
                    name = f"{class_name}.{method}"
                    if (suite, name) in assigned:
                        raise ValueError(f"Overlapping scenario: {suite}/{name}")
                    assigned.add((suite, name))
                    expected[name] = count
            if not expected:
                raise ValueError(f"Empty CI suite: {group}/{suite}")
            groups[group][suite] = expected
    items_contract = Counter({name: count for name, count in groups["Items"].get("host", {}).items()
                              if name in HOST_CONTRACT})
    if items_contract != HOST_CONTRACT or set(groups["Reads"].get("host", {})) & HOST_CONTRACT.keys():
        raise ValueError("Run the original complete host contract exactly once, in Items")
    return groups


def verify_discovery(discovered, expected):
    actual = Counter(discovered)
    required = Counter({name: 1 for name in expected})
    if actual != required:
        raise ValueError(f"Discovery mismatch: missing={required - actual}, extra={actual - required}")


def verify_results(path, expected):
    root = ET.parse(path).getroot()
    methods = {}
    for definition in root.findall("t:TestDefinitions/t:UnitTest", NS):
        method = definition.find("t:TestMethod", NS)
        methods[definition.attrib["id"]] = (
            method.attrib["className"].split(",")[0] + "." + method.attrib["name"]
        )
    actual = Counter()
    identities = set()
    observed = []
    for result in root.findall("t:Results/t:UnitTestResult", NS):
        name = methods[result.attrib["testId"]]
        identity = (name, result.attrib["testName"])
        if result.attrib["outcome"] != "Passed" or result.find("t:InnerResults", NS) is not None:
            raise ValueError(f"Required scenario did not pass as a leaf result: {identity}")
        if identity in identities:
            raise ValueError(f"Duplicate executed case: {identity}")
        identities.add(identity)
        actual[name] += 1
        output = result.find("t:Output/t:StdOut", NS)
        observed.append({
            "method": name,
            "case": result.attrib["testName"],
            "duration": result.attrib["duration"],
            "outcome": result.attrib["outcome"],
            "evidence": output.text if output is not None else None,
        })
    if actual != expected:
        raise ValueError(f"Execution mismatch: missing={expected - actual}, extra={actual - expected}")
    summary = root.find("t:ResultSummary", NS)
    counters = summary.find("t:Counters", NS) if summary is not None else None
    if (summary is None or summary.attrib["outcome"] not in {"Completed", "Passed"}
            or counters is None
            or any(int(counters.attrib[key]) != sum(expected.values())
                   for key in ("total", "executed", "passed"))):
        raise ValueError("The test run did not complete with every required case passing")
    return observed


def run_command(command, environment):
    print("+ " + " ".join(map(str, command)), flush=True)
    process = subprocess.Popen(command, env=environment, start_new_session=True)
    try:
        returncode = process.wait()
    finally:
        # A crashing/canceled testhost may not execute AssemblyCleanup.
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    if returncode:
        raise subprocess.CalledProcessError(returncode, command)


def main():
    parser = argparse.ArgumentParser(description="Run disjoint, verified in-memory emulator CI scenarios")
    parser.add_argument("--group", choices=("Items", "Reads"), required=True)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--results-directory", type=Path, required=True)
    parser.add_argument("--dotnet", default="dotnet")
    args = parser.parse_args()
    if os.name != "posix":
        raise ValueError("This runner owns POSIX process groups; use Linux or macOS")
    executable = os.environ.get("COSMOS_IN_MEMORY_EMULATOR_PATH")
    if not executable or not Path(executable).is_file() or not os.access(executable, os.X_OK):
        raise ValueError("COSMOS_IN_MEMORY_EMULATOR_PATH must name the executable pinned Rust host")

    def cancel(signum, frame):
        raise SystemExit(128 + signum)

    signal.signal(signal.SIGTERM, cancel)
    groups = load_scenarios(HERE / "ci-scenarios.json")
    args.results_directory.mkdir(parents=True, exist_ok=True)
    environment = dict(os.environ, COSMOS_EMULATOR_BACKEND="InMemory")
    report = {
        "group": args.group,
        "configuration": args.configuration,
        "framework": "net6.0",
        "emulatorExecutableSha256": hashlib.sha256(Path(executable).read_bytes()).hexdigest(),
        "emulatorSourceRevision": os.environ.get("COSMOS_IN_MEMORY_EMULATOR_SOURCE_REVISION"),
        "sdkRevision": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=HERE, text=True).strip(),
        "assigned": groups[args.group],
        "suites": {},
    }
    (args.results_directory / "assigned-scenarios.json").write_text(json.dumps(report, indent=2))
    for suite, expected in groups[args.group].items():
        project = PROJECTS[suite]
        project_file = project / (project.name + ".csproj")
        assembly = project / "bin" / args.configuration / "net6.0" / (project.name + ".dll")
        run_command([args.dotnet, "build", str(project_file), "-c", args.configuration,
                     "--verbosity", "quiet"], environment)
        approved = {name for group in groups.values() for name in group.get(suite, {})}
        discovery = args.results_directory / f"{suite}-discovered.txt"
        discover_command = [args.dotnet, "vstest", str(assembly), "/ListFullyQualifiedTests",
                            f"/ListTestsTargetPath:{discovery}"]
        if suite == "shared":
            discover_command.append("/TestCaseFilter:" + "|".join(
                f"FullyQualifiedName={name}" for name in sorted(approved)))
        run_command(discover_command, environment)
        verify_discovery(discovery.read_text(encoding="utf-8-sig").splitlines(), approved)
        test_command = [
            args.dotnet, "test", str(project_file), "-c", args.configuration, "--no-build",
            "--logger", "console;verbosity=normal", "--logger", f"trx;LogFileName={suite}.trx",
            "--results-directory", str(args.results_directory),
        ]
        if suite == "shared" or set(expected) != approved:
            test_command += ["--filter", "|".join(f"FullyQualifiedName={name}" for name in expected)]
        started = time.monotonic()
        run_command(test_command, environment)
        observed = verify_results(args.results_directory / f"{suite}.trx", expected)
        report["suites"][suite] = {
            "discoveredApprovedMethods": len(approved),
            "assignedMethods": len(expected),
            "executedCases": len(observed),
            "testCommandSeconds": time.monotonic() - started,
            "results": observed,
        }
        print(f"Verified {suite}/{args.group}: {len(expected)} methods, {len(observed)} passing cases, no skips")
    (args.results_directory / "verified-scenarios.json").write_text(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()

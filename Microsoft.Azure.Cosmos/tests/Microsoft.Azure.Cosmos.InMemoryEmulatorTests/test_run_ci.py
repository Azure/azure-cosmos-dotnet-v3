# Copyright (c) Microsoft Corporation. All rights reserved.

from collections import Counter
import copy
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import time
import unittest
import xml.etree.ElementTree as ET

import run_ci


class CoverageGateTests(unittest.TestCase):
    def test_checked_in_groups_are_nonempty_and_disjoint(self):
        groups = run_ci.load_scenarios(run_ci.HERE / "ci-scenarios.json")
        self.assertEqual(35, sum(groups["Items"]["shared"].values()))
        self.assertEqual(7, sum(groups["Reads"]["shared"].values()))
        self.assertEqual(17, sum(groups["Items"]["host"].values()))
        self.assertEqual(4, sum(groups["Reads"]["host"].values()))
        contract = {name: count for name, count in groups["Items"]["host"].items() if name in run_ci.HOST_CONTRACT}
        self.assertEqual(run_ci.HOST_CONTRACT, contract)

    def test_rejects_duplicate_manifest_keys(self):
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            json.loads('{"Items": {}, "Items": {}}', object_pairs_hook=run_ci.unique_object)

    def test_rejects_overlapping_or_missing_groups(self):
        manifest = json.loads((run_ci.HERE / "ci-scenarios.json").read_text())
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "manifest.json"
            overlap = copy.deepcopy(manifest)
            overlap["Reads"]["shared"].update(overlap["Items"]["shared"])
            path.write_text(json.dumps(overlap))
            with self.assertRaisesRegex(ValueError, "Overlapping"):
                run_ci.load_scenarios(path)
            del manifest["Reads"]
            path.write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError, "both CI jobs"):
                run_ci.load_scenarios(path)

    def test_discovery_rejects_missing_extra_and_duplicate_methods(self):
        run_ci.verify_discovery(["A", "B"], {"A", "B"})
        for discovered in (["A"], ["A", "B", "C"], ["A", "A", "B"]):
            with self.subTest(discovered=discovered), self.assertRaisesRegex(ValueError, "Discovery mismatch"):
                run_ci.verify_discovery(discovered, {"A", "B"})

    def test_original_host_contract_cannot_be_removed_or_moved(self):
        manifest = json.loads((run_ci.HERE / "ci-scenarios.json").read_text())
        class_name = "Microsoft.Azure.Cosmos.InMemoryEmulatorTests.ItemTests"
        method = "PatchPersistsUpdatedValue"
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "manifest.json"
            for move in (False, True):
                changed = copy.deepcopy(manifest)
                count = changed["Items"]["host"][class_name].pop(method)
                if move:
                    changed["Reads"]["host"][class_name] = {method: count}
                path.write_text(json.dumps(changed))
                with self.subTest(move=move), self.assertRaisesRegex(ValueError, "original complete host contract"):
                    run_ci.load_scenarios(path)

    def test_results_require_exact_passing_leaf_cases(self):
        namespace = run_ci.NS["t"]
        root = ET.Element("TestRun", xmlns=namespace)
        definitions = ET.SubElement(root, "TestDefinitions")
        definition = ET.SubElement(definitions, "UnitTest", id="id")
        ET.SubElement(definition, "TestMethod", className="Class", name="Method")
        results = ET.SubElement(root, "Results")
        result = ET.SubElement(results, "UnitTestResult", testId="id", testName="case",
                               duration="00:00:00.001", outcome="Passed")
        summary = ET.SubElement(root, "ResultSummary", outcome="Completed")
        ET.SubElement(summary, "Counters", total="1", executed="1", passed="1")
        expected = Counter({"Class.Method": 1})
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.trx"

            def verify():
                ET.ElementTree(root).write(path)
                return run_ci.verify_results(path, expected)

            self.assertEqual(1, len(verify()))
            for outcome in ("NotExecuted", "Inconclusive", "Failed"):
                result.set("outcome", outcome)
                with self.subTest(outcome=outcome), self.assertRaisesRegex(ValueError, "did not pass"):
                    verify()
            result.set("outcome", "Passed")
            results.append(copy.deepcopy(result))
            with self.assertRaisesRegex(ValueError, "Duplicate executed"):
                verify()
            results.remove(results[-1])
            results.remove(result)
            with self.assertRaisesRegex(ValueError, "Execution mismatch"):
                verify()
            results.append(result)
            summary.set("outcome", "Failed")
            with self.assertRaisesRegex(ValueError, "did not complete"):
                verify()

    def test_failed_command_cleans_up_its_owned_children(self):
        with tempfile.TemporaryDirectory() as directory:
            ready = Path(directory) / "ready"
            child_code = (
                "import os, socket, time; from pathlib import Path; "
                "server=socket.socket(); server.bind(('127.0.0.1', 0)); server.listen(); "
                f"Path({str(ready)!r}).write_text(str(server.getsockname()[1])); "
                "time.sleep(60)"
            )
            parent_code = (
                "import subprocess, sys, time; from pathlib import Path; "
                f"subprocess.Popen([sys.executable, '-c', {child_code!r}]); "
                f"ready=Path({str(ready)!r}); "
                "\nfor _ in range(100):"
                "\n if ready.exists(): sys.exit(7)"
                "\n time.sleep(0.05)"
                "\nsys.exit(8)"
            )
            with self.assertRaises(subprocess.CalledProcessError) as error:
                run_ci.run_command([sys.executable, "-c", parent_code], dict(os.environ))
            self.assertEqual(7, error.exception.returncode)
            port = int(ready.read_text())
            for attempt in range(100):
                with socket.socket() as probe:
                    if probe.connect_ex(("127.0.0.1", port)) != 0:
                        break
                time.sleep(0.01)
            else:
                self.fail("A child listener survived its owning command's failure")


if __name__ == "__main__":
    unittest.main()

"""Exercise the real gitlink gate in disposable repositories, without submodule checkout."""
import os
import subprocess
import sys
import tempfile
from pathlib import Path


def exercise(repository, scenario):
    repository = repository.resolve()
    workflow_case = scenario.startswith("workflow-")
    workflow_route = "main"
    if workflow_case:
        _, workflow_route, scenario = scenario.split("-", 2)
    gate = repository / "scripts/gitlink-rc-gate.sh"
    with tempfile.TemporaryDirectory(prefix="parties-gitlink-") as temporary:
        root = Path(temporary)
        env = dict(os.environ, GIT_AUTHOR_NAME="Fixture", GIT_AUTHOR_EMAIL="fixture@example.invalid",
                   GIT_COMMITTER_NAME="Fixture", GIT_COMMITTER_EMAIL="fixture@example.invalid")

        def git(*args):
            return subprocess.check_output(["git", *args], cwd=root, env=env, text=True).strip()

        git("init", "--quiet")
        (root / ".gitmodules").write_text('[submodule "root"]\n path = references/root\n url = https://example.invalid/root.git\n')
        old, middle, new = "a" * 40, "b" * 40, "c" * 40

        def commit(pointer, ledger="", parents=None, *, gitlink=True, mode_bits="160000", gitlink_sha=None):
            (root / ".gitlink-signoff.tsv").write_text(ledger)
            git("add", ".gitmodules", ".gitlink-signoff.tsv")
            if gitlink:
                git("update-index", "--add", "--cacheinfo", f"{mode_bits},{gitlink_sha or pointer},references/root")
            else:
                git("update-index", "--force-remove", "--", "references/root")
            tree = git("write-tree")
            parent_args = ["-p", git("rev-parse", "HEAD")] if parents is None and (root / ".git/refs/heads/main").exists() else []
            if parents is not None:
                parent_args = [item for parent in parents for item in ("-p", parent)]
            revision = git("commit-tree", tree, *parent_args, "-m", "test: construct gitlink gate fixture")
            git("update-ref", "refs/heads/main", revision)
            git("symbolic-ref", "HEAD", "refs/heads/main")
            return revision

        def signoff(pointer, owner="owner", disposition="validated-advance"):
            return f"references/root|{pointer}|{disposition}|fixture|{owner}|2026-10-22\n"

        base = commit(old)
        if scenario.startswith("delayed-declaration-"):
            git("update-index", "--add", "--cacheinfo", f"160000,{middle},references/delayed")
            commit(old)
            with (root / ".gitmodules").open("a") as modules:
                modules.write('[submodule "delayed"]\n path = references/delayed\n url = https://example.invalid/delayed.git\n')
            ledger = f"references/delayed|{middle}|validated-advance|fixture|owner|2026-10-22\n" if scenario.endswith("signed") and not scenario.endswith("unsigned") else ""
            commit(old, ledger)
        elif scenario.startswith("merge-"):
            initial = base
            base = commit(new)
            git("update-ref", "refs/heads/main", initial)
            branch_pointer = middle if "branch" in scenario else old
            branch = commit(branch_pointer)
            merge_pointer = middle if "resolution" in scenario else new
            ledger = signoff(middle) if scenario.endswith("signed") and not scenario.endswith("unsigned") else ""
            commit(merge_pointer, ledger, parents=[base, branch])
        elif scenario == "unchanged":
            commit(old)
        elif scenario in {"signed", "diff-signed"}:
            commit(new, signoff(new))
        elif scenario == "stale":
            commit(new, signoff(middle))
        elif scenario == "placeholder":
            commit(new, signoff(new, "<OWNER>"))
        elif scenario == "reset":
            commit(new, signoff(new, disposition="reset"))
        elif scenario == "multi-unsigned":
            commit(middle)
            commit(new, signoff(new))
        elif scenario == "temporary-unsigned":
            commit(middle)
            commit(old, signoff(old))
        elif scenario == "multi-signed":
            commit(middle, signoff(middle))
            commit(new, signoff(middle) + signoff(new))
        elif scenario == "undeclared-bump":
            (root / ".gitmodules").write_text("")
            commit(new)
        elif scenario == "malformed-declarations":
            (root / ".gitmodules").write_text("[malformed")
            commit(new, signoff(new))
        elif scenario == "removed-gitlink":
            commit(old, gitlink=False)
        elif scenario == "gitlink-mode-change":
            blob = git("hash-object", "-w", "--", str(root / ".gitmodules"))
            commit(old, mode_bits="100644", gitlink_sha=blob)
        else:
            commit(new)
        if scenario == "empty-base":
            base = ""
        elif scenario == "missing-base":
            base = "d" * 40
        elif scenario == "zero-base":
            base = "0" * 40
        elif scenario == "invalid-base":
            base = "main"
        elif scenario == "nonancestor":
            tree = git("write-tree")
            base = git("commit-tree", tree, "-m", "test: construct gitlink gate fixture")
        mode = "--diff" if scenario.startswith("diff-") else "--push-range"
        if workflow_case:
            workflow = (repository / ".github/workflows/rc-gate.yml").read_text().replace("\r\n", "\n")
            steps = workflow.split("      - name: ")
            step = next(item for item in steps if item.startswith("Determine base ref\n"))
            script = step.split("        run: |\n", 1)[1]
            script = "\n".join(line[10:] if line.startswith("          ") else line for line in script.splitlines())
            output = root / "github-output"
            env.update(EVENT_NAME="push", EVENT_REF="refs/heads/main", EVENT_BEFORE=base,
                       BASE_REF="main", MANUAL_BASE="", GITHUB_OUTPUT=str(output))
            expected_base, expected_mode = base, "push-range"
            if workflow_route in {"rc", "tag"}:
                git("tag", "v0.0.0", base)
                env["EVENT_REF"] = "refs/heads/rc/fixture" if workflow_route == "rc" else "refs/tags/vfixture"
                expected_base, expected_mode = "v0.0.0", "diff"
            elif workflow_route in {"manual", "pr"}:
                git("update-ref", "refs/remotes/origin/main", base)
                env["EVENT_NAME"] = "workflow_dispatch" if workflow_route == "manual" else "pull_request"
                env["MANUAL_BASE"] = base if workflow_route == "manual" else ""
                expected_base, expected_mode = (base if workflow_route == "manual" else "origin/main"), "diff"
            selection = subprocess.run(["bash", "-c", script], cwd=root, env=env, capture_output=True, text=True)
            invalid = scenario in {"missing-base", "zero-base", "invalid-base", "nonancestor", "empty-base"}
            if invalid:
                assert selection.returncode != 0, (scenario, selection.stdout, selection.stderr)
                assert not output.exists() or not output.read_text().strip()
                print(scenario + ": workflow rejected unusable main event.before")
                return
            assert selection.returncode == 0, selection.stderr
            values = dict(line.split("=", 1) for line in output.read_text().splitlines())
            assert values == {"ref": expected_base, "mode": expected_mode}, (scenario, values, expected_base)
            (root / "scripts").mkdir()
            (root / "scripts/gitlink-rc-gate.sh").write_bytes(gate.read_bytes())
            gate_step = next(item for item in steps if item.startswith("Root gitlink RC gate\n"))
            gate_script = gate_step.split("        run: ", 1)[1].strip()
            env.update(GATE_BASE=values["ref"], GATE_MODE=values["mode"])
            result = subprocess.run(["bash", "-c", gate_script], cwd=root, env=env, capture_output=True, text=True)
        else:
            result = subprocess.run(["bash", str(gate), mode, base], cwd=root, env=env, capture_output=True, text=True)
        expected = scenario in {"unchanged", "signed", "multi-signed", "diff-signed", "delayed-declaration-signed", "merge-inherited", "merge-branch-signed", "merge-resolution-signed"}
        assert (result.returncode == 0) == expected, (scenario, result.stdout, result.stderr)
        if scenario == "empty-base":
            assert "--push-range requires <base-ref>" in result.stderr, result.stderr
        push_range = (not workflow_case and mode == "--push-range") or (workflow_case and workflow_route == "main")
        if push_range and scenario in {"unsigned", "removed-gitlink", "gitlink-mode-change"}:
            assert "push-range" in result.stderr, result.stderr
        print(scenario + ": verified " + ("pass" if expected else "rejection"))
        print(result.stdout + result.stderr)


if __name__ == "__main__":
    exercise(Path(sys.argv[1]), sys.argv[2])

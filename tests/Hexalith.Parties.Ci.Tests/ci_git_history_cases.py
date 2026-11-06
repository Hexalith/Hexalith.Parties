"""Exercise direct-checkout history preparation with real hermetic Git origins."""
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


def exercise(repository, scenario):
    helper = repository.resolve() / "scripts/prepare-ci-git-history.sh"
    with tempfile.TemporaryDirectory(prefix="parties-ci-history-test-") as temporary:
        directory = Path(temporary)
        env = dict(os.environ, GIT_AUTHOR_NAME="Fixture", GIT_AUTHOR_EMAIL="fixture@example.invalid",
                   GIT_COMMITTER_NAME="Fixture", GIT_COMMITTER_EMAIL="fixture@example.invalid",
                   GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=str(directory / "no-global-config"))

        def run(cwd, *arguments, check=True):
            return subprocess.run(arguments, cwd=cwd, env=env, capture_output=True, text=True,
                                  check=check, timeout=30)

        def git(cwd, *arguments, check=True):
            return run(cwd, "git", *arguments, check=check).stdout.strip()

        def commit(cwd, description, gitlink=None):
            git(cwd, "add", ".")
            if gitlink is not None:
                git(cwd, "update-index", "--add", "--cacheinfo", gitlink)
            git(cwd, "-c", "core.hooksPath=/dev/null", "commit", "--quiet", "-m", description)
            return git(cwd, "rev-parse", "HEAD")

        nested = directory / "nested"
        nested.mkdir()
        git(nested, "init", "--quiet", "--initial-branch=main")
        (nested / "marker").write_text("nested must remain uninitialized\n")
        nested_head = commit(nested, "test: create nested origin")
        nested_origin = directory / "nested.git"
        git(directory, "clone", "--bare", str(nested), str(nested_origin))

        owner = directory / "owner"
        owner.mkdir()
        git(owner, "init", "--quiet", "--initial-branch=main")
        (owner / "historical-validator.py").write_text("authentic historical validator\n")
        historical = commit(owner, "test: retain historical validator")
        git(owner, "tag", "v1.0.0")
        (owner / "historical-validator.py").unlink()
        (owner / "selected.txt").write_text("selected body\n")
        parent = commit(owner, "test: replace historical body")
        (owner / ".gitmodules").write_text(
            '[submodule "nested"]\n path = dependencies/nested\n url = ' + nested_origin.as_uri() + '\n')
        selected = commit(owner, "test: declare nested dependency without initializing it",
                          f"160000,{nested_head},dependencies/nested")
        origin = directory / "owner.git"
        git(directory, "clone", "--bare", str(owner), str(origin))

        root = directory / "root"
        root.mkdir()
        git(root, "init", "--quiet", "--initial-branch=main")
        (root / ".gitmodules").write_text(
            '[submodule "direct"]\n path = references/direct\n url = ' + origin.as_uri() + '\n')
        root_head = commit(root, "test: select exact direct dependency", f"160000,{selected},references/direct")
        child = root / "references/direct"
        git(root, "clone", "--depth=1", "--no-tags", "--no-recurse-submodules", origin.as_uri(), str(child))
        assert git(child, "rev-parse", "--is-shallow-repository") == "true"
        assert run(child, "git", "cat-file", "-e", historical + "^{commit}", check=False).returncode != 0
        assert run(child, "git", "rev-parse", "--verify", "HEAD^", check=False).returncode != 0
        assert run(child, "git", "rev-parse", "--verify", "refs/tags/v1.0.0", check=False).returncode != 0
        # The remote advances, while the root continues to select the preceding immutable source.
        (owner / "selected.txt").write_text("new remote body\n")
        commit(owner, "test: advance only the configured origin")
        git(owner, "push", str(origin), "main")
        if scenario in ("full-no-tags", "mismatched-head"):
            git(child, "-c", "submodule.recurse=false", "fetch", "--unshallow", "--no-tags", "--no-recurse-submodules", "origin")
            assert git(child, "rev-parse", "--is-shallow-repository") == "false"
            assert run(child, "git", "rev-parse", "--verify", "refs/tags/v1.0.0", check=False).returncode != 0
        if scenario == "mismatched-head":
            git(child, "-c", "submodule.recurse=false", "checkout", "--detach", parent)
        if scenario == "missing-direct":
            shutil.rmtree(child)
            child.mkdir()
        else:
            git(child, "config", "submodule.recurse", "true")
            git(child, "config", "fetch.recurseSubmodules", "true")
            (child / "selected.txt").write_bytes(b"local dirty body\r\n")
            (child / "untracked.txt").write_bytes(b"untracked bytes\x00\r\n")
        # Only the committed root declarations are authoritative, even with a dirty local copy.
        with (root / ".gitmodules").open("a") as declarations:
            declarations.write('[submodule "unselected"]\n path = references/unselected\n url = ' + nested_origin.as_uri() + '\n')
        root_modules = (root / ".gitmodules").read_bytes()
        git(root, "config", "submodule.recurse", "true")
        git(root, "config", "fetch.recurseSubmodules", "true")
        result = run(root, "bash", str(helper), check=False)
        if scenario in ("missing-direct", "mismatched-head"):
            assert result.returncode != 0, result.stdout + result.stderr
            expected = "already prepared own checkout" if scenario == "missing-direct" else "differs from its recorded selection"
            assert expected in result.stderr, result.stdout + result.stderr
            if scenario == "missing-direct":
                assert not (child / ".git").exists()
        else:
            assert result.returncode == 0, result.stdout + result.stderr
            assert git(child, "rev-parse", "--is-shallow-repository") == "false"
            assert git(child, "rev-parse", "HEAD") == selected
            assert git(child, "symbolic-ref", "HEAD") == "refs/heads/main"
            assert git(child, "rev-parse", "HEAD^") == parent
            assert git(child, "rev-parse", "refs/tags/v1.0.0") == historical
            assert git(child, "show", historical + ":historical-validator.py") == "authentic historical validator"
        if scenario != "missing-direct":
            assert (child / "selected.txt").read_bytes() == b"local dirty body\r\n"
            assert (child / "untracked.txt").read_bytes() == b"untracked bytes\x00\r\n"
            assert not (child / "dependencies/nested/.git").exists()
            assert not (child / ".git/modules").exists()
        assert not (root / "references/unselected").exists()
        assert git(root, "rev-parse", "HEAD") == root_head
        assert git(root, "rev-parse", "HEAD:references/direct") == selected
        assert (root / ".gitmodules").read_bytes() == root_modules


if __name__ == "__main__":
    exercise(Path(sys.argv[1]), sys.argv[2])

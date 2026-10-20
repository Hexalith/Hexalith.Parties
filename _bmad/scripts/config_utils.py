"""Shared strict TOML loading and structural merge support."""

from __future__ import annotations

import tomllib
from pathlib import Path
from typing import Any, Iterable


class ConfigError(ValueError):
    """Raised when a present configuration layer cannot be used safely."""


_KEYED_MERGE_FIELDS = ("code", "id")


def load_toml(path: Path, *, required: bool = False) -> dict[str, Any]:
    """Load a TOML table, allowing absence only for optional layers."""
    if not path.exists():
        if required:
            raise ConfigError(f"required TOML file not found: {path}")
        return {}
    if not path.is_file():
        raise ConfigError(f"TOML layer is not a file: {path}")
    try:
        with path.open("rb") as stream:
            parsed = tomllib.load(stream)
    except tomllib.TOMLDecodeError as error:
        raise ConfigError(f"failed to parse {path}: {error}") from error
    except OSError as error:
        raise ConfigError(f"failed to read {path}: {error}") from error
    if not isinstance(parsed, dict):
        raise ConfigError(f"TOML layer did not parse to a table: {path}")
    try:
        _validate_keyed_arrays(parsed)
    except ConfigError as error:
        raise ConfigError(f"invalid TOML layer {path}: {error}") from error
    return parsed


def _detect_keyed_merge_field(items: list[Any]) -> str | None:
    if not items or not all(isinstance(item, dict) for item in items):
        return None
    for candidate in _KEYED_MERGE_FIELDS:
        if all(candidate in item for item in items):
            for item in items:
                value = item[candidate]
                if not isinstance(value, str):
                    raise ConfigError(
                        f"keyed array identifier `{candidate}` must be a string, "
                        f"got {type(value).__name__}"
                    )
                if not value:
                    raise ConfigError(
                        f"keyed array identifier `{candidate}` must not be empty"
                    )
            return candidate
    return None


def _merge_arrays(base: list[Any], override: list[Any], path: str) -> list[Any]:
    keyed_field = _detect_keyed_merge_field(base + override)
    if keyed_field is None:
        return list(base) + list(override)

    _validate_unique_keys(base, keyed_field, f"{path} (base)")
    _validate_unique_keys(override, keyed_field, f"{path} (override)")
    result: list[Any] = []
    index_by_key: dict[str, int] = {}
    for item in base:
        copied = dict(item)
        index_by_key[copied[keyed_field]] = len(result)
        result.append(copied)
    for item in override:
        copied = dict(item)
        key = copied[keyed_field]
        if key in index_by_key:
            result[index_by_key[key]] = copied
        else:
            index_by_key[key] = len(result)
            result.append(copied)
    return result


def _validate_unique_keys(items: list[Any], keyed_field: str, path: str) -> None:
    """Require uniqueness within one input using the selected merge identity."""
    seen: dict[str, int] = {}
    for index, item in enumerate(items):
        key = item[keyed_field]
        if key in seen:
            raise ConfigError(
                f"duplicate keyed array identifier `{keyed_field}` at {path}: "
                f"items {seen[key]} and {index}"
            )
        seen[key] = index


def _validate_keyed_arrays(value: Any, path: str = "$") -> None:
    """Reject ambiguous identities, including arrays introduced without a merge."""
    if isinstance(value, dict):
        for key, child in value.items():
            _validate_keyed_arrays(child, f"{path}.{key}")
    elif isinstance(value, list):
        keyed_field = _detect_keyed_merge_field(value)
        if keyed_field is not None:
            _validate_unique_keys(value, keyed_field, path)
        for index, child in enumerate(value):
            _validate_keyed_arrays(child, f"{path}[{index}]")


def _structural_merge(base: Any, override: Any, path: str = "$") -> Any:
    """Merge already-validated inputs without rescanning their subtrees."""
    if isinstance(base, dict) and isinstance(override, dict):
        result = dict(base)
        for key, value in override.items():
            result[key] = (
                _structural_merge(result[key], value, f"{path}.{key}")
                if key in result else value
            )
        return result
    if isinstance(base, list) and isinstance(override, list):
        return _merge_arrays(base, override, path)
    return override


def structural_merge(base: Any, override: Any) -> Any:
    """Merge unique keyed arrays by identity and append other arrays recursively."""
    _validate_keyed_arrays(base)
    _validate_keyed_arrays(override)
    return _structural_merge(base, override)


def merge_layers(layers: Iterable[dict[str, Any]]) -> dict[str, Any]:
    merged: dict[str, Any] = {}
    for layer in layers:
        merged = structural_merge(merged, layer)
    return merged


def _load_toml_layers(paths: Iterable[Path]) -> dict[str, Any]:
    """Load and merge layers while retaining file context for merge failures."""
    merged: dict[str, Any] = {}
    previous: list[Path] = []
    for index, path in enumerate(paths):
        layer = load_toml(path, required=index == 0)
        try:
            merged = structural_merge(merged, layer)
        except ConfigError as error:
            sources = ", ".join(str(source) for source in previous)
            raise ConfigError(
                f"failed to merge TOML layer {path} over earlier layers [{sources}]: {error}"
            ) from error
        previous.append(path)
    return merged


def load_central_config(project_root: Path) -> dict[str, Any]:
    bmad_dir = project_root / "_bmad"
    return _load_toml_layers(
        (
            bmad_dir / "config.toml",
            bmad_dir / "config.user.toml",
            bmad_dir / "custom" / "config.toml",
            bmad_dir / "custom" / "config.user.toml",
        )
    )


def load_customization(project_root: Path | None, skill_dir: Path) -> dict[str, Any]:
    skill_name = skill_dir.name
    custom_dir = project_root / "_bmad" / "custom" if project_root else None
    paths = [skill_dir / "customize.toml"]
    if custom_dir is not None:
        paths.extend(
            (
                custom_dir / f"{skill_name}.toml",
                custom_dir / f"{skill_name}.user.toml",
            )
        )
    return _load_toml_layers(paths)

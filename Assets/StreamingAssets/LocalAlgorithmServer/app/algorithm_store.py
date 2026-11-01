import ast
import hashlib
import json
import os
import re
import threading
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, Optional, Tuple


MAX_ALGORITHM_BYTES = 1024 * 1024


class AlgorithmStoreError(Exception):
    pass


class AlgorithmStore:
    """Stores immutable algorithm files and atomically switches the manifest."""

    def __init__(self, data_dir: Path) -> None:
        self.data_dir = data_dir.resolve()
        self.algorithm_dir = self.data_dir / "algorithms"
        self.manifest_path = self.data_dir / "current_algorithm.json"
        self.previous_manifest_path = (
            self.data_dir / "previous_algorithm.json"
        )
        self._lock = threading.RLock()
        self.algorithm_dir.mkdir(parents=True, exist_ok=True)

    def ensure_default(self, default_file: Path) -> None:
        with self._lock:
            if not default_file.is_file():
                raise AlgorithmStoreError(
                    f"Default algorithm does not exist: {default_file}"
                )

            content = default_file.read_bytes()
            default_sha256 = hashlib.sha256(content).hexdigest()
            current = self.get_status()

            if (
                current is not None
                and current.get("algorithm_id") == "project-default"
                and current.get("sha256") == default_sha256
            ):
                # 当前缓存已经是这个项目文件，不重复写入。
                return

            self.install(
                algorithm_id="project-default",
                file_name=default_file.name,
                content=content,
                version=f"project-{default_sha256[:12]}",
                source="project-streaming-assets",
                expected_sha256=default_sha256,
                signature_verified=False,
            )


    def get_status(self) -> Optional[Dict[str, Any]]:
        with self._lock:
            if not self.manifest_path.is_file():
                return None

            try:
                manifest = json.loads(
                    self.manifest_path.read_text(encoding="utf-8")
                )
            except (OSError, json.JSONDecodeError) as exc:
                raise AlgorithmStoreError(
                    f"Cannot read algorithm manifest: {exc}"
                ) from exc

            file_name = str(manifest.get("file_name", ""))
            algorithm_path = (self.algorithm_dir / file_name).resolve()
            if (
                not file_name
                or algorithm_path.parent != self.algorithm_dir
                or not algorithm_path.is_file()
            ):
                raise AlgorithmStoreError(
                    "Algorithm manifest points to an invalid file."
                )

            # Migrate manifests created by the earlier local-service version.
            manifest.setdefault(
                "algorithm_id",
                str(manifest.get("name", "legacy-algorithm")),
            )
            manifest.setdefault("signature_verified", False)
            return manifest

    def get_snapshot(self) -> Tuple[Dict[str, Any], Path]:
        """Return one consistent manifest/path pair under the store lock."""
        with self._lock:
            status = self.get_status()
            if status is None:
                raise AlgorithmStoreError("No algorithm is installed.")
            path = (self.algorithm_dir / status["file_name"]).resolve()
            if path.parent != self.algorithm_dir or not path.is_file():
                raise AlgorithmStoreError(
                    "The installed algorithm file is invalid."
                )
            return dict(status), path

    def install(
        self,
        algorithm_id: str,
        file_name: str,
        content: bytes,
        version: str,
        source: str,
        expected_sha256: Optional[str],
        signature_verified: bool = False,
    ) -> Tuple[bool, Dict[str, Any]]:
        with self._lock:
            self._validate_algorithm(file_name, content)

            actual_sha256 = hashlib.sha256(content).hexdigest()
            expected = (expected_sha256 or "").strip().lower()
            if expected and expected != actual_sha256:
                raise AlgorithmStoreError(
                    "SHA-256 mismatch: the downloaded algorithm is incomplete "
                    "or has been changed."
                )

            current = self.get_status()
            if (
                current is not None
                and current["sha256"] == actual_sha256
                and current["algorithm_id"] == self._safe_id(algorithm_id)
            ):
                return False, current

            safe_name = self._safe_name(Path(file_name).stem)
            safe_version = self._safe_version(version)
            target_name = f"{safe_name}_{actual_sha256[:16]}.py"
            target_path = (self.algorithm_dir / target_name).resolve()
            if target_path.parent != self.algorithm_dir:
                raise AlgorithmStoreError("Invalid algorithm target path.")

            self._atomic_write_bytes(target_path, content)

            manifest: Dict[str, Any] = {
                "algorithm_id": self._safe_id(algorithm_id),
                "name": safe_name,
                "version": safe_version,
                "sha256": actual_sha256,
                "installed_at": datetime.now(timezone.utc).isoformat(),
                "file_name": target_name,
                "source": (source or "unknown")[:128],
                "signature_verified": bool(signature_verified),
            }

            if current is not None:
                self._atomic_write_text(
                    self.previous_manifest_path,
                    json.dumps(current, ensure_ascii=False, indent=2),
                )
            self._atomic_write_text(
                self.manifest_path,
                json.dumps(manifest, ensure_ascii=False, indent=2),
            )
            return True, manifest

    @staticmethod
    def _validate_algorithm(file_name: str, content: bytes) -> None:
        if not file_name.lower().endswith(".py"):
            raise AlgorithmStoreError("Only .py algorithm files are accepted.")
        if not content:
            raise AlgorithmStoreError("The algorithm file is empty.")
        if len(content) > MAX_ALGORITHM_BYTES:
            raise AlgorithmStoreError("The algorithm file exceeds 1 MB.")

        try:
            source = content.decode("utf-8-sig")
        except UnicodeDecodeError as exc:
            raise AlgorithmStoreError(
                "The algorithm file must use UTF-8 encoding."
            ) from exc

        try:
            tree = ast.parse(source, filename=file_name)
        except SyntaxError as exc:
            raise AlgorithmStoreError(
                f"Algorithm syntax error at line {exc.lineno}: {exc.msg}"
            ) from exc

        has_schedule = any(
            isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef))
            and node.name == "schedule"
            for node in tree.body
        )
        if not has_schedule:
            raise AlgorithmStoreError(
                "The algorithm must define schedule(problem: dict) -> dict."
            )

    @staticmethod
    def _safe_name(value: str) -> str:
        result = re.sub(r"[^A-Za-z0-9_.-]+", "_", value).strip("._")
        return (result or "algorithm")[:64]

    @staticmethod
    def _safe_id(value: str) -> str:
        result = re.sub(
            r"[^A-Za-z0-9_.-]+",
            "_",
            value or "",
        ).strip("._")
        if not result:
            raise AlgorithmStoreError(
                "Algorithm ID cannot be empty."
            )
        return result[:128]

    @staticmethod
    def _safe_version(value: str) -> str:
        result = (value or "").strip()
        if not result:
            raise AlgorithmStoreError("Algorithm version cannot be empty.")
        return result[:64]

    @staticmethod
    def _atomic_write_bytes(target: Path, content: bytes) -> None:
        temporary = target.with_name(target.name + ".tmp")
        temporary.write_bytes(content)
        os.replace(temporary, target)

    @staticmethod
    def _atomic_write_text(target: Path, content: str) -> None:
        temporary = target.with_name(target.name + ".tmp")
        temporary.write_text(content, encoding="utf-8")
        os.replace(temporary, target)

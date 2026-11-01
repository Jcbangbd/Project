import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
import uuid
from pathlib import Path
from typing import Any, Dict, Optional

PROCESS_EXIT_GRACE_SECONDS = 45

from .algorithm_store import AlgorithmStore, AlgorithmStoreError


class AlgorithmRunError(Exception):
    pass


class AlgorithmRunner:
    """Runs the current algorithm in a child process with a hard timeout."""

    def __init__(self, base_dir: Path, store: AlgorithmStore) -> None:
        self.base_dir = base_dir.resolve()
        self.store = store
        self.jobs_dir = store.data_dir / "jobs"
        self.jobs_dir.mkdir(parents=True, exist_ok=True)

    def run(
        self,
        problem: Dict[str, Any],
        request_id: Optional[str],
        task_id: str,
        task_version: int,
        timeout_seconds: int,
    ) -> Dict[str, Any]:
        try:
            status, algorithm_path = self.store.get_snapshot()
        except AlgorithmStoreError as exc:
            raise AlgorithmRunError(str(exc)) from exc

        actual_request_id = (request_id or str(uuid.uuid4()))[:128]
        job_dir = Path(
            tempfile.mkdtemp(prefix="job_", dir=str(self.jobs_dir))
        ).resolve()
        input_path = job_dir / "input.json"
        output_path = job_dir / "output.json"

        try:
            input_path.write_text(
                json.dumps(problem, ensure_ascii=False),
                encoding="utf-8",
            )

            command = [
                sys.executable,
                "-m",
                "app.worker",
                "--algorithm",
                str(algorithm_path),
                "--input",
                str(input_path),
                "--output",
                str(output_path),
            ]

            child_environment = os.environ.copy()
            child_environment.pop("LOCAL_API_KEY", None)
            child_environment.pop("LOCAL_SERVER_INSTANCE_ID", None)
            child_environment["PYTHONPATH"] = str(self.base_dir)
            creation_flags = (
                subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
            )

            started = time.perf_counter()
            process_timeout_seconds = (
                    timeout_seconds +
                    PROCESS_EXIT_GRACE_SECONDS
            )

            try:
                completed = subprocess.run(
                    command,
                    cwd=str(self.base_dir),
                    env=child_environment,
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    timeout=process_timeout_seconds,
                    creationflags=creation_flags,
                    check=False,
                )
            except subprocess.TimeoutExpired as exc:
                raise AlgorithmRunError(
                    "Algorithm process did not exit within "
                    f"{process_timeout_seconds} seconds. "
                    f"The solver time limit was {timeout_seconds} seconds."
                ) from exc

            duration_ms = (time.perf_counter() - started) * 1000.0
            if completed.returncode != 0:
                detail = (completed.stderr or completed.stdout).strip()
                if len(detail) > 4000:
                    detail = detail[-4000:]
                raise AlgorithmRunError(
                    "Algorithm process failed: " + (detail or "unknown error")
                )

            if not output_path.is_file():
                raise AlgorithmRunError(
                    "Algorithm finished without creating output.json."
                )

            try:
                result = json.loads(output_path.read_text(encoding="utf-8"))
            except (OSError, json.JSONDecodeError) as exc:
                raise AlgorithmRunError(
                    f"Algorithm returned invalid JSON: {exc}"
                ) from exc

            if not isinstance(result, dict):
                raise AlgorithmRunError(
                    "Algorithm result must be a JSON object."
                )

            return {
                "success": True,
                "request_id": actual_request_id,
                "task_id": task_id,
                "task_version": task_version,
                "algorithm_version": status["version"],
                "algorithm_sha256": status["sha256"],
                "duration_ms": round(duration_ms, 3),
                "result": result,
            }
        finally:
            shutil.rmtree(job_dir, ignore_errors=True)

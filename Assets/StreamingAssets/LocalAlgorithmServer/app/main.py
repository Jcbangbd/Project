import hmac
import os
from contextlib import asynccontextmanager
from pathlib import Path
from typing import Optional

from fastapi import Depends, FastAPI, File, Form, Header, HTTPException, UploadFile
from starlette.concurrency import run_in_threadpool

from .algorithm_runner import AlgorithmRunError, AlgorithmRunner
from .algorithm_store import AlgorithmStore, AlgorithmStoreError, MAX_ALGORITHM_BYTES
from .algorithm_sync import AlgorithmSyncError, AlgorithmSyncService
from .models import (
    AlgorithmInstallResponse,
    AlgorithmRunRequest,
    AlgorithmRunResponse,
    AlgorithmStatusResponse,
    AlgorithmSyncRequest,
    AlgorithmSyncResponse,
    HealthResponse,
)


BASE_DIR = Path(__file__).resolve().parent.parent
DEFAULT_ALGORITHM = BASE_DIR / "algorithms" / "default_scheduler.py"
DATA_DIR = Path(
    os.environ.get("LOCAL_ALGORITHM_DATA_DIR", str(BASE_DIR / "data"))
).resolve()
API_KEY = os.environ.get("LOCAL_API_KEY", "")
INSTANCE_ID = os.environ.get("LOCAL_SERVER_INSTANCE_ID", "manual")

store = AlgorithmStore(DATA_DIR)
runner = AlgorithmRunner(BASE_DIR, store)
sync_service = AlgorithmSyncService(store)


@asynccontextmanager
async def lifespan(_: FastAPI):
    store.ensure_default(DEFAULT_ALGORITHM)
    yield


app = FastAPI(
    title="VR Local Rail-Crane Algorithm Service",
    version="1.0.0",
    lifespan=lifespan,
)


def require_api_key(
    x_local_api_key: Optional[str] = Header(
        default=None,
        alias="X-Local-Api-Key",
    ),
) -> None:
    if not API_KEY:
        raise HTTPException(
            status_code=503,
            detail="LOCAL_API_KEY is not configured.",
        )
    if x_local_api_key is None or not hmac.compare_digest(
        x_local_api_key,
        API_KEY,
    ):
        raise HTTPException(status_code=401, detail="Invalid local API key.")


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    try:
        algorithm = store.get_status()
    except AlgorithmStoreError:
        algorithm = None

    return HealthResponse(
        service="vr-local-algorithm",
        ready=True,
        instance_id=INSTANCE_ID,
        process_id=os.getpid(),
        algorithm=algorithm,
    )


@app.get(
    "/api/v1/algorithm/status",
    response_model=AlgorithmStatusResponse,
    dependencies=[Depends(require_api_key)],
)
def algorithm_status(
) -> AlgorithmStatusResponse:
    try:
        algorithm = store.get_status()
    except AlgorithmStoreError as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc

    return AlgorithmStatusResponse(
        has_algorithm=algorithm is not None,
        algorithm=algorithm,
    )


@app.post(
    "/api/v1/algorithm/sync",
    response_model=AlgorithmSyncResponse,
    dependencies=[Depends(require_api_key)],
)
async def synchronize_algorithm(
    request: AlgorithmSyncRequest,
) -> AlgorithmSyncResponse:
    try:
        response = await run_in_threadpool(
            sync_service.synchronize,
            request.algorithm_id,
            request.version,
            request.sha256,
            request.file_size,
            request.signature or "",
            request.download_url,
            request.download_timeout_seconds,
            request.source,
        )
    except AlgorithmSyncError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc

    return AlgorithmSyncResponse(**response)


@app.post(
    "/api/v1/algorithm/install",
    response_model=AlgorithmInstallResponse,
    dependencies=[Depends(require_api_key)],
)
async def install_algorithm(
    file: UploadFile = File(...),
    algorithm_id: str = Form(default="manual-algorithm"),
    version: str = Form(...),
    source: str = Form(default="remote-server"),
    expected_sha256: Optional[str] = Form(default=None),
) -> AlgorithmInstallResponse:
    content = await file.read(MAX_ALGORITHM_BYTES + 1)
    try:
        installed, algorithm = store.install(
            algorithm_id=algorithm_id,
            file_name=file.filename or "algorithm.py",
            content=content,
            version=version,
            source=source,
            expected_sha256=expected_sha256,
            signature_verified=False,
        )
    except AlgorithmStoreError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    finally:
        await file.close()

    return AlgorithmInstallResponse(
        installed=installed,
        message=(
            "Algorithm installed."
            if installed
            else "The same algorithm is already installed."
        ),
        algorithm=algorithm,
    )


@app.post(
    "/api/v1/algorithm/run",
    response_model=AlgorithmRunResponse,
    dependencies=[Depends(require_api_key)],
)
async def run_algorithm(
    request: AlgorithmRunRequest,
) -> AlgorithmRunResponse:
    try:
        response = await run_in_threadpool(
            runner.run,
            request.input,
            request.request_id,
            request.task_id,
            request.task_version,
            request.timeout_seconds,
        )
    except AlgorithmRunError as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc

    return AlgorithmRunResponse(**response)

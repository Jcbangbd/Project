from typing import Any, Dict, Optional

from pydantic import BaseModel, Field


class AlgorithmInfo(BaseModel):
    algorithm_id: str
    name: str
    version: str
    sha256: str
    installed_at: str
    file_name: str
    source: str
    signature_verified: bool = False


class HealthResponse(BaseModel):
    service: str
    ready: bool
    instance_id: str
    process_id: int
    algorithm: Optional[AlgorithmInfo] = None


class AlgorithmStatusResponse(BaseModel):
    has_algorithm: bool
    algorithm: Optional[AlgorithmInfo] = None


class AlgorithmInstallResponse(BaseModel):
    installed: bool
    message: str
    algorithm: AlgorithmInfo


class AlgorithmSyncRequest(BaseModel):
    algorithm_id: str = Field(
        min_length=1,
        max_length=128,
        pattern=r"^[A-Za-z0-9_.-]+$",
    )
    version: str = Field(min_length=1, max_length=64)
    sha256: str = Field(pattern=r"^[A-Fa-f0-9]{64}$")
    file_size: int = Field(default=0, ge=0, le=1024 * 1024)
    signature: Optional[str] = Field(default=None, max_length=4096)
    download_url: str = Field(min_length=1, max_length=4096)
    download_timeout_seconds: int = Field(default=60, ge=5, le=300)
    source: str = Field(default="remote-server", max_length=128)


class AlgorithmSyncResponse(BaseModel):
    synchronized: bool
    already_installed: bool
    message: str
    algorithm: AlgorithmInfo


class AlgorithmRunRequest(BaseModel):
    request_id: Optional[str] = Field(default=None, max_length=128)
    task_id: str = Field(min_length=1, max_length=128)
    task_version: int = Field(ge=1)
    input: Dict[str, Any] = Field(default_factory=dict)
    timeout_seconds: int = Field(default=600, ge=1, le=600)


class AlgorithmRunResponse(BaseModel):
    success: bool
    request_id: str
    task_id: str
    task_version: int
    algorithm_version: str
    algorithm_sha256: str
    duration_ms: float
    result: Dict[str, Any]

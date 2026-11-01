import base64
import hashlib
import os
from pathlib import Path
from typing import Any, Dict
from urllib.parse import urlparse
from urllib.request import Request, urlopen

from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PublicKey

from .algorithm_store import AlgorithmStore, AlgorithmStoreError


class AlgorithmSyncError(Exception):
    pass


class AlgorithmSyncService:
    def __init__(self, store: AlgorithmStore) -> None:
        self.store = store
        self.require_signature = (
            os.environ.get("REQUIRE_ALGORITHM_SIGNATURE", "false")
            .strip()
            .lower()
            in {"1", "true", "yes"}
        )
        configured_key = os.environ.get(
            "ALGORITHM_PUBLIC_KEY_PATH",
            "",
        ).strip()
        self.public_key_path = (
            Path(configured_key).resolve()
            if configured_key
            else None
        )

    def synchronize(
        self,
        algorithm_id: str,
        version: str,
        expected_sha256: str,
        expected_size: int,
        signature: str,
        download_url: str,
        timeout_seconds: int,
        source: str,
    ) -> Dict[str, Any]:
        expected_hash = expected_sha256.strip().lower()
        current = self.store.get_status()
        if (
            current is not None
            and current.get("algorithm_id") == algorithm_id
            and current.get("sha256", "").lower() == expected_hash
        ):
            return {
                "synchronized": True,
                "already_installed": True,
                "message": "The required algorithm is already installed.",
                "algorithm": current,
            }

        content = self._download(
            download_url,
            timeout_seconds,
        )
        if expected_size > 0 and len(content) != expected_size:
            raise AlgorithmSyncError(
                "Algorithm size mismatch: "
                f"expected {expected_size}, received {len(content)}."
            )

        actual_hash = hashlib.sha256(content).hexdigest()
        if actual_hash != expected_hash:
            raise AlgorithmSyncError(
                "Algorithm SHA-256 mismatch."
            )

        signature_verified = self._verify_signature(
            content,
            signature,
        )

        try:
            installed, manifest = self.store.install(
                algorithm_id=algorithm_id,
                file_name=f"{algorithm_id}.py",
                content=content,
                version=version,
                source=source,
                expected_sha256=expected_hash,
                signature_verified=signature_verified,
            )
        except AlgorithmStoreError as exc:
            raise AlgorithmSyncError(str(exc)) from exc

        return {
            "synchronized": True,
            "already_installed": not installed,
            "message": (
                "Algorithm downloaded, verified and installed."
                if installed
                else "The required algorithm is already installed."
            ),
            "algorithm": manifest,
        }

    @staticmethod
    def _download(
        download_url: str,
        timeout_seconds: int,
    ) -> bytes:
        parsed = urlparse(download_url)
        if parsed.scheme not in {"http", "https"}:
            raise AlgorithmSyncError(
                "Algorithm download URL must use HTTP or HTTPS."
            )
        if not parsed.hostname:
            raise AlgorithmSyncError(
                "Algorithm download URL has no host."
            )

        request = Request(
            download_url,
            headers={
                "Accept": "text/x-python,application/octet-stream",
                "User-Agent": "VtpVR-LocalAlgorithm/2.0",
            },
            method="GET",
        )

        maximum = 1024 * 1024
        try:
            with urlopen(request, timeout=timeout_seconds) as response:
                content = response.read(maximum + 1)
        except Exception as exc:
            raise AlgorithmSyncError(
                f"Cannot download algorithm: {exc}"
            ) from exc

        if not content:
            raise AlgorithmSyncError(
                "The downloaded algorithm is empty."
            )
        if len(content) > maximum:
            raise AlgorithmSyncError(
                "The downloaded algorithm exceeds 1 MB."
            )
        return content

    def _verify_signature(
        self,
        content: bytes,
        encoded_signature: str,
    ) -> bool:
        signature_text = (encoded_signature or "").strip()
        if not signature_text:
            if self.require_signature:
                raise AlgorithmSyncError(
                    "The server did not provide an algorithm signature."
                )
            return False

        if (
            self.public_key_path is None
            or not self.public_key_path.is_file()
        ):
            if self.require_signature:
                raise AlgorithmSyncError(
                    "Algorithm public key file is missing."
                )
            return False

        try:
            key_data = self.public_key_path.read_bytes()
            public_key = serialization.load_pem_public_key(
                key_data
            )
            if not isinstance(public_key, Ed25519PublicKey):
                raise AlgorithmSyncError(
                    "Algorithm public key is not an Ed25519 key."
                )
            signature_bytes = base64.b64decode(
                signature_text,
                validate=True,
            )
            public_key.verify(signature_bytes, content)
            return True
        except InvalidSignature as exc:
            raise AlgorithmSyncError(
                "Algorithm signature verification failed."
            ) from exc
        except AlgorithmSyncError:
            raise
        except Exception as exc:
            raise AlgorithmSyncError(
                f"Cannot verify algorithm signature: {exc}"
            ) from exc

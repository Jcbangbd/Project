using System;
using Vtp.VR.Rest;

namespace Vtp.VR.LocalAlgorithm
{
    [Serializable]
    public sealed class LocalFastApiConfig
    {
        public string host = "127.0.0.1";
        public int port = 8000;
        public string serverFolder = "LocalAlgorithmServer";
        public string pythonEnvironmentFolder =
            "LocalAlgorithmPythonEnvironment";
        public string bundledPythonRelativePath = "runtime/python.exe";
        public string bootstrapPythonExecutable = "";
        public bool autoCreatePythonEnvironment = true;
        public bool autoInstallDependencies = true;
        public bool reinstallWhenRequirementsChange = true;
        public int environmentSetupTimeoutSeconds = 300;
        public int startupTimeoutSeconds = 20;
        public int requestTimeoutSeconds = 30;
        public bool stopServerOnQuit = true;
        public bool requireAlgorithmSignature = false;
        public string algorithmPublicKeyRelativePath =
            "config/algorithm_public_key.pem";
    }

    [Serializable]
    public sealed class AlgorithmInfoDto
    {
        public string algorithm_id;
        public string name;
        public string version;
        public string sha256;
        public string installed_at;
        public string file_name;
        public string source;
        public bool signature_verified;
    }

    [Serializable]
    public sealed class LocalFastApiHealthDto
    {
        public string service;
        public bool ready;
        public string instance_id;
        public int process_id;
        public AlgorithmInfoDto algorithm;
    }

    [Serializable]
    public sealed class AlgorithmStatusDto
    {
        public bool has_algorithm;
        public AlgorithmInfoDto algorithm;
    }

    [Serializable]
    public sealed class AlgorithmInstallDto
    {
        public bool installed;
        public string message;
        public AlgorithmInfoDto algorithm;
    }

    [Serializable]
    public sealed class AlgorithmSyncRequestDto
    {
        public string algorithm_id;
        public string version;
        public string sha256;
        public long file_size;
        public string signature;
        public string download_url;
        public int download_timeout_seconds;
        public string source;
    }

    [Serializable]
    public sealed class AlgorithmSyncResponseDto
    {
        public bool synchronized;
        public bool already_installed;
        public string message;
        public AlgorithmInfoDto algorithm;
    }

    [Serializable]
    public sealed class SchedulingRunRequestDto
    {
        public string request_id;
        public string task_id;
        public int task_version;
        public TaskCaseDocumentDto input;
        public int timeout_seconds;
    }

    [Serializable]
    public sealed class SchedulingRunResponseDto
    {
        public bool success;
        public string request_id;
        public string task_id;
        public int task_version;
        public string algorithm_version;
        public string algorithm_sha256;
        public float duration_ms;
        public SchedulingResultDto result;
    }
}

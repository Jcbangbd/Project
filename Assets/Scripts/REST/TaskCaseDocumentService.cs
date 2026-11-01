using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Vtp.VR.Rest
{
    /// <summary>
    /// 下载、校验并缓存教师发布的调度算例。
    /// </summary>
    public sealed class TaskCaseDocumentService
    {
        private readonly RemoteFileService _fileService;

        public TaskCaseDocumentService(RestApiClient client)
        {
            _fileService = new RemoteFileService(client);
        }

        public void DownloadAndValidate(
            VrTaskDetailDto task,
            Action<ValidatedTaskCase> onSuccess,
            Action<RestApiError> onError)
        {
            if (task == null ||
                task.caseDocument == null ||
                string.IsNullOrWhiteSpace(task.caseDocument.fileId))
            {
                InvokeError(
                    onError,
                    RestApiError.Client(
                        "任务没有关联有效的算例文件。"));
                return;
            }

            _fileService.DownloadByFileId(
                task.caseDocument.fileId,
                delegate(byte[] bytes)
                {
                    ProcessBytes(task, bytes, onSuccess, onError);
                },
                onError);
        }

        private static void ProcessBytes(
            VrTaskDetailDto task,
            byte[] bytes,
            Action<ValidatedTaskCase> onSuccess,
            Action<RestApiError> onError)
        {
            if (bytes == null || bytes.Length == 0)
            {
                InvokeError(
                    onError,
                    RestApiError.Client("下载的任务算例为空。"));
                return;
            }

            if (task.caseDocument.fileSize > 0 &&
                bytes.LongLength != task.caseDocument.fileSize)
            {
                InvokeError(
                    onError,
                    RestApiError.Client(
                        "任务算例大小校验失败，期望 " +
                        task.caseDocument.fileSize +
                        "，实际 " +
                        bytes.LongLength +
                        "。"));
                return;
            }

            if (!string.IsNullOrWhiteSpace(
                    task.caseDocument.fileMd5))
            {
                string actual = ComputeMd5(bytes);
                string expected =
                    NormalizeHash(task.caseDocument.fileMd5);
                if (!string.Equals(
                        actual,
                        expected,
                        StringComparison.OrdinalIgnoreCase))
                {
                    InvokeError(
                        onError,
                        RestApiError.Client("任务算例MD5校验失败。"));
                    return;
                }
            }

            TaskCaseDocumentDto document;
            try
            {
                string json = Encoding.UTF8.GetString(bytes);
                document =
                    JsonUtility.FromJson<TaskCaseDocumentDto>(json);
            }
            catch (Exception exception)
            {
                InvokeError(
                    onError,
                    RestApiError.Client(
                        "任务算例JSON无法解析：" +
                        exception.Message));
                return;
            }

            string validationError;
            if (!TaskCaseDocumentValidator.TryValidate(
                    document,
                    task,
                    out validationError))
            {
                InvokeError(
                    onError,
                    RestApiError.Client(
                        "任务算例校验失败：" +
                        validationError));
                return;
            }

            string localPath;
            try
            {
                localPath = Save(task, bytes);
            }
            catch (Exception exception)
            {
                InvokeError(
                    onError,
                    RestApiError.Client(
                        "任务算例缓存失败：" +
                        exception.Message));
                return;
            }

            if (onSuccess != null)
                onSuccess(new ValidatedTaskCase(document, localPath));
        }

        private static string Save(
            VrTaskDetailDto task,
            byte[] bytes)
        {
            string fileName =
                MakeSafeFileName(task.caseDocument.fileName);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "task-case.json";

            string directory = Path.Combine(
                Application.persistentDataPath,
                "RemoteTaskCases",
                MakeSafeFileName(task.taskId),
                "v" + task.version);
            Directory.CreateDirectory(directory);

            string finalPath = Path.Combine(directory, fileName);
            string temporaryPath = finalPath + ".download";

            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            File.WriteAllBytes(temporaryPath, bytes);
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(temporaryPath, finalPath);
            return finalPath;
        }

        private static string ComputeMd5(byte[] bytes)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(bytes);
                StringBuilder builder =
                    new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                    builder.Append(hash[index].ToString("x2"));
                return builder.ToString();
            }
        }

        private static string NormalizeHash(string value)
        {
            return (value ?? string.Empty)
                .Replace("-", string.Empty)
                .Trim()
                .ToLowerInvariant();
        }

        private static string MakeSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result = value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int index = 0; index < invalid.Length; index++)
                result = result.Replace(invalid[index], '_');
            return result;
        }

        private static void InvokeError(
            Action<RestApiError> onError,
            RestApiError error)
        {
            if (onError != null)
                onError(error);
        }
    }
}

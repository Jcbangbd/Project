using System;
using System.Collections.Generic;

/// <summary>
/// 客户端用的消息类型 & 数据结构
/// </summary>
public enum MessageType : byte
{
    RegisterRequest = 1,
    RegisterResponse = 2,
    LoginRequest = 3,
    LoginResponse = 4,
    Heartbeat = 5,
    TrackActivityRequest = 6,
    TrackActivityResponse = 7,
    TeacherLogsRequest = 8,
    TeacherLogsResponse = 9,
    TeacherSummaryRequest = 10,
    TeacherSummaryResponse = 11,
    ChangePasswordRequest = 12,
    ChangePasswordResponse = 13
}


[Serializable]
public class RegisterRequest
{
    public string Username = string.Empty;
    public string Password = string.Empty;
    public string StudentNo = string.Empty;  // 新增
    public string Name = string.Empty;
}

[Serializable]
public class RegisterResponse
{
    public bool Success;
    public string Message = string.Empty;
    public int UserId;
}

[Serializable]
public class LoginRequest
{
    public string Username = string.Empty;
    public string Password = string.Empty;
}

[Serializable]
public class LoginResponse
{
    public bool Success;
    public int UserId;
    public string Username = string.Empty;
    public string Message = string.Empty;
}
[Serializable]
public class ChangePasswordRequest
{
    public string Username = string.Empty;
    public string OldPassword = string.Empty;
    public string NewPassword = string.Empty;
}

[Serializable]
public class ChangePasswordResponse
{
    public bool Success;
    public string Message = string.Empty;
}

/// <summary>
/// 单条日志记录 —— 这里已经包含 学号 + 姓名 + 账号
/// </summary>
[Serializable]
public class LogEntry
{
    public int LogId;

    public string StudentNo = string.Empty;
    public string Name = string.Empty;
    public string Username = string.Empty;

    public string ActivityType = string.Empty;
    public string Detail = string.Empty;
    public DateTime StartTime;
    public double DurationSeconds;
    public string Location = string.Empty;
}

[Serializable]
public class TeacherLogsRequest
{
    public int FilterUserId = 0;
}

[Serializable]
public class TeacherLogsResponse
{
    public bool Success;
    public string Message = string.Empty;
    public List<LogEntry> Logs = new List<LogEntry>();
}

[Serializable]
public class TrackActivityRequest
{
    public int UserId;
    public string ActivityType = string.Empty;
    public string Detail = string.Empty;
    public DateTime StartTime;
    public double DurationSeconds;
    public string Location = string.Empty;
}

[Serializable]
public class TrackActivityResponse
{
    public bool Success;
    public string Message = string.Empty;
}
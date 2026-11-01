using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [Header("服务器配置")]
    public string serverIP = "120.26.231.151";
    public int serverPort = 8080;

    [Header("连接状态")]
    [SerializeField] private bool _isConnected = false;
    [SerializeField] private bool _isConnecting = false;

    public bool IsConnected => _isConnected;
    public bool IsConnecting => _isConnecting;

    // 当前登录用户
    public int CurrentUserId { get; private set; } = 0;
    public string CurrentUsername { get; private set; } = string.Empty;

    public void SetCurrentUser(int userId, string username)
    {
        CurrentUserId = userId;
        CurrentUsername = username;
    }

    // 事件
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnConnectionError;
    public event Action<LoginResponse> OnLoginResponse;
    public event Action<RegisterResponse> OnRegisterResponse;

    private TcpClient _client;
    private NetworkStream _stream;
    private BinaryWriter _writer;
    private BinaryReader _reader;
    private CancellationTokenSource _cancellationTokenSource;
    public event Action<ChangePasswordResponse> OnChangePasswordResponse;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Debug.Log("网络管理器初始化完成");
    }

    void Start()
    {
        ConnectToServer();
    }

    void OnDestroy()
    {
        Disconnect();
    }

    void Update()
    {
        if (_isConnected && _stream != null && _stream.DataAvailable)
        {
            try
            {
                int messageLength = _reader.ReadInt32();
                MessageType messageType = (MessageType)_reader.ReadByte();
                byte[] data = _reader.ReadBytes(messageLength);

                ProcessServerMessage(messageType, data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"读取消息错误: {ex.Message}");
                Disconnect();
            }
        }
    }

    // 连接服务器
    public async void ConnectToServer()
    {
        if (_isConnected || _isConnecting)
        {
            Debug.Log("已在连接或已连接");
            return;
        }

        _isConnecting = true;

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _client = new TcpClient();

            Debug.Log($"正在连接服务器 {serverIP}:{serverPort}...");

            var connectTask = _client.ConnectAsync(serverIP, serverPort);
            var timeoutTask = Task.Delay(10000);
            var completed = await Task.WhenAny(connectTask, timeoutTask);

            if (completed == timeoutTask)
                throw new TimeoutException("连接超时");

            await connectTask;

            _stream = _client.GetStream();
            _writer = new BinaryWriter(_stream);
            _reader = new BinaryReader(_stream);
            _isConnected = true;
            _isConnecting = false;

            Debug.Log("连接服务器成功！");
            OnConnected?.Invoke();
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _isConnecting = false;

            string errorMsg = $"连接服务器失败: {ex.Message}";
            Debug.LogError(errorMsg);
            OnConnectionError?.Invoke(errorMsg);
        }
    }

    // 处理服务器消息
    private void ProcessServerMessage(MessageType type, byte[] data)
    {
        try
        {
            switch (type)
            {
                case MessageType.LoginResponse:
                    var loginResp = MessageSerializer.Deserialize<LoginResponse>(data);
                    OnLoginResponse?.Invoke(loginResp);
                    break;

                case MessageType.RegisterResponse:
                    var regResp = MessageSerializer.Deserialize<RegisterResponse>(data);
                    OnRegisterResponse?.Invoke(regResp);
                    break;

                case MessageType.Heartbeat:
                    Debug.Log("收到心跳包");
                    break;

                case MessageType.TrackActivityResponse:
                    var actResp = MessageSerializer.Deserialize<TrackActivityResponse>(data);
                    Debug.Log($"学习记录返回: {actResp.Success} - {actResp.Message}");
                    break;

                case MessageType.ChangePasswordResponse:
                    var cpResp = MessageSerializer.Deserialize<ChangePasswordResponse>(data);
                    OnChangePasswordResponse?.Invoke(cpResp);
                    break;

                default:
                    Debug.LogWarning($"未知消息类型: {type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"处理消息错误: {ex.Message}");
        }
    }

    // 登录
    public void SendLoginRequest(string username, string password)
    {
        if (!_isConnected)
        {
            Debug.LogWarning("未连接到服务器，无法发送登录请求");
            OnConnectionError?.Invoke("未连接到服务器");
            return;
        }

        try
        {
            var request = new LoginRequest
            {
                Username = username,
                Password = password
            };

            byte[] requestData = MessageSerializer.Serialize(request);
            byte[] networkMessage = MessageSerializer.CreateMessage(MessageType.LoginRequest, requestData);

            SendRawMessage(networkMessage);
            Debug.Log($"发送登录请求: {username}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"发送登录请求失败: {ex.Message}");
        }
    }
    public void SendChangePasswordRequest(string username, string oldPassword, string newPassword)
    {
        if (!_isConnected)
        {
            Debug.LogWarning("未连接到服务器，无法发送修改密码请求");
            OnConnectionError?.Invoke("未连接到服务器");
            return;
        }

        try
        {
            var request = new ChangePasswordRequest
            {
                Username = username,
                OldPassword = oldPassword,
                NewPassword = newPassword
            };

            byte[] requestData = MessageSerializer.Serialize(request);
            byte[] networkMessage = MessageSerializer.CreateMessage(MessageType.ChangePasswordRequest, requestData);

            SendRawMessage(networkMessage);
            Debug.Log($"发送修改密码请求: {username}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"发送修改密码请求失败: {ex.Message}");
        }
    }

    // 注册（现在不一定要用）
    public void SendRegisterRequest(string username, string password)
    {
        if (!_isConnected)
        {
            Debug.LogWarning("未连接到服务器，无法发送注册请求");
            OnConnectionError?.Invoke("未连接到服务器");
            return;
        }

        try
        {
            var request = new RegisterRequest
            {
                Username = username,
                Password = password
            };

            byte[] requestData = MessageSerializer.Serialize(request);
            byte[] networkMessage = MessageSerializer.CreateMessage(MessageType.RegisterRequest, requestData);

            SendRawMessage(networkMessage);
            Debug.Log($"发送注册请求: {username}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"发送注册请求失败: {ex.Message}");
        }
    }

    // 发送流程学习记录
    public void SendActivityLog(string flowName, string detail, DateTime startTime, double durationSeconds, string location)
    {
        if (!_isConnected || CurrentUserId <= 0)
        {
            Debug.LogWarning("未登录或未连接服务器，暂不发送学习记录");
            return;
        }

        try
        {
            var request = new TrackActivityRequest
            {
                UserId = CurrentUserId,
                ActivityType = flowName,
                Detail = detail,
                StartTime = startTime,
                DurationSeconds = durationSeconds,
                Location = location
            };

            byte[] requestData = MessageSerializer.Serialize(request);
            byte[] msg = MessageSerializer.CreateMessage(MessageType.TrackActivityRequest, requestData);
            SendRawMessage(msg);

            Debug.Log($"发送学习记录: {flowName}, 时长 {durationSeconds:F1} 秒");
        }
        catch (Exception ex)
        {
            Debug.LogError($"发送学习记录失败: {ex.Message}");
        }
    }

    // 底层发送
    private void SendRawMessage(byte[] message)
    {
        if (!_isConnected || _writer == null) return;

        try
        {
            _writer.Write(message);
            _writer.Flush();
        }
        catch (Exception ex)
        {
            Debug.LogError($"发送消息失败: {ex.Message}");
            Disconnect();
        }
    }


    public void Disconnect()
    {
        _isConnected = false;
        _isConnecting = false;
        _cancellationTokenSource?.Cancel();

        _writer?.Close();
        _reader?.Close();
        _stream?.Close();
        _client?.Close();

        _writer = null;
        _reader = null;
        _stream = null;
        _client = null;

        Debug.Log("与服务器断开连接");
        OnDisconnected?.Invoke();
    }

    public void Reconnect()
    {
        Disconnect();
        ConnectToServer();
    }
}
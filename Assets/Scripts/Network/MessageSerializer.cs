using System.IO;
using System.Text;
using UnityEngine;

public static class MessageSerializer
{
    public static byte[] Serialize<T>(T obj)
    {
        string json = JsonUtility.ToJson(obj);
        return Encoding.UTF8.GetBytes(json);
    }

    public static T Deserialize<T>(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);
        return JsonUtility.FromJson<T>(json);
    }

    public static byte[] CreateMessage(MessageType type, byte[] data)
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            writer.Write(data.Length);  // 消息长度 (4字节)
            writer.Write((byte)type);   // 消息类型 (1字节)
            writer.Write(data);         // 消息内容 (N字节)
            return ms.ToArray();
        }
    }

    public static (MessageType type, byte[] data) ParseMessage(byte[] rawData)
    {
        using (var ms = new MemoryStream(rawData))
        using (var reader = new BinaryReader(ms))
        {
            int length = reader.ReadInt32();
            MessageType type = (MessageType)reader.ReadByte();
            byte[] data = reader.ReadBytes(length);
            return (type, data);
        }
    }
}
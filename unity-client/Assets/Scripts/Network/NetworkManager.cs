using UnityEngine;
using System;
using System.Net.Sockets;
using Google.Protobuf;
using MyPokemon.Protocol;
using MyPokemon.Utils;
using System.Collections.Generic;

/// <summary>
/// 网络管理器：负责处理与服务器的TCP连接和消息收发
/// </summary>
public class NetworkManager : MonoBehaviour
{
    // TCP客户端连接对象
    private TcpClient client;
    // 网络数据流，用于读写数据
    private NetworkStream stream;
    // 用于读取消息长度的缓冲区（varint32格式，最多需要5字节）
    private byte[] varintBuffer = new byte[5];
    // 用于读取消息内容的缓冲区（最大16KB）
    private byte[] messageBuffer = new byte[1024 * 16];
    
    // 单例模式，保证整个游戏中只有一个网络管理器实例
    public static NetworkManager Instance { get; private set; }
    
    // 当收到位置广播消息时触发的事件
    public event Action<PositionBroadcast> OnPositionBroadcast;

    // 添加消息队列
    private readonly Queue<PositionBroadcast> messageQueue = new Queue<PositionBroadcast>();
    private readonly object queueLock = new object();

    /// <summary>
    /// Unity启动时的初始化函数
    /// 实现单例模式，确保场景切换时不会销毁网络连接
    /// </summary>
    private void Awake()
    {
        // 单例模式的标准实现
        if (Instance == null)
        {
            Instance = this;
            // 确保切换场景时不会销毁这个对象
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 如果已经存在实例，销毁新创建的对象
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 连接到游戏服务器
    /// </summary>
    public void Connect(string host = "localhost", int port = 5000)
    {
        try
        {
            client = new TcpClient();
            GameLogger.LogNetwork($"正在连接服务器 {host}:{port}...");
            client.Connect(host, port);
            stream = client.GetStream();
            BeginReadVarint();
            GameLogger.LogNetwork("成功连接到服务器!");
        }
        catch (Exception e)
        {
            GameLogger.LogError("连接服务器失败", e);
        }
    }

    /// <summary>
    /// 开始异步读取varint32格式的消息长度
    /// </summary>
    private void BeginReadVarint()
    {
        // 异步读取第一个字节
        stream.BeginRead(varintBuffer, 0, 1, OnVarintByte, 0);
    }

    /// <summary>
    /// 处理varint32字节的读取回调
    /// 实现了与服务器端DotNetty相同的消息长度解码逻辑
    /// </summary>
    private void OnVarintByte(IAsyncResult ar)
    {
        try
        {
            // 当前读取的字节位置
            int position = (int)ar.AsyncState;
            int bytesRead = stream.EndRead(ar);
            
            if (bytesRead > 0)
            {
                // 解析varint32的一个字节（7位数据）
                int value = varintBuffer[position] & 0x7F;
                // 检查最高位是否为1（表示还有更多字节）
                if ((varintBuffer[position] & 0x80) != 0)
                {
                    // 防止varint32超过5字节
                    if (position >= 4)
                    {
                        Debug.LogError("Invalid varint32");
                        BeginReadVarint();
                        return;
                    }
                    // 继续读取下一个字节
                    stream.BeginRead(varintBuffer, position + 1, 1, OnVarintByte, position + 1);
                    return;
                }

                // 计算完整的消息长度
                int messageLength = 0;
                for (int i = 0; i <= position; i++)
                {
                    messageLength |= (varintBuffer[i] & 0x7F) << (7 * i);
                }

                // 开始读取消息内容
                if (messageLength > 0 && messageLength < messageBuffer.Length)
                {
                    stream.BeginRead(messageBuffer, 0, messageLength, OnMessageReceived, messageLength);
                    return;
                }
            }
            
            // 继续读取下一条消息
            BeginReadVarint();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error reading varint: {e.Message}");
        }
    }

    /// <summary>
    /// 处理消息内容的读取回调
    /// </summary>
    private void OnMessageReceived(IAsyncResult ar)
    {
        try
        {
            int messageLength = (int)ar.AsyncState;
            int bytesRead = stream.EndRead(ar);
            
            if (bytesRead == messageLength)
            {
                var message = PositionBroadcast.Parser.ParseFrom(messageBuffer, 0, messageLength);
                lock (queueLock)
                {
                    messageQueue.Enqueue(message);
                }
            }
            
            BeginReadVarint();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // 在主线程中处理消息
    private void Update()
    {
        lock (queueLock)
        {
            while (messageQueue.Count > 0)
            {
                var message = messageQueue.Dequeue();
                GameLogger.LogNetwork($"收到位置广播 - 玩家数量: {message.Positions.Count}");
                foreach (var pos in message.Positions)
                {
                    GameLogger.LogNetwork($"玩家位置 - ID: {pos.PlayerId}, 位置: ({pos.X:F2}, {pos.Y:F2}), 朝向: {pos.Direction}");
                }
                OnPositionBroadcast?.Invoke(message);
            }
        }
    }

    /// <summary>
    /// 将整数编码为varint32格式
    /// 实现了与服务器端DotNetty相同的长度编码逻辑
    /// </summary>
    private byte[] EncodeVarint32(int value)
    {
        // 不支持负数
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException("value", "Cannot encode negative values.");
        }

        byte[] result = new byte[5];
        int position = 0;

        while (true)
        {
            // 检查是否是最后一个字节
            if ((value & ~0x7F) == 0)
            {
                // 最后一个字节，最高位设为0
                result[position++] = (byte)value;
                break;
            }

            // 非最后字节，最高位设为1，表示还有后续字节
            result[position++] = (byte)((value & 0x7F) | 0x80);
            value >>= 7;
        }

        // 裁剪到实际使用的长度
        byte[] trimmed = new byte[position];
        Array.Copy(result, trimmed, position);
        return trimmed;
    }

    /// <summary>
    /// 发送位置更新请求到服务器
    /// </summary>
    public void SendPosition(float x, float y, int direction)
    {
        try
        {
            var request = new PositionRequest(x, y, direction);
            GameLogger.LogNetwork($"NetworkManager正在发送位置: ({x:F2}, {y:F2}) 朝向: {direction}");
            byte[] messageData = request.ToByteArray();
            byte[] lengthVarint = EncodeVarint32(messageData.Length);
            
            stream.Write(lengthVarint, 0, lengthVarint.Length);
            stream.Write(messageData, 0, messageData.Length);
            GameLogger.LogNetwork("位置发送完成");
        }
        catch (Exception e)
        {
            GameLogger.LogError("发送位置失败", e);
        }
    }

    /// <summary>
    /// Unity销毁对象时调用，清理网络资源
    /// </summary>
    private void OnDestroy()
    {
        // 关闭网络连接并释放资源
        client?.Close();
        stream?.Dispose();
    }
} 
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Deb = UnityEngine.Debug;

public class TCPClient
{
    private static TcpClient client;

    public TCPClient()
    {
        client = new TcpClient();
        client.NoDelay = true;
        client.SendBufferSize = 0;
    }

    /// <summary>
    /// 非同期で接続要求を出す
    /// </summary>
    /// <param name="remoteIp">リモートIPアドレス</param>
    public void ConnectAsync(IPAddress remoteIp)
    {
        Thread t = new Thread(new ThreadStart(() =>
        {
            client.Connect(remoteIp, 8888);
        }));
        t.IsBackground = true;
        t.Start();
    }

    /// <summary>
    /// サーバに接続されているかどうかを返す
    /// </summary>
    public bool isConnected { get { return client == null ? false : client.Connected; } }

    /// <summary>
    /// ストリームを返す
    /// </summary>
    /// <returns></returns>
    public NetworkStream GetStream()
    {
        return client.Connected ? client.GetStream() : null;
    }
}
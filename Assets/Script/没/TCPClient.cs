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

public class TCPClient : ITCPBase
{
    private static TcpClient client;
    private IPAddress addr;
    private int port;

    public TCPClient(IPAddress remoteAddr, int port)
    {
        client = new TcpClient();
        client.NoDelay = true;
        client.SendBufferSize = 0;
        addr = remoteAddr;
        this.port = port;
    }

    /// <summary>
    /// 非同期で接続要求を出す
    /// </summary>
    /// <param name="remoteIp">リモートIPアドレス</param>
    public void ConnectAsync()
    {
        Thread t = new Thread(new ThreadStart(() =>
        {
            client.Connect(addr, port);
        }));
        t.IsBackground = true;
        t.Start();
        Deb.Log("Client : 接続を開始しました");
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

    public void Close()
    {
        client.Close();
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

using Deb = UnityEngine.Debug;

/// <summary>
/// 1対1の通信に対応する
/// </summary>
public class TCPServer
{
    private TcpListener listner;
    private TcpClient client;

    /// <summary>
    /// クライアント接続を受け入れているか
    /// </summary>
    public bool Accepted { get; private set; }

    public TCPServer()
    {
        listner = new TcpListener(IPAddress.Any, 8888);
        listner.Start();
        UnityEngine.Debug.Log("サーバを開始しました");
    }

    /// <summary>
    /// 一台から接続を受け付けるまで
    /// 非同期で接続を待ち受ける
    /// 既に接続されているクライアントが存在する場合は
    /// 何もしない
    /// </summary>
    public void AcceptConnectAsync()
    {
        if(client != null)
            return;
        Deb.Log("接続を開始します");
        Thread t = new Thread(new ThreadStart(() =>
        {
            client = listner.AcceptTcpClient();
            Accepted = true;
            Deb.Log("接続が完了しました");
        }));
        t.IsBackground = true;
        t.Start();
    }

    /// <summary>
    /// ストリームを得る
    /// </summary>
    public NetworkStream GetStream()
    {
        if(client == null)
            return null;
        return client.GetStream();
    }

    /// <summary>
    /// クライアントの接続を解除する
    /// </summary>
    public void CloseClient()
    {
        if(client != null)
            client.Close();
    }

    /// <summary>
    /// サーバの接続を切断する
    /// </summary>
    public void CloseServer()
    {
        if(listner != null)
            listner.Stop();
    }
}
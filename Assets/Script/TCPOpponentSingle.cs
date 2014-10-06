using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// 1対1の通信機能を提供する
/// TCPを用いる
/// </summary>
public class TCPOpponentSingle
{
    public readonly bool isServer;
    private Socket sock;//通信用ソケット
    private Socket acc_sock;//サーバの待ち受け用ソケット
    private PacketQueue sendQueue;//送信用バッファ
    private PacketQueue recvQueue;//受信用バッファ
    private volatile bool isRunningWork;//非同期送受信が行われているかどうか
    private readonly int PACKET_SIZE;//送受信に用いるパケット単体のサイズ
    private byte[] packet;//送受信で一時退避に用いる(小さすぎてパケット容量が超過すると例外発生。大きすぎると容量無駄)

    /// <summary>
    /// 通信相手に接続できていればtrueを返す
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="isServer">サーバとして使用するか</param>
    /// <param name="packetSize">データ送信に用いるパケットサイズ</param>
    public TCPOpponentSingle(bool isServer, int packetSize = 1024)
    {
        this.isServer = isServer;
        PACKET_SIZE = packetSize;
        packet = new byte[PACKET_SIZE];
        sendQueue = new PacketQueue();
        recvQueue = new PacketQueue();
        if(isServer)
            acc_sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IPv4);
        else
        {
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IPv4);
            sock.NoDelay = true;
            sock.SendBufferSize = 0;
        }
    }

    /// <summary>
    /// サーバの待ち受けを開始する
    /// クライアントの接続が完了するとIsConnectedプロパティがtrueを返す
    /// </summary>
    /// <param name="port">アプリケーションで使用する使用するポート番号</param>
    /// <returns>クライアント接続待ち受けスレッド起動の成否</returns>
    public bool StartServer(int port)
    {
        if(!isServer)
            return false;
        try
        {
            acc_sock.Bind(new IPEndPoint(IPAddress.Any, port));
            acc_sock.Listen(1);
        }
        catch(Exception e)
        {
            Debug.Log(e.Message);
            return false;
        }

        return AcceptAsync();
    }

    /// <summary>
    /// 別スレッドで接続待ちする
    /// 接続が完了したらIsConnectedプロパティがtrueを返す
    /// </summary>
    /// <returns>スレッド起動の成否</returns>
    private bool AcceptAsync()
    {
        if(!isServer)
            return false;

        //ソケットが待ち受け許可可能状態であれば
        if(acc_sock.Poll(0, SelectMode.SelectRead))
        {
            try
            {
                //スレッドを起動し、接続待ちさせる
                Thread thread = new Thread(new ThreadStart(() =>
                {
                    sock = acc_sock.Accept();
                    IsConnected = true;
                }));
                thread.Start();
            }
            catch(Exception e)
            {
                Debug.Log(e.Message);
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// クライアントからサーバへの接続を非同期で試行する
    /// 接続が完了した場合IsConnectedプロパティはtrueを返す
    /// </summary>
    /// <param name="remoteEP">リモートエンドポイント</param>
    /// <returns>コネクション用スレッド起動の成否</returns>
    public bool ConnectAsync(IPEndPoint remoteEP)
    {
        if(isServer)
            return false;
        try
        {
            Thread thread = new Thread(new ThreadStart(() =>
            {
                sock.Connect(remoteEP);
                IsConnected = true;
            }));
            thread.Start();
        }
        catch(Exception e)
        {
            Debug.Log(e.Message);
            return false;
        }
        return true;
    }

    /// <summary>
    /// 別スレッドで送受信処理を実行する
    /// 送信バッファに格納されたパケットを送信し
    /// パケットを受信したら受信バッファに格納する
    /// </summary>
    public void RunWorkAsync()
    {
        if(IsConnected)
            isRunningWork = true;

        Thread thread = new Thread(new ThreadStart(() =>
        {
            while(isRunningWork)
            {
                //送信処理
                SendQueueDispach();
                //受信処理
                ReceiveQueueDispach();
            }
        }));
    }

    /// <summary>
    /// 送信用キューに溜まっているパケットを送信する
    /// </summary>
    private void SendQueueDispach()
    {
        if(sock.Poll(0, SelectMode.SelectWrite))
        {
            int sendSize;//送信するパケットのサイズ

            //送信バッファからパケットを取り出す
            while((sendSize = sendQueue.Dequeue(ref packet)) > 0)
            {
                Array.Clear(packet, 0, packet.Length);
                //パケットの取り出しに成功したらそのパケットを送信する
                sock.Send(packet, sendSize, SocketFlags.None);
            }
        }
    }

    /// <summary>
    /// 送られてきたパケットを受信用キューに格納する
    /// 2つ以上のパケットが送られてきた時にどう動作するかわからん（受け取るのか、たまったままになるか）
    /// このメソッドは要注意
    /// </summary>
    private void ReceiveQueueDispach()
    {
        //受信可能データが存在したら
        while(sock.Poll(0, SelectMode.SelectRead))
        {
            Array.Clear(packet, 0, packet.Length);
            //ソケットからデータを受信
            int recvSize = sock.Receive(packet, packet.Length, SocketFlags.None);
            //受信したデータを受信用キューに格納
            if(recvSize > 0)
                recvQueue.Enqueue(packet.Take(recvSize).ToArray<byte>());
            else
                break;
        }
    }

    /// <summary>
    /// 送信データをパケットとしてキューに格納する
    /// </summary>
    /// <param name="data">データ</param>
    /// <returns>格納したデータのサイズ</returns>
    /// <exception cref="ArgumentException">データ長が0またはパケットサイズを超える場合</exception>
    public int Send(byte[] data)
    {
        if(data.Length > PACKET_SIZE || data.Length == 0)
            throw new ArgumentException(this.ToString() + " : 送信データのサイズが不正です");
        return sendQueue.Enqueue(data);
    }

    /// <summary>
    /// 受信用キューからパケットデータを取得する
    /// 取得出来なかった場合はnullを返す
    /// </summary>
    /// <returns>パケットデータ</returns>
    public byte[] Receive()
    {
        byte[] buf;
        if(recvQueue.PeekSize() > 0)
        {
            buf = new byte[recvQueue.PeekSize()];
            recvQueue.Dequeue(ref buf);
            return buf;
        }
        else
            return null;
    }
}
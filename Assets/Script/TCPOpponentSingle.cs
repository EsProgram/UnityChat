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
    /// <summary>
    /// 通信処理で例外が発生した場合に呼ばれるコールバックメソッド
    /// 通信相手の切断などで接続できなくなった場合などに呼ばれる
    /// </summary>
    public event Action OnErrorCommunicateEvent;

    private Socket sock;//通信用ソケット
    private Socket acc_sock;//サーバの待ち受け用ソケット
    private PacketQueue sendQueue;//送信用バッファ
    private PacketQueue recvQueue;//受信用バッファ
    private Thread runWorkThread;//送受信ディスパッチに用いるスレッド
    private volatile bool isRunningWork;//非同期送受信が行われているかどうか
    private readonly int PACKET_SIZE;//送受信に用いるパケット単体のサイズ
    private byte[] packet;//送受信で一時退避に用いる(小さすぎてパケット容量が超過すると例外発生。大きすぎると容量無駄)

    /// <summary>
    /// 通信相手に接続できていればtrueを返す
    /// </summary>
    public bool IsConnected
    {
        get
        {
            return sock == null ? false : sock.Connected;
        }
    }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="isServer">サーバとして使用するか</param>
    /// <param name="packetSize">
    /// データ送信に用いるパケットサイズ
    /// このパケットサイズ以上のデータを送受信しようとすると例外が投げられる
    /// </param>
    public TCPOpponentSingle(bool isServer, int packetSize = 1024)
    {
        this.isServer = isServer;
        PACKET_SIZE = packetSize;
        packet = new byte[PACKET_SIZE];
        sendQueue = new PacketQueue();
        recvQueue = new PacketQueue();
        if(isServer)
            acc_sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        else
        {
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
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
            acc_sock.Listen(10);
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
        try
        {
            //スレッドを起動し、接続待ちさせる
            Thread thread = new Thread(new ThreadStart(() =>
            {
                sock = acc_sock.Accept();
                //IsConnected = true;
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
    /// 接続完了後に呼び出し可能
    /// 別スレッドで送受信処理を実行する
    /// 送信バッファに格納されたパケットを送信し
    /// パケットを受信したら受信バッファに格納する
    /// </summary>
    /// <returns>送受信スレッド呼び出し成否</returns>
    public bool RunWorkAsync()
    {
        if(IsConnected)
            isRunningWork = true;
        else
            return false;

        runWorkThread = new Thread(new ThreadStart(() =>
        {
            try
            {
                while(isRunningWork)
                {
                    //送信処理
                    SendQueueDispach();
                    //受信処理
                    ReceiveQueueDispach();
                    Thread.Sleep(10);
                }
            }
            catch(Exception e)
            {
                Debug.Log(e.Message);
                DisConnect();
                if(OnErrorCommunicateEvent != null)
                    OnErrorCommunicateEvent();
            }
        }));
        runWorkThread.Start();
        return true;
    }

    /// <summary>
    /// 送信用キューに溜まっているパケットを送信する
    /// </summary>
    private void SendQueueDispach()
    {
        if(!IsConnected)
            throw new InvalidOperationException("接続が完了していないためSendQueueDispachを呼び出せません");
        if(sock.Poll(0, SelectMode.SelectWrite))
        {
            int sendSize;//送信するパケットのサイズ

            //送信バッファからパケットを取り出す
            while((sendSize = sendQueue.Dequeue(ref packet)) > 0)
            {
                //パケットの取り出しに成功したらそのパケットを送信する
                sock.Send(packet, sendSize, SocketFlags.None);
                Array.Clear(packet, 0, packet.Length);
            }
        }
    }

    /// <summary>
    /// 送られてきたパケットを受信用キューに格納する
    /// </summary>
    private void ReceiveQueueDispach()
    {
        if(!IsConnected)
            throw new InvalidOperationException("接続が完了していないためReceiveQueueDispatchを呼び出せません");
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
    /// 接続完了後に呼び出し可能
    /// 送信データをパケットとしてキューに格納する
    /// </summary>
    /// <param name="data">データ</param>
    /// <returns>格納したデータのサイズ</returns>
    /// <exception cref="ArgumentException">データ長が0またはパケットサイズを超える場合</exception>
    public int Send(byte[] data)
    {
        if(!IsConnected)
            throw new InvalidOperationException("接続が完了していないためSendを呼び出せません");
        //送信データがパケット長を超えるか送信データが空であればエラー
        if(data.Length > PACKET_SIZE || data.Length == 0)
            throw new ArgumentException(this.ToString() + " : 送信データのサイズが不正です");
        return sendQueue.Enqueue(data);
    }

    /// <summary>
    /// 接続完了後に呼び出し可能
    /// 受信用キューからパケットデータを取得する
    /// 取得出来なかった場合は-1を返す
    /// </summary>
    /// <returns>パケットデータサイズ</returns>
    public int Receive(ref byte[] data)
    {
        if(!IsConnected)
            throw new InvalidOperationException("接続が完了していないためReceiveを呼び出せません");
        //キューから取り出すパケットサイズが0より上なら
        if(recvQueue.PeekSize() > 0)
            return recvQueue.Dequeue(ref data);
        else
            return -1;
    }

    /// <summary>
    /// 接続を解除する
    /// 接続解除したソケットは再利用可能
    /// </summary>
    public void DisConnect()
    {
        if(isRunningWork)
        {
            isRunningWork = false;
            runWorkThread.Join();
        }
        if(sock != null)
            sock.Disconnect(true);
    }
}
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
    /// サーバソケット生成エラー発生時に呼び出される
    /// </summary>
    public event Action<Exception> OnStartServerErrorEvent = delegate { };
    /// <summary>
    /// サーバの待ち受け処理エラー発生時に呼び出される
    /// </summary>
    public event Action<Exception> OnAcceptErrorEvent = delegate { };
    /// <summary>
    /// クライアント接続処理エラー発生時に呼び出される
    /// </summary>
    public event Action<Exception> OnConnectErrorEvent = delegate { };
    /// <summary>
    /// 送受信スレッドでのエラー発生時に呼び出される
    /// Send/Receiveメソッドでのエラー復旧時などに用いる
    /// </summary>
    public event Action<Exception> OnRunWorkErrorEvent = delegate { };

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
    /// データ送信に用いるパケットの最大長
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
    /// コンストラクタ
    /// </summary>
    /// <param name="isServer">サーバとして使用するか</param>
    /// <param name="OnErr_StartServer">サーバソケット生成エラー時に呼び出されるコールバック</param>
    /// <param name="OnErr_Accept">サーバ待ち受け処理エラー発生時に呼び出されるコールバック</param>
    /// <param name="OnErr_Connect">クライアント接続処理エラー発生時に呼び出されるコールバック</param>
    /// <param name="OnErr_RunWork">送受信処理エラー発生時に呼び出されるコールバック</param>
    /// <param name="packetSize">
    /// データ送信に用いるパケットの最大長
    /// このパケットサイズ以上のデータを送受信しようとすると例外が投げられる
    /// </param>
    private TCPOpponentSingle(bool isServer,
                             Action<Exception> OnErr_StartServer,
                             Action<Exception> OnErr_Accept,
                             Action<Exception> OnErr_Connect,
                             Action<Exception> OnErr_RunWork,
                             int packetSize = 1024)
        : this(isServer, packetSize)
    {
        OnStartServerErrorEvent += OnErr_StartServer;
        OnAcceptErrorEvent += OnErr_Accept;
        OnConnectErrorEvent += OnErr_Connect;
        OnRunWorkErrorEvent += OnErr_RunWork;
    }

    /// <summary>
    /// サーバ用コンストラクタ
    /// </summary>
    /// <param name="OnErr_StartServer">サーバソケット生成エラー時に呼び出されるコールバック</param>
    /// <param name="OnErr_Accept">サーバ待ち受け処理エラー発生時に呼び出されるコールバック</param>
    /// <param name="OnErr_RunWork">送受信処理エラー発生時に呼び出されるコールバック</param>
    /// <param name="packetSize">
    /// データ送信に用いるパケットの最大長
    /// このパケットサイズ以上のデータを送受信しようとすると例外が投げられる
    /// </param>
    public TCPOpponentSingle(Action<Exception> OnErr_StartServer,
                             Action<Exception> OnErr_Accept,
                             Action<Exception> OnErr_RunWork,
                             int packetSize = 1024)
        : this(true, OnErr_StartServer, OnErr_Accept, null, OnErr_RunWork, packetSize) { }

    /// <summary>
    /// クライアント用コンストラクタ
    /// </summary>
    /// <param name="OnErr_Connect">クライアント接続処理エラー発生時に呼び出されるコールバック</param>
    /// <param name="OnErr_RunWork">送受信処理エラー発生時に呼び出されるコールバック</param>
    /// <param name="packetSize">
    /// データ送信に用いるパケットの最大長
    /// このパケットサイズ以上のデータを送受信しようとすると例外が投げられる
    /// </param>
    public TCPOpponentSingle(Action<Exception> OnErr_Connect,
                             Action<Exception> OnErr_RunWork,
                             int packetSize = 1024)
        : this(false, null, null, OnErr_Connect, OnErr_RunWork, packetSize) { }

    /// <summary>
    /// サーバの待ち受けを開始する
    /// クライアントの接続が完了するとIsConnectedプロパティがtrueを返す
    /// </summary>
    /// <param name="port">アプリケーションで使用する使用するポート番号</param>
    public void StartServer(int port)
    {
        if(!isServer)
            return;
        try
        {
            acc_sock.Bind(new IPEndPoint(IPAddress.Any, port));
            acc_sock.Listen(0);
        }
        catch(Exception e)
        {
            OnStartServerErrorEvent(e);
        }

        AcceptAsync();
    }

    /// <summary>
    /// 別スレッドで接続待ちする
    /// 接続が完了したらIsConnectedプロパティがtrueを返す
    /// 接続待ちをキャンセルするには
    /// </summary>
    private void AcceptAsync()
    {
        if(!isServer)
            return;

        //ソケットが待ち受け許可可能状態であれば
        try
        {
            //スレッドを起動し、接続待ちさせる
            Thread wait_accept = new Thread(new ThreadStart(() =>
            {
                sock = acc_sock.Accept();
                Debug.Log("Server : 接続に成功しました");
            }));
            wait_accept.Start();
        }
        catch(Exception e)
        {
            OnAcceptErrorEvent(e);
        }
        return;
    }

    /// <summary>
    /// クライアントからサーバへの接続を非同期で試行する
    /// 接続が完了した場合IsConnectedプロパティはtrueを返す
    /// </summary>
    /// <param name="remoteEP">リモートエンドポイント</param>
    public void ConnectAsync(IPEndPoint remoteEP)
    {
        if(isServer)
            return;
        try
        {
            Thread wait_connect = new Thread(new ThreadStart(() =>
            {
                sock.Connect(remoteEP);
                Debug.Log("Client : 接続に成功しました");
            }));
            wait_connect.Start();
        }
        catch(Exception e)
        {
            OnConnectErrorEvent(e);
        }
    }

    /// <summary>
    /// 接続完了後に呼び出し可能
    /// 別スレッドで送受信処理を実行する
    /// 送信バッファに格納されたパケットを送信し
    /// パケットを受信したら受信バッファに格納する
    /// この操作をIsRunningWorkがtrueの間繰り返す
    /// </summary>
    /// <param name="millisecondsTimeout">スレッドの送受信繰り返しの休憩時間</param>
    /// <exception cref="InvalidOperationException">接続が確立されていない場合(通信相手が遮断されているかを判断できるのはSendメソッド実行時時のみ)</exception>
    public void RunWorkAsync(int millisecondsTimeout = 10)
    {
        if(!IsConnected)
            throw new InvalidOperationException("ReceiveQueueDispach : 接続が完了していないためReceiveQueueDispatchを呼び出せません");
        if(IsConnected)
            isRunningWork = true;
        else
            return;

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
                    Thread.Sleep(millisecondsTimeout);
                }
            }
            catch(Exception e)
            {
                OnRunWorkErrorEvent(e);
            }
        }));
        runWorkThread.Start();
    }

    /// <summary>
    /// 送信用キューに溜まっているパケットを送信する
    /// 接続されていない場合は実行されない
    /// </summary>
    private void SendQueueDispach()
    {
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
    /// 接続されていない場合は実行されない
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
    /// 接続完了後に呼び出し可能
    /// 送信データをパケットとしてキューに格納する
    /// 送信したデータのサイズを返す
    /// </summary>
    /// <param name="data">データ</param>
    /// <returns>格納したデータのサイズ</returns>
    /// <exception cref="ArgumentException">データ長が0またはパケットサイズを超える場合</exception>
    /// <exception cref="InvalidOperationException">接続が確立されていない場合</exception>
    public int Send(byte[] data)
    {
        if(!IsConnected)
            throw new InvalidOperationException("Send : 接続が完了していないためSendを呼び出せません");
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
    /// <exception cref="InvalidOperationException">接続が確立されていない場合</exception>
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
    /// 接続を解除し通信用ソケットをクローズする
    /// </summary>
    public void Close()
    {
        if(isRunningWork)
        {
            isRunningWork = false;
            runWorkThread.Join();
        }
        if(sock != null)
        {
            if(sock.Connected)
                sock.Shutdown(SocketShutdown.Both);
            sock.Close();
        }
        if(acc_sock != null)
        {
            if(acc_sock.Connected)
                acc_sock.Shutdown(SocketShutdown.Both);
            acc_sock.Close();
        }
    }
}
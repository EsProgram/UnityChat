using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;

/// <summary>
/// TCPOpponentSingleテスト使用クラス
/// Int32型の大きさを１つのパケットとして、Int32型の範囲内の乱数を送受信する。
/// </summary>
public class TestUseTcp : MonoBehaviour
{
    private const int PACKET_SIZE = sizeof(int);
    private TCPOpponentSingle trans;
    private SceneState scene_state = SceneState.SelectRoll;
    private byte[] recvMessage = new byte[PACKET_SIZE];
    private float timer;

    private enum SceneState
    {
        SelectRoll,
        WaitConnect,
        SendAndReceive,
    }

    private void OnGUI()
    {
        switch(scene_state)
        {
            case SceneState.SelectRoll:
                if(GUI.Button(new Rect(1, 1, 150, 40), "サーバになる"))
                {
                    trans = new TCPOpponentSingle(OnErrStartServer, OnErrAccept, OnErrRunWork, PACKET_SIZE);

                    trans.StartServer(54321);
                    scene_state = SceneState.WaitConnect;
                    timer = Time.time;
                }
                if(GUI.Button(new Rect(1, 50, 150, 40), "クライアントになる"))
                {
                    trans = new TCPOpponentSingle(OnErrConnect, OnErrRunWork, PACKET_SIZE);

                    trans.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 54321));
                    scene_state = SceneState.WaitConnect;
                    timer = Time.time;
                }

                break;

            case SceneState.WaitConnect:
                //接続待ち状態の表示
                GUI.Label(new Rect(1, 1, 150, 40), "接続待ちしています");
                if(trans.IsConnected)
                {
                    trans.RunWorkAsync(millisecondsTimeout: 100);
                    scene_state = SceneState.SendAndReceive;
                }
                //クライアントがしばらくの時間接続できなかった場合の復帰処理を書く
                if(!trans.isServer && !trans.IsConnected && Time.time - timer > 2f)
                {
                    trans.Close();
                    scene_state = SceneState.SelectRoll;
                }
                //サーバがAccept待ちをキャンセルできるようにする
                if(trans.isServer)
                    if(GUI.Button(new Rect(1, 50, 150, 40), "待ち受けを解除"))
                    {
                        trans.Close();
                        scene_state = SceneState.SelectRoll;
                    }

                break;

            case SceneState.SendAndReceive:
                //Sendテスト
                if(GUI.Button(new Rect(1, 1, 150, 40), "乱数を送る"))
                    try
                    {
                        trans.Send(BitConverter.GetBytes(UnityEngine.Random.Range(int.MinValue, int.MaxValue)));
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("Send : " + e.Message);
                        trans.Close();
                        scene_state = SceneState.SelectRoll;
                    }

                //Receiveテスト
                if(GUI.Button(new Rect(1, 50, 150, 40), "乱数を受け取る"))
                    try
                    {
                        trans.Receive(ref recvMessage);
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("Receive : " + e.Message);
                        trans.Close();
                        scene_state = SceneState.SelectRoll;
                    }

                //結果表示
                GUI.Label(new Rect(1, 100, 150, 40), "受け取った乱数:" + BitConverter.ToInt32(recvMessage, 0));

                //Disconnect
                if(GUI.Button(new Rect(1, 150, 100, 40), "Disconnect"))
                {
                    trans.Close();
                    scene_state = SceneState.SelectRoll;
                }
                break;

            default:
                break;
        }
    }

    private void OnErrStartServer(Exception e)
    {
        Debug.Log("StartServer : " + e.Message);
        trans.Close();
        scene_state = SceneState.SelectRoll;
    }

    private void OnErrConnect(Exception e)
    {
        Debug.Log("ConnectAsync : " + e.Message);
        trans.Close();
        scene_state = SceneState.SelectRoll;
    }

    private void OnErrAccept(Exception e)
    {
        Debug.Log("AcceptAsync" + e.Message);
        trans.Close();
        scene_state = SceneState.SelectRoll;
    }

    private void OnErrRunWork(Exception e)
    {
        Debug.Log("RunWorkThreadErr : " + e.Message);
        trans.Close();
        scene_state = SceneState.SelectRoll;
    }
}
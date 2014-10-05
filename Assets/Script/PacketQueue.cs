using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

public class PacketQueue
{
    /// <summary>
    /// パケットの情報
    /// </summary>
    private class PacketInfo
    {
        /// <summary>
        /// メモリ上の位置
        /// </summary>
        public readonly int offset;

        /// <summary>
        /// パケットのサイズ
        /// </summary>
        public readonly int size;

        public PacketInfo(int size, int offset)
        {
            this.offset = offset;
            this.size = size;
        }
    };

    private MemoryStream m_stream;
    private List<PacketInfo> packets_info;

    //データの最後尾位置(データ追加の開始位置)
    private int last_offset = 0;

    public PacketQueue()
    {
        m_stream = new MemoryStream();
        packets_info = new List<PacketInfo>();
    }

    /// <summary>
    /// 指定したデータを待ち行列に格納する
    /// データサイズはデータ配列の長さになる
    /// </summary>
    /// <param name="data">パケットデータ</param>
    /// <returns>格納したパケットデータのサイズ</returns>
    public int Enqueue(byte[] data)
    {
        PacketInfo info = new PacketInfo(data.Length, last_offset);
        // パケット格納情報を保存.
        packets_info.Add(info);
        // パケットデータを保存.
        m_stream.Position = last_offset;
        m_stream.Write(data, 0, data.Length);
        m_stream.Flush();
        last_offset += data.Length;
        return data.Length;
    }

    /// <summary>
    /// 待ち行列から1つのパケットを取り出す
    /// </summary>
    /// <param name="buffer">パケットデータ</param>
    /// <returns>受け取ったパケットデータのサイズ</returns>
    public int Dequeue(ref byte[] buffer)
    {
        if(packets_info.Count <= 0)
        {
            return -1;
        }

        PacketInfo info = packets_info[0];

        // バッファから該当するパケットデータを取得する.
        m_stream.Position = info.offset;
        int recvSize = m_stream.Read(buffer, 0, info.size);

        // キューデータを取り出したので先頭要素を削除.
        if(recvSize > 0)
            packets_info.RemoveAt(0);

        // すべてのキューデータを取り出したときはストリームをクリアしてメモリを節約する.
        if(packets_info.Count == 0)
            Clear();

        return recvSize;
    }

    public void Clear()
    {
        byte[] buffer = m_stream.GetBuffer();
        Array.Clear(buffer, 0, buffer.Length);

        m_stream.Position = 0;
        m_stream.SetLength(0);
        last_offset = 0;
    }
}
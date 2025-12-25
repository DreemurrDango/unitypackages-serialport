using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

#region   //外部引用dll类
public class cnCommWrapper
{
    [DllImport("cnCommWrapper")]
    public static extern IntPtr CreateComm();
    [DllImport("cnCommWrapper")]
    public static extern void DisposeComm(IntPtr comm);
    [DllImport("cnCommWrapper")]
    public static extern bool IsOpen(IntPtr comm);
    [DllImport("cnCommWrapper")]
    public static extern bool SetBufferSize(IntPtr comm, int dwInputSize, int dwOutputSize);
    [DllImport("cnCommWrapper")]
    public static extern void ClearInputBuffer(IntPtr comm);
    [DllImport("cnCommWrapper")]
    public static extern int GetInputSize(IntPtr comm);
    [DllImport("cnCommWrapper")]
    public static extern bool Open(IntPtr comm, int dwPort, string szSetStr);
    [DllImport("cnCommWrapper")]
    public static extern void Close(IntPtr comm);
    [DllImport("cnCommWrapper")]
    public static extern bool SetRTS(IntPtr comm, bool OnOrOff);
    [DllImport("cnCommWrapper")]
    public static extern int Read(IntPtr comm, byte[] Buffer, int dwBufferLength, int dwWaitTime = 10);
    [DllImport("cnCommWrapper")]
    public static extern int Write(IntPtr comm, byte[] Buffer, int dwBufferLength, int dwWaitTime = 20);
}
#endregion

#region     //串口结构
//
// 摘要:
//     指定在建立串行端口的通信使用的控制协议 System.IO.Ports.SerialPort 对象。
public enum Handshake
{
    //
    // 摘要:
    //     无法控制用于在握手。
    None = 0,
    //
    // 摘要:
    //     使用 XON/XOFF 软件控制协议。 XOFF 控件发送，以停止数据传输。 XON 控制发送以继续传输。 这些软件控制而不是请求发送 (RTS) 使用，并清除硬件控件的发送
    //     (CTS)。
    XOnXOff = 1,
    //
    // 摘要:
    //     使用请求发送 (RTS) 硬件流控制。 RTS 通知可用于传输数据。 RTS 行输入的缓冲区变满之后，如果将设置为 false。 RTS 行会将设置为 true
    //     更多的空间变得可用时输入缓冲区中。
    RequestToSend = 2,
    //
    // 摘要:
    //     使用请求-发送 (RTS) 硬件控制和 XON/XOFF 软件控制。
    RequestToSendXOnXOff = 3
}

//
// 摘要:
//     指定的奇偶校验位 System.IO.Ports.SerialPort 对象。
public enum Parity
{
    //
    // 摘要:
    //     没有奇偶校验检查时发生。
    None = 0,
    //
    // 摘要:
    //     设置奇偶校验位，以便设置了位数为奇数。
    Odd = 1,
    //
    // 摘要:
    //     设置奇偶校验位，以便设置了位的计数为偶数。
    Even = 2,
    //
    // 摘要:
    //     将奇偶校验位设置为 1。
    Mark = 3,
    //
    // 摘要:
    //     将奇偶校验位设置为 0。
    Space = 4
}
//
// 摘要:
//     指定停止上使用的比特数 System.IO.Ports.SerialPort 对象。
public enum StopBits
{
    //
    // 摘要:
    //     使用没有停止位。 不支持此值 System.IO.Ports.SerialPort.StopBits 属性。
    None = 0,
    //
    // 摘要:
    //     使用一个停止位。
    One = 1,
    //
    // 摘要:
    //     使用两个停止位。
    Two = 2,
    //
    // 摘要:
    //     使用 1.5 停止位。
    OnePointFive = 3
}

#endregion

namespace DreemurrStudio.SerialPortSystem
{
    #region   //串口类
    public class Port
    {
        private IntPtr m_comm;
        private string m_portName;
        private int m_portNum;
        private int m_baudRate;
        private char m_Parity = 'n';
        private int m_dataBits = 8;
        private int m_StopBits = 1;

        public Port(string portName, int baudRate)
        {
            this.m_portName = portName;
            string _name = this.m_portName.Substring(3);
            if (!int.TryParse(_name, out m_portNum)) this.m_portNum = -1;
            this.m_baudRate = baudRate;
            m_comm = cnCommWrapper.CreateComm();
        }

        public Port(string portName, int baudRate, Parity parity, StopBits stopBits) : this(portName, baudRate)
        {
            this.m_portName = portName;
            string _name = this.m_portName.Substring(3);
            if (!int.TryParse(_name, out m_portNum)) this.m_portNum = -1;
            this.m_baudRate = baudRate;
            switch (parity)
            {
                case Parity.None:
                    m_Parity = 'n';
                    break;
                case Parity.Even:
                    m_Parity = 'e';
                    break;
                case Parity.Odd:
                    m_Parity = 'o';
                    break;
                case Parity.Mark:
                    m_Parity = 'm';
                    break;
                case Parity.Space:
                    m_Parity = 's';
                    break;
            }
            switch (stopBits)
            {
                case StopBits.One:
                    m_StopBits = 1;
                    break;
                case StopBits.Two:
                    m_StopBits = 2;
                    break;
                case StopBits.OnePointFive:
                    m_StopBits = 3;
                    break;
            }
            m_comm = cnCommWrapper.CreateComm();
        }

        public void Open()
        {
            string setStr = string.Format("{0},{1},{2},{3}", m_baudRate, m_Parity, m_dataBits, m_StopBits);
            bool ret = cnCommWrapper.Open(m_comm, this.m_portNum, setStr);
            return;
        }

        public void Close()
        {
            cnCommWrapper.Close(m_comm);
            cnCommWrapper.DisposeComm(m_comm);
        }

        public bool IsOpen => cnCommWrapper.IsOpen(m_comm);

        /// <summary>
        /// 从串口中读取字节数组
        /// </summary>
        /// <param name="buffer">接受输出的16位数组</param>
        /// <returns>读取到的有效字节数</returns>
        public int Read(byte[] buffer) => cnCommWrapper.Read(m_comm, buffer, buffer.Length);

        /// <summary>
        /// 从串口中读取字符串
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public int Read(out string str)
        {
            byte[] buffer = new byte[1024];
            int count = cnCommWrapper.Read(m_comm, buffer, buffer.Length);
            str = System.Text.Encoding.Default.GetString(buffer);
            return count;
        }

        /// <summary>
        /// 串口发送字节
        /// </summary>
        /// <param name="buffer"></param>
        public void Write(byte[] buffer) => cnCommWrapper.Write(m_comm, buffer, buffer.Length);
        /// <summary>
        /// 串口发送字符串
        /// </summary>
        public void Write(string str)
        {
            var buffer = System.Text.Encoding.Default.GetBytes(str);
            cnCommWrapper.Write(m_comm, buffer, buffer.Length);
        }


        /// <summary>
        /// 将内容为十六进制数组的字符串转为字节数组
        /// </summary>
        /// <param name="hexStr">要进行转换的字符串，其内容应当为16位数组，可带连接符</param>
        /// <returns></returns>
        public static byte[] HexConverByte(string hexStr)
        {
            var str = hexStr;
            //剔除所有空格与连接符
            str = str.Replace(" ", "");
            str = str.Replace("-", "");
            //奇数个字符时去除最后一个字符
            if (str.Length % 2 == 1)
                Debug.LogWarning($"进行转换后的字符串{str}中字母个数为奇数，将移除最后一个字符");
            var len = str.Length / 2;
            var bytes = new byte[len];
            for (int i = 0; i < len; i++)
                bytes[i] = (byte)(Convert.ToInt32(str.Substring(i*2,2), 16));
            return bytes;
        }


        /// <summary>
        /// 字符串转byte数组
        /// </summary>
        /// <param name="Str"></param>
        /// <returns></returns>
        public static byte[] StrConverByte(string Str) => Encoding.Default.GetBytes(Str);


        /// <summary>
        /// byte转字符串，相比于BitConverter.ToString(bytes)，没有"-"连接符
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string BytesConverStr(byte[] bytes) => Encoding.Default.GetString(bytes);

        /// <summary>
        /// byte转字符串，相比于BitConverter.ToString(bytes)，没有"-"连接符
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string BytesConverHex(byte[] bytes) // 0xae00cf => "AE00CF "
        {
            string hexString = string.Empty;
            StringBuilder strB = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                strB.Append(bytes[i].ToString("X2"));
            }
            hexString = strB.ToString();
            return hexString;
        }

        ///<summary>
        ///单个byte转16进制字符串
        /// </summary>
        public static string ByteConverHex(byte bytes) // 0xae00cf => "AE00CF "
        {
            string hexString = string.Empty;
            StringBuilder strB = new StringBuilder();
            strB.Append(bytes.ToString("X2"));
            hexString = strB.ToString();
            return hexString;
        }
    }
    #endregion
}
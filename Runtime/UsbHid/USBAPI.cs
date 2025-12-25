using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections;
using System.Text.RegularExpressions;
using System.IO;

public class USBAPI : MonoBehaviour
{
    [DllImport("USBDll")]
    private extern static bool UsbDeviceOpen(ref IntPtr HA, int wVID, int wPID, int MemberIndex);

    [DllImport("USBDll")]
    private extern static void UsbDeviceClose(IntPtr handle);

    [DllImport("USBDll")]
    private extern static bool UsbDeviceWrite(IntPtr handle, byte[] WriteBuffer);

    [DllImport("USBDll")]
    private extern static bool UsbDeviceRead(IntPtr handle, byte[] ReadBuffer);


    //读取设备文件
    [DllImport("Kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile
        (
            IntPtr hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            ref uint lpNumberOfBytesRead,
            IntPtr lpOverlapped
        );

    IntPtr USBIntPtr = IntPtr.Zero;  //

    private Thread UsbReceiveTh;        //usb数据接收线程

    private bool IsThreadStart;

    private byte[] ReceiveTemp =new byte[1024];
    public byte[] UsbReceiveBuff { get { return ReceiveTemp; } set { ReceiveTemp = value; } }

    //public static USBAPI _SerialInstance;

    //private void Awake()
    //{
    //    _SerialInstance = this;
    //}

    void Start()
    {
        UsbInit();
        InvokeRepeating("ScanUsb", 1, 0.5f);//定时检测是否USB掉线
    }

    private void UsbInit()
    {
        if (!UsbDeviceOpen(ref USBIntPtr, 0x0101, 0x0101, 0x00)) return;  //如果没有打开usb则返回
        IsThreadStart = true;
        UsbReceiveTh = new Thread(UsbRead);
        UsbReceiveTh.IsBackground = true;
        UsbReceiveTh.Start();
        Debug.Log("初始化完成");
    }

    private void ScanUsb()
    {
        if (IsThreadStart) return;

        Debug.Log("定时检测");
        Invoke("UsbInit", 1); //继续初始化

    }

    private void UsbClose()
    {
        IsThreadStart = false;//停止线程的更新
        if(UsbReceiveTh!=null&&UsbReceiveTh.IsAlive) UsbReceiveTh.Abort();
        UsbDeviceClose(USBIntPtr);
        USBIntPtr = IntPtr.Zero;
        Debug.Log("USB退出");
    }


    public void UsbSendBuff(string temp)//向USB写入字符串
    {
        //if (!IsThreadStart) return;
        //UsbDeviceWrite(USBIntPtr, temp);
    }

    void FixedUpdate()   //定期回收垃圾       
    {
        if (Time.frameCount % 120 == 0) System.GC.Collect();
    }

    private void UsbRead()//从指定USB设备读取数据线程
    {
        Debug.Log("进入读取线程");
    
        byte[] ReceiveBuff = new byte[1024];
        
        while (IsThreadStart)
        {
            if (USBIntPtr != IntPtr.Zero && UsbDeviceRead(USBIntPtr, ReceiveBuff))
            {
                Array.Clear(UsbReceiveBuff, 0, UsbReceiveBuff.Length);
                ReceiveBuff.CopyTo(UsbReceiveBuff, 0);
            }
            else
            {
                //IsThreadStart = false;
                UsbClose();
            }
        }
    }

    void OnApplicationQuit()
    {
        UsbClose();
    }
}




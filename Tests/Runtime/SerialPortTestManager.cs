using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DreemurrStudio.SerialPortSystem.DEMO
{
    /// <summary>
    /// 串口测试管理器
    /// </summary>
    public class SerialPortTestManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("测试通信串口")]
        private SerialPort serialPort;

        public void OnReceiveByteEvent(string keyName, byte[] data)
        {
            Debug.Log($"接收到16位通信事件{keyName},完整数据: {System.Text.Encoding.UTF8.GetString(data)}");
        }

        public void OnReceiveStrEvent(string keyName, string data)
        {
            Debug.Log($"接收到字符通信事件{keyName},完整数据: {data}");
        }

        [Header("带检验位")]
        [SerializeField]
        [Tooltip("要测试的检验位类型")]
        private SerialPort.CheckCodeType checkCodeType;
        [SerializeField]
        [Tooltip("要测试的检验位长度")]
        private int checkCodeLength = 2;
        [SerializeField]
        [InlineButton("TestCheckCode", "测试检验位")]
        [Tooltip("用于自动添加检验位的表示原十六进制数组数据字符串")]
        private string testHexCode;

        /// <summary>
        /// 测试发送添加检验位的数据
        /// </summary>
        public void TestCheckCode() => serialPort.Send(testHexCode, checkCodeType, checkCodeLength);
    }
}
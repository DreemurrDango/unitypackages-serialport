using System.Collections.Generic;
using System;
using UnityEngine;
using System.IO;
using Debug = UnityEngine.Debug;
using System.Diagnostics;
using UnityEngine.Events;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DreemurrStudio.SerialPortSystem
{
    /// <summary>
    /// 串流端口传输管理者
    /// 注意：该串口适用于通信频率较低（<50HZ）且包长度较固定的串口通信
    /// </summary>
    /// <remarks>
    /// 通过该组件统一进行端口传输，设置组件enable值可开关传输功能
    /// 由于该软件作用于游戏全局，应将其实例放入Main场景种
    /// </remarks>
    public class SerialPort : MonoBehaviour
    {
        /// <summary>
        /// 端口名固定前缀
        /// </summary>
        private const string PortNamePrefix = "COM";

        /// <summary>
        /// 串口对象实例列表
        /// </summary>
        private static List<SerialPort> _instances = new List<SerialPort>();

        /// <summary>
        /// 建议在全局仅一个串口时使用，获取第一个同时也是唯一一个串口实例
        /// </summary>
        /// <returns></returns>
        public static SerialPort GetInstance() => _instances[0];
        /// <summary>
        /// 根据对象名获取串口实例
        /// </summary>
        /// <param name="objectName">串口对象名</param>
        /// <returns></returns>
        public static SerialPort GetInstance(string objectName)
        {
            if (_instances.Count == 0) throw new Exception("串口实例尚未完成初始化！");
            var p = _instances.Find(p => p.gameObject.name == objectName);
            if (p == null) throw new Exception($"不存在名称为{objectName}的串口对象！其名称应与游戏对象名一致");
            return p;
        }
        /// <summary>
        /// 根据端口ID获取串口实例
        /// </summary>
        /// <param name="id">端口ID</param>
        /// <returns></returns>
        public static SerialPort GetInstance(int id)
        {
            if (_instances.Count == 0) throw new Exception("串口实例尚未完成初始化！");
            var p = _instances.Find(p => p.portID == id);
            if (p == null) throw new Exception($"不存在ID为{id}的串口对象！");
            return p;
        }

        /// <summary>
        /// 通信数据格式
        /// </summary>
        public enum DataFormat
        {
            /// <summary>
            /// 十六位数字，一次两字节
            /// </summary>
            hex,
            /// <summary>
            /// ASCII码
            /// </summary>
            ascii,
        }

        /// <summary>
        /// 串口事件结构
        /// </summary>
        [System.Serializable]
        public struct SerialEvent
        {
            /// <summary>
            /// 串口事件名称，键值匹配时在程序中使用该值作为事件名称，因而不可以重复
            /// </summary>
            public string name;
            /// <summary>
            /// 要匹配的键值，判断收到的数据是否以该值开头
            /// </summary>
            public string key;
        }

        /// <summary>
        /// 检验码类型
        /// </summary>
        [System.Serializable]
        public enum CheckCodeType
        {
            None,
            CRC16,
            XOR,
            SUM
        }

        /// <summary>
        /// 端口配置信息
        /// </summary>
        [Serializable]
        public struct PortInfo
        {
            /// <summary>
            /// 端口的ID
            /// </summary>
            public int portID;
            /// <summary>
            /// 串口的传输波特率，应当与设备或串口助手的设置保持一致
            /// </summary>
            public int baudRate;
            /// <summary>
            ///接收数据缓冲池（字节数组）的长度
            /// </summary>
            public int bufferLength;
            /// <summary>
            /// 数据格式
            /// </summary>
            public DataFormat format;
            /// <summary>
            /// 数据包的起始位
            /// </summary>
            public int validDataStart;
            /// <summary>
            /// 数据包的长度，默认值为-1，表示从起始位到结尾
            /// </summary>
            public int validDataLength;
            /// <summary>
            /// 事件键值的开始位（对于数据包）
            /// </summary>
            public int eventKeyStart;
            /// <summary>
            /// 事件键值的位数
            /// </summary>
            public int eventKeyLength;
            /// <summary>
            /// 端口事件列表
            /// </summary>
            public List<SerialEvent> serialEvents;
        }

        //[TitleGroup("端口配置")]
        //[SerializeField]
        //[Tooltip("是否开启自动设置端口ID，开启后将不再使用配置的ID，而是自动获取端口号不为1的最小号作为端口")]
        //private bool autoSetPortID = false;

        [TitleGroup("端口配置")]
        [SerializeField]
        [Tooltip("端口ID，范围为1~20")]
        private int portID = 7;

        [TitleGroup("端口配置")]
        [SerializeField]
        [Tooltip("串口的波特率")]
        private int baudRate = 9600;

        [TitleGroup("端口配置")]
        [SerializeField]
        [Tooltip("接收数据缓冲池（字节数组）的长度，根据实际情况调整，建议在8~64之间且为2的幂")]
        private int bufferLength = 64;

        [TitleGroup("端口配置")]
        [SerializeField]
        [Tooltip("数据通信格式")]
        private DataFormat dataFormat = DataFormat.hex;

        [TitleGroup("端口配置")]
        [SerializeField]
        [Tooltip("数据包的起始位，从0开始，若通信方式为16进制则为字节位数")]
        private int validDataStart = 0;

        [TitleGroup("端口配置")]
        [SerializeField]
        [Tooltip("数据包的长度，默认值为-1，表示从起始位到结尾（若通信方式为16进制，长度单位为字节位数）")]
        private int validDataLength = -1;

        [TitleGroup("端口配置/配置文件")]
        [SerializeField]
        [Tooltip("配置文件的文件夹，于StreamAssets路径下的相对路径，一般无需更改\n需要确保文件夹存在，否则可能弹出报错")]
        private string configFileFolderPath = "Configs/ComConfigs";

#if UNITY_EDITOR
        [BoxGroup("收到数据")]
        [ReadOnly, SerializeField, Multiline(3)]
        [Tooltip("显示上一次接收到的完整数据")]
        private string receiveDataShow;
        [BoxGroup("收到数据")]
        [ReadOnly, SerializeField, Multiline(3)]
        [Tooltip("显示上一次接收到的有效的数据部分")]
        private string validDataShow;
#endif

        [ToggleGroup("useUnityEvent", "使用Unity事件")]
        [SerializeField]
        [Tooltip("是否使用Unity事件转发")]
        private bool useUnityEvent = false;

        [ToggleGroup("useUnityEvent")]
        [SerializeField]
        [Tooltip("事件字符串键值的开始位，在数据包中的位数")]
        private int eventKeyStart = 0;

        [ToggleGroup("useUnityEvent")]
        [SerializeField]
        [Tooltip("事件字符串键值的位数（字符字数）")]
        private int eventKeyLength = 2;

        [ToggleGroup("useUnityEvent")]
        [SerializeField]
        [Tooltip("注册的串口事件")]
        private List<SerialEvent> serialEvents;

        [ToggleGroup("useUnityEvent")]
        [Tooltip("收到十六位串口数据时事件，事件参数为 <事件名,有效十六位数组数据>")]
        public UnityEvent<string, byte[]> OnReceiveSerialByteEvent;
        [ToggleGroup("useUnityEvent")]
        [Tooltip("收到Ascii码串口数据时事件，事件参数为 <事件名,有效Ascii字符串数据>")]
        public UnityEvent<string, string> OnReceiveSerialStrEvent;

        /// <summary>
        /// 收到数据时动作
        /// </summary>
        public Action<byte[]> OnHexDataReceived;
        /// <summary>
        /// 收到字符串数据时动作
        /// </summary>
        public Action<string> OnStrDataReceived;

        /// <summary>
        /// 上一次接收到的字节数组数据
        /// </summary>
        private List<byte> receiveBytesBuffer;
        /// <summary>
        /// 上一次接收到的，有效的字节数组数据部分
        /// </summary>
        private byte[] validBytesData;
        /// <summary>
        /// 上一次接收到的字符串数据
        /// </summary>
        private string receiveStrData;
        /// <summary>
        /// 上一次接收到的，有效的字符串数据部分
        /// </summary>
        private string validStrData;
        /// <summary>
        /// 旧的文件名
        /// </summary>
        private string oldFileName;
        /// <summary>
        /// 进行数据传输的端口对象
        /// </summary>
        private Port serialPort;

        /// <summary>
        /// 仅用于Update循环中的临时常驻变量：字符串数据缓存值
        /// </summary>
        private string sd = "";
        /// <summary>
        /// 仅用于Update循环中的临时常驻变量：字节数组数据缓存值
        /// </summary>
        private byte[] bd;

        /// <summary>
        /// 获取配置文件路径名
        /// </summary>
        private string ConfigFilePath => GetConfigFullPath(gameObject.name);
        /// <summary>
        /// 获取本机端口名
        /// </summary>
        private string PortName => PortNamePrefix + portID;
        /// <summary>
        /// 获取当前收到的字节数组数据
        /// </summary>
        public List<byte> ReceiveBytesBuffer => receiveBytesBuffer;
        /// <summary>
        /// 获得上一次收到的字符串数组数据
        /// </summary>
        public string ReceiveStrData => receiveStrData;

        protected void Awake()
        {
            DontDestroyOnLoad(gameObject);
            LoadPortInfo();
            try
            {
                serialPort = new Port(PortName, baudRate);
                serialPort.Open();
            }
            catch (Exception ex)
            {
                Debug.LogError($"串口初始化失败: {ex.Message}");
            }

            if (!serialPort.IsOpen)
            {
                Debug.LogError($"串口初始化失败，{PortName}未能成功打开");
                return;
            }
            Debug.Log($"{PortName}串口已开启");
            _instances.Add(this);
            //初始化数据缓存池
            bd = new byte[bufferLength];
            receiveBytesBuffer = new List<byte>(bufferLength);
        }

        protected void OnDestroy()
        {
            if (!serialPort.IsOpen) return; 
            serialPort.Close();
            Debug.Log($"{PortName}串口已关闭");
        }

        /// <summary>
        /// 根据文件名获取配置文件的完整路径
        /// </summary>
        /// <param name="fileName">文件名，应该与游戏对象名一致</param>
        /// <returns></returns>
        public string GetConfigFullPath(string fileName) =>
            Path.Combine(Application.streamingAssetsPath, configFileFolderPath, fileName + ".json");

        #region 数据接收

        private void Update()
        {
            if (!serialPort.IsOpen) return;
            switch (dataFormat)
            {
                case DataFormat.hex:
                    if (serialPort.Read(bd) > 0)
                    {
                        OnReceive(bd);
                        Array.Clear(bd, 0, bufferLength);
                    }
                    break;
                case DataFormat.ascii:
                    if (serialPort.Read(out sd) > 0)
                    {
                        OnReceive(sd);
                        sd = "";
                    }
                    break;
            }
        }

        /// <summary>
        /// 对获取到的字节组数据进行处理
        /// </summary>
        /// <param name="data">获取到的字节组原数据</param>
        private void OnReceive(byte[] data)
        {
            receiveBytesBuffer = new List<byte>(data);
            //截取出有效数据包部分
            var vaildLength = validDataLength <= 0 ? data.Length - validDataStart : validDataLength;
            if(validDataStart + vaildLength > data.Length)
            {
                Debug.LogWarning($"收到的包不足以截取有效数据包，数据长度为{data.Length}，" +
                    $"但尝试截取起始于{validDataStart}，长{vaildLength}个字节的数据包，将忽略对此包的处理");
                return;
            }
            validBytesData = receiveBytesBuffer.GetRange(validDataStart, vaildLength).ToArray();
#if UNITY_EDITOR
			receiveDataShow = FormatByteString(data);
			validDataShow = FormatByteString(validBytesData);
#endif
            //使用UNITY事件自动转发时
            if (useUnityEvent && serialEvents != null && serialEvents.Count > 0)
            {
                //转为字符串，获取键值部分
                var s = Port.BytesConverHex(validBytesData);
                var key = s[eventKeyStart..(eventKeyStart + eventKeyLength)];
                //比对键值(忽略大小写)，找到符合的串口事件
                var e = serialEvents.Find(e => string.Equals(key, e.key, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(e.name)) Debug.Log($"未找到与{s}的KEY值{key}对应的串口数据事件，" +
                    $"请检查配置表中是否已配置对应的参数");
                else
                {
                    OnReceiveSerialByteEvent?.Invoke(e.name, validBytesData);
                    Debug.Log($"收到事件：{e.name}，完整数据：{s}，键值为：{key}");
                }
            }
            OnHexDataReceived?.Invoke(validBytesData);
        }

        /// <summary>
        /// 对ascii字符串数据进行处理后发送
        /// </summary>
        /// <param name="data">获取到的ascii字符串原数据</param>
        private void OnReceive(string data)
        {
            receiveStrData = data;
            //截取出有效数据包部分
            var vaildLength = validDataLength <= 0 ? data.Length : validDataLength;
            validStrData = receiveStrData[validDataStart..(validDataStart + vaildLength)];
#if UNITY_EDITOR
            receiveDataShow = data;
            validDataShow = validStrData;
#endif
            //使用UNITY事件自动转发时
            if (useUnityEvent && serialEvents != null && serialEvents.Count > 0)
            {
                //比对键值(忽略大小写)，找到符合的串口事件
                var key = validStrData[eventKeyStart..(eventKeyStart + eventKeyLength)];
                var e = serialEvents.Find(e => string.Equals(key, e.key, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(e.name))
                    Debug.Log($"未找到与{validStrData}的KEY值{key}对应的串口数据事件，" +
                        $"请检查配置表中是否已配置对应的参数");
                else
                {
                    OnReceiveSerialStrEvent?.Invoke(e.name, validStrData);
                    Debug.Log($"收到事件：{e.name}，完整数据：{validStrData}，键值为：{key}");
                }
            }
            OnStrDataReceived?.Invoke(validStrData);
        }
        #endregion

        #region 数据发送
        /// <summary>
        /// 写入数据，最终数据会根据通信方式进行转换
        /// </summary>
        /// <param name="data">数字字符串，将根据通信方式进行转换</param>
        public void Send(string data)
        {
            if (!serialPort.IsOpen) return;
            switch (dataFormat)
            {
                case DataFormat.hex:
                    Send(Port.HexConverByte(data));
                    break;
                case DataFormat.ascii:
                    serialPort.Write(data);
                    break;
            }
        }
        /// <summary>
        /// 以字符串形式写入字节数组数据，并支持自动添加校验码
        /// </summary>
        /// <param name="data">表示16进制数组数据的字符串</param>
        /// <param name="checkCodeType">校验码类型</param>
        /// <param name="checkCodeLength">要添加的校验码长度</param>
        public void Send(string data, CheckCodeType checkCodeType, int checkCodeLength)
            => Send(Port.HexConverByte(data), checkCodeType, checkCodeLength);

        /// <summary>
        /// 写入字节数组数据，支持自动添加校验码
        /// </summary>
        /// <param name="data">16进制数组，为规范起见，请确保数据格式为0xFF</param>
        /// <param name="checkCodeType">校验码类型</param>
        /// <param name="checkCodeLength">要添加的校验码长度</param>
        public void Send(byte[] data, CheckCodeType checkCodeType = CheckCodeType.None, int checkCodeLength = 0)
        {
            if (!serialPort.IsOpen) return;
            if (dataFormat != DataFormat.hex)
                throw new Exception("通信方式不为HEX码传输！");

            byte[] sendData = new byte[data.Length + checkCodeLength];

            if (checkCodeType != CheckCodeType.None && checkCodeLength > 0)
            {
                switch (checkCodeType)
                {
                    case CheckCodeType.CRC16:
                        sendData = AddCRC16(data, checkCodeLength);
                        break;
                    case CheckCodeType.XOR:
                        sendData = AddXOR(data, checkCodeLength);
                        break;
                    case CheckCodeType.SUM:
                        sendData = AddSUM(data, checkCodeLength);
                        break;
                }
            }
            else sendData = data;
            serialPort.Write(sendData);
        }
#endregion

        #region 校验码生成
        /// <summary>
        /// CRC16校验码生成并拼接原数据
        /// </summary>
        /// <param name="data">原16位数据</param>
        /// <param name="length">要添加的检验码长度</param>
        /// <returns>原数据+校验码</returns>
        private byte[] AddCRC16(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                        crc = (ushort)((crc >> 1) ^ 0xA001);
                    else
                        crc >>= 1;
                }
            }
            byte[] result = new byte[length];
            result[0] = (byte)(crc & 0xFF);
            if (length > 1)
                result[1] = (byte)((crc >> 8) & 0xFF);

            // 拼接原数据和校验码
            byte[] fullData = new byte[data.Length + result.Length];
            Buffer.BlockCopy(data, 0, fullData, 0, data.Length);
            Buffer.BlockCopy(result, 0, fullData, data.Length, result.Length);
            return fullData;
        }

        /// <summary>
        /// XOR校验码生成并拼接原数据
        /// </summary>
        /// <param name="data">原16位数据</param>
        /// <param name="length">要添加的检验码长度</param>
        /// <returns>原数据+校验码</returns>
        private byte[] AddXOR(byte[] data, int length)
        {
            byte xor = 0;
            for (int i = 0; i < data.Length; i++)
                xor ^= data[i];
            byte[] result = new byte[length];
            for (int i = 0; i < length; i++)
                result[i] = xor;

            // 拼接原数据和校验码
            byte[] fullData = new byte[data.Length + result.Length];
            Buffer.BlockCopy(data, 0, fullData, 0, data.Length);
            Buffer.BlockCopy(result, 0, fullData, data.Length, result.Length);
            return fullData;
        }

        /// <summary>
        /// SUM校验码生成并拼接原数据
        /// </summary>
        /// <param name="data">原16位数据</param>
        /// <param name="length">要添加的检验码长度</param>
        /// <returns>原数据+校验码</returns>
        private byte[] AddSUM(byte[] data, int length)
        {
            int sum = 0;
            for (int i = 0; i < data.Length; i++)
                sum += data[i];
            byte[] result = new byte[length];
            for (int i = 0; i < length; i++)
                result[i] = (byte)((sum >> (8 * i)) & 0xFF);

            // 拼接原数据和校验码
            byte[] fullData = new byte[data.Length + result.Length];
            Buffer.BlockCopy(data, 0, fullData, 0, data.Length);
            Buffer.BlockCopy(result, 0, fullData, data.Length, result.Length);
            return fullData;
        }
        #endregion

        #region 编辑器内操作
        /// <summary>
        /// 编辑器内端口参数是否有未保存的修改值
        /// </summary>
        private bool haveUnsavedChange;
        /// <summary>
        /// 从文件中读取端口信息
        /// </summary>
        [ButtonGroup("端口配置/配置文件/配置操作")]
        [Button("读取配置")]
        [ContextMenu("读取配置")]
        public void LoadPortInfo()
        {
            var data = "";
            var path = ConfigFilePath;
            if (!File.Exists(path))
                throw new Exception($"{path}不存在或已被移动！");
            using (var sr = File.OpenText(ConfigFilePath))
            {
                data = sr.ReadToEnd();
                sr.Close();
            }
            var info = JsonUtility.FromJson<PortInfo>(data);
            //载入所读取的数据
            this.portID = info.portID;
            this.baudRate = info.baudRate;
            this.bufferLength = info.bufferLength;
            this.dataFormat = info.format;
            this.validDataStart = info.validDataStart;
            this.validDataLength = info.validDataLength;
            //读取端口映射事件
            if (info.serialEvents.Count > 0)
            {
                this.useUnityEvent = true;
                this.eventKeyStart = info.eventKeyStart;
                this.eventKeyLength = info.eventKeyLength;
                this.serialEvents = info.serialEvents;
            }
            else
            {
                this.useUnityEvent = false;
                this.eventKeyStart = 0;
                this.eventKeyLength = -1;
                this.serialEvents = new List<SerialEvent>();
            }
            haveUnsavedChange = false;
            Debug.Log($"已成功从配置文件{path}读取配置数据到场景对象{gameObject.name}中");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            haveUnsavedChange = true;
        }

        /// <summary>
        /// 修剪字节数组末尾的0x00字节并格式化为字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>修建末尾0x00后显示的字符串</returns>
		private static string FormatByteString(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0) return string.Empty;
			var length = bytes.Length;
			while (length > 0 && bytes[length - 1] == 0) length--;
			if (length == 0) length = 1; // 至少展示一个字节，避免完全空白
			return length == bytes.Length ? BitConverter.ToString(bytes) : BitConverter.ToString(bytes, 0, length);
		}

        [ButtonGroup("端口配置/配置文件/配置操作")]
        [Button("打开配置文件")]
        [ContextMenu("打开配置文件")]
        public void OpenConfigFile()
        {
            if (!File.Exists(ConfigFilePath))
                throw new Exception($"配置文件{ConfigFilePath}不存在或已被重命名，请先保存当前配置！");
            Process.Start(ConfigFilePath);
        }

        /// <summary>
        /// 将在Inspector窗口的数据写入配置文件中
        /// </summary>
        [InfoBox("请保存配置以确保更改生效", "haveUnsavedChange")]
        [ButtonGroup("端口配置/配置文件/配置操作")]
        [Button("保存配置")]
        [ContextMenu("保存配置")]
        private void SavePortInfo()
        {
            var info = new PortInfo
            {
                portID = portID,
                baudRate = baudRate,
                bufferLength = bufferLength,
                format = dataFormat,
                validDataStart = validDataStart,
                validDataLength = validDataLength,
                serialEvents = serialEvents,
                eventKeyStart = eventKeyStart,
                eventKeyLength = eventKeyLength
            };
            string path;
            //由旧的配置文件更名时，删除旧的配置文件
            if (oldFileName != gameObject.name && !string.IsNullOrEmpty(oldFileName))
            {
                path = GetConfigFullPath(oldFileName);
                if (File.Exists(path))
                {
                    File.Delete(GetConfigFullPath(oldFileName));
                    Debug.Log($"已删除旧的配置文件：{path}");
                }
            }
            path = ConfigFilePath;
            //不存在配置时，将其保存
            if (!File.Exists(path)) File.Create(path).Close();
            //将数据转为JSON，并进行保存
            var data = JsonUtility.ToJson(info, true);
            oldFileName = gameObject.name;
            //文件读取流
            using var sw = new StreamWriter(path, false);
            sw.WriteLine(data);
            sw.Close();
            AssetDatabase.Refresh();
            haveUnsavedChange = false;
            Debug.Log($"已将串口配置保存到{path}");
        }


        [FoldoutGroup("调试-接收")]
        [SerializeField]
        [Tooltip("模拟接收到的字符串")]
        private string receiveData;

        /// <summary>
        /// 模拟接收数据
        /// </summary>
        [FoldoutGroup("调试-接收")]
        [Button("模拟接收")]
        [ContextMenu("模拟接收")]
        public void SimulateReceiveData()
        {
            switch (dataFormat)
            {
                case DataFormat.hex:
                    var bytes = Port.HexConverByte(receiveData);
                    OnReceive(bytes);
                    break;
                case DataFormat.ascii:
                    OnReceive(receiveData);
                    break;
            }
        }

        [FoldoutGroup("调试-发送")]
        [SerializeField]
        [Tooltip("模拟发送的数据，将根据通信方式自动转换")]
        private string sendData;

        [FoldoutGroup("调试-发送")]
        [Button("模拟发送")]
        [ContextMenu("模拟发送")]
        public void SimulateSendData() => Send(sendData);

#endif
        #endregion
    }
}
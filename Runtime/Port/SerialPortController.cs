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
    /// 串流端口传输控制器：提供对串口的配置、流程控制、数据解析、验证、事件转发、编辑器内调试功能
    /// </summary>
    /// <remarks>
    /// 该高级串口管理器提供了对串口的全面控制，可以通过配置与扩展实现各种串口通信需求
    /// </remarks>
    public class SerialPortController : MonoBehaviour
    {

        /// <summary>
        /// 端口名固定前缀
        /// </summary>
        private const string PortNamePrefix = "COM";
        /// <summary>
        /// 配置文件的文件夹名，处于StreamAssets路径下，一般无需更改
        /// </summary>
        private const string ConfigFileFolderPath = "Configs/ComConfigs";
        /// <summary>
        /// 串口数据缓冲池的长度，建议为2的幂次方<para/>
        /// 默认为1024字节，可以容纳20000B/s以下的传输速率，在其上可能会出现数据丢失（受帧率影响），最多到32768字节<para/>
        /// </summary>
        private const int BufferLength = 1024;

        #region 全局获取实例
        /// <summary>
        /// 串口对象实例列表
        /// </summary>
        private static List<SerialPortController> _instances = new List<SerialPortController>();

        /// <summary>
        /// 建议在全局仅一个串口时使用，获取第一个同时也是唯一一个串口实例
        /// </summary>
        /// <returns></returns>
        public static SerialPortController GetInstance() => _instances[0];
        /// <summary>
        /// 根据对象名获取串口实例
        /// </summary>
        /// <param name="objectName">串口对象名</param>
        /// <returns></returns>
        public static SerialPortController GetInstance(string objectName)
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
        public static SerialPortController GetInstance(int id)
        {
            if (_instances.Count == 0) throw new Exception("串口实例尚未完成初始化！");
            var p = _instances.Find(p => p.portID == id);
            if (p == null) throw new Exception($"不存在ID为{id}的串口对象！");
            return p;
        }
        #endregion

        /// <summary>
        /// 根据文件名获取配置文件的完整路径
        /// </summary>
        /// <param name="fileName">文件名，应该与游戏对象名一致</param>
        /// <returns></returns>
        public static string GetConfigFullPath(string fileName) =>
            Path.Combine(Application.streamingAssetsPath, ConfigFileFolderPath, fileName + ".json");

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
        /// 数据包处理结构体，通过数据包的起始标记和长度来拆解出数据包
        /// </summary>
        [System.Serializable]
        public struct Packet
        {
            /// <summary>
            /// 数据包的起始标记
            /// </summary>
            public string startFlag;
            /// <summary>
            /// 数据包的总长度，默认值为-1，表示不进行包处理，直接从起始位到结尾<para/>
            /// 注：若通信方式为16进制，长度单位为字节位数
            /// </summary>
            public int length;
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
            /// 数据格式
            /// </summary>
            public DataFormat format;
            /// <summary>
            /// 事件键值的开始位（对于有效数据包）
            /// </summary>
            public int eventKeyStart;
            /// <summary>
            /// 事件键值的位数(无论是否为16位读写都为字符数)
            /// </summary>
            public int eventKeyLength;
            /// <summary>
            /// 端口事件列表
            /// </summary>
            public List<SerialEvent> serialEvents;
        }

        [TitleGroup("串口配置")]
        [SerializeField]
        [Tooltip("端口ID，范围为1~20")]
        private int portID = 7;

        [TitleGroup("串口配置")]
        [SerializeField]
        [Tooltip("串口的波特率")]
        private int baudRate = 9600;

        [TitleGroup("串口配置")]
        [SerializeField]
        [Tooltip("接收数据缓冲池（字节数组）的长度，根据实际情况调整，建议在8~64之间且为2的幂")]
        private int bufferLength = 64;

        [TitleGroup("串口配置")]
        [SerializeField]
        [Tooltip("数据通信格式")]
        private DataFormat dataFormat = DataFormat.hex;


        /// <summary>
        /// 是否未使用包处理，仅用于OdinInspector的ToggleGroup属性
        /// </summary>
        private bool NotUsePackctHandle => !usePackctHandle;
        [Space]
        [SerializeField]
        [InfoBox("出现数据包过载时请启用包处理", "NotUsePackctHandle")]
        [ToggleGroup("usePackctHandle", "使用包处理")]
        [Tooltip("是否使用包处理")]
        private bool usePackctHandle = false;

        [ToggleGroup("usePackctHandle", "使用包处理")]
        [SerializeField]
        [Tooltip("数据包的起始标记，一般为连续特殊字节，为空时，表示不使用包处理")]
        private string packetFlag = "AA";

        [ToggleGroup("usePackctHandle", "使用包处理")]
        [SerializeField]
        [Tooltip("数据包的长度，默认值为-1，表示从起始位到结尾（若通信方式为16进制，长度单位为字节位数）")]
        private int packetLength = -1;

        /// <summary>
        /// 是否使用数据包检验器
        /// </summary>
        private bool UsePacketCheck => packetCheck != null;
        //TODO: 需要实现IPacketCheck接口的具体类，提供数据包校验功能
        [TitleGroup("数据处理")]
        [SerializeField]
        [Tooltip("使用的端口数据检验器，若为空则不进行校验")]
        private IPacketCheck packetCheck;

        [TitleGroup("数据处理")]
        [SerializeField]
        [Tooltip("数据包中有效数据的起始位，从0开始，若通信方式为16进制则为字节位数")]
        private int validDataStart = 0;

        [TitleGroup("数据处理")]
        [SerializeField]
        [Tooltip("数据包中有效数据的长度，默认值为-1，表示从起始位到结尾（若通信方式为16进制，长度单位为字节位数）")]
        private int validDataLength = -1;

#if UNITY_EDITOR
        [BoxGroup("收到数据")]
        [ReadOnly, SerializeField, Multiline(5)]
        [Tooltip("显示当前缓存区的完整数据")]
        private string receiveDataShow;
        [BoxGroup("收到数据")]
        [ReadOnly, SerializeField, Multiline(3)]
        [Tooltip("显示上一次接收到的数据包的完整数据")]
        private string packetDataShow;
        [BoxGroup("收到数据")]
        [ReadOnly, SerializeField]
        [Tooltip("显示上一次接收到的有效的数据部分")]
        private string validDataShow;
#endif

        [ToggleGroup("useUnityEvent", "使用Unity事件")]
        [SerializeField]
        [Tooltip("是否使用Unity事件转发")]
        private bool useUnityEvent = false;

        [ToggleGroup("useUnityEvent")]
        [SerializeField]
        [Tooltip("事件字符串键值的开始位序号(从0开始)，在字符串中的位数")]
        private int eventKeyStart = 0;

        [ToggleGroup("useUnityEvent")]
        [SerializeField]
        [Tooltip("事件字符串键值的位数（无论是否为16位通信都为字符字数），设为-1时表示全部长度")]
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
        public Action<byte[]> OnHexPacketReceived;
        /// <summary>
        /// 收到字符串数据时动作
        /// </summary>
        public Action<string> OnStrPacketReceived;

        /// <summary>
        /// 所有接收到的字节数组数据
        /// </summary>
        private List<byte> receiveBytesBuffer;
        /// <summary>
        /// 所有接收到的字符串数据
        /// </summary>
        private string receiveStrData;
        /// <summary>
        /// 上一次接收到的，有效的字节数组数据部分
        /// </summary>
        private byte[] validBytesData;
        /// <summary>
        /// 上一次接收到的，有效的字符串数据部分
        /// </summary>
        private string validStrData;
        /// <summary>
        /// 旧的文件名，用于替换配置文件时删除旧的配置文件
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

            if (!serialPort.IsOpen) return;
            Debug.Log($"{PortName}串口已开启");
            _instances.Add(this);
            //初始化数据缓存池
            sd = "";
            bd = new byte[bufferLength];
            receiveStrData = "";
            receiveBytesBuffer = new List<byte>();
        }

        protected void OnDestroy()
        {
            if (serialPort.IsOpen) serialPort.Close();
            Debug.Log($"{PortName}串口已关闭");
        }

        private void Update()
        {
            if (!serialPort.IsOpen) return;
            switch (dataFormat)
            {
                case DataFormat.hex:
                    var len = serialPort.Read(bd);
                    if (len > 0)
                    {
                        OnReceiveData(bd, len);
                        Array.Clear(bd, 0, bufferLength);
                    }
                    break;
                case DataFormat.ascii:
                    if (serialPort.Read(out sd) > 0)
                    {
                        OnReceiveData(sd);
                        sd = "";
                    }
                    break;
            }
        }

        /// <summary>
        /// 对数据包进行处理，进行检验后拆分出有效数据包部分，并触发串口事件
        /// </summary>
        /// <param name="packet">数据包</param>
        /// <param name="validStartIndex">有效数据起始序号</param>
        /// <param name="validLength">有效数据长度</param>
        private void PacketHandle(byte[] packet, int validStartIndex, int validLength)
        {
            var packetStr = BitConverter.ToString(packet);
            // 数据校验
            if (UsePacketCheck && !packetCheck.CheckPacketData(packet))
            {
                Debug.Log($"数据包{packetStr}校验失败，已丢弃该数据包！");
                return;
            }
            //截取出有效数据包部分
            if (validLength <= 0) validLength = packet.Length - validStartIndex;
            validBytesData = packet[validStartIndex..(validStartIndex + validLength)];
            var validDataStr = BitConverter.ToString(validBytesData);
            Debug.Log($"收到数据包：{packetStr}，有效数据部分：{validDataStr}");
#if UNITY_EDITOR
            packetDataShow = packetStr;
            validDataShow = validDataStr;
#endif
            if (useUnityEvent && serialEvents != null && serialEvents.Count > 0)
                CheckSerialEvent(validBytesData, eventKeyStart, eventKeyLength);
            OnHexPacketReceived?.Invoke(validBytesData);
        }

        /// <summary>
        /// 检查是否触发了串口事件
        /// </summary>
        /// <param name="validData">有效数据</param>
        /// <param name="keyStart">键值起始位置</param>
        /// <param name="keyLength">键值长度</param>
        private void CheckSerialEvent(byte[] validData,int keyStart,int keyLength)
        {
            if (keyStart < 0) throw new Exception($"键值起始位置不可以<0：{keyStart}");
            //转为字符串，获取键值部分
            var validDataStr = Port.BytesConverHex(validData);
            var key = validDataStr[keyStart..(keyStart + keyLength)];
            //比对键值(忽略大小写)，找到符合的串口事件
            var e = serialEvents.Find(e => string.Equals(key, e.key, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(e.name)) Debug.Log($"未找到与{validDataStr}的KEY值{key}对应的串口数据事件，" +
                $"请检查配置表中是否已配置对应的参数");
            else
            {
                OnReceiveSerialByteEvent?.Invoke(e.name, validData);
                Debug.Log($"收到事件：{e.name}，完整数据：{validDataStr}，键值为：{key}");
            }
        }

        /// <summary>
        /// 对获取到的字节组数据进行处理
        /// </summary>
        /// <param name="data">获取到的字节组原数据</param>
        /// <param name="len">有效字节长度</param>
        private void OnReceiveData(byte[] data,int len)
        {
            receiveBytesBuffer.AddRange(data[0..len]);
            var receiveBytes = receiveBytesBuffer.ToArray();
#if UNITY_EDITOR
            receiveDataShow = BitConverter.ToString(receiveBytes);
#endif
            // 如果使用包拆分，则进行拆分数据出有效数据包部分
            if (usePackctHandle && !string.IsNullOrEmpty(packetFlag))
            {
                var str = BitConverter.ToString(receiveBytes).Replace("-", "");
                for (int i = 0;true;)
                {
                    //查找数据包起始标记位置
                    var strStartIndex = str.IndexOf(packetFlag, i,StringComparison.OrdinalIgnoreCase);
                    //如果未找到起始标记，则结束循环
                    if (strStartIndex < 0) break;
                    // 转换为字节索引
                    var startIndex = strStartIndex / 2; 
                    // 找到起始标记，但数据结尾超过了缓冲池长度，说明数据包不完整，丢弃前面的无效数据然后直接返回
                    if (packetLength > 0 && (startIndex + packetLength) > receiveBytesBuffer.Count)
                    {
                        receiveBytesBuffer.RemoveRange(0, startIndex);
#if UNITY_EDITOR
                        Debug.Log($"以{packetFlag}开始的数据包{BitConverter.ToString(receiveBytesBuffer.ToArray())}不完整，已丢弃前{startIndex}个字节的数据以等待拼接剩余数据");
#endif
                        return;
                    }
                    // 存在完整的数据包，对其进行处理
                    // 计算数据包的字节长度，注意是字节长度而不是字符长度
                    var length = packetLength < 0 ? receiveBytes.Length - startIndex: packetLength;
                    //对数据包进行处理
                    PacketHandle(receiveBytes[startIndex..(startIndex + length)],validDataStart,validDataLength);
                    i = strStartIndex + length * 2;
                }
                receiveBytesBuffer.Clear();
            }
            //不使用包拆分时，直接将当前获取到的数据作为一整个包进行解析
            else
            {
                PacketHandle(receiveBytes, validDataStart, validDataLength);
                receiveBytesBuffer.Clear();
            }
        }

        /// <summary>
        /// 对ascii字符串数据进行处理后发送
        /// </summary>
        /// <param name="data">获取到的ascii字符串原数据</param>
        private void OnReceiveData(string data)
        {
            receiveStrData = data;
            //截取出有效数据包部分
            if (validDataLength <= 0) validDataLength = bufferLength;
#if UNITY_EDITOR
            receiveDataShow = data;
            validDataShow = validStrData = receiveStrData[validDataStart..(validDataStart + validDataLength)];
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
            OnStrPacketReceived?.Invoke(validStrData);
        }

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
                    serialPort.Write(Port.HexConverByte(data));
                    break;
                case DataFormat.ascii:
                    serialPort.Write(data);
                    break;
            }
        }
        /// <summary>
        /// 写入字节数组数据
        /// </summary>
        /// <param name="data">16进制数组，为规范起见，请确保数据格式为0xFF</param>
        public void Send(byte[] data)
        {
            if (!serialPort.IsOpen) return;
            if (dataFormat != DataFormat.hex)
                throw new Exception("通信方式不为HEX码传输！");
            serialPort.Write(data);
        }
        /// <summary>
        /// 发送单个16进制数
        /// </summary>
        /// <param name="data">16位数据</param>
        public void Send(byte data)
        {
            var bytes = new byte[1];
            bytes[0] = data;
            serialPort.Write(bytes);
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
        [ButtonGroup("串口配置/配置操作")]
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
            this.dataFormat = info.format;
            //读取端口映射事件
            if (info.serialEvents.Count > 0)
            {
                //this.useUnityEvent = true;
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

        [ButtonGroup("串口配置/配置操作")]
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
        [InfoBox("修改配置后请务必保存配置以确保更改生效", "haveUnsavedChange",InfoMessageType = InfoMessageType.Warning)]
        [HorizontalGroup("串口配置/保存配置")]
        [Button("保存配置")]
        [ContextMenu("保存配置")]
        private void SavePortInfo()
        {
            var info = new PortInfo
            {
                portID = portID,
                baudRate = baudRate,
                format = dataFormat,
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
                    OnReceiveData(bytes,bytes.Length);
                    break;
                case DataFormat.ascii:
                    OnReceiveData(receiveData);
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
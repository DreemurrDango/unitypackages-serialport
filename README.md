# Unity 串口通信模块 (Serial Port Module)

## 概述

本模块为 Unity 项目提供了一套完整、强大且易于配置的串口（COM Port）通信解决方案。它封装了底层的串口操作，提供了两种功能级别的工作组件，并集成了数据包处理、事件转发和校验码生成等高级功能

该模块旨在帮助开发者快速实现 Unity 应用程序与外部硬件设备（如 Arduino、PLC、传感器等）之间稳定可靠的数据交换

## 核心功能

*   **两种模式**: 提供 `SerialPort` (基础) 和 `SerialPortController` (高级) 两种组件，以适应不同复杂度的通信需求
*   **可视化配置**: 基于 Odin Inspector 提供了友好的 Inspector 面板，所有参数（端口号、波特率等）均可轻松配置
*   **外部化配置**: 支持将串口配置保存为 JSON 文件并存放于 `StreamingAssets` 目录，允许在程序打包后动态修改串口参数
*   **数据包解析**: `SerialPortController` 支持基于“起始标志位”和“包长度”的自动数据包拆分与拼接，能有效处理连续、高速的数据流
*   **事件驱动**: 可将接收到的特定数据自动映射为 `UnityEvent`，实现低耦合的事件驱动式编程
*   **校验码支持**: 发送数据时可自动计算并附加 **CRC16**, **XOR**, **SUM** 等多种校验码，确保数据传输的完整性
*   **全局单例访问**: 提供静态的 `GetInstance()` 方法，方便在任何脚本中快速获取串口实例
*   **编辑器内调试**: 提供模拟发送和接收数据的功能，方便在没有物理设备的情况下进行开发和调试

## 组件选择

| 特性 | `SerialPort` (基础组件) | `SerialPortController` (高级组件) |
| :--- | :--- | :--- |
| **适用场景** | 通信频率较低、数据包结构简单固定 | 通信频率高、数据流连续、需要处理粘包/断包问题 |
| **数据处理** | 将接收缓冲区的数据视为一个完整包 | 自动在数据流中寻找包头，拼接和拆分数据包 |
| **核心优势** | 配置简单，快速使用 | 处理高频/复杂数据流，功能更全面 |

**建议**: 如果不确定，可以从 `SerialPort` 开始，它在大部分情况下已经足够使用；如果遇到数据粘包或断包问题，再切换到 `SerialPortController` 以利用其高级功能

---

## 快速开始

### 1. 依赖项

*   **Odin Inspector**: 本模块的编辑器界面依赖于该插件。请确保项目中已导入 Odin Inspector

### 2. 安装与设置

1.  在场景中创建一个新的空对象（例如命名为 "SerialPort"）
2.  将 `SerialPort` (或 `SerialPortController`) 组件添加到该对象上
3.  在 Inspector 面板中配置以下核心参数：
    *   **Port ID**: 硬件设备连接的 COM 端口号（例如 3）
    *   **Baud Rate**: 波特率，必须与硬件设备设置一致（例如 9600）
    *   **Data Format**: 选择 `hex` (十六进制) 或 `ascii` (字符串)
4.  点击 **"保存配置"** 按钮，模块会自动在 `Assets/StreamingAssets/Configs/ComConfigs/` 目录下生成一个与游戏对象同名的 JSON 配置文件

### 3. 发送数据

在你的任何脚本中，通过直接指定或 `GetInstance()` 获取串口实例并调用 `Send` 方法

```csharp 示例代码
using DreemurrStudio.SerialPortSystem;
using UnityEngine;
public class MyDeviceController : MonoBehaviour 
{ 
    void Start() 
    { 
        // 获取串口实例 SerialPortController myPort = SerialPortController.GetInstance("MyDevicePort");
        // 1. 发送表示十六进制值的字符串
        myPort.Send("AA 55 01 00 FF");

        // 2. 发送字节数组
        byte[] data = { 0xAA, 0x55, 0x02, 0x00, 0xFF };
        myPort.Send(data);

        // 3. 发送并自动添加 CRC16 校验码
        // 模块会自动计算 data 的 CRC16 校验码并附加到末尾后发送
        myPort.Send(data, SerialPort.CheckCodeType.CRC16, 2);
    }
}
```

### 4. 接收数据

接收数据主要有两种方式：**C# 事件** 和 **Unity 事件**

#### 方式一：通过 C# Action 订阅 (推荐)

这种方式性能更好，代码更清晰
```csharp 示例代码
using DreemurrStudio.SerialPortSystem;
using UnityEngine; 
using System;

public class MyDataReceiver : MonoBehaviour 
{ 
    private SerialPortController myPort;
    void Start()
    {
        // 获取并缓存串口实例
        myPort = SerialPortController.GetInstance("MyDevicePort");

        // 订阅16进制数据包接收事件
        myPort.OnHexPacketReceived += HandleHexData;
    }

    private void HandleHexData(byte[] data)
    {
        // 在这里处理接收到的有效数据包
        Debug.Log("收到数据包: " + BitConverter.ToString(data));
    }

    void OnDestroy()
    {
        // 销毁时务必取消订阅，以防内存泄漏
        if (myPort != null)
            myPort.OnHexPacketReceived -= HandleHexData;
    }
}
```

#### 方式二：通过 UnityEvent 转发

这种方式允许你在 Inspector 面板中直接拖拽对象和方法，适合快速实现或非程序员使用

1.  在 `SerialPortController` 组件的 Inspector 中，勾选 **"使用 Unity 事件"**
2.  配置 **"事件字符串键值"** 的起始位和长度。例如，如果你的数据包前2个字节是命令代码，则 `eventKeyStart` 设为 0，`eventKeyLength` 设为 4 (2个字节=4个十六进制字符)
3.  在 **"注册的串口事件"** 列表中添加新事件：
    *   **Name**: 定义一个事件名，例如 "ButtonDown"
    *   **Key**: 设置要匹配的键值，例如 "010A"
4.  在 `OnReceiveSerialByteEvent` 事件栏中，点击 "+" 号，将你的脚本组件拖入，并选择要执行的公共方法

现在，当模块收到一个以 `010A` 开头的数据包时，就会自动调用你指定的方法
```csharp 示例代码
using UnityEngine;
public class MyGameEvents : MonoBehaviour 
{ 
    // 这个方法需要是 public 的，以便在 Inspector 中挂载指定
    public void OnSerialEvent(string eventName, byte[] data)
    { 
        if (eventName == "ButtonDown") 
        Debug.Log("接收到按下按钮指令！"); 
    } 
}
```

如果你想测试该模块是否正确地生效，可以打开插件目录下的 `Tests/SerialPortTest.unity` 场景，进入运行模式并结合串口助手软件进行测试

---

## 高级用法：数据包处理

当硬件连续发送数据时，可能会出现多个数据包粘连在一起（"粘包"）或一个数据包被分成几次接收（"断包"）的情况。`SerialPortController` 的包处理功能可以解决这个问题

1.  在 `SerialPortController` 组件的 Inspector 中，勾选 **"使用包处理"**
2.  **Packet Flag**: 设置你的数据包起始标志。例如，如果每个数据包都以 `AA BB` 开头，就填入 `AABB`
3.  **Packet Length**: 设置每个数据包的**固定**字节长度。如果长度可变，请保持 `-1`

启用后，模块会持续在接收到的数据流中搜索 `AABB`，一旦找到，就会根据 `Packet Length` 尝试提取一个完整的数据包，然后才将这个干净的数据包传递给后续的处理逻辑
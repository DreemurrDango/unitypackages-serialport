namespace DreemurrStudio.SerialPortSystem
{
    /// <summary>
    /// 串口数据包校验接口
    /// </summary>
    public interface IPacketCheck
    {
        /// <summary>
        /// 校验16位数组的串口数据包是否有效
        /// </summary>
        /// <param name="data">待检查的数据包</param>
        /// <returns>如果有效则返回true，否则返回false</returns>
        public bool CheckPacketData(byte[] data) => true;

        /// <summary>
        /// 校验字符串的串口数据包是否有效
        /// </summary>
        /// <param name="data">待检查的字符串数据包</param>
        /// <returns>如果有效则返回true，否则返回false</returns>
        public bool CheckPacketData(string data) => true;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SocketCommon
{
    public class AsyncUserToken
    {
        /// <summary>  
        /// 客户端IP地址  
        /// </summary>  
        public IPAddress IPAddress { get; set; }

        /// <summary>  
        /// 远程地址  
        /// </summary>  
        public EndPoint Remote { get; set; }

        /// <summary>  
        /// 通信SOKET  
        /// </summary>  
        public Socket Socket { get; set; }

        /// <summary>  
        /// 连接创建时间  
        /// </summary>  
        public DateTime ConnectTime { get; set; }

        /// <summary>  
        /// 数据缓存区  
        /// </summary>  
        public byte[] Buffer { get; set; }
        /// <summary>
        /// 拷贝索引
        /// </summary>
        public int CopyOffset { get; set; }

        /// <summary>
        /// 当前会话包总长度
        /// </summary>
        public int PackageLength { get; set; }

        /// <summary>
        /// 组id
        /// </summary>
        public string GroupId { get; set; }

    }
}

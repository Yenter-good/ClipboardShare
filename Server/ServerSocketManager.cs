using SocketCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Server
{
    public class ServerSocketManager
    {
        private int m_maxConnectNum;    //最大连接数  
        private int m_revBufferSize;    //最大接收字节数  
        BufferManager m_bufferManager;
        const int opsToAlloc = 2;
        Socket listenSocket;            //监听Socket  
        SocketEventPool m_pool;
        int m_clientCount;              //连接的客户端数量  
        Semaphore m_maxNumberAcceptedClients;

        Dictionary<string, List<AsyncUserToken>> m_clients; //客户端列表  
        byte[] lenBytes = new byte[4];

        #region 定义委托  

        /// <summary>  
        /// 接收到客户端的数据  
        /// </summary>  
        /// <param name="token">客户端</param>  
        /// <param name="buff">客户端数据</param>  
        public delegate void OnReceiveData(string groupId, string userId, TransferStructure recv);

        /// <summary>
        /// 客户端数据接收开始时
        /// </summary>
        /// <param name="token"></param>
        /// <param name="size"></param>
        public delegate void OnRecieveStart(EndPoint remoteEP, int hash, int size);

        /// <summary>
        /// 客户端数据接收进度
        /// </summary>
        /// <param name="token"></param>
        /// <param name="size"></param>
        public delegate void OnRecieveProcess(EndPoint remoteEP, int hash, float process);

        /// <summary>
        /// 客户端数据接收结束时
        /// </summary>
        /// <param name="token"></param>
        public delegate void OnRecieveEnd(EndPoint remoteEP, int hash);
        #endregion

        #region 定义事件  
        /// <summary>  
        /// 接收到客户端的数据事件  
        /// </summary>  
        public event OnReceiveData ReceiveClientData;

        /// <summary>  
        /// 客户端数据接收开始时触发  
        /// </summary>  
        public event OnRecieveStart RecieveStart;

        /// <summary>  
        /// 客户端数据接收结束时触发  
        /// </summary>  
        public event OnRecieveEnd RecieveEnd;

        /// <summary>  
        /// 客户端数据接收进度
        /// </summary>  
        public event OnRecieveProcess RecieveProcess;
        #endregion

        /// <summary>  
        /// 构造函数  
        /// </summary>  
        /// <param name="numConnections">最大连接数</param>  
        /// <param name="receiveBufferSize">缓存区大小</param>  
        public ServerSocketManager(int numConnections, int receiveBufferSize)
        {
            m_clientCount = 0;
            m_maxConnectNum = numConnections;
            m_revBufferSize = receiveBufferSize;
            // 分配缓冲区，以便最大数量的套接字可以同时向套接字发送一个未完成的读和写
            m_bufferManager = new BufferManager(receiveBufferSize * numConnections * opsToAlloc, receiveBufferSize);

            m_pool = new SocketEventPool(20, false);
            m_maxNumberAcceptedClients = new Semaphore(numConnections, numConnections);
        }

        /// <summary>  
        /// 初始化  
        /// </summary>  
        public void Init()
        {
            // 分配一个大字节缓冲区，所有I/O操作都使用它。这有助于防止内存碎片
            m_bufferManager.InitBuffer();
            m_clients = new Dictionary<string, List<AsyncUserToken>>();
            // 预分配SocketAsyncEventArgs对象池

            for (int i = 0; i < m_maxConnectNum; i++)
            {
                var readWriteEventArg = new SocketAsyncEventArgs();
                m_bufferManager.SetBuffer(readWriteEventArg);
                m_pool.Push(readWriteEventArg);
            }
        }


        /// <summary>  
        /// 启动服务  
        /// </summary>  
        /// <param name="localEndPoint"></param>  
        public bool Start(IPEndPoint localEndPoint)
        {
            try
            {
                m_clients = new Dictionary<string, List<AsyncUserToken>>();
                listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                listenSocket.Bind(localEndPoint);
                // listen启动服务器
                listenSocket.Listen(20);
                // post接受监听套接字
                StartAccept(null);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>  
        /// 停止服务  
        /// </summary>  
        public void Stop()
        {
            foreach (var item in m_clients)
            {
                try
                {
                    foreach (var token in item.Value)
                        this.CloseClient(token);
                }
                catch (Exception) { }
            }
            try
            {
                listenSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception) { }

            listenSocket.Close();
            lock (m_clients) { m_clients.Clear(); }
        }


        public void CloseClient(AsyncUserToken token)
        {
            try
            {
                token.Socket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            finally
            {
                m_clients[token.GroupId].Remove(token);
            }
        }


        /// <summary>
        /// 开始接受来自客户端的连接请求的操作
        /// </summary>
        /// <param name="acceptEventArg">在服务器的监听套接字上发出accept操作时使用的上下文对象</param>
        void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
            }
            else
            {
                // 必须清除套接字，因为上下文对象正在被重用
                acceptEventArg.AcceptSocket = null;
            }

            m_maxNumberAcceptedClients.WaitOne();
            if (!listenSocket.AcceptAsync(acceptEventArg))
            {
                ProcessAccept(acceptEventArg);
            }
        }

        /// <summary>
        /// 此方法是与套接字关联的回调方法。AcceptAsync操作，并在accept操作完成时调用
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            try
            {
                Interlocked.Increment(ref m_clientCount);
                // 获取接受的客户端连接的套接字，并将其放入ReadEventArg对象用户令牌中
                SocketAsyncEventArgs readEventArgs = m_pool.Pop();
                readEventArgs.Completed += IO_Completed;

                AsyncUserToken userToken = (AsyncUserToken)readEventArgs.UserToken;
                if (userToken == null)
                {
                    userToken = new AsyncUserToken();
                    readEventArgs.UserToken = userToken;
                }
                userToken.Socket = e.AcceptSocket;
                userToken.ConnectTime = DateTime.Now;
                userToken.Remote = e.AcceptSocket.RemoteEndPoint;
                userToken.IPAddress = ((IPEndPoint)(e.AcceptSocket.RemoteEndPoint)).Address;

                if (!userToken.Socket.ReceiveAsync(readEventArgs))
                {
                    ProcessReceive(readEventArgs, userToken);
                }
            }
            catch (Exception ex)
            {
            }

            // 接受下一个连接请求
            if (e.SocketError == SocketError.OperationAborted) return;
            StartAccept(e);
        }


        void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            // 确定刚刚完成的操作类型并调用关联的处理程序
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    var token = (AsyncUserToken)e.UserToken;
                    ProcessReceive(e, token);
                    if (token.Socket != null && token.Socket.Connected)
                        token.Socket.ReceiveAsync(e);
                    else
                        CloseClient(token);
                    break;
            }

        }


        /// <summary>
        /// 此方法在异步接收操作完成时调用。
        /// 如果远程主机关闭了连接，则套接字关闭。
        /// 如果接收到数据，则将数据回显到客户机。
        /// </summary>
        /// <param name="e"></param>
        private void ProcessReceive(SocketAsyncEventArgs e, AsyncUserToken currentToken)
        {
            // 检查远程主机是否关闭了连接
            try
            {
                if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
                {

                    if (currentToken.GroupId != null)
                    {
                        var tokens = m_clients[currentToken.GroupId];
                        foreach (var token in tokens)
                        {
                            //if (token == currentToken)
                            //    continue;
                            this.SendMessageWithoutHead(token, e.Buffer, e.Offset, e.BytesTransferred);
                        }

                        return;
                    }

                    if (currentToken.PackageLength == 0)
                    {
                        Array.Copy(e.Buffer, e.Offset, lenBytes, 0, 4);
                        currentToken.PackageLength = BitConverter.ToInt32(lenBytes, 0);
                        currentToken.Buffer = new byte[currentToken.PackageLength];
                        currentToken.CopyOffset = 0;

                        Array.Copy(e.Buffer, e.Offset + 4, currentToken.Buffer, currentToken.CopyOffset, e.BytesTransferred - 4);
                        currentToken.CopyOffset += e.BytesTransferred - 4;
                    }
                    else
                    {
                        Array.Copy(e.Buffer, e.Offset, currentToken.Buffer, currentToken.CopyOffset, e.BytesTransferred);
                        currentToken.CopyOffset += e.BytesTransferred;
                    }

                    //判断包的长度  
                    if (currentToken.PackageLength <= currentToken.Buffer.Length)
                    {
                        //包够长时,则提取出来,交给后面的程序去处理  
                        var result = currentToken.Buffer.BeginDeserialize<TransferStructure>();

                        //从数据池中移除这组数据  
                        lock (currentToken.Buffer)
                        {
                            currentToken.Buffer = null;
                            currentToken.CopyOffset = 0;
                            currentToken.PackageLength = 0;
                            GC.Collect();
                        }

                        if (result.TransferProtocol == TransferProtocol.GroupId)
                        {
                            var groupId = result.Data.ToString();
                            currentToken.GroupId = groupId;

                            if (!m_clients.ContainsKey(groupId))
                                m_clients[groupId] = new List<AsyncUserToken>();
                            m_clients[groupId].Add(currentToken);
                        }
                    }

                }
                else
                {
                    CloseClientSocket(e);
                }
            }
            catch (Exception ex)
            {
                lock (currentToken.Buffer)
                {
                    currentToken.Buffer = null;
                    currentToken.PackageLength = 0;
                    GC.Collect();
                }
            }
        }

        //关闭客户端  
        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            AsyncUserToken token = e.UserToken as AsyncUserToken;
            token.Buffer = null;
            token.Buffer = null;
            GC.Collect();
            lock (m_clients)
            {
                foreach (var item in m_clients)
                {
                    if (item.Value.Contains(token))
                        item.Value.Remove(token);
                }
            }
            //如果有事件,则调用事件,发送客户端数量变化通知  
            // 关闭与客户端关联的套接字
            try
            {
                token.Socket.Close();
            }
            catch (Exception) { }
            // 减量计数器跟踪连接到服务器的客户机总数
            Interlocked.Decrement(ref m_clientCount);
            m_maxNumberAcceptedClients.Release();
            // 释放SocketAsyncEventArg，以便其他客户端可以重用它们
            e.UserToken = new AsyncUserToken();
            m_pool.Push(e);
        }

        private void SendMessageWithoutHead(AsyncUserToken token, byte[] message, int offset, int length)
        {
            if (token == null || token.Socket == null || !token.Socket.Connected)
                return;
            try
            {
                //新建异步发送对象, 发送消息  
                SocketAsyncEventArgs sendArg = new SocketAsyncEventArgs();
                sendArg.UserToken = token;
                sendArg.SetBuffer(message, offset, length);  //将数据放置进去.  
                token.Socket.SendAsync(sendArg);
                GC.Collect();
            }
            catch
            {
            }
        }
    }
}

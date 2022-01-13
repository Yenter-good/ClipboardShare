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
    public class SocketManager
    {
        private int m_maxConnectNum;    //最大连接数  
        private int m_revBufferSize;    //最大接收字节数  
        BufferManager m_bufferManager;
        const int opsToAlloc = 2;
        Socket listenSocket;            //监听Socket  
        SocketEventPool m_pool;
        int m_clientCount;              //连接的客户端数量  
        Semaphore m_maxNumberAcceptedClients;

        List<AsyncUserToken> m_clients; //客户端列表  

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

        #region 定义属性  

        /// <summary>  
        /// 获取客户端列表  
        /// </summary>  
        public List<AsyncUserToken> ClientList { get { return m_clients; } }

        #endregion

        /// <summary>  
        /// 构造函数  
        /// </summary>  
        /// <param name="numConnections">最大连接数</param>  
        /// <param name="receiveBufferSize">缓存区大小</param>  
        public SocketManager(int numConnections, int receiveBufferSize)
        {
            m_clientCount = 0;
            m_maxConnectNum = numConnections;
            m_revBufferSize = receiveBufferSize;
            // 分配缓冲区，以便最大数量的套接字可以同时向套接字发送一个未完成的读和写
            m_bufferManager = new BufferManager(receiveBufferSize * numConnections * opsToAlloc, receiveBufferSize);

            m_pool = new SocketEventPool(numConnections);
            m_maxNumberAcceptedClients = new Semaphore(numConnections, numConnections);
        }

        /// <summary>  
        /// 初始化  
        /// </summary>  
        public void Init()
        {
            // 分配一个大字节缓冲区，所有I/O操作都使用它。这有助于防止内存碎片
            m_bufferManager.InitBuffer();
            m_clients = new List<AsyncUserToken>();
            // 预分配SocketAsyncEventArgs对象池
            SocketAsyncEventArgs readWriteEventArg;

            for (int i = 0; i < m_maxConnectNum; i++)
            {
                readWriteEventArg = new SocketAsyncEventArgs();
                readWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                readWriteEventArg.UserToken = new AsyncUserToken();

                // 将缓冲池中的字节缓冲区分配给SocketAsyncEventArg对象
                m_bufferManager.SetBuffer(readWriteEventArg);
                // 将SocketAsyncEventArg添加到池中
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
                m_clients = new List<AsyncUserToken>();
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
            foreach (AsyncUserToken token in m_clients)
            {
                try
                {
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
            int c_count = m_clients.Count;
            lock (m_clients) { m_clients.Clear(); }
        }


        public void CloseClient(AsyncUserToken token)
        {
            try
            {
                token.Socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception) { }
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

                lock (m_clients) { m_clients.Add(userToken); }

                if (!e.AcceptSocket.ReceiveAsync(readEventArgs))
                {
                    ProcessReceive(readEventArgs);
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
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
                default:
                    throw new ArgumentException("在套接字上完成的最后一个操作不是接收或发送");
            }

        }


        /// <summary>
        /// 此方法在异步接收操作完成时调用。
        /// 如果远程主机关闭了连接，则套接字关闭。
        /// 如果接收到数据，则将数据回显到客户机。
        /// </summary>
        /// <param name="e"></param>
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            // 检查远程主机是否关闭了连接
            AsyncUserToken token = (AsyncUserToken)e.UserToken;
            try
            {
                if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
                {
                    if (!token.BeginAccept)
                    {
                        byte[] lenBytes = new byte[4];
                        Array.Copy(e.Buffer, e.Offset, lenBytes, 0, 4);
                        token.PackageLength = BitConverter.ToInt32(lenBytes, 0);
                        RecieveStart?.BeginInvoke(token.Remote, token.GetHashCode(), token.PackageLength, null, null);
                    }
                    token.BeginAccept = true;

                    //读取数据  
                    byte[] data = new byte[e.BytesTransferred];
                    Array.Copy(e.Buffer, e.Offset, data, 0, e.BytesTransferred);
                    lock (token.Buffer)
                    {
                        token.Buffer.AddRange(data);
                    }

                    //判断包的长度  
                    if (token.PackageLength > token.Buffer.Count - 4)
                    {
                        //触发进度更改事件 
                        RecieveProcess?.BeginInvoke(token.Remote, token.GetHashCode(), token.Buffer.Count * 1.0f / token.PackageLength, null, null);
                        //长度不够时,退出循环,让程序继续接收  
                    }
                    else
                    {  //包够长时,则提取出来,交给后面的程序去处理  
                        byte[] rev = token.Buffer.GetRange(4, token.PackageLength).ToArray();

                        //从数据池中移除这组数据  
                        lock (token.Buffer)
                        {
                            token.Buffer.Clear();
                            token.PackageLength = 0;
                            token.BeginAccept = false;
                            GC.Collect();
                        }

                        var result = rev.BeginDeserialize<TransferStructure>();
                        if (result.TransferProtocol != TransferProtocol.GroupId)
                        {
                            //将数据包交给后台处理 
                            ReceiveClientData?.BeginInvoke(token.GroupId, token.UserId, result, null, null);
                            RecieveEnd?.BeginInvoke(token.Remote, token.GetHashCode(), null, null);
                        }
                        else if (result.TransferProtocol == TransferProtocol.GroupId)
                        {
                            var ids = result.Data as string[];
                            token.GroupId = ids[0];
                            token.UserId = ids[1];
                        }
                        //这里API处理完后,并没有返回结果,当然结果是要返回的,却不是在这里, 这里的代码只管接收.  
                        //若要返回结果,可在API处理中调用此类对象的SendMessage方法,统一打包发送.不要被微软的示例给迷惑了.  
                    }

                    //继续接收
                    if (!token.Socket.ReceiveAsync(e))
                        this.ProcessReceive(e);
                }
                else
                {
                    CloseClientSocket(e);
                }
            }
            catch (Exception ex)
            {
                lock (token.Buffer)
                {
                    token.Buffer.Clear();
                    token.PackageLength = 0;
                    token.BeginAccept = false;
                    GC.Collect();
                }
            }
        }

        /// <summary>
        /// 此方法在异步发送操作完成时调用。
        /// 该方法在套接字上发出另一个receive，以读取从客户机发送的任何附加数据
        /// </summary>
        /// <param name="e"></param>
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                // 完成将数据回传给客户端
                AsyncUserToken token = (AsyncUserToken)e.UserToken;
                // 读取从客户端发送的下一个数据块
                bool willRaiseEvent = token.Socket.ReceiveAsync(e);
                if (!willRaiseEvent)
                {
                    ProcessReceive(e);
                }
            }
            else
            {
                CloseClientSocket(e);
            }
        }

        //关闭客户端  
        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            AsyncUserToken token = e.UserToken as AsyncUserToken;
            token.Buffer.Clear();
            token.Buffer = null;
            GC.Collect();
            lock (m_clients) { m_clients.Remove(token); }
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

        public int SendMessage(string groupId, string userId, TransferStructure data)
        {
            var buffer = data.BeginSerializable();
            var tokens = m_clients.Where(p => p.GroupId == groupId );
            foreach (var token in tokens)
            {
                this.SendMessage(token, buffer);
            }
            return tokens.Count();
        }

        /// <summary>  
        /// 对数据进行打包,然后再发送  
        /// </summary>  
        /// <param name="token"></param>  
        /// <param name="message"></param>  
        /// <returns></returns>  
        private void SendMessage(AsyncUserToken token, byte[] message)
        {
            if (token == null || token.Socket == null || !token.Socket.Connected)
                return;
            try
            {
                //对要发送的消息,制定简单协议,头4字节指定包的大小,方便客户端接收(协议可以自己定)  
                byte[] buff = new byte[message.Length + 4];
                byte[] len = BitConverter.GetBytes(message.Length);
                Array.Copy(len, buff, 4);
                Array.Copy(message, 0, buff, 4, message.Length);
                //新建异步发送对象, 发送消息  
                SocketAsyncEventArgs sendArg = new SocketAsyncEventArgs();
                sendArg.UserToken = token;
                sendArg.SetBuffer(buff, 0, buff.Length);  //将数据放置进去.  
                token.Socket.SendAsync(sendArg);
            }
            catch
            {
            }
        }
    }
}

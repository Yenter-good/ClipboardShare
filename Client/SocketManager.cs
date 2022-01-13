using SocketCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Client
{
    public class SocketManager : IDisposable
    {
        private const int BuffSize = 1024;

        /// <summary>
        /// 用于发送/接收消息的套接字。
        /// </summary>
        private Socket clientSocket;

        /// <summary>
        /// 已连接套接字的标志。
        /// </summary>
        private Boolean connected = false;

        private IPEndPoint hostEndPoint;

        private static AutoResetEvent autoConnectEvent = new AutoResetEvent(false);

        BufferManager m_bufferManager;
        //定义接收数据的对象
        List<byte> m_buffer;
        //发送与接收的MySocketEventArgs变量定义.
        private List<MySocketAsyncEventArgs> listArgs = new List<MySocketAsyncEventArgs>();
        private MySocketAsyncEventArgs receiveEventArgs = new MySocketAsyncEventArgs();
        int tagCount = 0;

        /// <summary>
        /// 当前连接状态
        /// </summary>
        public bool Connected { get { return clientSocket != null && clientSocket.Connected; } }

        //服务器主动发出数据受理委托及事件
        public delegate void OnServerDataReceived(byte[] receiveBuff);
        public event OnServerDataReceived ServerDataHandler;

        //服务器主动关闭连接委托及事件
        public delegate void OnServerStop();
        public event OnServerStop ServerStopEvent;

        /// <summary>
        /// 创建未初始化的客户端实例。要启动发送/接收处理，请调用Connect方法，然后调用SendReceive方法。
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public SocketManager(string ip, int port)
        {
            // 实例化端点和套接字。
            hostEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            clientSocket = new Socket(hostEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            m_bufferManager = new BufferManager(BuffSize * 2, BuffSize);
            m_buffer = new List<byte>();
        }

        /// <summary>
        /// 连接到主机
        /// </summary>
        /// <returns>0.连接成功, 其他值失败,参考SocketError的值列表</returns>
        public SocketError Connect(string groupId, string userId)
        {
            SocketAsyncEventArgs connectArgs = new SocketAsyncEventArgs();

            connectArgs.RemoteEndPoint = hostEndPoint;
            connectArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnConnect);

            clientSocket.ConnectAsync(connectArgs);
            autoConnectEvent.WaitOne(); //阻塞. 让程序在这里等待,直到连接响应后再返回连接结果
            if (connectArgs.SocketError == SocketError.Success)
                this.Send(new TransferStructure() { TransferProtocol = TransferProtocol.GroupId, Data = new string[] { groupId, userId } });
            return connectArgs.SocketError;
        }

        public void Disconnect()
        {
            if (clientSocket.Connected)
                clientSocket.Disconnect(false);
        }

        /// <summary>
        /// 连接操作的Calback
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConnect(object sender, SocketAsyncEventArgs e)
        {
            // 表示连接结束。
            autoConnectEvent.Set(); //释放阻塞.
            // 设置socket connected的标志。
            connected = (e.SocketError == SocketError.Success);
            //如果连接成功,则初始化socketAsyncEventArgs
            if (connected)
                initArgs(e);
        }


        #region args

        /// <summary>
        /// 初始化收发参数
        /// </summary>
        /// <param name="e"></param>
        private void initArgs(SocketAsyncEventArgs e)
        {
            m_bufferManager.InitBuffer();
            //发送参数
            initSendArgs();
            //接收参数
            receiveEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
            receiveEventArgs.UserToken = clientSocket;
            receiveEventArgs.ArgsTag = 0;
            m_bufferManager.SetBuffer(receiveEventArgs);

            //启动接收,不管有没有,一定得启动.否则有数据来了也不知道.
            if (!e.ConnectSocket.ReceiveAsync(receiveEventArgs))
                ProcessReceive(receiveEventArgs);
        }

        /// <summary>
        /// 初始化发送参数MySocketEventArgs
        /// </summary>
        /// <returns></returns>
        MySocketAsyncEventArgs initSendArgs()
        {
            MySocketAsyncEventArgs sendArg = new MySocketAsyncEventArgs();
            sendArg.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
            sendArg.UserToken = clientSocket;
            sendArg.RemoteEndPoint = hostEndPoint;
            sendArg.IsUsing = false;
            Interlocked.Increment(ref tagCount);
            sendArg.ArgsTag = tagCount;
            lock (listArgs)
            {
                listArgs.Add(sendArg);
            }
            return sendArg;
        }



        void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            MySocketAsyncEventArgs mys = (MySocketAsyncEventArgs)e;
            // 确定刚刚完成的操作类型并调用关联的处理程序
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    mys.IsUsing = false; //数据发送已完成.状态设为False
                    ProcessSend(e);
                    break;
                default:
                    throw new ArgumentException("在套接字上完成的最后一个操作不是接收或发送");
            }
        }

        //
        /// <summary>
        /// 此方法在异步接收操作完成时调用。
        /// 如果远程主机关闭了连接，则套接字将关闭。
        /// 如果接收到数据，则将数据回传给客户端。
        /// </summary>
        /// <param name="e"></param>
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            try
            {
                // 检查远程主机是否已关闭连接
                Socket token = (Socket)e.UserToken;
                if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
                {
                    //读取数据
                    byte[] data = new byte[e.BytesTransferred];
                    Array.Copy(e.Buffer, e.Offset, data, 0, e.BytesTransferred);
                    lock (m_buffer)
                    {
                        m_buffer.AddRange(data);
                    }

                    do
                    {
                        //注意: 这里是需要和服务器有协议的,我做了个简单的协议,就是一个完整的包是包长(4字节)+包数据,便于处理,当然你可以定义自己需要的; 
                        //判断包的长度,前面4个字节.
                        byte[] lenBytes = m_buffer.GetRange(0, 4).ToArray();
                        int packageLen = BitConverter.ToInt32(lenBytes, 0);
                        if (packageLen <= m_buffer.Count - 4)
                        {
                            //包够长时,则提取出来,交给后面的程序去处理
                            byte[] rev = m_buffer.GetRange(4, packageLen).ToArray();
                            //从数据池中移除这组数据,为什么要lock,你懂的
                            lock (m_buffer)
                            {
                                m_buffer.RemoveRange(0, packageLen + 4);
                            }
                            //将数据包交给前台去处理
                            DoReceiveEvent(rev);
                        }
                        else
                        {   //长度不够,还得继续接收,需要跳出循环
                            break;
                        }
                    } while (m_buffer.Count > 4);
                    //注意:你一定会问,这里为什么要用do-while循环?   
                    //如果当服务端发送大数据流的时候,e.BytesTransferred的大小就会比服务端发送过来的完整包要小,  
                    //需要分多次接收.所以收到包的时候,先判断包头的大小.够一个完整的包再处理.  
                    //如果服务器短时间内发送多个小数据包时, 这里可能会一次性把他们全收了.  
                    //这样如果没有一个循环来控制,那么只会处理第一个包,  
                    //剩下的包全部留在m_buffer中了,只有等下一个数据包过来后,才会放出一个来.
                    //继续接收
                    if (!token.ReceiveAsync(e))
                        this.ProcessReceive(e);
                }
                else
                {
                    ProcessError(e);
                }
            }
            catch (Exception xe)
            {
                Console.WriteLine(xe.Message);
            }
        }

        /// <summary>
        /// 此方法在异步发送操作完成时调用。
        /// 该方法在套接字上发出另一个接收，以读取从客户端发送的任何其他数据
        /// </summary>
        /// <param name="e"></param>
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                ProcessError(e);
            }
        }

        #endregion

        #region read write

        /// <summary>
        /// 如果出现故障，关闭套接字，并根据SocketError抛出SockeException。
        /// </summary>
        /// <param name="e"></param>
        private void ProcessError(SocketAsyncEventArgs e)
        {
            Socket s = (Socket)e.UserToken;
            if (s == null)
            {
                ServerStopEvent.Invoke();
                return;
            }
            if (s.Connected)
            {
                // 关闭与客户端关联的套接字
                try
                {
                    s.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                }
                finally
                {
                    if (s.Connected)
                    {
                        s.Close();
                    }
                    connected = false;
                }
            }
            //这里一定要记得把事件移走,如果不移走,当断开服务器后再次连接上,会造成多次事件触发.
            foreach (MySocketAsyncEventArgs arg in listArgs)
                arg.Completed -= IO_Completed;
            receiveEventArgs.Completed -= IO_Completed;

            if (ServerStopEvent != null)
                ServerStopEvent();
        }

        /// <summary>
        /// 与主机交换消息。
        /// </summary>
        /// <param name="sendBuffer"></param>
        public void Send(byte[] sendBuffer)
        {
            if (connected)
            {
                //先对数据进行包装,就是把包的大小作为头加入,这必须与服务器端的协议保持一致,否则造成服务器无法处理数据.
                byte[] buff = new byte[sendBuffer.Length + 4];
                Array.Copy(BitConverter.GetBytes(sendBuffer.Length), buff, 4);
                Array.Copy(sendBuffer, 0, buff, 4, sendBuffer.Length);
                //查找有没有空闲的发送MySocketEventArgs,有就直接拿来用,没有就创建新的.So easy!
                MySocketAsyncEventArgs sendArgs = listArgs.Find(a => a.IsUsing == false);
                if (sendArgs == null)
                {
                    sendArgs = initSendArgs();
                }
                lock (sendArgs) //要锁定,不锁定让别的线程抢走了就不妙了.
                {
                    sendArgs.IsUsing = true;
                    sendArgs.SetBuffer(buff, 0, buff.Length);
                }
                clientSocket.SendAsync(sendArgs);
            }
            else
            {
                throw new SocketException((Int32)SocketError.NotConnected);
            }
        }

        public void Send(TransferStructure data)
        {
            var buffer = data.BeginSerializable();

            this.Send(buffer);
        }

        /// <summary>
        /// 使用新进程通知事件回调
        /// </summary>
        /// <param name="buff"></param>
        private void DoReceiveEvent(byte[] buff)
        {
            if (ServerDataHandler == null) return;
            //ServerDataHandler(buff); //可直接调用.
            //但我更喜欢用新的线程,这样不拖延接收新数据.
            Thread thread = new Thread(new ParameterizedThreadStart((obj) =>
            {
                ServerDataHandler((byte[])obj);
            }));
            thread.IsBackground = true;
            thread.Start(buff);
        }

        #endregion

        #region IDisposable Members

        // 释放资源
        public void Dispose()
        {
            autoConnectEvent.Close();
            if (clientSocket.Connected)
            {
                clientSocket.Close();
            }
        }

        #endregion
    }
}

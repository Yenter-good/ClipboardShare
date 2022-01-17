using Server;
using SocketCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace ClipboardShareServer
{
    internal class Program
    {
        private static ServerSocketManager _sm;

        static void Main(string[] args)
        {
            InitServer();
            while (true)
            {
                Console.ReadLine();
            }
        }

        private static void InitServer()
        {
            _sm = new ServerSocketManager(100, 2048);
            _sm.Init();
            _sm.ReceiveClientData += sm_ReceiveClientData;
            _sm.Start(new IPEndPoint(IPAddress.Any, 9999));
        }

        private static void sm_ReceiveClientData(string groupId, string userId, TransferStructure recv)
        {
            //var count = _sm.SendMessage(groupId, userId, recv);
            //if (count != 0)
            //    Console.WriteLine($"{DateTime.Now}:组<{groupId}>正在转发到{count}个客户端");
        }
    }
}

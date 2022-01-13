using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SocketCommon
{
    [Serializable]
    public class TransferStructure
    {
        public object Data { get; set; }
        public TransferProtocol TransferProtocol { get; set; }
    }
    public enum TransferProtocol
    {
        SyncClipboard,
        GroupId,
        UserId,
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SocketCommon
{
    [Serializable]
    public class ClipboardData : EventArgs
    {
        public string Type { get; set; }
        public object Data { get; set; }

        public new static bool Equals(object a, object b)
        {
            if (a is MemoryStream stream && b is MemoryStream stream1)
                return MemoryStreamEquals(stream, stream1);
            else if (a is Array array && b is Array array1)
                return ArrayEquals(array, array1);

            return a.ToString() == b.ToString();
        }

        private static bool ArrayEquals(Array array, Array array1)
        {
            if (array.Length != array1.Length)
                return false;
            var same = true;
            for (int i = 0; i < array.Length; i++)
                same &= Equals(array.GetValue(i), array1.GetValue(i));
            return same;
        }

        private static bool MemoryStreamEquals(MemoryStream a, MemoryStream b)
        {
            if (a.Length != b.Length)
                return false;

            var aData = a.ReadByte();
            var same = true;

            while (aData != -1)
            {
                if (aData != b.ReadByte())
                {
                    same = false;
                    break;
                }
                aData = a.ReadByte();
            }

            a.Seek(0, SeekOrigin.Begin);
            b.Seek(0, SeekOrigin.Begin);

            return same;
        }

    }

}

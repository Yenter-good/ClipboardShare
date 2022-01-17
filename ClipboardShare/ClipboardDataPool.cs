using SocketCommon;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClipboardShare
{
    internal class ClipboardDataPool
    {
        private Stack<ClipboardData> _stack;
        private int _count;

        public ClipboardDataPool(int count)
        {
            _count = count;
            _stack = new Stack<ClipboardData>();
            for (int i = 0; i < count; i++)
                _stack.Push(new ClipboardData());
        }

        public void Push(ClipboardData data)
        {
            data.Type = null;
            data.Data = null;
            if (_stack.Count < _count)
                _stack.Push(data);
        }

        public ClipboardData Pull()
        {
            if (_stack.Count <= 0)
                return new ClipboardData();
            return _stack.Pop();
        }
    }
}

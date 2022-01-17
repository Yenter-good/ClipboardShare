using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace SocketCommon
{
    public class SocketEventPool
    {
        ConcurrentStack<SocketAsyncEventArgs> m_pool;


        public SocketEventPool(int capacity, bool init)
        {
            m_pool = new ConcurrentStack<SocketAsyncEventArgs>();
            if (init)
            {
                for (int i = 0; i < capacity; i++)
                    m_pool.Push(new SocketAsyncEventArgs());
            }
        }

        public void Push(SocketAsyncEventArgs item)
        {
            if (item == null) { throw new ArgumentNullException("添加到SocketAsyncEventArgsPool的项不能为空"); }
            lock (m_pool)
            {
                m_pool.Push(item);
            }
        }

        /// <summary>
        /// 从池中删除SocketAsyncEventArgs实例并返回从池中删除的对象
        /// </summary>
        /// <returns></returns>
        public SocketAsyncEventArgs Pop()
        {
            lock (m_pool)
            {
                var success = m_pool.TryPop(out var args);
                if (success)
                    return args;
                else
                    return new SocketAsyncEventArgs();
            }
        }

        /// <summary>
        /// 池中SocketAsyncEventArgs实例的数量
        /// </summary>
        public int Count
        {
            get { return m_pool.Count; }
        }

        public void Clear()
        {
            m_pool.Clear();
        }
    }
}

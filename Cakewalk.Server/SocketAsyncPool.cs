using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using Cakewalk.Shared.Packets;
using System.Collections.Concurrent;
using System.Threading;

namespace Cakewalk.Server
{
    /// <summary>
    /// Pool for socket async context objects that share the same memory buffer
    /// </summary>
    public class SocketAsyncPool
    {
        /// <summary>
        /// Memory pool for all send / receive buffers
        /// </summary>
        private byte[] m_buffer;

        /// <summary>
        /// GCHandle for the buffer, so that it can be pinned.
        /// </summary>
        private GCHandle m_bufferHandle;

        /// <summary>
        /// Pool of async context objects
        /// </summary>
        private SocketAsyncEventArgs[] m_eventArgs;

        /// <summary>
        /// Queue of which objects are free
        /// </summary>
        private ConcurrentQueue<int> m_freeObjects;

        /// <summary>
        /// How many bytes each buffer contains
        /// </summary>
        private int m_bufferSize;

        /// <summary>
        /// Create a pool of async socket context objects
        /// </summary>
        public SocketAsyncPool(int bufferSize, int maxCount)
        {
            m_bufferSize = bufferSize;

            //Allocate and pin memory pool
            m_buffer = new byte[maxCount * bufferSize];
            m_bufferHandle = GCHandle.Alloc(m_buffer, GCHandleType.Pinned);

            m_eventArgs = new SocketAsyncEventArgs[maxCount];
            m_freeObjects = new ConcurrentQueue<int>();

            //Setup all context objects with their buffers
            for (int i = 0; i < maxCount; i++)
            {
                m_eventArgs[i] = new SocketAsyncEventArgs();
                m_eventArgs[i].UserToken = i;
                m_eventArgs[i].SetBuffer(m_buffer, i*bufferSize, bufferSize);

                m_freeObjects.Enqueue(i);
            }
        }

        /// <summary>
        /// Get a context object from the pool
        /// </summary>
        public SocketAsyncEventArgs GetArgs()
        {
            int freeIndex = 0;

            while (!m_freeObjects.TryDequeue(out freeIndex))
            {
                Thread.Sleep(0);
            }

            return m_eventArgs[freeIndex];
        }

        /// <summary>
        /// Return a context object to the pool
        /// </summary>
        /// <param name="args"></param>
        public void ReturnArgs(SocketAsyncEventArgs args)
        {
            m_freeObjects.Enqueue((int)args.UserToken);
        }
    }
}

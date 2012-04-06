using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Cakewalk.Server;
using Cakewalk.Shared;
using Cakewalk.Shared.Packets;
using Cakewalk.Server.Zones;
using System.Collections.Generic;

namespace Cakewalk
{
    /// <summary>
    /// The heart of the server. Handles everybody connected.
    /// </summary>
    public class World
    {
        /// <summary>
        /// How fast the world will try to update
        /// </summary>
        private const int WORLD_UPDATE_TARGET_MS = 50;

        /// <summary>
        /// The amount of currently connected users
        /// </summary>
        public int CCU
        {
            get { return m_entities.Count; }
        }

        /// <summary>
        /// Handles all random number generation
        /// </summary>
        private Random m_rng = new Random(Environment.TickCount);

        /// <summary>
        /// The TCP port to listen for connections on
        /// </summary>
        private const int TCP_PORT = 25189;

        /// <summary>
        /// The TCP listener for the world
        /// </summary>
        private TCPDispatcher m_tcp;

        /// <summary>
        /// The time (tick count) when the last world update started
        /// </summary>
        private int m_lastWorldUpdateTime;

        /// <summary>
        /// Dictionary of World ID -> Entity of everybody connected
        /// </summary>
        private ConcurrentDictionary<int, ServerEntity> m_entities;

        /// <summary>
        /// The thread that world updates will run on
        /// </summary>
        private Thread m_worldUpdateThread;

        /// <summary>
        /// Flag to stop world updates
        /// </summary>
        private bool m_updateWorld;

        /// <summary>
        /// Manages all zones in the world
        /// </summary>
        public ZoneManager ZoneManager
        {
            get;
            private set;
        }

        /// <summary>
        /// A whole neww worlddddddd!
        /// </summary>
        public World()
        {
            m_entities = new ConcurrentDictionary<int, ServerEntity>();

            m_tcp = new TCPDispatcher(IPAddress.Any, TCP_PORT);
            m_tcp.SocketConnected += TCP_SocketConnected;

            ZoneManager = new ZoneManager();
        }

        /// <summary>
        /// Event handler for TCP connections.
        /// </summary>
        private void TCP_SocketConnected(Socket socket)
        {
            //Keep going until we find an ID for them
            while (true)
            {
                //Generate an ID
                int worldID = m_rng.Next();

                //See if it's unique
                if (!m_entities.ContainsKey(worldID))
                {
                    //Add them in
                    ServerEntity entity = new ServerEntity(socket, worldID, this);
                    if (m_entities.TryAdd(worldID, entity))
                    {
                        //DONE!
                        Console.WriteLine(worldID.ToString() + " joined");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Start the world running.
        /// </summary>
        public void Start()
        {
            //Start the tcp listener
            m_tcp.Start();

            //Set up world updates
            m_lastWorldUpdateTime = Environment.TickCount;
            m_updateWorld = true;

            //Start world update thread
            m_worldUpdateThread = new Thread(WorldUpdate);
            m_worldUpdateThread.Priority = ThreadPriority.AboveNormal;
            m_worldUpdateThread.Start();
        }

        /// <summary>
        /// Stop the world running
        /// </summary>
        public void Stop()
        {
            //Kill the update thread
            m_updateWorld = false;
            m_worldUpdateThread.Join();

            //Stop listening for new connections
            m_tcp.Stop();
        }

        /// <summary>
        /// World update logic
        /// </summary>
        private void WorldUpdate()
        {
            //Check if we're supposed to be running
            while (m_updateWorld)
            {
                //Calculate update delta
                TimeSpan dt = TimeSpan.FromTicks(Environment.TickCount - m_lastWorldUpdateTime);

                int updateStartTime = Environment.TickCount;

                //Time for some expensive O(n) badness...
                foreach (ServerEntity entity in m_entities.Values)
                {
                    //Handle disconnect
                    if (!entity.IsConnected)
                    {
                        ServerEntity e = null;
                        if (m_entities.TryRemove(entity.WorldID, out e))
                        {
                            Console.WriteLine("Entity disconnected: " + e.WorldID);
                            ZoneManager.RemoveEntity(e);
                            e.Dispose();
                        }

                        continue;
                    }

                    if (entity.AuthState == EntityAuthState.Authorised)
                    {
                        //Update nearby entities with each other
                        ZoneManager.PushNearbyEntities(entity);
                    }

                    //Call update on this entity
                    entity.Update(dt);
                }

                //Track update times
                m_lastWorldUpdateTime = updateStartTime;

                //Calculate how long to sleep for, based on how long the world update took
                int sleepTime = WORLD_UPDATE_TARGET_MS - (int)TimeSpan.FromTicks(Environment.TickCount - updateStartTime).TotalMilliseconds;

                if (sleepTime < 0)
                {
                    Console.WriteLine("World update in overtime: " + Math.Abs(sleepTime));
                }

                Thread.Sleep(Math.Max(1, sleepTime));
            }
        }

        /// <summary>
        /// Resolve a world ID to a name
        /// </summary>
        /// <returns></returns>
        public string GetNameForWorldID(int worldID)
        {
            string name = string.Empty;

            ServerEntity entity = null;
            if (m_entities.TryGetValue(worldID, out entity))
            {
                name = entity.Name;
            }

            return name;
        }
    }
}

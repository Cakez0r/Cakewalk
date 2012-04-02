using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Cakewalk.Server;
using Cakewalk.Shared;
using Cakewalk.Shared.Packets;
using System.IO.Compression;
using System.IO;

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
        private const int WORLD_UPDATE_TARGET_MS = 100;

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
        /// A whole neww worlddddddd!
        /// </summary>
        public World()
        {
            m_entities = new ConcurrentDictionary<int, ServerEntity>();
            m_tcp = new TCPDispatcher(IPAddress.Any, TCP_PORT);
            m_tcp.SocketConnected += TCP_SocketConnected;
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
                    //Lock entities so nobody else adds this ID in parallel
                    lock (m_entities)
                    {
                        //Check that nobody added the ID while we were locking
                        if (!m_entities.ContainsKey(worldID))
                        {
                            //Add them in
                            ServerEntity entity = new ServerEntity(socket, worldID);
                            if (m_entities.TryAdd(worldID, entity))
                            {
                                //DONE!
                                Console.WriteLine(worldID.ToString() + " joined");
                                break;
                            }
                        }
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
                    //Cleanup any disconnects
                    if (!entity.IsConnected)
                    {
                        ServerEntity e = null;
                        if (m_entities.TryRemove(entity.WorldID, out e))
                        {
                            Console.WriteLine("Entity disconnected: " + e.WorldID);
                            e.Dispose();
                        }
                    }

                    //Push states of nearby entities to this entity
                    PushToNearbyEntities(entity);

                    //Call update on this entity
                    entity.Update(dt);
                }

                //Track update times
                m_lastWorldUpdateTime = updateStartTime;

                //Calculate how long to sleep for, based on how long the world update took
                int sleepTime = WORLD_UPDATE_TARGET_MS - (int)TimeSpan.FromTicks(Environment.TickCount - updateStartTime).TotalMilliseconds;

                Thread.Sleep(Math.Max(1, sleepTime));
            }
        }

        /// <summary>
        /// Sends the state of nearby entities to the specified entity
        /// </summary>
        private void PushToNearbyEntities(ServerEntity entity)
        {
            //Only if he's authorised
            if (entity.AuthState == EntityAuthState.Authorised)
            {
                //Catalogue nearby entities
                Dictionary<int, EntityState> states = new Dictionary<int, EntityState>();

                //Sweep all entities in the world... ugh, optimise...!
                foreach (ServerEntity e in m_entities.Values)
                {
                    if (e.AuthState != EntityAuthState.Authorised || e.WorldID == entity.WorldID)
                    {
                        //Skip unauthorised entities and don't send to self
                        continue;
                    }

                    //Add this entities state to the catalogue
                    //TODO: Add a range check here. Just send everybody to everybody for now.
                    states.Add(e.WorldID, e.LastState);
                }

                //If we have anything to send...
                if (states.Count > 0)
                {
                    //Some horrible copy-paste, but it is a massive performance gain on memory used and packets sent.
                    //If this can be done in a better way, please fix!

                    //Determine what size update packet we need
                    dynamic packet = null;
                    int maxStates = 0;
                    while (states.Count > 0) //Loop while there are still states to send
                    {
                        if (states.Count <= 5)
                        {
                            packet = PacketFactory.CreatePacket<PushStates5>();     
                            maxStates = 5;
                        }
                        else if (states.Count <= 10) 
                        {
                            packet = PacketFactory.CreatePacket<PushStates10>();    
                            maxStates = 10;
                        }
                        else if (states.Count <= 25)
                        {
                            packet = PacketFactory.CreatePacket<PushStates25>();    
                            maxStates = 25;
                        }
                        else if (states.Count <= 50)
                        {
                            packet = PacketFactory.CreatePacket<PushStates50>();
                            maxStates = 50;
                        }
                        else
                        {
                            packet = PacketFactory.CreatePacket<PushStates100>(); 
                            maxStates = 100;
                        }

                        foreach (KeyValuePair<int, EntityState> kvp in states)
                        {
                            //Fill the packet with states
                            packet.AddState(kvp.Key, kvp.Value.X, kvp.Value.Y, kvp.Value.Rot, kvp.Value.Time, kvp.Value.Flags);
                            if (packet.StateCount == maxStates)
                            {
                                //Stop when the packet is full
                                break;
                            }
                        }

                        //Remove any sent entities from the catalogue so that they're not resent
                        for (int i = 0; i < packet.StateCount; i++)
                        {
                            states.Remove(packet.GetWorldID(i));
                        }

                        //Send the packet off
                        entity.SendPacket(packet);
                    }
                }
            }
        }
    }
}

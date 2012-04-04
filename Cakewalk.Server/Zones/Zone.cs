using System.Collections.Generic;
using Cakewalk.Shared;
using Cakewalk.Shared.Packets;

namespace Cakewalk.Server.Zones
{
    /// <summary>
    /// Represents one zone in the world
    /// </summary>
    public class Zone
    {
        /// <summary>
        /// The ID of this zone.
        /// </summary>
        public int ID
        {
            get;
            private set;
        }

        /// <summary>
        /// Index of entities in this zone
        /// </summary>
        private Dictionary<int, ServerEntity> m_entities;

        /// <summary>
        /// Create a new zone
        /// </summary>
        public Zone(int id)
        {
            ID = id;
            m_entities = new Dictionary<int, ServerEntity>();
        }

        /// <summary>
        /// Add an entity to this zone
        /// </summary>
        public void AddEntity(ServerEntity entity)
        {
            m_entities.Add(entity.WorldID, entity);
        }

        /// <summary>
        /// Remove an entity from this zone
        /// </summary>
        public void RemoveEntity(ServerEntity entity)
        {
            if (m_entities.ContainsKey(entity.WorldID))
            {
                m_entities.Remove(entity.WorldID);
            }
        }

        /// <summary>
        /// Sends states of nearby enemies to each other
        /// </summary>
        public void PushNearbyEntities(ServerEntity entity)
        {
            //Create a chunk of packets
            CoalescedData packet = PacketFactory.CreatePacket<CoalescedData>();

            foreach (ServerEntity e in m_entities.Values)
            {
                if (e.AuthState != EntityAuthState.Authorised || e.WorldID == entity.WorldID)
                {
                    //Skip unauthorised entities and don't send to self
                    continue;
                }

                //Add a push state into the coalesced data packet for this entity
                PushState state = PacketFactory.CreatePacket<PushState>();
                state.WorldID = e.WorldID;
                state.State = e.LastState;

                //If we ran out of room in this packet, fire it off and create a new one
                if (!packet.TryAddPacket(state))
                {
                    entity.SendPacket(packet);
                    packet = PacketFactory.CreatePacket<CoalescedData>();
                    packet.TryAddPacket(state);
                }
            }

            //If we have anything left to send...
            if (packet.PacketCount > 0)
            {
                entity.SendPacket(packet);
            }
        }
    }
}

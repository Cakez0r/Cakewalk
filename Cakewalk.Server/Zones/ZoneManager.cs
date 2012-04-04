using System.Collections.Generic;

namespace Cakewalk.Server.Zones
{
    /// <summary>
    /// Handles the indexing and updating of all zones in the world.
    /// </summary>
    public class ZoneManager
    {
        /// <summary>
        /// List of all zones
        /// </summary>
        private Dictionary<int, Zone> m_zones;

        /// <summary>
        /// Index of what zones all entities are in
        /// </summary>
        Dictionary<ServerEntity, Zone> m_userZones;

        /// <summary>
        /// Create a new zone manager
        /// </summary>
        public ZoneManager()
        {
            m_zones = new Dictionary<int, Zone>();

            for (int i = 0; i < 10; i++)
            {
                m_zones.Add(i, new Zone(i));
            }

            m_userZones = new Dictionary<ServerEntity, Zone>();
        }

        /// <summary>
        /// Tells all zones to push states to entities that are near to each other.
        /// </summary>
        public void PushNearbyEntities(ServerEntity entity)
        {
            Zone zone = null;
            m_userZones.TryGetValue(entity, out zone);
            if (zone != null)
            {
                zone.PushNearbyEntities(entity);
            }
        }

        /// <summary>
        /// Tries to move a user in to a new zone.
        /// </summary>
        public bool RequestZoneTransfer(ServerEntity entity, int newZoneID)
        {
            //Check the zone exists
            if (m_zones.ContainsKey(newZoneID))
            {
                Zone newZone = m_zones[newZoneID];

                //See if the user is already in a zone
                if (m_userZones.ContainsKey(entity))
                {
                    //Remove them from their current zone
                    Zone currentZone = m_userZones[entity];
                    currentZone.RemoveEntity(entity);
                }
                else
                {
                    //Add them to the zone index
                    m_userZones.Add(entity, newZone);
                }

                //Move them in to their new zone
                m_userZones[entity] = newZone;
                newZone.AddEntity(entity);

                return true;
            }

            return false;
        }
    }
}

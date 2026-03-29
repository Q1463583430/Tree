using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RoomIdMainStatMappingV2
{
    public string roomId = string.Empty;
    public RoomMainStatSelector mainStat = RoomMainStatSelector.Stamina;
}

[CreateAssetMenu(fileName = "ProductionModifierV2Settings", menuName = "Production/V2/Modifier Settings")]
public class ProductionModifierV2Settings : ScriptableObject
{
    public bool useModifierV2 = true;
    public bool enableVerboseLog = false;
    public RoomMainStatSelector defaultMainStat = RoomMainStatSelector.AverageAll;
    public List<RoomIdMainStatMappingV2> roomIdMainStatMappings = new List<RoomIdMainStatMappingV2>();

    public RoomMainStatSelector ResolveMainStat(string roomId)
    {
        string normalized = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
        if (roomIdMainStatMappings != null)
        {
            for (int i = 0; i < roomIdMainStatMappings.Count; i++)
            {
                RoomIdMainStatMappingV2 mapping = roomIdMainStatMappings[i];
                if (mapping == null)
                {
                    continue;
                }

                string mappingId = string.IsNullOrWhiteSpace(mapping.roomId) ? string.Empty : mapping.roomId.Trim();
                if (!string.Equals(mappingId, normalized, StringComparison.Ordinal))
                {
                    continue;
                }

                return mapping.mainStat;
            }
        }

        return defaultMainStat;
    }
}

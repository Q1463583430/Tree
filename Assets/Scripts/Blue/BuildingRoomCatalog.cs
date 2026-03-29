using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum BuildingModuleType
{
    Production,
    Convert,
    Growth,
    Other
}

[Serializable]
public class RoomDefinition
{
    public string roomName = "New Room";
    [Tooltip("留空时默认使用 roomName 作为房间数据键")]
    public string roomDataKey = string.Empty;
    [TextArea(2, 5)]
    public string description = "Room description";
    public int sizeX = 2;
    [FormerlySerializedAs("sizeZ")]
    public int sizeY = 2;
    public float blockHeight = 1f;
    public GameObject blockPrefab;
    public bool useCustomOccupiedCells = false;

    [Header("占格偏移 (相对原点)")]
    public List<Vector2Int> occupiedCells = new List<Vector2Int> { Vector2Int.zero };

    public string GetRoomDataKey()
    {
        if (!string.IsNullOrWhiteSpace(roomDataKey))
        {
            return roomDataKey.Trim();
        }

        return string.IsNullOrWhiteSpace(roomName) ? string.Empty : roomName.Trim();
    }
}

public class BuildingRoomCatalog : MonoBehaviour
{
    [Header("生产模块房间")]
    public List<RoomDefinition> productionRooms = new List<RoomDefinition>();

    [Header("转化模块房间")]
    public List<RoomDefinition> convertRooms = new List<RoomDefinition>();

    [Header("养成模块房间")]
    public List<RoomDefinition> growthRooms = new List<RoomDefinition>();

    [Header("其他模块房间")]
    public List<RoomDefinition> otherRooms = new List<RoomDefinition>();

    public List<RoomDefinition> GetRooms(BuildingModuleType module)
    {
        switch (module)
        {
            case BuildingModuleType.Convert:
                return convertRooms;
            case BuildingModuleType.Growth:
                return growthRooms;
            case BuildingModuleType.Other:
                return otherRooms;
            default:
                return productionRooms;
        }
    }

    public void ValidateRooms()
    {
        ValidateModuleRooms(productionRooms);
        ValidateModuleRooms(convertRooms);
        ValidateModuleRooms(growthRooms);
        ValidateModuleRooms(otherRooms);
    }

    private void Reset()
    {
        ValidateRooms();
    }

    private void OnValidate()
    {
        ValidateRooms();
    }

    private static void ValidateModuleRooms(List<RoomDefinition> rooms)
    {
        if (rooms == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] == null)
            {
                rooms[i] = new RoomDefinition();
                continue;
            }

            if (string.IsNullOrWhiteSpace(rooms[i].roomName))
            {
                rooms[i].roomName = "Room " + (i + 1);
            }
            else
            {
                rooms[i].roomName = rooms[i].roomName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(rooms[i].roomDataKey))
            {
                rooms[i].roomDataKey = rooms[i].roomDataKey.Trim();
            }

            rooms[i].sizeX = Mathf.Max(1, rooms[i].sizeX);
            rooms[i].sizeY = Mathf.Max(1, rooms[i].sizeY);
            rooms[i].blockHeight = Mathf.Max(0.1f, rooms[i].blockHeight);

            if (rooms[i].occupiedCells == null)
            {
                rooms[i].occupiedCells = new List<Vector2Int>();
            }

            // 明确统一为 sizeX * sizeY 的矩形占格。
            FillRectangleOccupiedCells(rooms[i]);

            RemoveDuplicateOffsets(rooms[i].occupiedCells);
        }
    }

    private static void FillRectangleOccupiedCells(RoomDefinition room)
    {
        room.occupiedCells.Clear();
        for (int y = 0; y < room.sizeY; y++)
        {
            for (int x = 0; x < room.sizeX; x++)
            {
                room.occupiedCells.Add(new Vector2Int(x, y));
            }
        }
    }

    private static void RemoveDuplicateOffsets(List<Vector2Int> cells)
    {
        HashSet<Vector2Int> set = new HashSet<Vector2Int>();
        for (int i = cells.Count - 1; i >= 0; i--)
        {
            if (!set.Add(cells[i]))
            {
                cells.RemoveAt(i);
            }
        }
    }
}

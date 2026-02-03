using UnityEngine;
using System.Collections.Generic;

public enum ConnectorType { Grass, Road, Water, Forest, Empty } // 示例连接器类型

public class TileData : MonoBehaviour
{
    public string tileName; // 瓦片名称，方便调试
    public GameObject prefab; // 对预制件自身的引用 (可选，如果脚本直接挂在预制件上)

    // 定义瓦片四条边的连接器类型
    // 顺序可以是：北 (Z+), 东 (X+), 南 (Z-), 西 (X-)
    public ConnectorType northConnector;
    public ConnectorType eastConnector;
    public ConnectorType southConnector;
    public ConnectorType westConnector;

    // (可选) 瓦片旋转后的连接器
    // 如果允许瓦片旋转，你需要一个方法来获取旋转后各边的连接器类型
    public ConnectorType GetConnector(Direction direction, int rotationSteps) // rotationSteps = 0, 1, 2, 3 (代表0, 90, 180, 270度Y轴旋转)
    {
        Direction rotatedDirection = direction.Rotate(rotationSteps); // 你需要实现 Direction 枚举和 Rotate 方法
        switch (rotatedDirection)
        {
            case Direction.North: return northConnector;
            case Direction.East: return eastConnector;
            case Direction.South: return southConnector;
            case Direction.West: return westConnector;
            default: return ConnectorType.Empty; // 或抛出异常
        }
    }
}

// 辅助枚举 (示例)
public enum Direction { North, East, South, West }

public static class DirectionExtensions
{
    public static Direction Opposite(this Direction dir)
    {
        switch (dir)
        {
            case Direction.North: return Direction.South;
            case Direction.East: return Direction.West;
            case Direction.South: return Direction.North;
            case Direction.West: return Direction.East;
            default: throw new System.ArgumentOutOfRangeException();
        }
    }

    public static Direction Rotate(this Direction dir, int steps) // steps = 0, 1, 2, 3 for 0, 90, 180, 270 deg clockwise
    {
        int current = (int)dir;
        current = (current + steps) % 4;
        return (Direction)current;
    }

    public static Vector2Int ToVector2Int(this Direction dir)
    {
        switch (dir)
        {
            case Direction.North: return Vector2Int.up;    // (0, 1)
            case Direction.East: return Vector2Int.right; // (1, 0)
            case Direction.South: return Vector2Int.down;  // (0, -1)
            case Direction.West: return Vector2Int.left;  // (-1, 0)
            default: throw new System.ArgumentOutOfRangeException();
        }
    }
}

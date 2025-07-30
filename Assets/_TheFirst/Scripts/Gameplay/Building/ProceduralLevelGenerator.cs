using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ProceduralWFCLikeGenerator : MonoBehaviour
{
    [Header("生成设置")]
    public int gridWidth = 10;
    public int gridHeight = 10;
    public float tileSize = 10f; // 每个瓦片的尺寸 (假设是正方形)
    public List<GameObject> tilePrefabs; // 你所有模块预制件的列表 (每个都应有 TileData 组件)

    private class Cell
    {
        public bool collapsed = false;
        public List<(TileData tile, int rotation)> possibleTiles = new List<(TileData, int)>();
        public TileData chosenTile = null;
        public int chosenRotation = 0;
        public Vector2Int gridPosition;

        public Cell(Vector2Int pos, List<TileData> allTileDatas)
        {
            gridPosition = pos;
            // 初始时，每个单元格都可以是任何瓦片的任何旋转
            foreach (var tileData in allTileDatas)
            {
                for (int r = 0; r < 4; r++) // 0, 90, 180, 270 度旋转
                {
                    possibleTiles.Add((tileData, r));
                }
            }
        }
    }

    private Cell[,] grid;
    private List<TileData> allAvailableTileDatas = new List<TileData>();

    void Start()
    {
        Initialize();
        Generate();
    }

    void Initialize()
    {
        grid = new Cell[gridWidth, gridHeight];
        foreach (var prefab in tilePrefabs)
        {
            TileData data = prefab.GetComponent<TileData>();
            if (data != null)
            {
                allAvailableTileDatas.Add(data);
            }
            else
            {
                Debug.LogError($"预制件 '{prefab.name}' 缺少 TileData 组件!", prefab);
            }
        }

        if (allAvailableTileDatas.Count == 0)
        {
            Debug.LogError("没有可用的瓦片数据，无法生成！");
            enabled = false; // 禁用脚本
            return;
        }

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                grid[x, y] = new Cell(new Vector2Int(x, y), allAvailableTileDatas);
            }
        }
    }

    void Generate()
    {
        // 简单实现：从一个角开始，逐个填充，或者随机选择未坍缩的单元格
        // 这里我们用一个简单的顺序填充作为示例，更复杂的WFC会选择熵最小的
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (!CollapseCell(x, y))
                {
                    Debug.LogError($"无法为单元格 ({x},{y}) 找到有效的瓦片。生成可能不完整或有冲突。");
                    // 可以选择停止生成，或者尝试回溯等更高级的策略
                }
            }
        }
        Debug.Log("地块生成完成 (或尝试完成)。");
    }

    bool CollapseCell(int x, int y)
    {
        Cell currentCell = grid[x, y];
        if (currentCell.collapsed) return true; // 已经坍缩过了

        // 1. 根据邻居筛选当前单元格的可能性
        FilterPossibilities(currentCell);

        if (currentCell.possibleTiles.Count == 0)
        {
            return false; // 没有有效的瓦片可选
        }

        // 2. 从可能性中随机选择一个
        int randomIndex = Random.Range(0, currentCell.possibleTiles.Count);
        (TileData chosenTileData, int chosenRotation) = currentCell.possibleTiles[randomIndex];

        currentCell.chosenTile = chosenTileData;
        currentCell.chosenRotation = chosenRotation;
        currentCell.collapsed = true;
        currentCell.possibleTiles.Clear(); // 清空可能性，只保留选中的
        currentCell.possibleTiles.Add((chosenTileData, chosenRotation));


        // 3. 实例化选中的瓦片
        Vector3 position = new Vector3(x * tileSize, 0, y * tileSize);
        Quaternion rotation = Quaternion.Euler(0, chosenRotation * 90f, 0);
        Instantiate(chosenTileData.gameObject, position, rotation, this.transform); // 作为子对象实例化
        // Debug.Log($"在 ({x},{y}) 放置了 {chosenTileData.tileName}，旋转 {chosenRotation * 90} 度");


        // 4. (可选，但WFC核心) 传播约束到邻居 (这一步会使算法更像WFC)
        // PropagateConstraints(x,y); // 这一步会迭代地更新邻居的可能性，如果邻居的可能性变为0则可能需要回溯

        return true;
    }

    void FilterPossibilities(Cell cellToFilter)
    {
        List<(TileData tile, int rotation)> validOptions = new List<(TileData, int)>();

        foreach ((TileData candidateTile, int candidateRotation) in cellToFilter.possibleTiles)
        {
            bool isValidCandidate = true;
            // 检查四个方向的邻居
            foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
            {
                Vector2Int neighborPos = cellToFilter.gridPosition + dir.ToVector2Int();

                // 检查邻居是否在网格内
                if (neighborPos.x >= 0 && neighborPos.x < gridWidth &&
                    neighborPos.y >= 0 && neighborPos.y < gridHeight)
                {
                    Cell neighborCell = grid[neighborPos.x, neighborPos.y];
                    if (neighborCell.collapsed) // 如果邻居已经确定了
                    {
                        // 获取当前候选瓦片在这个方向上的连接器类型
                        ConnectorType candidateConnector = candidateTile.GetConnector(dir, candidateRotation);
                        // 获取邻居瓦片在相对方向上的连接器类型
                        ConnectorType neighborConnector = neighborCell.chosenTile.GetConnector(dir.Opposite(), neighborCell.chosenRotation);

                        if (!AreConnectorsCompatible(candidateConnector, neighborConnector))
                        {
                            isValidCandidate = false;
                            break; // 这个候选瓦片不行了
                        }
                    }
                    // else: 邻居还没确定，暂时不施加约束 (更完整的WFC会处理未确定邻居的可能性)
                }
                else // 超出边界的处理
                {
                    ConnectorType candidateConnector = candidateTile.GetConnector(dir, candidateRotation);
                    if (!IsBoundaryCompatible(candidateConnector)) // 假设边界只能是 Empty 或特定边界类型
                    {
                        isValidCandidate = false;
                        break;
                    }
                }
            }

            if (isValidCandidate)
            {
                validOptions.Add((candidateTile, candidateRotation));
            }
        }
        cellToFilter.possibleTiles = validOptions;
    }

    // 定义连接器如何匹配的规则
    bool AreConnectorsCompatible(ConnectorType c1, ConnectorType c2)
    {
        // 最简单的规则：必须完全相同
        // return c1 == c2;

        // 更灵活的规则：
        if (c1 == ConnectorType.Empty || c2 == ConnectorType.Empty) return true; // 空可以和任何连接 (或者特定规则)
        if (c1 == c2) return true;
        // 添加其他兼容性规则，例如 Road 可以和 Grass 连接（如果设计如此）
        // if ((c1 == ConnectorType.Road && c2 == ConnectorType.Grass) || (c1 == ConnectorType.Grass && c2 == ConnectorType.Road)) return true;
        return false;
    }

    bool IsBoundaryCompatible(ConnectorType connector)
    {
        // 定义哪些连接器类型可以作为地图的边界
        return connector == ConnectorType.Empty || connector == ConnectorType.Water || connector == ConnectorType.Forest; // 示例
    }

    // PropagateConstraints 方法会更复杂，它会在一个单元格坍缩后，
    // 递归地更新其所有邻居的可能性列表，如果某个邻居的可能性列表改变了，
    // 又会继续传播给那个邻居的邻居，直到没有可能性再改变。
    // 这是WFC算法的核心，但对于模块不多的情况，可能仅在坍缩前Filter一次也够用。
    // void PropagateConstraints(int x, int y) { /* ... */ }
}


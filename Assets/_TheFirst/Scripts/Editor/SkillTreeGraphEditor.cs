using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class SkillTreeGraphEditor : EditorWindow
{
    private SkillTreeGraphView _graphView;
    private WeaponStatBlock _currentWeapon;

    [MenuItem("Tools/Skill Tree Graph v2")]
    public static void OpenWindow()
    {
        var window = GetWindow<SkillTreeGraphEditor>();
        window.titleContent = new GUIContent("Skill Tree Editor");
    }

    private void OnEnable()
    {
        ConstructGraphView();
        GenerateToolbar();
    }

    private void OnDisable()
    {
        if (_graphView != null)
        {
            rootVisualElement.Remove(_graphView);
        }
    }

    private void ConstructGraphView()
    {
        _graphView = new SkillTreeGraphView(this)
        {
            name = "Skill Tree Graph"
        };
        _graphView.StretchToParentSize();
        rootVisualElement.Add(_graphView);
    }

    private void GenerateToolbar()
    {
        var toolbar = new UnityEditor.UIElements.Toolbar();

        // 武器选择器
        var weaponSelector = new UnityEditor.UIElements.ObjectField("Select Weapon")
        {
            objectType = typeof(WeaponStatBlock)
        };
        weaponSelector.style.width = 300;
        
        weaponSelector.RegisterValueChangedCallback(evt =>
        {
            _currentWeapon = evt.newValue as WeaponStatBlock;
            _graphView.LoadSkillTree(_currentWeapon);
        });

        toolbar.Add(weaponSelector);
        
        var saveButton = new Button(() => {
             AssetDatabase.SaveAssets(); 
        }) { text = "Save Assets" };
        toolbar.Add(saveButton);

        var layoutButton = new Button(() => {
            _graphView.AutoLayout();
        }) { text = "Auto Layout" };
        toolbar.Add(layoutButton);

        rootVisualElement.Add(toolbar);
    }
}

public class SkillTreeGraphView : GraphView
{
    private SkillTreeGraphEditor _editor;
    private WeaponStatBlock _activeWeapon;
    private List<SkillTreeNodeData> _allNodes = new List<SkillTreeNodeData>();

    public SkillTreeGraphView(SkillTreeGraphEditor editor)
    {
        _editor = editor;
        
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();
        
        // 加载样式表 (如果有的话，这里使用默认的)
        // this.styleSheets.Add(EditorGUIUtility.Load("GraphView/GraphView.uss") as StyleSheet);
        
        // 监听图表变化（连线创建/删除）
        graphViewChanged = OnGraphViewChanged;
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        var compatiblePorts = new List<Port>();
        ports.ForEach(port =>
        {
            // 基本规则：
            // 1. 不能连自己
            // 2. 不能连同节点
            // 3. 必须是 Input <-> Output
            if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
            {
                compatiblePorts.Add(port);
            }
        });
        return compatiblePorts;
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
    {
        // 1. 处理连线创建 (Edges Created)
        if (graphViewChange.edgesToCreate != null)
        {
            foreach (var edge in graphViewChange.edgesToCreate)
            {
                var parentNode = edge.output.node as SkillTreeEditorNode;
                var childNode = edge.input.node as SkillTreeEditorNode;

                if (parentNode != null && childNode != null)
                {
                    // 添加前置关系: Child 依赖 Parent
                    if (!childNode.Data.prerequisites.Contains(parentNode.Data))
                    {
                        childNode.Data.prerequisites.Add(parentNode.Data);
                        EditorUtility.SetDirty(childNode.Data);
                    }
                }
            }
        }

        // 2. 处理元素删除 (Elements Removed) -> 主要是连线删除
        if (graphViewChange.elementsToRemove != null)
        {
            foreach (var element in graphViewChange.elementsToRemove)
            {
                if (element is Edge edge)
                {
                    var parentNode = edge.output.node as SkillTreeEditorNode;
                    var childNode = edge.input.node as SkillTreeEditorNode;

                    if (parentNode != null && childNode != null)
                    {
                        // 移除前置关系
                        if (childNode.Data.prerequisites.Contains(parentNode.Data))
                        {
                            childNode.Data.prerequisites.Remove(parentNode.Data);
                            EditorUtility.SetDirty(childNode.Data);
                        }
                    }
                }
            }
        }

        return graphViewChange;
    }

    public void LoadSkillTree(WeaponStatBlock weapon)
    {
        _activeWeapon = weapon;
        
        // 【修复连线断裂】临时禁用回调，防止 DeleteElements 触发 OnGraphViewChanged
        // 从而误删数据中的 prerequisites 引用
        graphViewChanged = null;
        DeleteElements(graphElements.ToList());
        graphViewChanged = OnGraphViewChanged; // 恢复回调

        _allNodes.Clear();

        if (weapon == null) return;

        string[] guids = AssetDatabase.FindAssets("t:SkillTreeNodeData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var nodeData = AssetDatabase.LoadAssetAtPath<SkillTreeNodeData>(path);
            if (nodeData != null && nodeData.associatedWeapon == weapon)
            {
                _allNodes.Add(nodeData);
            }
        }

        Dictionary<SkillTreeNodeData, SkillTreeEditorNode> nodeMap = new Dictionary<SkillTreeNodeData, SkillTreeEditorNode>();

        // 1. 创建节点
        foreach (var data in _allNodes)
        {
            var node = CreateEditorNode(data);
            AddElement(node);
            nodeMap[data] = node;
        }

        // 2. 创建连线
        foreach (var data in _allNodes)
        {
            if (data.prerequisites != null)
            {
                 if (!nodeMap.ContainsKey(data)) continue;
                 var targetNode = nodeMap[data];
                 foreach (var prereq in data.prerequisites)
                 {
                     if (prereq != null && nodeMap.ContainsKey(prereq))
                     {
                         var sourceNode = nodeMap[prereq];
                         var edge = sourceNode.outputPort.ConnectTo(targetNode.inputPort);
                         AddElement(edge);
                     }
                 }
            }
        }

        // 3. 自动布局检查
        bool allAtZero = _allNodes.All(n => n.graphPosition.sqrMagnitude < 1f);
        if (allAtZero)
        {
            AutoLayout(nodeMap);
        }
    }

    public void AutoLayout()
    {
        var nodeMap = new Dictionary<SkillTreeNodeData, SkillTreeEditorNode>();
        graphElements.ForEach(element => 
        {
            if (element is SkillTreeEditorNode node)
            {
                nodeMap[node.Data] = node;
            }
        });
        AutoLayout(nodeMap);
    }

    private void AutoLayout(Dictionary<SkillTreeNodeData, SkillTreeEditorNode> nodeMap)
    {
        if (nodeMap.Count == 0) return;

        var levels = new Dictionary<SkillTreeNodeData, int>();
        var children = new Dictionary<SkillTreeNodeData, List<SkillTreeNodeData>>();

        foreach (var node in _allNodes)
        {
            if (!children.ContainsKey(node)) children[node] = new List<SkillTreeNodeData>();
            
            if (node.prerequisites != null)
            {
                foreach (var p in node.prerequisites)
                {
                    if (p == null) continue;
                    if (!children.ContainsKey(p)) children[p] = new List<SkillTreeNodeData>();
                    children[p].Add(node);
                }
            }
        }

        var roots = _allNodes.Where(n => n.prerequisites == null || n.prerequisites.Count == 0).ToList();
        
        foreach (var node in _allNodes) levels[node] = 0;

        var queue = new Queue<SkillTreeNodeData>();
        foreach (var root in roots) queue.Enqueue(root);

        // 防止死循环（如果存在环路）
        int watchdog = 0;
        int maxWatchdog = _allNodes.Count * 2;

        while (queue.Count > 0 && watchdog < maxWatchdog)
        {
            watchdog++;
            var current = queue.Dequeue();
            int currentLevel = levels[current];

            if (children.ContainsKey(current))
            {
                foreach (var child in children[current])
                {
                    if (levels[child] < currentLevel + 1)
                    {
                        levels[child] = currentLevel + 1;
                        queue.Enqueue(child);
                    }
                }
            }
        }

        var nodesByLevel = new Dictionary<int, List<SkillTreeNodeData>>();
        foreach (var kvp in levels)
        {
            int level = kvp.Value;
            if (!nodesByLevel.ContainsKey(level)) nodesByLevel[level] = new List<SkillTreeNodeData>();
            nodesByLevel[level].Add(kvp.Key);
        }

        float startX = 50f;
        float startY = 50f;
        float xSpacing = 350f; 
        float ySpacing = 180f; 

        foreach (var level in nodesByLevel.Keys.OrderBy(k => k))
        {
            var nodesInLevel = nodesByLevel[level];
            nodesInLevel.Sort((a, b) => string.Compare(a.name, b.name));

            float currentY = startY;
            foreach (var nodeData in nodesInLevel)
            {
                if (nodeMap.ContainsKey(nodeData))
                {
                    var viewNode = nodeMap[nodeData];
                    Vector2 newPos = new Vector2(startX + level * xSpacing, currentY);
                    
                    nodeData.graphPosition = newPos;
                    viewNode.SetPosition(new Rect(newPos, Vector2.zero));
                    
                    currentY += ySpacing;
                }
            }
        }
    }

    private SkillTreeEditorNode CreateEditorNode(SkillTreeNodeData data)
    {
        var node = new SkillTreeEditorNode(data);
        
        if (data.graphPosition == Vector2.zero)
        {
             data.graphPosition = new Vector2(100 + (data.GetInstanceID() % 10) * 20, 100 + (data.GetInstanceID() % 5) * 20);
        }
        node.SetPosition(new Rect(data.graphPosition, Vector2.zero));

        node.RegisterCallback<GeometryChangedEvent>(evt => 
        {
            data.graphPosition = node.GetPosition().position;
            EditorUtility.SetDirty(data);
        });
        
        node.RegisterCallback<MouseDownEvent>(evt => {
            if (evt.clickCount == 2)
            {
                Selection.activeObject = data;
                EditorGUIUtility.PingObject(data);
            }
        });

        return node;
    }
}

// 继承自 UnityEditor.Experimental.GraphView.Node，避免冲突
public class SkillTreeEditorNode : UnityEditor.Experimental.GraphView.Node
{
    public SkillTreeNodeData Data { get; private set; }
    public Port inputPort;
    public Port outputPort;

    public SkillTreeEditorNode(SkillTreeNodeData data)
    {
        Data = data;
        title = data.skillName;
        if (string.IsNullOrEmpty(title)) title = data.name;

        // --- 样式自定义 ---
        // 节点宽度
        style.width = 250;
        
        // 标题容器背景色
        bool isMutuallyExclusive = data.mutuallyExclusive != null && data.mutuallyExclusive.Count > 0;
        // 颜色：深蓝灰色为底，如果是互斥则带红
        Color bannerColor = isMutuallyExclusive ? new Color(0.6f, 0.2f, 0.2f, 1f) : new Color(0.2f, 0.25f, 0.3f, 1f);
        titleContainer.style.backgroundColor = new StyleColor(bannerColor);
        
        // 标题文字居中
        var titleLabel = titleContainer.Q<Label>("title-label");
        if(titleLabel != null)
        {
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            titleLabel.style.fontSize = 14;
            titleLabel.style.paddingTop = 5;
            titleLabel.style.paddingBottom = 5;
        }

        // --- 端口创建 ---
        // Input: "In"
        inputPort = InstantiatePort(Orientation.Horizontal, UnityEditor.Experimental.GraphView.Direction.Input, Port.Capacity.Multi, typeof(bool));
        inputPort.portName = "In"; 
        inputContainer.Add(inputPort);

        // Output: "Next"
        outputPort = InstantiatePort(Orientation.Horizontal, UnityEditor.Experimental.GraphView.Direction.Output, Port.Capacity.Multi, typeof(bool));
        outputPort.portName = "Next";
        outputContainer.Add(outputPort);

        RefreshExpandedState();
        RefreshPorts();

        // --- 内容区扩展 ---
        // 创建一个自定义的 VisualElement 容器来放置描述
        var contentContainer = new VisualElement();
        contentContainer.style.paddingLeft = 8;
        contentContainer.style.paddingRight = 8;
        contentContainer.style.paddingTop = 8;
        contentContainer.style.paddingBottom = 8;
        contentContainer.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f, 0.8f));

        // 描述文本
        string descText = "No Description";
        if (data.possibleOptions != null && data.possibleOptions.Count > 0)
        {
            descText = data.possibleOptions[0].description;
            // 截断太长的描述
            if(descText.Length > 50) descText = descText.Substring(0, 47) + "...";
        }
        
        var descLabel = new Label(descText);
        descLabel.style.whiteSpace = WhiteSpace.Normal; // 允许换行
        descLabel.style.fontSize = 12;
        descLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
        contentContainer.Add(descLabel);

        // 互斥提示
        if (isMutuallyExclusive)
        {
            var conflictLabel = new Label($"<!> Exclusive with {data.mutuallyExclusive.Count} node(s)");
            conflictLabel.style.color = new StyleColor(new Color(1f, 0.4f, 0.4f));
            conflictLabel.style.marginTop = 4;
            contentContainer.Add(conflictLabel);
        }

        extensionContainer.Add(contentContainer);
        RefreshExpandedState();
    }
}

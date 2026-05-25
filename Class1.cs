using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Utage;
using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace UtageStoryFinalInjector
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public static Dictionary<string, string> CustomStoryCache = new Dictionary<string, string>();
        public static string TargetCsvPath;
        public static AdvSystemSaveData GlobalSaveData;

        public override void Load()
        {
            try { Console.OutputEncoding = Encoding.UTF8; Console.InputEncoding = Encoding.UTF8; } catch { }
            TargetCsvPath = Path.Combine(Paths.PluginPath, "shiyi_story.csv");
            ParseAndLoadCustomStory();
            Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            UnityEngine.Debug.Log($"[QuadDrive] ⚔️ 终极造物主引擎(全自动拓扑排版算法版) 已启动！");
        }

        private static void ParseAndLoadCustomStory()
        {
            if (!File.Exists(TargetCsvPath)) return;
            try
            {
                CustomStoryCache.Clear();
                string currentSheetName = null;
                StringBuilder sheetBuilder = new StringBuilder();

                foreach (var line in File.ReadLines(TargetCsvPath, Encoding.UTF8))
                {
                    if (line.StartsWith("#SHEET_NAME", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(currentSheetName) && sheetBuilder.Length > 0)
                            CustomStoryCache[currentSheetName] = sheetBuilder.ToString();
                        
                        string[] parts = line.Split(',');
                        if (parts.Length > 1) { currentSheetName = parts[1].Trim(); sheetBuilder.Clear(); }
                        continue;
                    }
                    if (!string.IsNullOrEmpty(currentSheetName)) sheetBuilder.AppendLine(line);
                }
                if (!string.IsNullOrEmpty(currentSheetName) && sheetBuilder.Length > 0)
                    CustomStoryCache[currentSheetName] = sheetBuilder.ToString();
            }
            catch (Exception ex) { UnityEngine.Debug.LogError(ex.Message); }
        }
    }

    // ==========================================================================================
    // 🌟 引擎 A & B & D (保持原样，负责剧本与金库接管)
    // ==========================================================================================
    [HarmonyPatch(typeof(AdvChapterData), nameof(AdvChapterData.AddScenario))]
    public class Patch_ScenarioDataMatrix
    {
        private static bool hasInitializedCustomMatrix = false;
        [HarmonyPrefix]
        public static bool Prefix(AdvChapterData __instance, Il2CppSystem.Collections.Generic.Dictionary<string, AdvScenarioData> scenarioDataTbl)
        {
            if (__instance == null || __instance.DataList == null || hasInitializedCustomMatrix) return true;
            try
            {
                var dataManager = UnityEngine.Object.FindObjectOfType<AdvDataManager>();
                if (dataManager == null) return true;
                var targetBook = __instance.DataList[0];
                if (targetBook != null && targetBook.ImportGridList != null)
                {
                    foreach (var kvp in Plugin.CustomStoryCache)
                    {
                        if (kvp.Key == "新的开始" || kvp.Key == "shiyi_new_plot")
                        {
                            targetBook.ImportGridList.Add(new AdvImportScenarioSheet(new StringGrid(kvp.Key, CsvType.Csv, kvp.Value), dataManager.SettingDataManager, dataManager.MacroManager));
                            UnityEngine.Debug.Log($"[Engine-A] 🚀 创世成功！[{kvp.Key}] 已进入官方管线！");
                        }
                    }
                    hasInitializedCustomMatrix = true;
                }
            } catch { } return true;
        }

        [HarmonyPostfix]
        public static void Postfix(AdvChapterData __instance, Il2CppSystem.Collections.Generic.Dictionary<string, AdvScenarioData> scenarioDataTbl)
        {
            if (scenarioDataTbl == null) return;
            foreach (var kvp in Plugin.CustomStoryCache)
            {
                if (kvp.Key == "新的开始" || kvp.Key == "shiyi_new_plot") continue;
                if (scenarioDataTbl.ContainsKey(kvp.Key) && scenarioDataTbl[kvp.Key]?.DataGrid != null)
                {
                    var newGrid = new StringGrid(kvp.Key, CsvType.Csv, kvp.Value);
                    scenarioDataTbl[kvp.Key].DataGrid.Rows.Clear();
                    for (int i = 0; i < newGrid.Rows.Count; i++) scenarioDataTbl[kvp.Key].DataGrid.Rows.Add(newGrid.Rows[i]);
                }
            }
        }
    }

    [HarmonyPatch(typeof(AdvSystemSaveData), nameof(AdvSystemSaveData.Init))]
    public class Patch_AdvSystemSaveDataBridge
    {
        [HarmonyPostfix]
        public static void Postfix(AdvSystemSaveData __instance) { if (__instance != null) Plugin.GlobalSaveData = __instance; }
    }

    // ==========================================================================================
    // 🌟 引擎 C（终极造物主）：剧本动态扫描 + BFS 树状布局算法 + 批量自动渲染
    // ==========================================================================================
    public class PlotNodeData
    {
        public string Label;
        public List<string> Targets = new List<string>();
        public int Depth = 0;
        public Vector2 Position;
        public GameObject UIGameObject;
    }

    public class PlotEdgeData
    {
        public string From;
        public string To;
        public string Kind;
        public string Text;
    }

    [HarmonyPatch(typeof(UI_PlotMap), nameof(UI_PlotMap.ShowMap))]
    public class Patch_UI_PlotMap_ShowMap
    {
        [HarmonyPostfix]
        public static void Postfix(UI_PlotMap __instance)
        {
            if (__instance == null) return;

            try
            {
                var elementsField = __instance.GetIl2CppType().GetField("plotChapterElements", Il2CppSystem.Reflection.BindingFlags.NonPublic | Il2CppSystem.Reflection.BindingFlags.Instance);
                if (elementsField == null) return;
                var elementsArray = elementsField.GetValue(__instance).Cast<Il2CppReferenceArray<UI_PlotChapterElement>>(); 
                if (elementsArray.Length == 0) return;

                var templateNode = elementsArray[elementsArray.Length - 1]; 
                Transform parentTrans = templateNode.transform.parent;

                // 检查是否已经生成过（防重复克隆）
                if (parentTrans.Find("AutoGraph_Root") != null) return;

                System.Console.WriteLine("\n================ [Engine-C 自动布局造物主] ================");

                // 创建一个容纳我们所有自定义节点的 Root 容器，方便管理
                GameObject graphRoot = new GameObject("AutoGraph_Root");
                graphRoot.transform.SetParent(parentTrans, false);

                // --- 1. The Scanner：扫描 CSV 提取网络拓扑图 ---
                Dictionary<string, PlotNodeData> nodeGraph = new Dictionary<string, PlotNodeData>();
                List<PlotEdgeData> edges = new List<PlotEdgeData>();

                foreach (var kvp in Plugin.CustomStoryCache)
                {
                    BuildGraphFromSheet(kvp.Key, kvp.Value, nodeGraph, edges);
                }

                if (nodeGraph.Count == 0 || edges.Count == 0)
                {
                    System.Console.WriteLine("⚠️ 未扫描到剧本中的 Jump/Selection 指令。当前扫描器已执行，但没有提取出有效分支边。");
                    return;
                }

                System.Console.WriteLine($"[Engine-C] 扫描完成：节点 {nodeGraph.Count} 个，连线 {edges.Count} 条。");

                // --- 2. The Math：广度优先搜索 (BFS) 计算层级深度 ---
                Dictionary<string, int> indegree = new Dictionary<string, int>();
                foreach (var node in nodeGraph.Keys)
                {
                    indegree[node] = 0;
                }
                foreach (var node in nodeGraph.Values)
                {
                    foreach (var target in node.Targets)
                    {
                        if (!indegree.ContainsKey(target)) indegree[target] = 0;
                        indegree[target]++;
                    }
                }

                List<string> startNodes = indegree.Where(x => x.Value == 0).Select(x => x.Key).ToList();
                if (startNodes.Count == 0)
                {
                    startNodes.Add(nodeGraph.Keys.First());
                }

                Queue<string> queue = new Queue<string>();
                HashSet<string> queued = new HashSet<string>();
                foreach (var startNode in startNodes)
                {
                    nodeGraph[startNode].Depth = 1;
                    queue.Enqueue(startNode);
                    queued.Add(startNode);
                }

                while (queue.Count > 0)
                {
                    string curr = queue.Dequeue();
                    int currDepth = nodeGraph[curr].Depth;
                    foreach (var target in nodeGraph[curr].Targets)
                    {
                        nodeGraph[target].Depth = Math.Max(nodeGraph[target].Depth, currDepth + 1);
                        if (queued.Add(target))
                        {
                            queue.Enqueue(target);
                        }
                    }
                }

                foreach (var node in nodeGraph.Values)
                {
                    if (node.Depth <= 0) node.Depth = 1;
                }

                // --- 3. The Math：计算绝对坐标 (Y轴居中算法) ---
                Dictionary<int, List<PlotNodeData>> layers = new Dictionary<int, List<PlotNodeData>>();
                foreach (var node in nodeGraph.Values)
                {
                    if (!layers.ContainsKey(node.Depth)) layers[node.Depth] = new List<PlotNodeData>();
                    layers[node.Depth].Add(node);
                }

                float maxLayoutX = 4500f; // 控制横向展开上限，避免节点飞出可视范围
                float maxLayoutY = 300f;  // 控制纵向展开范围，避免分支上下散得太开

                RectTransform templateRect = templateNode.GetComponent<RectTransform>();
                Vector2 templatePos = templateRect.anchoredPosition;

                int minDepth = int.MaxValue;
                int maxDepth = int.MinValue;
                foreach (var node in nodeGraph.Values)
                {
                    if (node.Depth < minDepth) minDepth = node.Depth;
                    if (node.Depth > maxDepth) maxDepth = node.Depth;
                }
                if (minDepth == int.MaxValue) minDepth = 1;
                if (maxDepth == int.MinValue) maxDepth = 1;

                int depthSpan = Math.Max(1, maxDepth - minDepth);
                float xSpacing = maxLayoutX / depthSpan;
                float baseX = templatePos.x;
                float baseY = templatePos.y;

                foreach (var layer in layers.Values)
                {
                    int count = layer.Count;
                    float yStep = count <= 1 ? 0f : (maxLayoutY / (count - 1));
                    float yStart = baseY - (maxLayoutY / 2f);

                    for (int i = 0; i < count; i++)
                    {
                        int normalizedDepth = Math.Max(0, layer[i].Depth - minDepth);
                        float x = baseX + (normalizedDepth * xSpacing);
                        float y = yStart + (i * yStep);

                        layer[i].Position = new Vector2(x, y);
                        System.Console.WriteLine($"[算法推演] 节点 {layer[i].Label} 计算坐标完毕: X={x}, Y={y}");
                    }
                }

                // --- 4. The Renderer：根据算法坐标批量生成节点与连线 ---
                // 找官方的线作为模板
                Component templateLineComp = null;
                foreach (var c in __instance.GetComponentsInChildren<Component>(true))
                {
                    if (c.GetIl2CppType().Name == "UI_PlotMapElementLine" && c.gameObject.name != "CustomLine_Shiyi")
                    {
                        templateLineComp = c; break;
                    }
                }

                // 渲染所有节点
                foreach (var node in nodeGraph.Values)
                {
                    GameObject newNode = UnityEngine.Object.Instantiate(templateNode.gameObject, graphRoot.transform);
                    newNode.name = "Node_" + node.Label;
                    newNode.GetComponent<RectTransform>().anchoredPosition = node.Position;

                    var btn = newNode.GetComponentInChildren<UnityEngine.UI.Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        string jumpLabel = node.Label; // 闭包捕获
                        btn.onClick.AddListener((Action)(() => {
                            System.Console.WriteLine($"[跳转] 进入分支: {jumpLabel}");
                            if (__instance.mainGame != null) __instance.mainGame.OpenStartLabel(jumpLabel);
                        }));
                    }
                    node.UIGameObject = newNode;

                    // 上色 (假设只要存在于自定义图里就亮起，或者你可以去查询金库状态)
                    newNode.GetComponent<UnityEngine.UI.Image>().color = UnityEngine.Color.white;
                }

                // 渲染所有连线
                if (templateLineComp != null)
                {
                    // 将官方末端节点，连向我们算出的起步节点 (Depth == 1)
                    foreach (var node in nodeGraph.Values)
                    {
                        if (node.Depth == 1) CreateLine(templateLineComp, templateNode, node.UIGameObject.GetComponent<UI_PlotChapterElement>(), graphRoot.transform);
                        
                        // 节点内部互相连线
                        foreach (var targetLabel in node.Targets)
                        {
                            if (nodeGraph.ContainsKey(targetLabel))
                            {
                                CreateLine(templateLineComp, node.UIGameObject.GetComponent<UI_PlotChapterElement>(), nodeGraph[targetLabel].UIGameObject.GetComponent<UI_PlotChapterElement>(), graphRoot.transform);
                            }
                        }
                    }
                }

                System.Console.WriteLine("================ [造物主手术结束] ================\n");
            }
            catch (Exception ex) { UnityEngine.Debug.LogError($"[Engine-C] 自动排版手术崩溃: {ex.Message}"); }
        }

        private static void BuildGraphFromSheet(string sheetName, string csvText, Dictionary<string, PlotNodeData> nodeGraph, List<PlotEdgeData> edges)
        {
            if (string.IsNullOrWhiteSpace(csvText)) return;

            string[] lines = csvText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return;

            int labelCol = -1;
            int cmdCol = -1;
            int arg1Col = -1;
            int textCol = -1;
            bool headerReady = false;
            string currentNode = NormalizeLabel(sheetName);
            string pendingSelectionSource = null;
            string pendingSelectionText = null;

            EnsureNode(nodeGraph, currentNode);

            foreach (var rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine) || rawLine.StartsWith("#")) continue;

                List<string> parts = ParseCsvLine(rawLine);
                if (parts.Count == 0) continue;

                if (!headerReady)
                {
                    for (int i = 0; i < parts.Count; i++)
                    {
                        string col = parts[i].Trim();
                        if (col.Equals("Command", StringComparison.OrdinalIgnoreCase)) cmdCol = i;
                        else if (col.Equals("Arg1", StringComparison.OrdinalIgnoreCase)) arg1Col = i;
                        else if (col.Equals("Text", StringComparison.OrdinalIgnoreCase)) textCol = i;
                        else if (col.Equals("Label", StringComparison.OrdinalIgnoreCase)) labelCol = i;
                    }

                    if (cmdCol >= 0)
                    {
                        headerReady = true;
                        continue;
                    }
                }

                string firstCell = GetCell(parts, 0);
                string cmd = GetCell(parts, cmdCol);
                string arg1 = NormalizeLabel(GetCell(parts, arg1Col));
                string text = GetCell(parts, textCol);
                string explicitLabel = NormalizeLabel(GetCell(parts, labelCol));

                if (!string.IsNullOrEmpty(explicitLabel))
                {
                    currentNode = explicitLabel;
                    EnsureNode(nodeGraph, currentNode);
                }

                if (IsLabelMarker(firstCell))
                {
                    string label = NormalizeLabel(firstCell);
                    EnsureNode(nodeGraph, label);

                    if (!string.IsNullOrEmpty(pendingSelectionSource))
                    {
                        AddEdge(nodeGraph, edges, pendingSelectionSource, label, "Selection", pendingSelectionText);
                        pendingSelectionSource = null;
                        pendingSelectionText = null;
                    }

                    currentNode = label;
                    continue;
                }

                if (string.Equals(cmd, "Selection", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(currentNode) && !string.IsNullOrEmpty(arg1))
                    {
                        pendingSelectionSource = currentNode;
                        pendingSelectionText = string.IsNullOrWhiteSpace(text) ? arg1 : text;
                    }
                    continue;
                }

                if (string.Equals(cmd, "Jump", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(currentNode) && !string.IsNullOrEmpty(arg1))
                    {
                        AddEdge(nodeGraph, edges, currentNode, arg1, "Jump", text);
                    }
                    continue;
                }
            }
        }

        private static void AddEdge(Dictionary<string, PlotNodeData> nodeGraph, List<PlotEdgeData> edges, string from, string to, string kind, string text)
        {
            from = NormalizeLabel(from);
            to = NormalizeLabel(to);
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;

            EnsureNode(nodeGraph, from);
            EnsureNode(nodeGraph, to);

            if (!nodeGraph[from].Targets.Contains(to))
            {
                nodeGraph[from].Targets.Add(to);
            }

            edges.Add(new PlotEdgeData
            {
                From = from,
                To = to,
                Kind = kind,
                Text = text,
            });

            System.Console.WriteLine($"[Scanner] {kind}: {from} -> {to}" + (string.IsNullOrWhiteSpace(text) ? "" : $" | {text}"));
        }

        private static void EnsureNode(Dictionary<string, PlotNodeData> nodeGraph, string label)
        {
            label = NormalizeLabel(label);
            if (string.IsNullOrEmpty(label)) return;
            if (!nodeGraph.ContainsKey(label))
            {
                nodeGraph[label] = new PlotNodeData { Label = label };
            }
        }

        private static bool IsLabelMarker(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string trimmed = value.Trim();
            return trimmed.StartsWith("*") && !trimmed.Contains(" ");
        }

        private static string NormalizeLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string trimmed = value.Trim().Trim('"');
            if (trimmed.StartsWith("*")) trimmed = trimmed.Substring(1).Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static string GetCell(List<string> cells, int index)
        {
            if (index < 0 || index >= cells.Count) return string.Empty;
            return cells[index]?.Trim() ?? string.Empty;
        }

        private static List<string> ParseCsvLine(string line)
        {
            List<string> result = new List<string>();
            if (line == null) return result;

            StringBuilder current = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            result.Add(current.ToString());
            return result;
        }

        // 辅助方法：物理克隆与焊接连线
        private static void CreateLine(Component templateLine, UI_PlotChapterElement u1, UI_PlotChapterElement u2, Transform parent)
        {
            try
            {
                GameObject lineObj = UnityEngine.Object.Instantiate(templateLine.gameObject, parent);
                var myCustomLine = lineObj.GetComponent(templateLine.GetIl2CppType());
                var flags = Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.NonPublic | Il2CppSystem.Reflection.BindingFlags.Instance;
                
                myCustomLine.GetIl2CppType().GetField("unit1", flags)?.SetValue(myCustomLine, u1);
                myCustomLine.GetIl2CppType().GetField("unit2", flags)?.SetValue(myCustomLine, u2);
 
                myCustomLine.GetIl2CppType().GetField("pos1", flags)?.SetValue(myCustomLine, templateLine.GetIl2CppType().GetField("pos1", flags)?.GetValue(templateLine));
                myCustomLine.GetIl2CppType().GetField("pos2", flags)?.SetValue(myCustomLine, templateLine.GetIl2CppType().GetField("pos2", flags)?.GetValue(templateLine));
 
                // 强制激活全彩状态
                myCustomLine.GetIl2CppType().GetField("activeLine", flags)?.GetValue(myCustomLine)?.Cast<GameObject>()?.SetActive(true);
                myCustomLine.GetIl2CppType().GetField("normalLine", flags)?.GetValue(myCustomLine)?.Cast<GameObject>()?.SetActive(false);
 
                myCustomLine.GetIl2CppType().GetMethod("SetPosition", flags, null, System.Array.Empty<Il2CppSystem.Type>(), null)?.Invoke(myCustomLine, null);
            }
            catch { }
        }
    }

    public static class MyPluginInfo
    {
        public const string PLUGIN_GUID = "com.shiyimv.utage_creator";
        public const string PLUGIN_NAME = "Utage Topology Creator";
        public const string PLUGIN_VERSION = "15.0.0";
    }
}
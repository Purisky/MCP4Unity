using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Reflection;
using System;
using System.Text.RegularExpressions;
using System.IO;

namespace MCP4Unity.Editor
{
    public enum ToolExecutionSource
    {
        EditorWindow,
        MCP
    }

    [Serializable]
    public class ToolExecutionHistory
    {
        public string toolName;
        public string timestamp;
        public List<string> parameterNames = new List<string>();
        public List<string> parameterValues = new List<string>();
        public string executionResult;
        public bool executionSuccess;
        public ToolExecutionSource executionSource;
        
        public ToolExecutionHistory() { }
        
        public ToolExecutionHistory(string toolName, Dictionary<string, string> parameters, string result, bool success, ToolExecutionSource source = ToolExecutionSource.EditorWindow)
        {
            this.toolName = toolName;
            this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            this.parameterNames = parameters.Keys.ToList();
            this.parameterValues = parameters.Values.ToList();
            this.executionResult = result;
            this.executionSuccess = success;
            this.executionSource = source;
        }
    }

    [Serializable]
    public class MCPEditorWindow : EditorWindow, ISerializationCallbackReceiver
    {
        public static MCPEditorWindow Inst;
        
        // Serialized fields for persistence
        [SerializeField]
        private string selectedToolName = "";
        [SerializeField]
        private List<string> parameterNames = new List<string>();
        [SerializeField]
        private List<string> parameterValues = new List<string>();
        [SerializeField]
        private string lastExecutionResult = "";
        [SerializeField]
        private bool lastExecutionSuccess = false;
        [SerializeField]
        private bool isDeserializing = false;
        [SerializeField]
        private bool isHistoryCollapsed = false;
        [SerializeField]
        private bool isHyperlinkMode = true; // 默认为超链接模式
        
        [MenuItem("Window/MCP Service Manager")]
        public static void ShowWindow()
        {
            Inst = GetWindow<MCPEditorWindow>();
        }
        
        Button startBtn;
        VisualElement toolListContainer;
        VisualElement toolDetailsContainer;
        VisualElement historyContainer;
        Dictionary<string, TextField> currentToolParameterFields = new();
        Label currentToolResultLabel;
        VisualElement currentToolResultContainer; // Container for result content (supports both text and hyperlinks)
        VisualElement currentToolResultHeader; // Header container with status and toggle button
        Label currentToolResultHeaderLabel; // Status label in header
        Button hyperlinkModeToggleButton; // Toggle button for hyperlink/text mode
        
        // 双UI元素缓存系统
        VisualElement textResultElement; // 纯文本显示元素
        VisualElement hyperlinkResultElement; // 超链接显示元素
        string cachedResultContent = null; // 缓存的结果内容
        Color cachedResultColor; // 缓存的结果颜色
        bool cachedResultIsError = false; // 缓存的错误状态
        MCPTool currentSelectedTool;
        List<MCPTool> availableTools = new List<MCPTool>();
        List<Button> toolButtons = new List<Button>();
        Button executeButton;
        
        // History UI fields
        TextField historySearchField;
        VisualElement historyFilterContainer;
        System.Collections.Generic.HashSet<string> activeFilters = new System.Collections.Generic.HashSet<string>();
        VisualElement historyContentContainer;
        Button historyCollapseButton;
        Button historyExpandButton; // 展开按钮，位于Detail栏
        Label historyHeaderLabel;
        VisualElement rightColumn; // 整个右栏容器
        VisualElement verticalSeparator2; // 第二个分隔符
        
        // Filter groups for mutual exclusion
        static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> filterGroups = 
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>
        {
            { "Status", new System.Collections.Generic.List<string> { "Success", "Failed" } },
            { "Tool", new System.Collections.Generic.List<string> { "Outdated", "Current" } },
            { "Source", new System.Collections.Generic.List<string> { "UI", "MCP" } }
        };
        
        // Stack trace parsing support
        private static readonly Regex stackTraceRegex = new Regex(@"^\s*at\s+(.+?)\s+(?:\[0x[0-9a-fA-F]+\]\s+)?in\s+(.+?):(\d+)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        
        [Serializable]
        public class StackTraceItem
        {
            public string methodName;
            public string filePath;
            public int lineNumber;
            public string originalLine;
            
            public StackTraceItem(string method, string file, int line, string original)
            {
                methodName = method;
                filePath = file;
                lineNumber = line;
                originalLine = original;
            }
        }
        int selectedHistoryIndex = -1;
        List<VisualElement> historyItemPool = new List<VisualElement>();
        List<ToolExecutionHistory> filteredHistory = new List<ToolExecutionHistory>();

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            

            VisualElement top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            top.style.justifyContent = Justify.SpaceBetween;
            root.Add(top);

            // Auto start toggle
            Toggle toggle = new("Auto Start")
            {
                value = EditorPrefs.GetBool("MCP4Unity_Auto_Start", true)
            };
            toggle.RegisterValueChangedCallback(OnToggle);
            top.Add(toggle);
            
            // Start/Stop button
            startBtn = new Button(OnClickStart) { text = "Start" };
            //startBtn.style.height = 30;
            top.Add(startBtn);
            


            // Separator
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = Color.gray;
            separator.style.marginTop = 10;
            separator.style.marginBottom = 10;
            root.Add(separator);
            
            // Three-column layout container
            var threeColumnContainer = new VisualElement();
            threeColumnContainer.style.flexDirection = FlexDirection.Row;
            threeColumnContainer.style.flexGrow = 1;
            root.Add(threeColumnContainer);
            
            // Left column - Tool List
            var leftColumn = new VisualElement();
            leftColumn.style.width = new Length(30, LengthUnit.Percent);
            leftColumn.style.minWidth = 180;
            leftColumn.style.maxWidth = 300;
            leftColumn.style.marginRight = 5;
            threeColumnContainer.Add(leftColumn);
            
            // Tool list header
            var toolListHeader = new VisualElement();
            toolListHeader.style.flexDirection = FlexDirection.Row;
            toolListHeader.style.alignItems = Align.Center;
            toolListHeader.style.marginBottom = 10;
            leftColumn.Add(toolListHeader);
            
            var toolsHeaderLabel = new Label("Tools");
            toolsHeaderLabel.style.fontSize = 16;
            toolsHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolsHeaderLabel.style.flexGrow = 1;
            toolListHeader.Add(toolsHeaderLabel);
            
            // Refresh tools button
            var refreshBtn = new Button(RefreshTools) { text = "Refresh" };
            refreshBtn.style.width = 60;
            refreshBtn.style.height = 25;
            refreshBtn.tooltip = "Refresh Tools";
            toolListHeader.Add(refreshBtn);
            
            // Tool list scroll view
            var toolListScrollView = new ScrollView();
            toolListScrollView.style.flexGrow = 1;
            toolListScrollView.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            toolListScrollView.style.borderTopWidth = 1;
            toolListScrollView.style.borderBottomWidth = 1;
            toolListScrollView.style.borderLeftWidth = 1;
            toolListScrollView.style.borderRightWidth = 1;
            toolListScrollView.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            toolListScrollView.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            toolListScrollView.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            toolListScrollView.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            leftColumn.Add(toolListScrollView);
            
            toolListContainer = toolListScrollView.contentContainer;
            
            // First vertical separator
            var verticalSeparator1 = new VisualElement();
            verticalSeparator1.style.width = 1;
            verticalSeparator1.style.backgroundColor = Color.gray;
            verticalSeparator1.style.marginLeft = 5;
            verticalSeparator1.style.marginRight = 5;
            threeColumnContainer.Add(verticalSeparator1);
            
            // Middle column - Tool Details
            var middleColumn = new VisualElement();
            middleColumn.style.width = new Length(45, LengthUnit.Percent);
            middleColumn.style.flexGrow = 1;
            middleColumn.style.minWidth = 300;
            threeColumnContainer.Add(middleColumn);
            
            // Tool details header
            var toolDetailsHeader = new VisualElement();
            toolDetailsHeader.style.flexDirection = FlexDirection.Row;
            toolDetailsHeader.style.alignItems = Align.Center;
            toolDetailsHeader.style.marginBottom = 10;
            
            var toolDetailsLabel = new Label("Details");
            toolDetailsLabel.style.fontSize = 16;
            toolDetailsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolDetailsLabel.style.flexGrow = 1;
            toolDetailsHeader.Add(toolDetailsLabel);
            
            // History expand button (shown when history is collapsed)
            historyExpandButton = new Button(ToggleHistoryCollapse);
            historyExpandButton.style.width = 80;
            historyExpandButton.style.height = 25;
            historyExpandButton.style.marginLeft = 5;
            historyExpandButton.tooltip = "Show History Panel";
            toolDetailsHeader.Add(historyExpandButton);
            
            middleColumn.Add(toolDetailsHeader);
            
            // Tool details scroll view
            var toolDetailsScrollView = new ScrollView();
            toolDetailsScrollView.style.flexGrow = 1;
            middleColumn.Add(toolDetailsScrollView);
            
            toolDetailsContainer = toolDetailsScrollView.contentContainer;
            
            // Second vertical separator
            verticalSeparator2 = new VisualElement();
            verticalSeparator2.style.width = 1;
            verticalSeparator2.style.backgroundColor = Color.gray;
            verticalSeparator2.style.marginLeft = 5;
            verticalSeparator2.style.marginRight = 5;
            threeColumnContainer.Add(verticalSeparator2);
            
            // Right column - Execution History
            rightColumn = new VisualElement();
            rightColumn.style.width = new Length(25, LengthUnit.Percent);
            rightColumn.style.minWidth = 100;
            rightColumn.style.maxWidth = 350;
            threeColumnContainer.Add(rightColumn);
            
            // History header
            var historyHeaderContainer = new VisualElement();
            historyHeaderContainer.style.flexDirection = FlexDirection.Row;
            historyHeaderContainer.style.alignItems = Align.Center;
            historyHeaderContainer.style.marginBottom = 10;
            historyHeaderContainer.style.justifyContent = Justify.SpaceBetween;
            rightColumn.Add(historyHeaderContainer);
            
            // historyHeaderLabel = new Label("Execution History");
            // historyHeaderLabel.style.fontSize = 16;
            // historyHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            // historyHeaderLabel.style.flexGrow = 1;
            // historyHeaderContainer.Add(historyHeaderLabel);
            
            // Collapse button
            historyCollapseButton = new Button(ToggleHistoryCollapse);
            historyCollapseButton.style.width = 25;
            historyCollapseButton.style.height = 25;
            historyCollapseButton.style.marginRight = 5;
            historyCollapseButton.tooltip = "Toggle History Panel";
            historyHeaderContainer.Add(historyCollapseButton);
            
            // Clear history button
            var clearHistoryBtn = new Button(ClearHistory) { text = "Clear" };
            clearHistoryBtn.style.width = 50;
            clearHistoryBtn.style.height = 25;
            clearHistoryBtn.tooltip = "Clear Execution History";
            historyHeaderContainer.Add(clearHistoryBtn);
            
            // History content container (for collapsing)
            historyContentContainer = new VisualElement();
            historyContentContainer.style.flexGrow = 1;
            rightColumn.Add(historyContentContainer);
            
            // Search and filter container
            var searchFilterContainer = new VisualElement();
            searchFilterContainer.style.marginBottom = 8;
            historyContentContainer.Add(searchFilterContainer);
            
            // Search field
            historySearchField = new TextField();
            historySearchField.style.height = 22;
            historySearchField.style.marginBottom = 4;
            historySearchField.value = "";
            historySearchField.RegisterValueChangedCallback(OnHistorySearchChanged);
            var searchPlaceholder = new Label("Search history...");
            searchPlaceholder.style.position = Position.Absolute;
            searchPlaceholder.style.left = 8;
            searchPlaceholder.style.top = 4;
            searchPlaceholder.style.color = new Color(0.5f, 0.5f, 0.5f);
            searchPlaceholder.style.fontSize = 12;
            searchPlaceholder.pickingMode = PickingMode.Ignore;
            historySearchField.Add(searchPlaceholder);
            historySearchField.RegisterCallback<FocusInEvent>(_ => searchPlaceholder.style.display = DisplayStyle.None);
            historySearchField.RegisterCallback<FocusOutEvent>(_ => 
            {
                if (string.IsNullOrEmpty(historySearchField.value))
                    searchPlaceholder.style.display = DisplayStyle.Flex;
            });
            searchFilterContainer.Add(historySearchField);
            
            // Compact filter container
            historyFilterContainer = new VisualElement();
            historyFilterContainer.style.flexDirection = FlexDirection.Row;
            historyFilterContainer.style.flexWrap = Wrap.Wrap;
            historyFilterContainer.style.marginBottom = 6;
            historyFilterContainer.style.marginTop = 4;
            
            // Create filter groups with borders
            foreach (var group in filterGroups)
            {
                // Group container with border
                var groupContainer = new VisualElement();
                groupContainer.style.flexDirection = FlexDirection.Row;
                groupContainer.style.marginRight = 6;
                groupContainer.style.marginBottom = 3;
                groupContainer.style.borderLeftWidth = 1;
                groupContainer.style.borderRightWidth = 1;
                groupContainer.style.borderTopWidth = 1;
                groupContainer.style.borderBottomWidth = 1;
                groupContainer.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
                groupContainer.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
                groupContainer.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
                groupContainer.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
                groupContainer.style.borderTopLeftRadius = 4;
                groupContainer.style.borderTopRightRadius = 4;
                groupContainer.style.borderBottomLeftRadius = 4;
                groupContainer.style.borderBottomRightRadius = 4;
                groupContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
                
                foreach (var filterName in group.Value)
                {
                    var filterButton = new Button(() => ToggleFilter(filterName));
                    filterButton.text = filterName;
                    filterButton.style.fontSize = 8;
                    filterButton.style.height = 18;
                    filterButton.style.marginLeft = 0;
                    filterButton.style.marginRight = 0;
                    filterButton.style.marginTop = 0;
                    filterButton.style.marginBottom = 0;
                    filterButton.style.paddingLeft = 4;
                    filterButton.style.paddingRight = 4;
                    filterButton.style.paddingTop = 1;
                    filterButton.style.paddingBottom = 1;
                    filterButton.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);
                    filterButton.style.borderLeftWidth = 0;
                    filterButton.style.borderRightWidth = group.Value.IndexOf(filterName) < group.Value.Count - 1 ? 1 : 0;
                    filterButton.style.borderTopWidth = 0;
                    filterButton.style.borderBottomWidth = 0;
                    filterButton.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
                    filterButton.style.borderTopLeftRadius = 0;
                    filterButton.style.borderTopRightRadius = 0;
                    filterButton.style.borderBottomLeftRadius = 0;
                    filterButton.style.borderBottomRightRadius = 0;
                    groupContainer.Add(filterButton);
                }
                
                historyFilterContainer.Add(groupContainer);
            }
            
            searchFilterContainer.Add(historyFilterContainer);
            
            // History scroll view
            var historyScrollView = new ScrollView();
            historyScrollView.style.flexGrow = 1;
            historyScrollView.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            historyScrollView.style.borderTopWidth = 1;
            historyScrollView.style.borderBottomWidth = 1;
            historyScrollView.style.borderLeftWidth = 1;
            historyScrollView.style.borderRightWidth = 1;
            historyScrollView.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            historyScrollView.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            historyScrollView.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            historyScrollView.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            historyContentContainer.Add(historyScrollView);
            
            historyContainer = historyScrollView.contentContainer;
            
            MCPService.OnStateChange += UpdateStartBtn;
            MCPService.OnStateChange += RefreshTools;
            UpdateStartBtn();
            RefreshTools();
            RefreshHistory();
            
            // Initialize collapse state
            UpdateHistoryCollapseState();
        }

        void OnClickStart()
        {
            if (MCPService.Inst.Running)
            {
                MCPService.Inst.Stop();
            }
            else
            {
                MCPService.Inst.Start();
            }
        }

        void OnToggle(ChangeEvent<bool> evt)
        {
            EditorPrefs.SetBool("MCP4Unity_Auto_Start", evt.newValue);
        }
        
        public void UpdateStartBtn()
        {
            startBtn.text = MCPService.Inst.Running ? "Stop" : "Start";
            titleContent.text = "MCP:" + (MCPService.Inst.Running ? "Running" : "Stopped");
        }
        
        void RefreshTools()
        {
            // Clear existing content
            toolListContainer.Clear();
            toolDetailsContainer.Clear();
            toolButtons.Clear();
            currentToolParameterFields.Clear();
            currentToolResultLabel = null;
            currentSelectedTool = null;
            availableTools.Clear();
            
            if (!MCPService.Inst.Running)
            {
                ShowServiceNotRunningMessage();
                return;
            }
            
            var tools = MCPFunctionInvoker.Tools;
            if (tools.Count == 0)
            {
                ShowNoToolsMessage();
                return;
            }
            
            // Populate available tools
            availableTools = tools.Values.OrderBy(t => GetToolCategory(t)).ThenBy(t => t.name).ToList();
            
            // Group tools by category
            var toolsByCategory = availableTools.GroupBy(t => GetToolCategory(t)).OrderBy(g => g.Key);
            
            foreach (var categoryGroup in toolsByCategory)
            {
                // Category header
                var categoryHeader = new Label(categoryGroup.Key);
                categoryHeader.style.fontSize = 13;
                categoryHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                categoryHeader.style.color = new Color(0.7f, 0.9f, 1f);
                categoryHeader.style.marginTop = 5;
                categoryHeader.style.marginBottom = 5;
                categoryHeader.style.marginLeft = 5;
                toolListContainer.Add(categoryHeader);
                
                // Tools in category
                foreach (var tool in categoryGroup.OrderBy(t => t.name))
                {
                    var toolButton = CreateToolButton(tool);
                    toolButtons.Add(toolButton);
                    toolListContainer.Add(toolButton);
                }
            }
            
            // Show initial tool selection prompt
            ShowToolSelectionPrompt();
            
            // Restore previously selected tool - prioritize serialized state over EditorPrefs
            string preferredTool = !string.IsNullOrEmpty(selectedToolName) ? selectedToolName : EditorPrefs.GetString("MCP4Unity_SelectedTool", "");
            if (!string.IsNullOrEmpty(preferredTool))
            {
                var preferredToolObj = availableTools.FirstOrDefault(t => t.name == preferredTool);
                if (preferredToolObj != null)
                {
                    SelectTool(preferredToolObj);
                    
                    // If this is from serialized state, restore parameters and execution result
                    if (!string.IsNullOrEmpty(selectedToolName))
                    {
                        EditorApplication.delayCall += () =>
                        {
                            // Restore parameter values after UI is created
                            for (int i = 0; i < parameterNames.Count && i < parameterValues.Count; i++)
                            {
                                if (currentToolParameterFields.TryGetValue(parameterNames[i], out var field))
                                {
                                    field.value = parameterValues[i];
                                }
                            }
                            
                            // Restore execution result
                            if (!string.IsNullOrEmpty(lastExecutionResult) && currentToolResultContainer != null)
                            {
                                // 根据上次执行是否成功设置颜色和检查是否为错误
                                if (lastExecutionSuccess)
                                {
                                    SetResultContent(lastExecutionResult, new Color(0.2f, 0.8f, 0.2f), false);
                                }
                                else
                                {
                                    SetResultContent(lastExecutionResult, new Color(0.8f, 0.2f, 0.2f), true);
                                }
                            }
                            else if (currentToolResultContainer != null)
                            {
                                // 如果没有执行历史，确保显示默认提示文本和颜色
                                SetResultContent("Execute the tool to see results here.", new Color(0.7f, 0.7f, 0.7f), false);
                            }
                        };
                    }
                }
            }
        }
        
        Button CreateToolButton(MCPTool tool)
        {
            var toolButton = new Button(() => SelectTool(tool));
            // Remove fixed height to allow dynamic sizing
            toolButton.style.minHeight = 50; // Set minimum height instead of fixed height
            toolButton.style.marginBottom = 2;
            toolButton.style.marginLeft = 5;
            toolButton.style.marginRight = 5;
            toolButton.style.paddingLeft = 10;
            toolButton.style.paddingRight = 10;
            toolButton.style.paddingTop = 8;
            toolButton.style.paddingBottom = 8;
            toolButton.style.alignItems = Align.FlexStart;
            
            // Tool name container
            var toolContent = new VisualElement();
            toolContent.style.flexDirection = FlexDirection.Column;
            toolContent.style.alignItems = Align.FlexStart;
            toolContent.style.flexGrow = 1;
            toolContent.style.width = new Length(100, LengthUnit.Percent);
            
            var toolName = new Label($"{tool.MethodInfo.Name}");
            toolName.style.fontSize = 12;
            toolName.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolName.style.color = new Color(0.9f, 0.9f, 0.9f);
            toolName.style.marginBottom = 4;
            toolName.style.whiteSpace = WhiteSpace.Normal; // Allow text wrapping
            toolContent.Add(toolName);
            
            // Tool description (show complete description)
            if (!string.IsNullOrEmpty(tool.description))
            {
                var descLabel = new Label(tool.description);
                descLabel.style.fontSize = 10;
                descLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                descLabel.style.whiteSpace = WhiteSpace.Normal; // Allow text wrapping
                descLabel.style.width = new Length(100, LengthUnit.Percent);
                descLabel.style.flexShrink = 0; // Prevent text from being compressed
                descLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                toolContent.Add(descLabel);
            }
            
            toolButton.Add(toolContent);
            
            // Set tooltip with enhanced information
            string tooltip = $"{tool.name}";
            if (!string.IsNullOrEmpty(tool.description))
            {
                tooltip += $"\n{tool.description}";
            }
            toolButton.tooltip = tooltip;
            
            return toolButton;
        }
        
        void ShowServiceNotRunningMessage()
        {
            var messageContainer = new VisualElement();
            messageContainer.style.alignItems = Align.Center;
            messageContainer.style.justifyContent = Justify.Center;
            messageContainer.style.flexGrow = 1;
            messageContainer.style.paddingTop = 50;
            
            var noServiceLabel = new Label("MCP Service is not running");
            noServiceLabel.style.fontSize = 14;
            noServiceLabel.style.color = Color.gray;
            noServiceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            messageContainer.Add(noServiceLabel);
            
            var instructionLabel = new Label("Start the service to see available tools");
            instructionLabel.style.fontSize = 12;
            instructionLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            instructionLabel.style.marginTop = 10;
            messageContainer.Add(instructionLabel);
            
            toolListContainer.Add(messageContainer);
            
            // Also show in details panel
            var detailsMessage = new Label("Start the MCP service to see available tools.");
            detailsMessage.style.color = Color.gray;
            detailsMessage.style.unityFontStyleAndWeight = FontStyle.Italic;
            detailsMessage.style.paddingTop = 20;
            detailsMessage.style.paddingLeft = 20;
            toolDetailsContainer.Add(detailsMessage);
        }
        
        void ShowNoToolsMessage()
        {
            var messageContainer = new VisualElement();
            messageContainer.style.alignItems = Align.Center;
            messageContainer.style.justifyContent = Justify.Center;
            messageContainer.style.flexGrow = 1;
            messageContainer.style.paddingTop = 50;
            
            var noToolsLabel = new Label("No MCP tools found");
            noToolsLabel.style.fontSize = 14;
            noToolsLabel.style.color = Color.gray;
            noToolsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            messageContainer.Add(noToolsLabel);
            
            toolListContainer.Add(messageContainer);
            
            // Also show in details panel
            var detailsMessage = new Label("No MCP tools found.");
            detailsMessage.style.color = Color.gray;
            detailsMessage.style.unityFontStyleAndWeight = FontStyle.Italic;
            detailsMessage.style.paddingTop = 20;
            detailsMessage.style.paddingLeft = 20;
            toolDetailsContainer.Add(detailsMessage);
        }
        
        void ShowToolSelectionPrompt()
        {
            toolDetailsContainer.Clear();
            currentSelectedTool = null;
            currentToolParameterFields.Clear();
            currentToolResultLabel = null;
            
            var promptContainer = new VisualElement();
            promptContainer.style.alignItems = Align.Center;
            promptContainer.style.justifyContent = Justify.Center;
            promptContainer.style.flexGrow = 1;
            promptContainer.style.paddingTop = 50;
            
            var promptLabel = new Label("Select a tool from the list");
            promptLabel.style.fontSize = 16;
            promptLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            promptLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            promptContainer.Add(promptLabel);
            
            var instructionLabel = new Label("Choose a tool from the left panel to configure and execute it");
            instructionLabel.style.fontSize = 12;
            instructionLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            instructionLabel.style.marginTop = 10;
            instructionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            promptContainer.Add(instructionLabel);
            
            // Show summary of available tools
            if (availableTools.Count > 0)
            {
                var summaryLabel = new Label($"Available: {availableTools.Count} tools across {GetUniqueCategories().Count} categories");
                summaryLabel.style.color = new Color(0.5f, 0.7f, 0.9f);
                summaryLabel.style.fontSize = 11;
                summaryLabel.style.marginTop = 20;
                promptContainer.Add(summaryLabel);
            }
            
            toolDetailsContainer.Add(promptContainer);
        }
        
        HashSet<string> GetUniqueCategories()
        {
            var categories = new HashSet<string>();
            foreach (var tool in availableTools)
            {
                categories.Add(GetToolCategory(tool));
            }
            return categories;
        }
        
        void SelectTool(MCPTool tool)
        {
            currentSelectedTool = tool;
            toolDetailsContainer.Clear();
            currentToolParameterFields.Clear();
            
            // Update button selection visual state
            UpdateToolButtonSelection(tool);
            
            CreateSelectedToolUI(tool);
            
            // Save selection
            EditorPrefs.SetString("MCP4Unity_SelectedTool", tool.name);
        }
        
        void UpdateToolButtonSelection(MCPTool selectedTool)
        {
            foreach (var button in toolButtons)
            {
                // Reset all buttons to default style
                button.style.backgroundColor = StyleKeyword.Null;
                button.style.borderLeftWidth = 0;
            }
            
            // Find and highlight selected button
            var selectedButton = toolButtons.FirstOrDefault(b => 
            {
                var toolNameLabel = b.Q<Label>();
                if (toolNameLabel != null)
                {
                    // Extract tool name from the label text (format: "toolname(paramCount)")
                    var labelText = toolNameLabel.text;
                    var parenIndex = labelText.IndexOf('(');
                    var toolName = parenIndex > 0 ? labelText[..parenIndex] : labelText;
                    return toolName == selectedTool.name;
                }
                return false;
            });
            
            if (selectedButton != null)
            {
                selectedButton.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 0.3f);
                selectedButton.style.borderLeftWidth = 3;
                selectedButton.style.borderLeftColor = new Color(0.2f, 0.6f, 1f);
            }
        }
        
        void CreateSelectedToolUI(MCPTool tool)
        {
            // Tool header with enhanced information
            var headerContainer = new VisualElement();
            headerContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            headerContainer.style.paddingTop = 15;
            headerContainer.style.paddingBottom = 15;
            headerContainer.style.paddingLeft = 20;
            headerContainer.style.paddingRight = 20;
            headerContainer.style.marginBottom = 15;
            headerContainer.style.borderLeftWidth = 4;
            headerContainer.style.borderLeftColor = new Color(0.2f, 0.6f, 1f);
            
            // Tool name and category
            var titleContainer = new VisualElement();
            titleContainer.style.flexDirection = FlexDirection.Row;
            titleContainer.style.alignItems = Align.Center;
            titleContainer.style.marginBottom = 8;
            
            var nameLabel = new Label(tool.MethodInfo.Name);
            nameLabel.style.fontSize = 18;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new Color(0.2f, 0.6f, 1f);
            titleContainer.Add(nameLabel);
            
            var categoryBadge = new Label($"[{GetToolCategory(tool)}]");
            categoryBadge.style.fontSize = 12;
            categoryBadge.style.marginLeft = 15;
            categoryBadge.style.color = new Color(0.8f, 0.8f, 0.8f);
            categoryBadge.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            categoryBadge.style.paddingLeft = 8;
            categoryBadge.style.paddingRight = 8;
            categoryBadge.style.paddingTop = 2;
            categoryBadge.style.paddingBottom = 2;
            titleContainer.Add(categoryBadge);
            
            headerContainer.Add(titleContainer);
            
            // Tool description
            if (!string.IsNullOrEmpty(tool.description))
            {
                var descLabel = new Label(tool.description);
                descLabel.style.fontSize = 13;
                descLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
                descLabel.style.whiteSpace = WhiteSpace.Normal;
                descLabel.style.marginBottom = 5;
                headerContainer.Add(descLabel);
            }
            
            // Parameters count info
            var paramCount = tool.inputSchema.orderedProperties.Count;
            //var paramInfoLabel = new Label($"Parameters: {paramCount}");
            //paramInfoLabel.style.fontSize = 11;
            //paramInfoLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            //headerContainer.Add(paramInfoLabel);
            
            toolDetailsContainer.Add(headerContainer);
            
            // Parameters section
            if (paramCount > 0)
            {
                var parametersHeader = new Label($"Parameters:{paramCount}");
                parametersHeader.style.fontSize = 14;
                parametersHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                parametersHeader.style.marginBottom = 10;
                parametersHeader.style.marginLeft = 20;
                parametersHeader.style.color = new Color(0.9f, 0.9f, 0.9f);
                toolDetailsContainer.Add(parametersHeader);
                
                var parametersContainer = new VisualElement();
                parametersContainer.style.paddingLeft = 20;
                parametersContainer.style.paddingRight = 20;
                parametersContainer.style.marginBottom = 20;
                
                foreach (var property in tool.inputSchema.orderedProperties)
                {
                    CreateParameterUI(parametersContainer, tool, property);
                }
                
                toolDetailsContainer.Add(parametersContainer);
            }
            
            // Execution section
            var executionContainer = new VisualElement();
            executionContainer.style.paddingLeft = 20;
            executionContainer.style.paddingRight = 20;
            executionContainer.style.marginBottom = 20;
            
            //var executionHeader = new Label("Execution");
            //executionHeader.style.fontSize = 14;
            //executionHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            //executionHeader.style.marginBottom = 10;
            //executionHeader.style.color = new Color(0.9f, 0.9f, 0.9f);
            //executionContainer.Add(executionHeader);
            
            var actionContainer = new VisualElement();
            actionContainer.style.flexDirection = FlexDirection.Row;
            actionContainer.style.alignItems = Align.Center;
            
            executeButton = new Button(() => ExecuteCurrentTool()) { text = "Execute Tool" };
            executeButton.style.width = 120;
            executeButton.style.height = 32;
            executeButton.style.backgroundColor = new Color(0.2f, 0.7f, 0.2f);
            executeButton.style.color = Color.white;
            executeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            actionContainer.Add(executeButton);
            
            executionContainer.Add(actionContainer);
            toolDetailsContainer.Add(executionContainer);
            
            // Result section
            var resultContainer = new VisualElement();
            resultContainer.style.paddingLeft = 20;
            resultContainer.style.paddingRight = 20;
            
            // var resultHeader = new Label("Result");
            // resultHeader.style.fontSize = 14;
            // resultHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            // resultHeader.style.marginBottom = 10;
            // resultHeader.style.color = new Color(0.9f, 0.9f, 0.9f);
            // resultContainer.Add(resultHeader);
            
            // Create header container with status and toggle button
            currentToolResultHeader = new VisualElement();
            currentToolResultHeader.style.flexDirection = FlexDirection.Row;
            currentToolResultHeader.style.justifyContent = Justify.SpaceBetween;
            currentToolResultHeader.style.alignItems = Align.Center;
            currentToolResultHeader.style.marginBottom = 10;
            
            // Status label
            currentToolResultHeaderLabel = new Label("Result");
            currentToolResultHeaderLabel.style.fontSize = 14;
            currentToolResultHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            currentToolResultHeaderLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            currentToolResultHeader.Add(currentToolResultHeaderLabel);
            
            // Toggle button for hyperlink/text mode
            hyperlinkModeToggleButton = new Button(() => ToggleHyperlinkMode());
            hyperlinkModeToggleButton.text = isHyperlinkMode ? "🔗" : "📄";
            hyperlinkModeToggleButton.style.fontSize = 12;
            hyperlinkModeToggleButton.style.paddingLeft = 8;
            hyperlinkModeToggleButton.style.paddingRight = 8;
            hyperlinkModeToggleButton.style.paddingTop = 4;
            hyperlinkModeToggleButton.style.paddingBottom = 4;
            hyperlinkModeToggleButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            hyperlinkModeToggleButton.style.borderLeftColor = new Color(0.5f, 0.5f, 0.5f);
            hyperlinkModeToggleButton.style.borderRightColor = new Color(0.5f, 0.5f, 0.5f);
            hyperlinkModeToggleButton.style.borderTopColor = new Color(0.5f, 0.5f, 0.5f);
            hyperlinkModeToggleButton.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f);
            hyperlinkModeToggleButton.style.borderLeftWidth = 1;
            hyperlinkModeToggleButton.style.borderRightWidth = 1;
            hyperlinkModeToggleButton.style.borderTopWidth = 1;
            hyperlinkModeToggleButton.style.borderBottomWidth = 1;
            hyperlinkModeToggleButton.style.borderTopLeftRadius = 4;
            hyperlinkModeToggleButton.style.borderTopRightRadius = 4;
            hyperlinkModeToggleButton.style.borderBottomLeftRadius = 4;
            hyperlinkModeToggleButton.style.borderBottomRightRadius = 4;
            currentToolResultHeader.Add(hyperlinkModeToggleButton);
            
            resultContainer.Add(currentToolResultHeader);
            
            // Create result content container
            currentToolResultContainer = new VisualElement();
            currentToolResultContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            currentToolResultContainer.style.paddingTop = 10;
            currentToolResultContainer.style.paddingBottom = 10;
            currentToolResultContainer.style.paddingLeft = 15;
            currentToolResultContainer.style.paddingRight = 15;
            
            // Add initial label
            currentToolResultLabel = new Label("Execute the tool to see results here.");
            currentToolResultLabel.style.fontSize = 12;
            currentToolResultLabel.style.whiteSpace = WhiteSpace.Normal;
            currentToolResultLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            currentToolResultContainer.Add(currentToolResultLabel);
            
            resultContainer.Add(currentToolResultContainer);
            
            // 初始化header状态显示
            if (currentToolResultHeaderLabel != null)
            {
                currentToolResultHeaderLabel.text = "Result";
                currentToolResultHeaderLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            }
            
            toolDetailsContainer.Add(resultContainer);
        }
        
        string GetToolCategory(MCPTool tool)
        {
            return tool.MethodInfo.DeclaringType.Name;
        }
        
        void CreateParameterUI(VisualElement parametersContainer, MCPTool tool, Property property)
        {
            var paramContainer = new VisualElement();
            paramContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            paramContainer.style.marginBottom = 8;
            paramContainer.style.paddingTop = 10;
            paramContainer.style.paddingBottom = 10;
            paramContainer.style.paddingLeft = 15;
            paramContainer.style.paddingRight = 15;
            
            // Parameter name and type
            var headerContainer = new VisualElement();
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.alignItems = Align.Center;
            headerContainer.style.marginBottom = 8;
            
            var paramLabel = new Label($"{property.Name}");
            paramLabel.style.fontSize = 12;
            paramLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            paramLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            headerContainer.Add(paramLabel);
            
            var typeLabel = new Label($"({property.type})");
            typeLabel.style.fontSize = 11;
            typeLabel.style.color = new Color(0.6f, 0.8f, 1f);
            typeLabel.style.marginLeft = 8;
            headerContainer.Add(typeLabel);
            
            paramContainer.Add(headerContainer);
            
            // Parameter description
            if (!string.IsNullOrEmpty(property.description))
            {
                var descLabel = new Label(property.description);
                descLabel.style.fontSize = 11;
                descLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                descLabel.style.whiteSpace = WhiteSpace.Normal;
                descLabel.style.marginBottom = 8;
                paramContainer.Add(descLabel);
            }
            
            // Input field container
            var inputContainer = new VisualElement();
            inputContainer.style.flexDirection = FlexDirection.Row;
            
            var paramField = new TextField();
            paramField.style.flexGrow = 1;
            
            // Check if it's a complex (non-primitive) type that should use JSON
            bool isComplexType = !property.IsPrimitiveType() && property.type == "object";
            bool isMultilineType = property.type == "string" || isComplexType;
            
            // For string type parameters and complex types, enable multiline and auto-resize
            if (isMultilineType)
            {
                paramField.multiline = true;
                paramField.style.minHeight = isComplexType ? 60 : 25;
                paramField.style.height = StyleKeyword.Auto; // Let UI Toolkit handle height automatically
                paramField.style.whiteSpace = WhiteSpace.Normal;
                
                // Set placeholder for complex types
                if (isComplexType)
                {
                    try 
                    {
                        // Try to create a default instance and show its JSON representation
                        if (property.Type != typeof(object) && !property.Type.IsAbstract && !property.Type.IsInterface)
                        {
                            var defaultInstance = Activator.CreateInstance(property.Type);
                            var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(defaultInstance, Newtonsoft.Json.Formatting.Indented);
                            paramField.value = jsonString;
                        }
                        else
                        {
                            paramField.value = "{\n  \"example\": \"value\"\n}";
                        }
                    }
                    catch
                    {
                        paramField.value = "{\n  \"example\": \"value\"\n}";
                    }
                }
            }
            else
            {
                paramField.style.height = 25;
            }
            
            // Set tooltip
            string tooltip = $"Type: {property.type}";
            if (isComplexType)
            {
                tooltip += " (JSON format)";
            }
            if (!string.IsNullOrEmpty(property.description))
            {
                tooltip += $"\nDescription: {property.description}";
            }
            paramField.tooltip = tooltip;
            // Add dropdown button if parameter has ParamDropdown attribute
            if (property.HasDropdown)
            {
                var dropdownButton = new Button(() => ShowParameterDropdown(tool, property, paramField))
                {
                    text = "▼"
                };
                dropdownButton.style.width = 30;
                dropdownButton.style.height = 25; // Fixed height for dropdown button
                dropdownButton.style.alignSelf = Align.FlexStart; // Always align to top
                dropdownButton.style.marginLeft = 5;
                dropdownButton.style.fontSize = 12;
                dropdownButton.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
                dropdownButton.tooltip = "Click to select from available options";
                inputContainer.Add(dropdownButton);
            }
            inputContainer.Add(paramField);
            currentToolParameterFields[property.Name] = paramField;
            

            
            paramContainer.Add(inputContainer);
            parametersContainer.Add(paramContainer);
        }
        
        void ShowParameterDropdown(MCPTool tool, Property property, TextField targetField)
        {
            try
            {
                var dropdownAttr = property.GetDropdownAttribute();
                if (dropdownAttr == null) return;
                
                // Find the method that provides dropdown options
                var targetType = tool.MethodInfo.DeclaringType;
                var dropdownMethod = targetType.GetMethod(dropdownAttr.MethodName, 
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (dropdownMethod == null)
                {
                    Debug.LogError($"Dropdown method '{dropdownAttr.MethodName}' not found in {targetType.Name}");
                    return;
                }
                
                // Build current parameter values from UI fields
                var currentParameters = new Dictionary<string, object>();
                foreach (var paramField in currentToolParameterFields)
                {
                    string value = paramField.Value.value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        currentParameters[paramField.Key] = value;
                    }
                }
                
                // Call the dropdown method to get options
                var parameters = new object[] { tool.MethodInfo, currentParameters };
                var result = dropdownMethod.Invoke(null, parameters);
                
                // Handle both List<string> and List<(string, string)> return types
                if (result is List<string> stringOptions && stringOptions.Count > 0)
                {
                    ShowDropdownMenuFromStrings(targetField, stringOptions);
                }
                else if (result is List<(string, string)> tupleOptions && tupleOptions.Count > 0)
                {
                    ShowDropdownMenuFromTuples(targetField, tupleOptions);
                }
                else
                {
                    Debug.LogWarning($"Dropdown method '{dropdownAttr.MethodName}' returned no options or unsupported type");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error showing dropdown for parameter '{property.Name}': {ex.Message}");
            }
        }
        
        void ShowDropdownMenuFromStrings(TextField targetField, List<string> options)
        {
            var menu = new GenericMenu();
            
            foreach (var option in options)
            {
                menu.AddItem(new GUIContent(option), false, () =>
                {
                    targetField.value = option;
                });
            }
            
            menu.ShowAsContext();
        }
        
        void ShowDropdownMenuFromTuples(TextField targetField, List<(string value, string display)> options)
        {
            var menu = new GenericMenu();
            
            foreach (var (value, display) in options)
            {
                menu.AddItem(new GUIContent(display), false, () =>
                {
                    targetField.value = value;
                });
            }
            
            menu.ShowAsContext();
        }
        
        void ExecuteCurrentTool()
        {
            if (currentSelectedTool == null || currentToolResultLabel == null)
            {
                return;
            }
            
            try
            {
                // Clear previous result
                SetResultContent("Executing...", new Color(0.8f, 0.8f, 0.2f), false);
                
                // Build parameters JObject
                var parameters = new JObject();
                foreach (var field in currentToolParameterFields)
                {
                    string value = field.Value.value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        // Try to parse as appropriate type
                        var property = currentSelectedTool.inputSchema.properties[field.Key];
                        
                        try
                        {
                            object convertedValue;
                            
                            // Check if it's a primitive type
                            if (property.IsPrimitiveType())
                            {
                                // Handle primitive types
                                if (property.Type == typeof(string))
                                {
                                    convertedValue = value;
                                }
                                else if (property.Type == typeof(int))
                                {
                                    convertedValue = int.Parse(value);
                                }
                                else if (property.Type == typeof(float))
                                {
                                    convertedValue = float.Parse(value);
                                }
                                else if (property.Type == typeof(double))
                                {
                                    convertedValue = double.Parse(value);
                                }
                                else if (property.Type == typeof(bool))
                                {
                                    convertedValue = bool.Parse(value);
                                }
                                else if (property.Type == typeof(DateTime))
                                {
                                    convertedValue = DateTime.Parse(value);
                                }
                                else
                                {
                                    convertedValue = value;
                                }
                            }
                            else
                            {
                                // Handle complex types - parse as JSON
                                try
                                {
                                    convertedValue = Newtonsoft.Json.JsonConvert.DeserializeObject(value, property.Type);
                                }
                                catch (Newtonsoft.Json.JsonException)
                                {
                                    // If JSON parsing fails, try to create a JObject/JToken
                                    var jsonToken = JToken.Parse(value);
                                    convertedValue = jsonToken.ToObject(property.Type);
                                }
                            }
                            
                            parameters[field.Key] = JToken.FromObject(convertedValue);
                        }
                        catch (Exception ex)
                        {
                            // If all parsing fails, try using the raw string
                            try
                            {
                                parameters[field.Key] = value;
                            }
                            catch
                            {
                                Debug.LogWarning($"Failed to parse parameter '{field.Key}' with value: {value}. Error: {ex.Message}");
                                throw new ArgumentException($"Invalid value for parameter '{field.Key}': {value}", ex);
                            }
                        }
                    }
                }
                
                // Call the tool
                var result = MCPFunctionInvoker.Invoke(currentSelectedTool.name, parameters);
                
                // Display result
                string resultText = result?.ToString() ?? "null";
                string successText = $"{resultText}"; // 移除 "✓ Success: " 前缀
                
                SetResultContent(successText, new Color(0.2f, 0.8f, 0.2f), false);
                UpdateResultHeaderStatus(true); // 更新header状态显示
                
                // Add to history
                var parameterDict = new Dictionary<string, string>();
                foreach (var field in currentToolParameterFields)
                {
                    parameterDict[field.Key] = field.Value.value ?? "";
                }
                AddToHistory(currentSelectedTool.name, parameterDict, successText, true);
                
                
                Debug.Log($"MCP Tool '{currentSelectedTool.name}' executed successfully. Result: {result}");
            }
            catch (System.Exception ex)
            {
                if (ex is TargetInvocationException tex)
                {
                    ex = tex.InnerException ?? ex;
                }
                string errorText = $"{ex}"; // 移除 "✗ Error:" 前缀
                SetResultContent(errorText, new Color(0.8f, 0.2f, 0.2f), true);
                UpdateResultHeaderStatus(false); // 更新header状态显示
                
                // Add to history even for errors
                var parameterDict = new Dictionary<string, string>();
                foreach (var field in currentToolParameterFields)
                {
                    parameterDict[field.Key] = field.Value.value ?? "";
                }
                AddToHistory(currentSelectedTool.name, parameterDict, errorText, false);
                
                
                Debug.LogError($"Error executing MCP Tool '{currentSelectedTool.name}':\n{ex}");
            }
        }
        
        // ISerializationCallbackReceiver implementation
        public void OnBeforeSerialize()
        {
            //Debug.Log($"MCPEditorWindow.OnBeforeSerialize() - SelectedTool: {selectedToolName}");
            
            // Save current tool selection and parameters
            if (currentSelectedTool != null)
            {
                selectedToolName = currentSelectedTool.name;
                
                // Save parameter values
                parameterNames.Clear();
                parameterValues.Clear();
                
                foreach (var kvp in currentToolParameterFields)
                {
                    if (kvp.Value != null)
                    {
                        parameterNames.Add(kvp.Key);
                        parameterValues.Add(kvp.Value.value ?? "");
                    }
                }
                
                // Save execution result
                if (currentToolResultContainer != null)
                {
                    var currentText = GetCurrentResultText();
                    // 只保存真正的执行结果，不保存默认提示文本
                    if (!string.IsNullOrEmpty(currentText) && 
                        currentText != "Execute the tool to see results here." &&
                        currentText != "Executing...")
                    {
                        lastExecutionResult = currentText;
                        // 成功/失败状态已经在执行时设置好了，不需要根据文本内容判断
                    }
                    else
                    {
                        // 清除之前的执行结果，因为当前没有有效的执行结果
                        lastExecutionResult = "";
                        lastExecutionSuccess = false;
                    }
                }
            }
        }

        public void OnAfterDeserialize()
        {
            //Debug.Log($"MCPEditorWindow.OnAfterDeserialize() - SelectedTool: {selectedToolName}");
            isDeserializing = true;
        }
        
        // Called when window is enabled (including after domain reload)
        void OnEnable()
        {
            //Debug.Log($"MCPEditorWindow.OnEnable() - IsDeserializing: {isDeserializing}, SelectedTool: {selectedToolName}");
            
            // 监听MCP历史更新
            MCPService.OnHistoryUpdated += OnMCPHistoryUpdated;
            
            if (isDeserializing)
            {
                // Delay restoration to ensure MCP service and tools are ready
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        isDeserializing = false;
                        // RefreshTools will handle state restoration
                        RefreshTools();
                        // 在工具刷新后刷新历史显示
                        FilterAndRefreshHistory();
                        // 恢复折叠状态
                        UpdateHistoryCollapseState();
                    }
                };
            }
            else
            {
                // 即使不是反序列化，也要确保折叠状态正确
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        UpdateHistoryCollapseState();
                    }
                };
            }
        }
        
        void OnDisable()
        {
            // 取消监听MCP历史更新
            MCPService.OnHistoryUpdated -= OnMCPHistoryUpdated;
        }
        
        void OnMCPHistoryUpdated()
        {
            // 直接刷新历史显示，不需要同步
            if (historyContainer != null)
            {
                FilterAndRefreshHistory();
            }
            
            // 更新展开按钮中的数量显示
            if (isHistoryCollapsed && historyExpandButton != null)
            {
                int historyCount = MCPService.MCPExecutionHistory?.Count ?? 0;
                historyExpandButton.text = $"History ({historyCount})";
            }
        }
        
        // History management methods
        void ToggleFilter(string filterName)
        {
            if (activeFilters.Contains(filterName))
            {
                activeFilters.Remove(filterName);
            }
            else
            {
                // Check if this filter belongs to any group and remove other filters in the same group
                foreach (var group in filterGroups.Values)
                {
                    if (group.Contains(filterName))
                    {
                        // Remove all other filters in this group
                        foreach (var otherFilter in group)
                        {
                            if (otherFilter != filterName)
                            {
                                activeFilters.Remove(otherFilter);
                            }
                        }
                        break;
                    }
                }
                
                activeFilters.Add(filterName);
            }
            
            // Update filter button visual states
            UpdateFilterButtonStates();
            
            // Refresh history display
            FilterAndRefreshHistory();
        }
        
        void UpdateFilterButtonStates()
        {
            if (historyFilterContainer == null) return;
            
            // Iterate through group containers
            foreach (VisualElement groupContainer in historyFilterContainer.Children())
            {
                foreach (Button button in groupContainer.Children().OfType<Button>())
                {
                    bool isActive = activeFilters.Contains(button.text);
                    if (isActive)
                    {
                        button.style.backgroundColor = new Color(0.2f, 0.5f, 0.8f, 0.9f);
                        button.style.color = new Color(1f, 1f, 1f);
                    }
                    else
                    {
                        button.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);
                        button.style.color = new Color(0.8f, 0.8f, 0.8f);
                    }
                }
            }
        }
        
        void OnHistorySearchChanged(ChangeEvent<string> evt)
        {
            FilterAndRefreshHistory();
        }
        
        void FilterAndRefreshHistory()
        {
            if (historySearchField == null) return;
            
            var searchText = historySearchField.value?.ToLower() ?? "";
            
            filteredHistory.Clear();
            
            foreach (var history in MCPService.MCPExecutionHistory)
            {
                // Apply search filter
                bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                   history.toolName.ToLower().Contains(searchText) ||
                                   history.timestamp.ToLower().Contains(searchText);
                
                if (!matchesSearch) continue;
                
                // Apply multi-select filters (if no filters are active, show all)
                bool matchesFilter = activeFilters.Count == 0 || CheckHistoryAgainstFilters(history);
                
                if (matchesFilter)
                {
                    filteredHistory.Add(history);
                }
            }
            
            RefreshHistoryDisplay();
        }
        
        bool CheckHistoryAgainstFilters(ToolExecutionHistory history)
        {
            foreach (var filter in activeFilters)
            {
                bool matches = filter switch
                {
                    "Success" => history.executionSuccess,
                    "Failed" => !history.executionSuccess,
                    "Outdated" => !IsToolCurrent(history),
                    "Current" => IsToolCurrent(history),
                    "UI" => history.executionSource == ToolExecutionSource.EditorWindow,
                    "MCP" => history.executionSource == ToolExecutionSource.MCP,
                    _ => true
                };
                
                if (!matches) return false; // AND logic: all active filters must match
            }
            return true;
        }
        
        bool IsToolCurrent(ToolExecutionHistory history)
        {
            var currentTool = availableTools.FirstOrDefault(t => t.name == history.toolName);
            if (currentTool == null) return false;
            
            var currentParamNames = currentTool.inputSchema.orderedProperties.Select(p => p.Name).ToList();
            var historyParamNames = history.parameterNames;
            
            return currentParamNames.Count == historyParamNames.Count &&
                   currentParamNames.All(p => historyParamNames.Contains(p));
        }
        
        void ClearHistory()
        {
            MCPService.MCPExecutionHistory.Clear();
            selectedHistoryIndex = -1;
            FilterAndRefreshHistory();
            
            // 更新展开按钮中的数量显示
            if (isHistoryCollapsed && historyExpandButton != null)
            {
                historyExpandButton.text = "History (0)";
            }
        }
        
        void RefreshHistory()
        {
            FilterAndRefreshHistory();
        }
        
        void RefreshHistoryDisplay()
        {
            if (historyContainer == null) return;
            
            // Clear all items - don't use object pool for simplicity and performance
            historyContainer.Clear();
            historyItemPool.Clear(); // Clear the pool as well
            
            if (filteredHistory.Count == 0)
            {
                var noHistoryLabel = new Label(MCPService.MCPExecutionHistory.Count == 0 ? "No execution history" : "No matching history");
                noHistoryLabel.style.color = Color.gray;
                noHistoryLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                noHistoryLabel.style.paddingTop = 20;
                noHistoryLabel.style.paddingLeft = 10;
                noHistoryLabel.style.fontSize = 12;
                historyContainer.Add(noHistoryLabel);
                return;
            }
            
            // Display filtered history in reverse order (newest first)
            for (int i = filteredHistory.Count - 1; i >= 0; i--)
            {
                var history = filteredHistory[i];
                var originalIndex = MCPService.MCPExecutionHistory.IndexOf(history);
                var historyItem = CreateHistoryItem(history, originalIndex);
                historyContainer.Add(historyItem);
            }
        }
        
        VisualElement CreateHistoryItem(ToolExecutionHistory history, int originalIndex)
        {
            // Always create new items to avoid callback issues
            var itemContainer = new VisualElement();
            
            // Configure item container
            bool isSelected = selectedHistoryIndex == originalIndex;
            bool isToolCurrent = IsToolCurrent(history);
            
            itemContainer.style.backgroundColor = isSelected ? 
                new Color(0.3f, 0.4f, 0.6f, 0.8f) : new Color(0.15f, 0.15f, 0.15f, 0.8f);
            itemContainer.style.marginBottom = 2;
            itemContainer.style.paddingTop = 6;
            itemContainer.style.paddingBottom = 6;
            itemContainer.style.paddingLeft = 8;
            itemContainer.style.paddingRight = 8;
            itemContainer.style.borderLeftWidth = 3;
            itemContainer.style.borderLeftColor = history.executionSuccess ? 
                new Color(0.2f, 0.6f, 0.2f) : new Color(0.6f, 0.2f, 0.2f);
            
            // Store data for callbacks to avoid closure issues
            itemContainer.userData = new { history, originalIndex };
            
            // Add hover effect
            itemContainer.RegisterCallback<MouseEnterEvent>(evt => 
            {
                var container = evt.currentTarget as VisualElement;
                var data = (dynamic)container.userData;
                if (selectedHistoryIndex != data.originalIndex)
                    container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            });
            itemContainer.RegisterCallback<MouseLeaveEvent>(evt => 
            {
                var container = evt.currentTarget as VisualElement;
                var data = (dynamic)container.userData;
                if (selectedHistoryIndex != data.originalIndex)
                    container.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            });
            
            // Handle clicks
            itemContainer.RegisterCallback<ClickEvent>(evt => 
            {
                var container = evt.currentTarget as VisualElement;
                var data = (dynamic)container.userData;
                LoadHistoryItem(data.history, data.originalIndex);
            });
            
            // Handle right clicks for context menu
            itemContainer.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button == 1) // Right click
                {
                    var container = evt.currentTarget as VisualElement;
                    var data = (dynamic)container.userData;
                    ShowHistoryContextMenu(data.history, data.originalIndex);
                }
            });
            
            // Create compact layout
            var mainContainer = new VisualElement();
            mainContainer.style.flexDirection = FlexDirection.Row;
            mainContainer.style.alignItems = Align.Center;
            
            // Status indicator (colored circle)
            var statusIndicator = new VisualElement();
            statusIndicator.style.width = 8;
            statusIndicator.style.height = 8;
            statusIndicator.style.backgroundColor = history.executionSuccess ? 
                new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            statusIndicator.style.borderTopLeftRadius = 4;
            statusIndicator.style.borderTopRightRadius = 4;
            statusIndicator.style.borderBottomLeftRadius = 4;
            statusIndicator.style.borderBottomRightRadius = 4;
            statusIndicator.style.marginRight = 6;
            mainContainer.Add(statusIndicator);
            
            // Tool info container
            var toolInfoContainer = new VisualElement();
            toolInfoContainer.style.flexGrow = 1;
            
            // Tool name with badges
            var toolNameContainer = new VisualElement();
            toolNameContainer.style.flexDirection = FlexDirection.Row;
            toolNameContainer.style.alignItems = Align.Center;
            
            var toolNameLabel = new Label(history.toolName);
            toolNameLabel.style.fontSize = 11;
            toolNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolNameLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            toolNameContainer.Add(toolNameLabel);
            
            // Add badges
            if (!isToolCurrent)
            {
                var badge = new Label("!");
                badge.style.fontSize = 10;
                badge.style.color = new Color(0.9f, 0.6f, 0.2f);
                badge.style.backgroundColor = new Color(0.3f, 0.2f, 0.1f);
                badge.style.paddingLeft = 3;
                badge.style.paddingRight = 3;
                badge.style.paddingTop = 1;
                badge.style.paddingBottom = 1;
                badge.style.marginLeft = 4;
                badge.style.borderTopLeftRadius = 2;
                badge.style.borderTopRightRadius = 2;
                badge.style.borderBottomLeftRadius = 2;
                badge.style.borderBottomRightRadius = 2;
                badge.tooltip = availableTools.Any(t => t.name == history.toolName) ? 
                    "Parameters changed" : "Tool no longer exists";
                toolNameContainer.Add(badge);
            }
            
            toolInfoContainer.Add(toolNameContainer);
            
            // Timestamp and parameter count
            var detailsContainer = new VisualElement();
            detailsContainer.style.flexDirection = FlexDirection.Row;
            detailsContainer.style.justifyContent = Justify.SpaceBetween;
            
            var timestampLabel = new Label(DateTime.Parse(history.timestamp).ToString("HH:mm:ss"));
            timestampLabel.style.fontSize = 9;
            timestampLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            detailsContainer.Add(timestampLabel);
            
            var rightInfoContainer = new VisualElement();
            rightInfoContainer.style.flexDirection = FlexDirection.Row;
            rightInfoContainer.style.alignItems = Align.Center;
            
            if (history.parameterNames.Count > 0)
            {
                var paramCountLabel = new Label($"{history.parameterNames.Count}p");
                paramCountLabel.style.fontSize = 9;
                paramCountLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                paramCountLabel.style.marginRight = 4;
                rightInfoContainer.Add(paramCountLabel);
            }
            
            // Source indicator
            var sourceLabel = new Label(history.executionSource == ToolExecutionSource.EditorWindow ? "UI" : "MCP");
            sourceLabel.style.fontSize = 8;
            sourceLabel.style.color = history.executionSource == ToolExecutionSource.EditorWindow ? 
                new Color(0.4f, 0.6f, 0.8f) : new Color(0.8f, 0.6f, 0.4f);
            sourceLabel.style.backgroundColor = history.executionSource == ToolExecutionSource.EditorWindow ? 
                new Color(0.2f, 0.3f, 0.4f, 0.8f) : new Color(0.4f, 0.3f, 0.2f, 0.8f);
            sourceLabel.style.paddingLeft = 3;
            sourceLabel.style.paddingRight = 3;
            sourceLabel.style.paddingTop = 1;
            sourceLabel.style.paddingBottom = 1;
            sourceLabel.style.borderTopLeftRadius = 2;
            sourceLabel.style.borderTopRightRadius = 2;
            sourceLabel.style.borderBottomLeftRadius = 2;
            sourceLabel.style.borderBottomRightRadius = 2;
            rightInfoContainer.Add(sourceLabel);
            
            detailsContainer.Add(rightInfoContainer);
            
            toolInfoContainer.Add(detailsContainer);
            mainContainer.Add(toolInfoContainer);
            
            itemContainer.Add(mainContainer);
            return itemContainer;
        }
        
        void ShowHistoryContextMenu(ToolExecutionHistory history, int index)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("删除"), false, () => DeleteHistoryItem(index));
            menu.AddItem(new GUIContent("删除此条及之前的所有记录"), false, () => DeleteHistoryItemAndBefore(index));
            menu.ShowAsContext();
        }
        
        void DeleteHistoryItem(int index)
        {
            if (index >= 0 && index < MCPService.MCPExecutionHistory.Count)
            {
                MCPService.MCPExecutionHistory.RemoveAt(index);
                if (selectedHistoryIndex == index)
                {
                    selectedHistoryIndex = -1;
                }
                else if (selectedHistoryIndex > index)
                {
                    selectedHistoryIndex--;
                }
                FilterAndRefreshHistory();
            }
        }
        
        void DeleteHistoryItemAndBefore(int index)
        {
            if (index >= 0 && index < MCPService.MCPExecutionHistory.Count)
            {
                // 删除从0到index的所有记录（包含index）
                // 因为历史记录已按时间排序，直接根据索引删除即可
                for (int i = index; i >= 0; i--)
                {
                    MCPService.MCPExecutionHistory.RemoveAt(i);
                }
                
                // 重置选中索引
                selectedHistoryIndex = -1;
                
                // 刷新显示
                FilterAndRefreshHistory();
            }
        }
        
        void LoadHistoryItem(ToolExecutionHistory history, int index)
        {
            
            // Only update selection if it changed
            if (selectedHistoryIndex != index)
            {
                selectedHistoryIndex = index;
                UpdateHistorySelectionDisplay();
            }
            
            // Find the tool in available tools
            var tool = availableTools.FirstOrDefault(t => t.name == history.toolName);
            bool toolExists = tool != null;
            
            if (toolExists)
            {
                // Select the tool normally
                SelectTool(tool);
                selectedToolName = history.toolName;
            }
            else
            {
                // Tool doesn't exist, create a fake selection for display
                currentSelectedTool = null;
                selectedToolName = history.toolName;
                CreateHistoryToolUI(history);
            }
            
            // Restore parameters and result after UI is created
            EditorApplication.delayCall += () =>
            {
                RestoreHistoryState(history, toolExists);
            };
        }
        
        void UpdateHistorySelectionDisplay()
        {
            // Only update the visual state of history items without recreating them
            if (historyContainer == null) return;
            
            int displayedIndex = 0;
            for (int i = filteredHistory.Count - 1; i >= 0; i--)
            {
                var history = filteredHistory[i];
                var originalIndex = MCPService.MCPExecutionHistory.IndexOf(history);
                
                if (displayedIndex < historyContainer.childCount)
                {
                    var historyElement = historyContainer[displayedIndex];
                    bool isSelected = selectedHistoryIndex == originalIndex;
                    
                    // Update background color based on selection
                    historyElement.style.backgroundColor = isSelected ? 
                        new Color(0.3f, 0.4f, 0.6f, 0.8f) : new Color(0.15f, 0.15f, 0.15f, 0.8f);
                }
                displayedIndex++;
            }
        }

        void CreateHistoryToolUI(ToolExecutionHistory history)
        {
            toolDetailsContainer.Clear();
            currentToolParameterFields.Clear();

            // Tool header with warning
            var headerContainer = new VisualElement();
            headerContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            headerContainer.style.paddingTop = 15;
            headerContainer.style.paddingBottom = 15;
            headerContainer.style.paddingLeft = 20;
            headerContainer.style.paddingRight = 20;
            headerContainer.style.marginBottom = 15;
            headerContainer.style.borderLeftWidth = 4;
            headerContainer.style.borderLeftColor = new Color(0.8f, 0.6f, 0.2f); // Orange warning color

            var titleContainer = new VisualElement();
            titleContainer.style.flexDirection = FlexDirection.Row;
            titleContainer.style.alignItems = Align.Center;
            titleContainer.style.marginBottom = 8;

            var nameLabel = new Label(history.toolName);
            nameLabel.style.fontSize = 18;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            titleContainer.Add(nameLabel);

            var warningBadge = new Label("[TOOL NOT FOUND]");
            warningBadge.style.fontSize = 12;
            warningBadge.style.marginLeft = 15;
            warningBadge.style.color = new Color(0.8f, 0.6f, 0.2f);
            warningBadge.style.backgroundColor = new Color(0.3f, 0.2f, 0.1f, 0.6f);
            warningBadge.style.paddingLeft = 8;
            warningBadge.style.paddingRight = 8;
            warningBadge.style.paddingTop = 2;
            warningBadge.style.paddingBottom = 2;
            titleContainer.Add(warningBadge);

            headerContainer.Add(titleContainer);

            var warningLabel = new Label("This tool is no longer available. Parameters shown are read-only.");
            warningLabel.style.fontSize = 12;
            warningLabel.style.color = new Color(0.8f, 0.6f, 0.2f);
            warningLabel.style.marginBottom = 5;
            headerContainer.Add(warningLabel);

            toolDetailsContainer.Add(headerContainer);

            // Parameters section
            if (history.parameterNames.Count > 0)
            {
                var parametersHeader = new Label($"Parameters: {history.parameterNames.Count}");
                parametersHeader.style.fontSize = 14;
                parametersHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                parametersHeader.style.marginBottom = 10;
                parametersHeader.style.marginLeft = 20;
                parametersHeader.style.color = new Color(0.9f, 0.9f, 0.9f);
                toolDetailsContainer.Add(parametersHeader);

                var parametersContainer = new VisualElement();
                parametersContainer.style.paddingLeft = 20;
                parametersContainer.style.paddingRight = 20;
                parametersContainer.style.marginBottom = 20;

                for (int i = 0; i < history.parameterNames.Count && i < history.parameterValues.Count; i++)
                {
                    CreateHistoryParameterUI(parametersContainer, history.parameterNames[i], history.parameterValues[i]);
                }

                toolDetailsContainer.Add(parametersContainer);
            }

            // Result section
            var resultContainer = new VisualElement();
            resultContainer.style.paddingLeft = 20;
            resultContainer.style.paddingRight = 20;

            var resultHeader = new Label($"Historical Result:{(history.executionSuccess ? "✓ Success" : "✗ Error")}");
            resultHeader.style.fontSize = 14;
            resultHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            resultHeader.style.marginBottom = 10;
            resultHeader.style.color = history.executionSuccess ?
                new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            resultContainer.Add(resultHeader);

            // Create result content container for history
            var historyResultContainer = new VisualElement();
            historyResultContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            historyResultContainer.style.paddingTop = 10;
            historyResultContainer.style.paddingBottom = 10;
            historyResultContainer.style.paddingLeft = 15;
            historyResultContainer.style.paddingRight = 15;

            // Determine colors and if it's an error
            var textColor = history.executionSuccess ?
                new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            bool isError = !history.executionSuccess;

            // Check if this is an error with potential stack trace and hyperlink mode is enabled
            if (isError && isHyperlinkMode && history.executionResult.Contains("at ") &&
                history.executionResult.Contains(" in "))
            {
                // Create hyperlink UI for stack trace
                var stackTraceUI = CreateStackTraceUI(history.executionResult, textColor);
                historyResultContainer.Add(stackTraceUI);
            }
            else
            {
                // Use regular label
                currentToolResultLabel = new Label(history.executionResult);
                currentToolResultLabel.style.fontSize = 12;
                currentToolResultLabel.style.whiteSpace = WhiteSpace.Normal;
                currentToolResultLabel.style.color = textColor;
                // 确保文本能正确换行
                currentToolResultLabel.style.overflow = Overflow.Visible;
                currentToolResultLabel.style.flexWrap = Wrap.Wrap;
                currentToolResultLabel.style.maxWidth = StyleKeyword.None;
                historyResultContainer.Add(currentToolResultLabel);
            }

            resultContainer.Add(historyResultContainer);

            toolDetailsContainer.Add(resultContainer);

        }
        
        void CreateHistoryParameterUI(VisualElement parametersContainer, string paramName, string paramValue)
        {
            var paramContainer = new VisualElement();
            paramContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            paramContainer.style.marginBottom = 8;
            paramContainer.style.paddingTop = 10;
            paramContainer.style.paddingBottom = 10;
            paramContainer.style.paddingLeft = 15;
            paramContainer.style.paddingRight = 15;
            
            var paramLabel = new Label(paramName);
            paramLabel.style.fontSize = 12;
            paramLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            paramLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            paramLabel.style.marginBottom = 5;
            paramContainer.Add(paramLabel);
            
            var paramField = new TextField();
            paramField.style.flexGrow = 1;
            paramField.style.height = 25;
            paramField.value = paramValue;
            paramField.SetEnabled(false); // Read-only for historical parameters
            paramField.style.opacity = 0.7f;
            
            paramContainer.Add(paramField);
            parametersContainer.Add(paramContainer);
            
            currentToolParameterFields[paramName] = paramField;
        }
        
        void RestoreHistoryState(ToolExecutionHistory history, bool toolExists)
        {
            // 切换历史记录时清除结果缓存
            ClearResultCache();
            
            // Restore parameter values
            for (int i = 0; i < history.parameterNames.Count && i < history.parameterValues.Count; i++)
            {
                if (currentToolParameterFields.TryGetValue(history.parameterNames[i], out var field))
                {
                    field.value = history.parameterValues[i];
                    if (!toolExists)
                    {
                        field.SetEnabled(false);
                        field.style.opacity = 0.7f;
                    }
                }
            }
            
            // Restore execution result using SetResultContent to preserve hyperlinks
            if (!string.IsNullOrEmpty(history.executionResult))
            {
                var textColor = history.executionSuccess ? 
                    new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
                bool isError = !history.executionSuccess;
                
                SetResultContent(history.executionResult, textColor, isError);
                UpdateResultHeaderStatus(history.executionSuccess); // 更新结果头部状态
            }
            
            // Hide execute button if tool doesn't exist or parameters don't match
            if (executeButton != null)
            {
                if (!toolExists)
                {
                    executeButton.style.display = DisplayStyle.None;
                }
                else
                {
                    // Check if current tool parameters match history parameters
                    var currentTool = availableTools.FirstOrDefault(t => t.name == history.toolName);
                    if (currentTool != null)
                    {
                        var currentParamNames = currentTool.inputSchema.orderedProperties.Select(p => p.Name).ToList();
                        var historyParamNames = history.parameterNames;
                        
                        // If parameter names don't match exactly, hide execute button
                        bool parametersMatch = currentParamNames.Count == historyParamNames.Count &&
                                             currentParamNames.All(p => historyParamNames.Contains(p));
                        
                        executeButton.style.display = parametersMatch ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                }
            }
        }
        
        void AddToHistory(string toolName, Dictionary<string, string> parameters, string result, bool success, ToolExecutionSource source = ToolExecutionSource.EditorWindow)
        {
            // 统一使用MCPService的历史记录方法
            MCPService.AddExecutionHistory(toolName, parameters, result, success, source);
        }
        
        void ToggleHistoryCollapse()
        {
            isHistoryCollapsed = !isHistoryCollapsed;
            UpdateHistoryCollapseState();
        }
        
        void UpdateHistoryCollapseState()
        {
            if (rightColumn == null || verticalSeparator2 == null || historyCollapseButton == null || historyExpandButton == null)
                return;
            
            if (isHistoryCollapsed)
            {
                // 隐藏整个右栏和分隔符
                rightColumn.style.display = DisplayStyle.None;
                verticalSeparator2.style.display = DisplayStyle.None;
                
                // 显示展开按钮，更新其文本显示历史数量
                historyExpandButton.style.display = DisplayStyle.Flex;
                int historyCount = MCPService.MCPExecutionHistory?.Count ?? 0;
                historyExpandButton.text = $"History ({historyCount})";
            }
            else
            {
                // 显示整个右栏和分隔符
                rightColumn.style.display = DisplayStyle.Flex;
                verticalSeparator2.style.display = DisplayStyle.Flex;
                
                // 隐藏展开按钮
                historyExpandButton.style.display = DisplayStyle.None;
                
                // 更新折叠按钮文本
                if (historyCollapseButton != null)
                {
                    historyCollapseButton.text = "◀";
                }
            }
        }
        
        // Stack trace parsing and hyperlink support methods
        
        /// <summary>
        /// Sets the result content with dual UI element caching system
        /// </summary>
        private void SetResultContent(string content, Color textColor, bool isError = false)
        {
            if (currentToolResultContainer == null) return;
            
            // 检查是否需要重新创建UI元素
            bool needsRecreate = cachedResultContent != content || 
                                 cachedResultColor.r != textColor.r || 
                                 cachedResultColor.g != textColor.g || 
                                 cachedResultColor.b != textColor.b || 
                                 cachedResultIsError != isError;
            
            if (needsRecreate)
            {
                // 清除现有内容
                currentToolResultContainer.Clear();
                
                // 更新缓存信息
                cachedResultContent = content;
                cachedResultColor = textColor;
                cachedResultIsError = isError;
                
                // 创建纯文本UI元素
                CreateTextResultElement(content, textColor);
                
                // 创建超链接UI元素（如果适用）
                if (isError && content.Contains("at ") && content.Contains(" in "))
                {
                    CreateHyperlinkResultElement(content, textColor);
                }
                else
                {
                    // 如果不是错误或没有堆栈跟踪，超链接元素就是文本元素的副本
                    hyperlinkResultElement = null;
                }
                
                // 将两个元素都添加到容器中
                currentToolResultContainer.Add(textResultElement);
                if (hyperlinkResultElement != null)
                {
                    currentToolResultContainer.Add(hyperlinkResultElement);
                }
            }
            
            // 根据当前模式显示/隐藏对应的元素
            UpdateResultDisplay();
        }
        
        /// <summary>
        /// 创建纯文本结果元素
        /// </summary>
        private void CreateTextResultElement(string content, Color textColor)
        {
            textResultElement = new VisualElement();
            
            currentToolResultLabel = new Label(content);
            currentToolResultLabel.style.fontSize = 12;
            currentToolResultLabel.style.whiteSpace = WhiteSpace.Normal;
            currentToolResultLabel.style.color = textColor;
            currentToolResultLabel.style.overflow = Overflow.Visible;
            currentToolResultLabel.style.flexWrap = Wrap.Wrap;
            currentToolResultLabel.style.maxWidth = StyleKeyword.None;
            
            textResultElement.Add(currentToolResultLabel);
        }
        
        /// <summary>
        /// 创建超链接结果元素
        /// </summary>
        private void CreateHyperlinkResultElement(string content, Color textColor)
        {
            hyperlinkResultElement = CreateStackTraceUI(content, textColor);
        }
        
        /// <summary>
        /// 根据当前模式更新结果显示
        /// </summary>
        private void UpdateResultDisplay()
        {
            if (textResultElement == null) return;
            
            bool shouldShowHyperlink = isHyperlinkMode && hyperlinkResultElement != null;
            
            // 显示/隐藏纯文本元素
            textResultElement.style.display = shouldShowHyperlink ? DisplayStyle.None : DisplayStyle.Flex;
            
            // 显示/隐藏超链接元素
            if (hyperlinkResultElement != null)
            {
                hyperlinkResultElement.style.display = shouldShowHyperlink ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        
        /// <summary>
        /// 清除结果UI缓存
        /// </summary>
        private void ClearResultCache()
        {
            cachedResultContent = null;
            textResultElement = null;
            hyperlinkResultElement = null;
            cachedResultColor = default(Color);
            cachedResultIsError = false;
        }
        
        /// <summary>
        /// Gets the current result text content
        /// </summary>
        private string GetCurrentResultText()
        {
            if (currentToolResultLabel != null)
            {
                return currentToolResultLabel.text ?? "";
            }
            
            // If using hyperlink UI, we need to extract text from the container
            if (currentToolResultContainer != null && currentToolResultContainer.childCount > 0)
            {
                var firstChild = currentToolResultContainer[0];
                if (firstChild is Label label)
                {
                    return label.text ?? "";
                }
                else if (firstChild is VisualElement container)
                {
                    // Extract text from hyperlink container - this is a simplified approach
                    var textParts = new List<string>();
                    ExtractTextFromElement(container, textParts);
                    return string.Join("", textParts);
                }
            }
            
            return "";
        }
        
        /// <summary>
        /// Recursively extracts text from UI elements
        /// </summary>
        private void ExtractTextFromElement(VisualElement element, List<string> textParts)
        {
            if (element is Label label)
            {
                textParts.Add(label.text ?? "");
            }
            else if (element is Button button)
            {
                textParts.Add(button.text ?? "");
            }
            
            // Recursively process children
            for (int i = 0; i < element.childCount; i++)
            {
                ExtractTextFromElement(element[i], textParts);
            }
        }
        
        /// <summary>
        /// Parses exception text and extracts stack trace items
        /// </summary>
        private List<StackTraceItem> ParseStackTrace(string exceptionText)
        {
            var stackTraceItems = new List<StackTraceItem>();
            if (string.IsNullOrEmpty(exceptionText))
                return stackTraceItems;
                
            var matches = stackTraceRegex.Matches(exceptionText);
            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count >= 4)
                {
                    string methodName = match.Groups[1].Value.Trim();
                    string filePath = match.Groups[2].Value.Trim();
                    if (Regex.IsMatch(filePath, @"^<[a-fA-F0-9]+>$"))
                    {
                        continue; // 跳过无效地址
                    }
                    
                    if (int.TryParse(match.Groups[3].Value, out int lineNumber))
                    {
                        stackTraceItems.Add(new StackTraceItem(methodName, filePath, lineNumber, match.Value));
                    }
                }
            }
            
            return stackTraceItems;
        }
        
        /// <summary>
        /// Creates a UI element with hyperlinks for stack trace
        /// </summary>
        private VisualElement CreateStackTraceUI(string exceptionText, Color textColor)
        {
            var container = new VisualElement();
            container.style.whiteSpace = WhiteSpace.Normal;
            container.style.flexWrap = Wrap.Wrap;
            container.style.overflow = Overflow.Visible;
            
            var stackTraceItems = ParseStackTrace(exceptionText);
            
            if (stackTraceItems.Count == 0)
            {
                // No stack trace found, use regular label
                var label = new Label(exceptionText);
                label.style.color = textColor;
                label.style.fontSize = 12;
                label.style.whiteSpace = WhiteSpace.Normal;
                // 确保文本能正确换行
                label.style.overflow = Overflow.Visible;
                label.style.flexWrap = Wrap.Wrap;
                label.style.maxWidth = StyleKeyword.None;
                container.Add(label);
                return container;
            }
            
            // Split text by lines and create UI elements
            var lines = exceptionText.Split('\n');
            foreach (var line in lines)
            {
                var lineContainer = new VisualElement();
                lineContainer.style.flexDirection = FlexDirection.Row;
                lineContainer.style.flexWrap = Wrap.Wrap;
                
                // Check if this line contains a stack trace
                var stackItem = stackTraceItems.FirstOrDefault(item => line.Contains(item.originalLine.Trim()));
                
                if (stackItem != null)
                {
                    // Create hyperlink for stack trace line
                    CreateStackTraceHyperlink(lineContainer, line, stackItem, textColor);
                }
                else
                {
                    // Regular text line
                    var label = new Label(line);
                    label.style.color = textColor;
                    label.style.fontSize = 12;
                    label.style.whiteSpace = WhiteSpace.Normal;
                    // 确保文本能正确换行
                    label.style.overflow = Overflow.Visible;
                    label.style.flexWrap = Wrap.Wrap;
                    label.style.maxWidth = StyleKeyword.None;
                    lineContainer.Add(label);
                }
                
                container.Add(lineContainer);
            }
            
            return container;
        }
        
        /// <summary>
        /// Creates a hyperlink for a stack trace line
        /// </summary>
        private void CreateStackTraceHyperlink(VisualElement container, string line, StackTraceItem stackItem, Color baseColor)
        {
            // Find the file path part in the line
            int atIndex = line.IndexOf("at ");
            int inIndex = line.IndexOf(" in ");
            
            if (atIndex >= 0 && inIndex > atIndex)
            {
                // Add prefix text (usually spaces and "at")
                if (atIndex > 0)
                {
                    var prefixLabel = new Label(line.Substring(0, atIndex));
                    prefixLabel.style.color = baseColor;
                    prefixLabel.style.fontSize = 12;
                    container.Add(prefixLabel);
                }
                
                // Add "at " text
                var atLabel = new Label("at ");
                atLabel.style.color = baseColor;
                atLabel.style.fontSize = 12;
                container.Add(atLabel);
                
                // Find method name and hex address part
                string middlePart = line.Substring(atIndex + 3, inIndex - atIndex - 3);
                
                // 优化方法名显示：移除冗长命名空间并简化基础类型
                string optimizedMethodText = GetOptimizedMethodDisplay(middlePart);
                var methodLabel = new Label(optimizedMethodText);
                methodLabel.style.color = baseColor;
                methodLabel.style.fontSize = 12;
                // 确保文本能正确换行
                methodLabel.style.whiteSpace = WhiteSpace.Normal;
                methodLabel.style.overflow = Overflow.Visible;
                methodLabel.style.flexWrap = Wrap.Wrap;
                methodLabel.style.maxWidth = StyleKeyword.None;
                container.Add(methodLabel);
                
                // Add " in " text
                var inLabel = new Label(" in ");
                inLabel.style.color = baseColor;
                inLabel.style.fontSize = 12;
                container.Add(inLabel);
                
                // Add clickable file path with optimized display
                string filePart = line.Substring(inIndex + 4);
                string optimizedDisplay = GetOptimizedPathDisplay(stackItem.filePath) + ":" + stackItem.lineNumber;
                var fileButton = new Button(() => OpenFileAtLine(stackItem.filePath, stackItem.lineNumber));
                fileButton.text = optimizedDisplay;
                fileButton.style.backgroundColor = Color.clear;
                fileButton.style.borderTopWidth = 0;
                fileButton.style.borderBottomWidth = 0;
                fileButton.style.borderLeftWidth = 0;
                fileButton.style.borderRightWidth = 0;
                fileButton.style.paddingTop = 0;
                fileButton.style.paddingBottom = 0;
                fileButton.style.paddingLeft = 0;
                fileButton.style.paddingRight = 0;
                fileButton.style.marginTop = 0;
                fileButton.style.marginBottom = 0;
                fileButton.style.marginLeft = 0;
                fileButton.style.marginRight = 0;
                fileButton.style.color = new Color(0.4f, 0.6f, 1.0f); // Blue hyperlink color
                fileButton.style.fontSize = 12;
                fileButton.style.unityTextAlign = TextAnchor.MiddleLeft;
                // 确保文件路径按钮能正确换行
                fileButton.style.whiteSpace = WhiteSpace.Normal;
                fileButton.style.overflow = Overflow.Visible;
                fileButton.style.flexWrap = Wrap.Wrap;
                fileButton.style.maxWidth = StyleKeyword.None;
                
                // Add hover effect
                fileButton.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    fileButton.style.color = new Color(0.6f, 0.8f, 1.0f);
                    // Note: Unity's FontStyle doesn't have Underline, using color change instead
                });
                
                fileButton.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    fileButton.style.color = new Color(0.4f, 0.6f, 1.0f);
                });
                
                container.Add(fileButton);
            }
            else
            {
                // Fallback: treat whole line as regular text
                var label = new Label(line);
                label.style.color = baseColor;
                label.style.fontSize = 12;
                // 确保文本能正确换行
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.overflow = Overflow.Visible;
                label.style.flexWrap = Wrap.Wrap;
                label.style.maxWidth = StyleKeyword.None;
                container.Add(label);
            }
        }
        
        /// <summary>
        /// 优化方法名显示，移除冗长的命名空间并简化基础类型名称
        /// </summary>
        private string GetOptimizedMethodDisplay(string originalMethodText)
        {
            try
            {
                if (string.IsNullOrEmpty(originalMethodText))
                    return originalMethodText;
                
                string optimized = originalMethodText;
                
                // 移除常见的命名空间前缀
                var namespacesToRemove = new string[]
                {
                    "TreeNode.Runtime.",
                    "TreeNode.Editor.",
                    "TreeNode.Utility.",
                    "System.Collections.Generic.",
                    "System.Collections.",
                    "System.",
                    "UnityEngine.",
                    "UnityEditor.",
                    "Microsoft.CSharp.",
                    "SkillEditorDemo."
                };
                
                foreach (var ns in namespacesToRemove)
                {
                    optimized = optimized.Replace(ns, "");
                }
                
                // 简化基础类型名称
                var typeReplacements = new Dictionary<string, string>
                {
                    { "System.String", "string" },
                    { "System.Int32", "int" },
                    { "System.Int64", "long" },
                    { "System.Int16", "short" },
                    { "System.UInt32", "uint" },
                    { "System.UInt64", "ulong" },
                    { "System.UInt16", "ushort" },
                    { "System.Byte", "byte" },
                    { "System.SByte", "sbyte" },
                    { "System.Boolean", "bool" },
                    { "System.Single", "float" },
                    { "System.Double", "double" },
                    { "System.Decimal", "decimal" },
                    { "System.Char", "char" },
                    { "System.Object", "object" },
                    { "System.Void", "void" },
                    
                    // 处理已经简化的类型（去掉System.前缀后的）
                    { "String", "string" },
                    { "Int32", "int" },
                    { "Int64", "long" },
                    { "Int16", "short" },
                    { "UInt32", "uint" },
                    { "UInt64", "ulong" },
                    { "UInt16", "ushort" },
                    { "Byte", "byte" },
                    { "SByte", "sbyte" },
                    { "Boolean", "bool" },
                    { "Single", "float" },
                    { "Double", "double" },
                    { "Decimal", "decimal" },
                    { "Char", "char" },
                    { "Object", "object" },
                    { "Void", "void" },
                    
                    // 集合类型简化
                    { "IList", "IList" },
                    { "ICollection", "ICollection" },
                    { "IEnumerable", "IEnumerable" },
                    { "List", "List" },
                    { "Dictionary", "Dictionary" }
                };
                
                foreach (var replacement in typeReplacements)
                {
                    // 使用词边界匹配，避免替换部分单词
                    optimized = System.Text.RegularExpressions.Regex.Replace(
                        optimized, 
                        @"\b" + System.Text.RegularExpressions.Regex.Escape(replacement.Key) + @"\b", 
                        replacement.Value);
                }
                
                return optimized;
            }
            catch (System.Exception)
            {
                // 出错时返回原始文本
                return originalMethodText;
            }
        }
        
        /// <summary>
        /// 优化文件路径显示，项目内文件显示相对路径，项目外文件显示绝对路径
        /// </summary>
        private string GetOptimizedPathDisplay(string originalPath)
        {
            try
            {
                // 标准化路径分隔符
                string normalizedPath = originalPath.Replace('\\', '/');
                
                // 获取项目根目录路径
                string projectPath = Application.dataPath.Replace("/Assets", "").Replace('\\', '/');
                
                // 检查是否为项目内路径
                if (normalizedPath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    // 计算相对于项目根目录的路径
                    string relativePath = normalizedPath.Substring(projectPath.Length);
                    if (relativePath.StartsWith("/"))
                        relativePath = relativePath.Substring(1);
                    return relativePath;
                }
                
                // 检查是否为Assets路径（相对路径）
                if (normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    return normalizedPath;
                }
                
                // 尝试在项目中查找同名文件
                string fileName = Path.GetFileName(normalizedPath);
                string[] guids = AssetDatabase.FindAssets($"t:Script {Path.GetFileNameWithoutExtension(fileName)}");
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (assetPath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return assetPath; // 返回项目内的相对路径
                    }
                }
                
                // 如果不是项目内文件，返回绝对路径
                return originalPath;
            }
            catch (Exception)
            {
                // 出错时返回原始路径
                return originalPath;
            }
        }
        
        /// <summary>
        /// Opens a file at the specified line in Unity editor
        /// </summary>
        private void OpenFileAtLine(string filePath, int lineNumber)
        {
            try
            {
                // Normalize path separators
                string normalizedPath = filePath.Replace('\\', '/');
                
                // Try to find the file in the project
                string[] guids = AssetDatabase.FindAssets("t:Script");
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (assetPath.EndsWith(Path.GetFileName(normalizedPath), StringComparison.OrdinalIgnoreCase))
                    {
                        // Open the script at the specified line
                        UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(assetPath, lineNumber);
                        return;
                    }
                }
                
                // If not found in assets, try to open directly if the file exists
                if (File.Exists(normalizedPath))
                {
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(normalizedPath, lineNumber);
                }
                else
                {
                    Debug.LogWarning($"Could not find file: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error opening file {filePath} at line {lineNumber}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 切换超链接模式和纯文本模式
        /// </summary>
        private void ToggleHyperlinkMode()
        {
            isHyperlinkMode = !isHyperlinkMode;
            
            // 更新按钮文本
            if (hyperlinkModeToggleButton != null)
            {
                hyperlinkModeToggleButton.text = isHyperlinkMode ? "🔗" : "📄";
            }
            
            // 刷新当前结果显示
            RefreshCurrentResultDisplay();
        }
        
        /// <summary>
        /// 刷新当前结果显示（仅切换显示模式，不重新创建内容）
        /// </summary>
        private void RefreshCurrentResultDisplay()
        {
            // 如果已有缓存的结果，只需要切换显示模式
            if (textResultElement != null)
            {
                UpdateResultDisplay();
            }
            // 如果没有缓存，则使用完整的设置流程
            else if (currentToolResultContainer != null && !string.IsNullOrEmpty(lastExecutionResult))
            {
                string content = lastExecutionResult;
                Color textColor = lastExecutionSuccess ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
                SetResultContent(content, textColor, !lastExecutionSuccess);
            }
        }
        
        /// <summary>
        /// 更新结果头部状态显示
        /// </summary>
        private void UpdateResultHeaderStatus(bool isSuccess)
        {
            if (currentToolResultHeaderLabel != null)
            {
                string statusIcon = isSuccess ? "✓" : "✗";
                string statusText = isSuccess ? "Success" : "Error";
                currentToolResultHeaderLabel.text = $"Result:{statusIcon} {statusText}";
                
                // 设置状态颜色
                Color statusColor = isSuccess ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
                currentToolResultHeaderLabel.style.color = statusColor;
            }
        }
    }
}

using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Reflection;
using System;

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
        MCPTool currentSelectedTool;
        List<MCPTool> availableTools = new List<MCPTool>();
        List<Button> toolButtons = new List<Button>();
        Button executeButton;
        bool isViewingHistory = false;
        
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
        int selectedHistoryIndex = -1;
        List<VisualElement> historyItemPool = new List<VisualElement>();
        List<ToolExecutionHistory> filteredHistory = new List<ToolExecutionHistory>();

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            
            // Auto start toggle
            Toggle toggle = new("Auto Start")
            {
                value = EditorPrefs.GetBool("MCP4Unity_Auto_Start", true)
            };
            toggle.RegisterValueChangedCallback(OnToggle);
            root.Add(toggle);
            
            // Start/Stop button
            startBtn = new Button(OnClickStart) { text = "Start" };
            startBtn.style.height = 30;
            root.Add(startBtn);
            


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
            leftColumn.style.minWidth = 200;
            leftColumn.style.maxWidth = 300;
            leftColumn.style.marginRight = 5;
            threeColumnContainer.Add(leftColumn);
            
            // Tool list header
            var toolListHeader = new VisualElement();
            toolListHeader.style.flexDirection = FlexDirection.Row;
            toolListHeader.style.alignItems = Align.Center;
            toolListHeader.style.marginBottom = 10;
            leftColumn.Add(toolListHeader);
            
            var toolsHeaderLabel = new Label("Available Tools");
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
            threeColumnContainer.Add(middleColumn);
            
            // Tool details header
            var toolDetailsHeader = new VisualElement();
            toolDetailsHeader.style.flexDirection = FlexDirection.Row;
            toolDetailsHeader.style.alignItems = Align.Center;
            toolDetailsHeader.style.marginBottom = 10;
            
            var toolDetailsLabel = new Label("Tool Details");
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
            rightColumn.style.minWidth = 200;
            rightColumn.style.maxWidth = 350;
            threeColumnContainer.Add(rightColumn);
            
            // History header
            var historyHeaderContainer = new VisualElement();
            historyHeaderContainer.style.flexDirection = FlexDirection.Row;
            historyHeaderContainer.style.alignItems = Align.Center;
            historyHeaderContainer.style.marginBottom = 10;
            rightColumn.Add(historyHeaderContainer);
            
            historyHeaderLabel = new Label("Execution History");
            historyHeaderLabel.style.fontSize = 16;
            historyHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            historyHeaderLabel.style.flexGrow = 1;
            historyHeaderContainer.Add(historyHeaderLabel);
            
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
                            if (!string.IsNullOrEmpty(lastExecutionResult) && currentToolResultLabel != null)
                            {
                                currentToolResultLabel.text = lastExecutionResult;
                                // 根据上次执行是否成功设置颜色
                                if (lastExecutionSuccess)
                                {
                                    currentToolResultLabel.style.color = new Color(0.2f, 0.8f, 0.2f); // 绿色表示成功
                                }
                                else
                                {
                                    currentToolResultLabel.style.color = new Color(0.8f, 0.2f, 0.2f); // 红色表示失败
                                }
                            }
                            else if (currentToolResultLabel != null)
                            {
                                // 如果没有执行历史，确保显示默认提示文本和颜色
                                currentToolResultLabel.text = "Execute the tool to see results here.";
                                currentToolResultLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
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
            isViewingHistory = false; // Reset viewing history flag when selecting a tool normally
            
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
            
            var resultHeader = new Label("Result");
            resultHeader.style.fontSize = 14;
            resultHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            resultHeader.style.marginBottom = 10;
            resultHeader.style.color = new Color(0.9f, 0.9f, 0.9f);
            resultContainer.Add(resultHeader);
            
            currentToolResultLabel = new Label("Execute the tool to see results here.");
            currentToolResultLabel.style.fontSize = 12;
            currentToolResultLabel.style.whiteSpace = WhiteSpace.Normal;
            currentToolResultLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            currentToolResultLabel.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            currentToolResultLabel.style.paddingTop = 10;
            currentToolResultLabel.style.paddingBottom = 10;
            currentToolResultLabel.style.paddingLeft = 15;
            currentToolResultLabel.style.paddingRight = 15;
            resultContainer.Add(currentToolResultLabel);
            
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
            paramField.style.height = 25;
            
            // Set tooltip
            string tooltip = $"Type: {property.type}";
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
                dropdownButton.style.height = 25;
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
                currentToolResultLabel.text = "Executing...";
                currentToolResultLabel.style.color = new Color(0.8f, 0.8f, 0.2f);
                
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
                            else
                            {
                                convertedValue = value;
                            }
                            
                            parameters[field.Key] = JToken.FromObject(convertedValue);
                        }
                        catch
                        {
                            // If parsing fails, use as string
                            parameters[field.Key] = value;
                        }
                    }
                }
                
                // Call the tool
                var result = MCPFunctionInvoker.Invoke(currentSelectedTool.name, parameters);
                
                // Display result
                string resultText = result?.ToString() ?? "null";
                
                currentToolResultLabel.text = $"✓ Success: \n{resultText}";
                currentToolResultLabel.style.color = new Color(0.2f, 0.8f, 0.2f);
                
                // Add to history
                var parameterDict = new Dictionary<string, string>();
                foreach (var field in currentToolParameterFields)
                {
                    parameterDict[field.Key] = field.Value.value ?? "";
                }
                AddToHistory(currentSelectedTool.name, parameterDict, currentToolResultLabel.text, true);
                
                isViewingHistory = false; // Reset viewing history flag
                
                Debug.Log($"MCP Tool '{currentSelectedTool.name}' executed successfully. Result: {result}");
            }
            catch (System.Exception ex)
            {
                string errorText = $"✗ Error: {ex.Message}";
                currentToolResultLabel.text = errorText;
                currentToolResultLabel.style.color = new Color(0.8f, 0.2f, 0.2f);
                
                // Add to history even for errors
                var parameterDict = new Dictionary<string, string>();
                foreach (var field in currentToolParameterFields)
                {
                    parameterDict[field.Key] = field.Value.value ?? "";
                }
                AddToHistory(currentSelectedTool.name, parameterDict, errorText, false);
                
                isViewingHistory = false; // Reset viewing history flag
                
                Debug.LogError($"Error executing MCP Tool '{currentSelectedTool.name}': {ex}");
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
                if (currentToolResultLabel != null)
                {
                    var currentText = currentToolResultLabel.text;
                    // 只保存真正的执行结果，不保存默认提示文本
                    if (!string.IsNullOrEmpty(currentText) && 
                        currentText != "Execute the tool to see results here." &&
                        currentText != "Executing...")
                    {
                        lastExecutionResult = currentText;
                        // 通过颜色判断是否成功执行（绿色表示成功，红色表示失败）
                        var color = currentToolResultLabel.style.color.value;
                        lastExecutionSuccess = (color.r < 0.5f && color.g > 0.5f); // 绿色色调判断
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
            menu.AddItem(new GUIContent("Delete"), false, () => DeleteHistoryItem(index));
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
        
        void LoadHistoryItem(ToolExecutionHistory history, int index)
        {
            isViewingHistory = true;
            
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
            
            var resultHeader = new Label("Historical Result");
            resultHeader.style.fontSize = 14;
            resultHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            resultHeader.style.marginBottom = 10;
            resultHeader.style.color = new Color(0.9f, 0.9f, 0.9f);
            resultContainer.Add(resultHeader);
            
            currentToolResultLabel = new Label(history.executionResult);
            currentToolResultLabel.style.fontSize = 12;
            currentToolResultLabel.style.whiteSpace = WhiteSpace.Normal;
            currentToolResultLabel.style.color = history.executionSuccess ? 
                new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            currentToolResultLabel.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            currentToolResultLabel.style.paddingTop = 10;
            currentToolResultLabel.style.paddingBottom = 10;
            currentToolResultLabel.style.paddingLeft = 15;
            currentToolResultLabel.style.paddingRight = 15;
            resultContainer.Add(currentToolResultLabel);
            
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
            
            // Restore execution result
            if (currentToolResultLabel != null)
            {
                currentToolResultLabel.text = history.executionResult;
                currentToolResultLabel.style.color = history.executionSuccess ? 
                    new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
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
    }
}

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
    public class MCPEditorWindow : EditorWindow
    {
        public static MCPEditorWindow Inst;
        [MenuItem("Window/MCP Service Manager")]
        public static void ShowWindow()
        {
            Inst = GetWindow<MCPEditorWindow>();
        }
        
        Button startBtn;
        VisualElement toolListContainer;
        VisualElement toolDetailsContainer;
        Dictionary<string, TextField> currentToolParameterFields = new();
        Label currentToolResultLabel;
        MCPTool currentSelectedTool;
        List<MCPTool> availableTools = new List<MCPTool>();
        List<Button> toolButtons = new List<Button>();

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
            
            // Two-column layout container
            var twoColumnContainer = new VisualElement();
            twoColumnContainer.style.flexDirection = FlexDirection.Row;
            twoColumnContainer.style.flexGrow = 1;
            root.Add(twoColumnContainer);
            
            // Left column - Tool List
            var leftColumn = new VisualElement();
            leftColumn.style.width = new Length(40, LengthUnit.Percent);
            leftColumn.style.minWidth = 250;
            leftColumn.style.maxWidth = 400;
            leftColumn.style.marginRight = 10;
            twoColumnContainer.Add(leftColumn);
            
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
            
            // Vertical separator
            var verticalSeparator = new VisualElement();
            verticalSeparator.style.width = 1;
            verticalSeparator.style.backgroundColor = Color.gray;
            verticalSeparator.style.marginLeft = 5;
            verticalSeparator.style.marginRight = 5;
            twoColumnContainer.Add(verticalSeparator);
            
            // Right column - Tool Details
            var rightColumn = new VisualElement();
            rightColumn.style.flexGrow = 1;
            twoColumnContainer.Add(rightColumn);
            
            // Tool details header
            var toolDetailsHeader = new Label("Tool Details");
            toolDetailsHeader.style.fontSize = 16;
            toolDetailsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolDetailsHeader.style.marginBottom = 10;
            rightColumn.Add(toolDetailsHeader);
            
            // Tool details scroll view
            var toolDetailsScrollView = new ScrollView();
            toolDetailsScrollView.style.flexGrow = 1;
            rightColumn.Add(toolDetailsScrollView);
            
            toolDetailsContainer = toolDetailsScrollView.contentContainer;
            
            MCPService.OnStateChange += UpdateStartBtn;
            MCPService.OnStateChange += RefreshTools;
            UpdateStartBtn();
            RefreshTools();
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
            
            // Restore previously selected tool
            string preferredTool = EditorPrefs.GetString("MCP4Unity_SelectedTool", "");
            var preferredToolObj = availableTools.FirstOrDefault(t => t.name == preferredTool);
            if (preferredToolObj != null)
            {
                SelectTool(preferredToolObj);
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
            
            var toolName = new Label($"{tool.name}({tool.inputSchema.orderedProperties.Count})");
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
                    var toolName = parenIndex > 0 ? labelText.Substring(0, parenIndex) : labelText;
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
            
            var nameLabel = new Label(tool.name);
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
            
            var executionHeader = new Label("Execution");
            executionHeader.style.fontSize = 14;
            executionHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            executionHeader.style.marginBottom = 10;
            executionHeader.style.color = new Color(0.9f, 0.9f, 0.9f);
            executionContainer.Add(executionHeader);
            
            var actionContainer = new VisualElement();
            actionContainer.style.flexDirection = FlexDirection.Row;
            actionContainer.style.alignItems = Align.Center;
            
            var executeButton = new Button(() => ExecuteCurrentTool()) { text = "Execute Tool" };
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
            
            inputContainer.Add(paramField);
            currentToolParameterFields[property.Name] = paramField;
            
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
                
                Debug.Log($"MCP Tool '{currentSelectedTool.name}' executed successfully. Result: {result}");
            }
            catch (System.Exception ex)
            {
                currentToolResultLabel.text = $"✗ Error: {ex.Message}";
                currentToolResultLabel.style.color = new Color(0.8f, 0.2f, 0.2f);
                Debug.LogError($"Error executing MCP Tool '{currentSelectedTool.name}': {ex}");
            }
        }
    }
}

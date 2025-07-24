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
        DropdownField toolSelector;
        VisualElement toolContainer;
        Dictionary<string, TextField> currentToolParameterFields = new Dictionary<string, TextField>();
        Label currentToolResultLabel;
        MCPTool currentSelectedTool;
        List<MCPTool> availableTools = new List<MCPTool>();

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
            
            // Tools section header
            var toolsHeader = new Label("MCP Tool Selector");
            toolsHeader.style.fontSize = 16;
            toolsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolsHeader.style.marginBottom = 10;
            root.Add(toolsHeader);
            
            // Refresh tools button
            var refreshBtn = new Button(RefreshTools) { text = "Refresh Tools" };
            refreshBtn.style.height = 25;
            refreshBtn.style.marginBottom = 10;
            root.Add(refreshBtn);
            
            // Tool selector dropdown
            toolSelector = new DropdownField("Select Tool:");
            toolSelector.style.marginBottom = 15;
            toolSelector.RegisterValueChangedCallback(OnToolSelected);
            root.Add(toolSelector);
            
            // Tool content container
            toolContainer = new VisualElement();
            toolContainer.style.flexGrow = 1;
            
            // Create scroll view for the tool content
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.Add(toolContainer);
            root.Add(scrollView);
            
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
            toolContainer.Clear();
            currentToolParameterFields.Clear();
            currentToolResultLabel = null;
            currentSelectedTool = null;
            availableTools.Clear();
            
            if (!MCPService.Inst.Running)
            {
                toolSelector.choices = new List<string> { "MCP Service is not running" };
                toolSelector.value = "MCP Service is not running";
                toolSelector.SetEnabled(false);
                
                var noServiceLabel = new Label("Start the MCP service to see available tools.");
                noServiceLabel.style.color = Color.gray;
                noServiceLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                noServiceLabel.style.paddingTop = 20;
                noServiceLabel.style.paddingLeft = 20;
                toolContainer.Add(noServiceLabel);
                return;
            }
            
            var tools = MCPFunctionInvoker.Tools;
            if (tools.Count == 0)
            {
                toolSelector.choices = new List<string> { "No tools available" };
                toolSelector.value = "No tools available";
                toolSelector.SetEnabled(false);
                
                var noToolsLabel = new Label("No MCP tools found.");
                noToolsLabel.style.color = Color.gray;
                noToolsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                noToolsLabel.style.paddingTop = 20;
                noToolsLabel.style.paddingLeft = 20;
                toolContainer.Add(noToolsLabel);
                return;
            }
            
            // Populate available tools and create dropdown options
            availableTools = tools.Values.OrderBy(t => GetToolCategory(t)).ThenBy(t => t.name).ToList();
            var dropdownOptions = new List<string>();
            
            // Add "Select a tool..." as first option
            dropdownOptions.Add("Select a tool...");
            
            // Create detailed dropdown options with category and description
            foreach (var tool in availableTools)
            {
                string category = GetToolCategory(tool);
                string displayName = $"[{category}] {tool.name}";
                if (!string.IsNullOrEmpty(tool.description))
                {
                    // Truncate description if too long
                    string desc = tool.description.Length > 50 ? 
                        tool.description.Substring(0, 50) + "..." : 
                        tool.description;
                    displayName += $" - {desc}";
                }
                dropdownOptions.Add(displayName);
            }
            
            toolSelector.choices = dropdownOptions;
            toolSelector.SetEnabled(true);
            
            // Restore previously selected tool or set to first option
            string preferredTool = EditorPrefs.GetString("MCP4Unity_SelectedTool", "");
            var preferredIndex = availableTools.FindIndex(t => t.name == preferredTool);
            if (preferredIndex >= 0)
            {
                toolSelector.value = dropdownOptions[preferredIndex + 1]; // +1 because of "Select a tool..." option
                SelectTool(availableTools[preferredIndex]);
            }
            else
            {
                toolSelector.value = "Select a tool...";
                ShowToolSelectionPrompt();
            }
        }
        
        void OnToolSelected(ChangeEvent<string> evt)
        {
            if (evt.newValue == "Select a tool..." || string.IsNullOrEmpty(evt.newValue))
            {
                ShowToolSelectionPrompt();
                return;
            }
            
            // Find the selected tool by matching the dropdown option
            var selectedIndex = toolSelector.choices.IndexOf(evt.newValue) - 1; // -1 because of "Select a tool..." option
            if (selectedIndex >= 0 && selectedIndex < availableTools.Count)
            {
                var selectedTool = availableTools[selectedIndex];
                SelectTool(selectedTool);
                
                // Save selection
                EditorPrefs.SetString("MCP4Unity_SelectedTool", selectedTool.name);
            }
        }
        
        void ShowToolSelectionPrompt()
        {
            toolContainer.Clear();
            currentSelectedTool = null;
            currentToolParameterFields.Clear();
            currentToolResultLabel = null;
            
            var promptLabel = new Label("Please select a tool from the dropdown above to configure and execute it.");
            promptLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            promptLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            promptLabel.style.paddingTop = 20;
            promptLabel.style.paddingLeft = 20;
            promptLabel.style.fontSize = 14;
            toolContainer.Add(promptLabel);
            
            // Show summary of available tools
            if (availableTools.Count > 0)
            {
                var summaryLabel = new Label($"Available: {availableTools.Count} tools across {GetUniqueCategories().Count} categories");
                summaryLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                summaryLabel.style.fontSize = 12;
                summaryLabel.style.paddingTop = 10;
                summaryLabel.style.paddingLeft = 20;
                toolContainer.Add(summaryLabel);
                
                // Show categories
                var categoriesContainer = new VisualElement();
                categoriesContainer.style.paddingLeft = 20;
                categoriesContainer.style.paddingTop = 10;
                
                foreach (var category in GetUniqueCategories())
                {
                    var toolsInCategory = availableTools.Where(t => GetToolCategory(t) == category).ToList();
                    var categoryLabel = new Label($"• {category}: {toolsInCategory.Count} tool{(toolsInCategory.Count != 1 ? "s" : "")}");
                    categoryLabel.style.color = new Color(0.5f, 0.7f, 0.9f);
                    categoryLabel.style.fontSize = 11;
                    categoryLabel.style.marginBottom = 2;
                    categoriesContainer.Add(categoryLabel);
                }
                
                toolContainer.Add(categoriesContainer);
            }
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
            toolContainer.Clear();
            currentToolParameterFields.Clear();
            
            CreateSelectedToolUI(tool);
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
            var paramInfoLabel = new Label($"Parameters: {paramCount}");
            paramInfoLabel.style.fontSize = 11;
            paramInfoLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            headerContainer.Add(paramInfoLabel);
            
            toolContainer.Add(headerContainer);
            
            // Parameters section
            if (paramCount > 0)
            {
                var parametersHeader = new Label("Parameters");
                parametersHeader.style.fontSize = 14;
                parametersHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                parametersHeader.style.marginBottom = 10;
                parametersHeader.style.marginLeft = 20;
                parametersHeader.style.color = new Color(0.9f, 0.9f, 0.9f);
                toolContainer.Add(parametersHeader);
                
                var parametersContainer = new VisualElement();
                parametersContainer.style.paddingLeft = 20;
                parametersContainer.style.paddingRight = 20;
                parametersContainer.style.marginBottom = 20;
                
                foreach (var property in tool.inputSchema.orderedProperties)
                {
                    CreateParameterUI(parametersContainer, tool, property);
                }
                
                toolContainer.Add(parametersContainer);
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
            toolContainer.Add(executionContainer);
            
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
            
            toolContainer.Add(resultContainer);
        }
        
        string GetToolCategory(MCPTool tool)
        {
            // Get category from the declaring type name
            string typeName = tool.MethodInfo.DeclaringType.Name;
            
            // Remove common suffixes to get cleaner category names
            if (typeName.EndsWith("Tools"))
            {
                typeName = typeName.Substring(0, typeName.Length - 5);
            }
            
            // Convert to more readable format
            switch (typeName.ToLower())
            {
                case "node": return "Node Operations";
                case "buff": return "Buff Management";
                case "asset": return "Asset Operations";
                case "file": return "File Operations";
                default: return typeName;
            }
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
                
                // Call the dropdown method to get options
                var parameters = new object[] { tool.MethodInfo, new Dictionary<string, object>() };
                var result = dropdownMethod.Invoke(null, parameters);
                
                if (result is List<string> options && options.Count > 0)
                {
                    ShowDropdownMenu(targetField, options);
                }
                else
                {
                    Debug.LogWarning($"Dropdown method '{dropdownAttr.MethodName}' returned no options");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error showing dropdown for parameter '{property.Name}': {ex.Message}");
            }
        }
        
        void ShowDropdownMenu(TextField targetField, List<string> options)
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
                
                currentToolResultLabel.text = $"✓ Success: {resultText}";
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

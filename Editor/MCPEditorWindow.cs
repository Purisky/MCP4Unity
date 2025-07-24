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
        VisualElement tabContainer;
        VisualElement tabButtonContainer;
        VisualElement currentTabContent;
        Dictionary<string, Dictionary<string, TextField>> toolParameterFields = new Dictionary<string, Dictionary<string, TextField>>();
        Dictionary<string, Label> toolResultLabels = new Dictionary<string, Label>();
        Dictionary<string, VisualElement> tabContents = new Dictionary<string, VisualElement>();
        Dictionary<string, Button> tabButtons = new Dictionary<string, Button>();
        string currentActiveTab = "";

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
            var toolsHeader = new Label("Available MCP Tools");
            toolsHeader.style.fontSize = 16;
            toolsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolsHeader.style.marginBottom = 10;
            root.Add(toolsHeader);
            
            // Refresh tools button
            var refreshBtn = new Button(RefreshTools) { text = "Refresh Tools" };
            refreshBtn.style.height = 25;
            refreshBtn.style.marginBottom = 10;
            root.Add(refreshBtn);
            
            // Tab buttons container
            tabButtonContainer = new VisualElement();
            tabButtonContainer.style.flexDirection = FlexDirection.Row;
            tabButtonContainer.style.marginBottom = 5;
            tabButtonContainer.style.borderBottomWidth = 1;
            tabButtonContainer.style.borderBottomColor = Color.gray;
            root.Add(tabButtonContainer);
            
            // Tab content container
            tabContainer = new VisualElement();
            tabContainer.style.flexGrow = 1;
            root.Add(tabContainer);
            
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
            // Clear existing tabs
            tabContainer.Clear();
            tabButtonContainer.Clear();
            toolParameterFields.Clear();
            toolResultLabels.Clear();
            tabContents.Clear();
            tabButtons.Clear();
            currentActiveTab = "";
            
            if (!MCPService.Inst.Running)
            {
                var noToolsLabel = new Label("MCP Service is not running. Start the service to see available tools.");
                noToolsLabel.style.color = Color.gray;
                noToolsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                noToolsLabel.style.paddingTop = 20;
                noToolsLabel.style.paddingLeft = 20;
                tabContainer.Add(noToolsLabel);
                return;
            }
            
            var tools = MCPFunctionInvoker.Tools;
            if (tools.Count == 0)
            {
                var noToolsLabel = new Label("No MCP tools found.");
                noToolsLabel.style.color = Color.gray;
                noToolsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                noToolsLabel.style.paddingTop = 20;
                noToolsLabel.style.paddingLeft = 20;
                tabContainer.Add(noToolsLabel);
                return;
            }
            
            // Group tools by category (based on class name or tool name patterns)
            var toolGroups = GroupToolsByCategory(tools);
            
            // Add "All Tools" tab if there are multiple categories
            if (toolGroups.Count > 1)
            {
                var allTools = tools.Values.ToList();
                CreateTab("All Tools", allTools);
            }
            
            // Create tabs for each category
            foreach (var group in toolGroups.OrderBy(g => g.Key))
            {
                CreateTab(group.Key, group.Value);
            }
            
            // Activate the previously selected tab or the first tab
            string preferredTab = EditorPrefs.GetString("MCP4Unity_ActiveTab", "");
            if (!string.IsNullOrEmpty(preferredTab) && tabButtons.ContainsKey(preferredTab))
            {
                ActivateTab(preferredTab);
            }
            else if (tabButtons.Count > 0)
            {
                var firstTab = tabButtons.Keys.First();
                ActivateTab(firstTab);
            }
        }
        
        Dictionary<string, List<MCPTool>> GroupToolsByCategory(Dictionary<string, MCPTool> tools)
        {
            var groups = new Dictionary<string, List<MCPTool>>();
            
            foreach (var toolPair in tools)
            {
                var tool = toolPair.Value;
                string category = GetToolCategory(tool);
                
                if (!groups.ContainsKey(category))
                {
                    groups[category] = new List<MCPTool>();
                }
                groups[category].Add(tool);
            }
            
            return groups;
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
        
        void CreateTab(string tabName, List<MCPTool> tools)
        {
            // Create tab button
            var tabButton = new Button(() => ActivateTab(tabName))
            {
                text = $"{tabName} ({tools.Count})"
            };
            tabButton.style.height = 25;
            tabButton.style.paddingLeft = 10;
            tabButton.style.paddingRight = 10;
            tabButton.style.marginRight = 2;
            tabButton.style.borderBottomLeftRadius = 0;
            tabButton.style.borderBottomRightRadius = 0;
            
            // Special styling for "All Tools" tab
            if (tabName == "All Tools")
            {
                tabButton.style.backgroundColor = new Color(0.1f, 0.3f, 0.1f);
                tabButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            
            tabButtonContainer.Add(tabButton);
            tabButtons[tabName] = tabButton;
            
            // Create tab content
            var tabContent = new VisualElement();
            tabContent.style.display = DisplayStyle.None;
            tabContent.style.flexGrow = 1;
            
            // Create scroll view for the tab content
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingTop = 10;
            
            var toolsContainer = new VisualElement();
            scrollView.Add(toolsContainer);
            tabContent.Add(scrollView);
            
            // Add header for the tab if it's not "All Tools"
            if (tabName != "All Tools")
            {
                var categoryHeader = new Label($"{tabName} - {tools.Count} tool{(tools.Count != 1 ? "s" : "")}");
                categoryHeader.style.fontSize = 12;
                categoryHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                categoryHeader.style.color = new Color(0.7f, 0.7f, 0.7f);
                categoryHeader.style.marginBottom = 10;
                categoryHeader.style.marginLeft = 10;
                toolsContainer.Add(categoryHeader);
            }
            
            // Add tools to this tab
            foreach (var tool in tools.OrderBy(t => t.name))
            {
                CreateToolUI(toolsContainer, tool);
            }
            
            tabContainer.Add(tabContent);
            tabContents[tabName] = tabContent;
        }
        
        void ActivateTab(string tabName)
        {
            if (currentActiveTab == tabName) return;
            
            // Deactivate current tab
            if (!string.IsNullOrEmpty(currentActiveTab))
            {
                if (tabContents.ContainsKey(currentActiveTab))
                {
                    tabContents[currentActiveTab].style.display = DisplayStyle.None;
                }
                if (tabButtons.ContainsKey(currentActiveTab))
                {
                    var prevButton = tabButtons[currentActiveTab];
                    // Reset to default color or special color for "All Tools"
                    if (currentActiveTab == "All Tools")
                    {
                        prevButton.style.backgroundColor = new Color(0.1f, 0.3f, 0.1f);
                    }
                    else
                    {
                        prevButton.style.backgroundColor = StyleKeyword.Null;
                    }
                    prevButton.style.color = StyleKeyword.Null;
                }
            }
            
            // Activate new tab
            currentActiveTab = tabName;
            if (tabContents.ContainsKey(tabName))
            {
                tabContents[tabName].style.display = DisplayStyle.Flex;
            }
            if (tabButtons.ContainsKey(tabName))
            {
                var activeButton = tabButtons[tabName];
                activeButton.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f);
                activeButton.style.color = Color.white;
            }
            
            // Save active tab preference
            EditorPrefs.SetString("MCP4Unity_ActiveTab", tabName);
        }
        
        void CreateToolUI(VisualElement container, MCPTool tool)
        {
            // Tool container with improved styling
            var toolContainer = new VisualElement();
            toolContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.1f);
            toolContainer.style.marginBottom = 8;
            toolContainer.style.paddingTop = 10;
            toolContainer.style.paddingBottom = 10;
            toolContainer.style.paddingLeft = 15;
            toolContainer.style.paddingRight = 15;
            toolContainer.style.borderLeftWidth = 3;
            toolContainer.style.borderLeftColor = new Color(0.2f, 0.6f, 1f);
            
            // Tool header container
            var headerContainer = new VisualElement();
            headerContainer.style.marginBottom = 8;
            
            // Tool name and category info
            var nameContainer = new VisualElement();
            nameContainer.style.flexDirection = FlexDirection.Row;
            nameContainer.style.alignItems = Align.Center;
            
            var nameLabel = new Label(tool.name);
            nameLabel.style.fontSize = 14;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new Color(0.2f, 0.6f, 1f);
            nameContainer.Add(nameLabel);
            
            // Add category badge for "All Tools" tab
            if (currentActiveTab == "All Tools")
            {
                var categoryBadge = new Label($"[{GetToolCategory(tool)}]");
                categoryBadge.style.fontSize = 10;
                categoryBadge.style.marginLeft = 10;
                categoryBadge.style.color = new Color(0.6f, 0.6f, 0.6f);
                categoryBadge.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
                categoryBadge.style.paddingLeft = 5;
                categoryBadge.style.paddingRight = 5;
                nameContainer.Add(categoryBadge);
            }
            
            headerContainer.Add(nameContainer);
            
            if (!string.IsNullOrEmpty(tool.description))
            {
                var descLabel = new Label(tool.description);
                descLabel.style.fontSize = 11;
                descLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                descLabel.style.whiteSpace = WhiteSpace.Normal;
                descLabel.style.marginTop = 2;
                headerContainer.Add(descLabel);
            }
            
            toolContainer.Add(headerContainer);
            
            // Parameters container
            var parametersContainer = new VisualElement();
            parametersContainer.style.marginLeft = 5;
            parametersContainer.style.marginBottom = 8;
            toolContainer.Add(parametersContainer);
            
            // Store parameter fields for this tool
            var paramFields = new Dictionary<string, TextField>();
            toolParameterFields[tool.name] = paramFields;
            
            // Create parameter input fields using ordered properties to maintain correct order
            foreach (var property in tool.inputSchema.orderedProperties)
            {
                CreateParameterUI(parametersContainer, tool, property, paramFields);
            }
            
            // Call button and result container
            var actionContainer = new VisualElement();
            actionContainer.style.flexDirection = FlexDirection.Row;
            actionContainer.style.alignItems = Align.Center;
            actionContainer.style.marginTop = 8;
            
            var callButton = new Button(() => CallTool(tool.name)) { text = "Execute Tool" };
            callButton.style.width = 100;
            callButton.style.height = 28;
            callButton.style.backgroundColor = new Color(0.2f, 0.7f, 0.2f);
            callButton.style.color = Color.white;
            actionContainer.Add(callButton);
            
            // Result label
            var resultLabel = new Label();
            resultLabel.style.flexGrow = 1;
            resultLabel.style.marginLeft = 10;
            resultLabel.style.fontSize = 11;
            resultLabel.style.whiteSpace = WhiteSpace.Normal;
            toolResultLabels[tool.name] = resultLabel;
            actionContainer.Add(resultLabel);
            
            toolContainer.Add(actionContainer);
            container.Add(toolContainer);
        }
        
        void CreateParameterUI(VisualElement parametersContainer, MCPTool tool, Property property, Dictionary<string, TextField> paramFields)
        {
            var paramContainer = new VisualElement();
            paramContainer.style.flexDirection = FlexDirection.Row;
            paramContainer.style.alignItems = Align.Center;
            paramContainer.style.marginBottom = 5;
            
            var paramLabel = new Label($"{property.Name}:");
            paramLabel.style.minWidth = 120;
            paramLabel.style.fontSize = 11;
            paramLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            paramContainer.Add(paramLabel);
            
            // Create input field container that can hold both text field and dropdown button
            var inputContainer = new VisualElement();
            inputContainer.style.flexDirection = FlexDirection.Row;
            inputContainer.style.flexGrow = 1;
            inputContainer.style.marginLeft = 8;
            
            var paramField = new TextField();
            paramField.style.flexGrow = 1;
            paramField.style.height = 20;
            
            // Set placeholder text based on type and description
            string placeholder = property.type;
            if (!string.IsNullOrEmpty(property.description))
            {
                placeholder += $" - {property.description}";
            }
            paramField.tooltip = placeholder;
            
            inputContainer.Add(paramField);
            paramFields[property.Name] = paramField;
            
            // Add dropdown button if parameter has ParamDropdown attribute
            if (property.HasDropdown)
            {
                var dropdownButton = new Button(() => ShowDropdown(tool, property, paramField))
                {
                    text = "▼"
                };
                dropdownButton.style.width = 25;
                dropdownButton.style.height = 20;
                dropdownButton.style.marginLeft = 2;
                dropdownButton.style.fontSize = 10;
                dropdownButton.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
                dropdownButton.tooltip = "Click to select from available options";
                inputContainer.Add(dropdownButton);
            }
            
            paramContainer.Add(inputContainer);
            parametersContainer.Add(paramContainer);
        }
        
        void ShowDropdown(MCPTool tool, Property property, TextField targetField)
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
        
        void CallTool(string toolName)
        {
            if (!toolParameterFields.TryGetValue(toolName, out var paramFields) ||
                !toolResultLabels.TryGetValue(toolName, out var resultLabel))
            {
                return;
            }
            
            try
            {
                // Clear previous result
                resultLabel.text = "";
                resultLabel.style.color = Color.white;
                
                // Build parameters JObject
                var parameters = new JObject();
                foreach (var field in paramFields)
                {
                    string value = field.Value.value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        // Try to parse as appropriate type
                        var tool = MCPFunctionInvoker.Tools[toolName];
                        var property = tool.inputSchema.properties[field.Key];
                        
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
                var result = MCPFunctionInvoker.Invoke(toolName, parameters);
                
                // Display result
                string resultText = result?.ToString() ?? "null";
                if (resultText.Length > 200)
                {
                    resultText = resultText.Substring(0, 200) + "...";
                }
                
                resultLabel.text = $"✓ {resultText}";
                resultLabel.style.color = new Color(0.2f, 0.8f, 0.2f);
                
                Debug.Log($"MCP Tool '{toolName}' called successfully. Result: {result}");
            }
            catch (System.Exception ex)
            {
                resultLabel.text = $"✗ Error: {ex.Message}";
                resultLabel.style.color = new Color(0.8f, 0.2f, 0.2f);
                Debug.LogError($"Error calling MCP Tool '{toolName}': {ex}");
            }
        }
    }
}

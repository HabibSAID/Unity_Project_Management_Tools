#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TasksBoard;

/// <summary>
/// ? Better inspector for TasksBoardAsset:
/// - Nice sections
/// - HelpBoxes (navigation + tips)
/// - Tooltips
/// - Quick buttons (Open window, Ping)
/// - Debug foldout for tasks
/// </summary>
[CustomEditor(typeof(TasksBoardAsset))]
public class TasksBoardAssetEditor : Editor
{
    // small UI helpers
    private static GUIStyle _headerStyle;
    private static GUIStyle _subHeaderStyle;
    private static GUIStyle _boxStyle;

    private static void EnsureStyles()
    {
        if (_headerStyle != null) return;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12
        };

        _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11
        };

        _boxStyle = new GUIStyle("HelpBox")
        {
            padding = new RectOffset(10, 10, 8, 8)
        };
    }

    public override void OnInspectorGUI()
    {
        EnsureStyles();
        serializedObject.Update();

        // Props
        var boardName = serializedObject.FindProperty("boardName");
        var tasks = serializedObject.FindProperty("tasks");

        var todoColor = serializedObject.FindProperty("todoColor");
        var pendingColor = serializedObject.FindProperty("pendingColor");
        var inProgressColor = serializedObject.FindProperty("inProgressColor");
        var completedColor = serializedObject.FindProperty("completedColor");

        var priorityLow = serializedObject.FindProperty("priorityLow");
        var priorityMedium = serializedObject.FindProperty("priorityMedium");
        var priorityHigh = serializedObject.FindProperty("priorityHigh");
        var priorityCritical = serializedObject.FindProperty("priorityCritical");

        var forceAccentFromPriority = serializedObject.FindProperty("forceAccentFromPriority");
        var allowDragBetweenColumns = serializedObject.FindProperty("allowDragBetweenColumns");

        var asset = (TasksBoardAsset)target;

        // ===== Header =====
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("TasksBoard", _headerStyle);

        using (new EditorGUILayout.VerticalScope(_boxStyle))
        {
            EditorGUILayout.HelpBox(
                "Open the board window from:\nTools > TasksBoard > Open Boards\n\n",
                MessageType.Info
            );

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Open Board Window", "Open this board in the TasksBoard window"), GUILayout.Height(26)))
                {
                    // If TasksBoardWindow exists, call it
                    // (Your project already has this method)
                    TasksBoardWindow.OpenBoard(asset, newWindow: true);
                }

                if (GUILayout.Button(new GUIContent("Ping Asset", "Ping/select this asset in the Project window"), GUILayout.Height(26)))
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
            }
        }

        EditorGUILayout.Space(8);

        // ===== Board Settings =====
        DrawSection(
            "Board",
            "General board settings. The board name is also used for the window title.",
            () =>
            {
                EditorGUILayout.PropertyField(boardName, new GUIContent("Board Name", "Displayed name for this board and used for window title."));
            }
        );

        // ===== Column Colors =====
        DrawSection(
            "Kanban Column Colors",
            "These colors are used for the column header pills in the board window.",
            () =>
            {
                EditorGUILayout.PropertyField(todoColor, new GUIContent("To Do", "Header pill color for To Do column."));
                EditorGUILayout.PropertyField(pendingColor, new GUIContent("Pending", "Header pill color for Pending column."));
                EditorGUILayout.PropertyField(inProgressColor, new GUIContent("In Progress", "Header pill color for In Progress column."));
                EditorGUILayout.PropertyField(completedColor, new GUIContent("Completed", "Header pill color for Completed column."));
            }
        );

        // ===== Priority Accent Colors =====
        DrawSection(
            "Priority Accent Colors",
            "Used for the task accent strip and the priority badge color.",
            () =>
            {
                EditorGUILayout.PropertyField(priorityLow, new GUIContent("Low", "Accent color for Low priority tasks."));
                EditorGUILayout.PropertyField(priorityMedium, new GUIContent("Medium", "Accent color for Medium priority tasks."));
                EditorGUILayout.PropertyField(priorityHigh, new GUIContent("High", "Accent color for High priority tasks."));
                EditorGUILayout.PropertyField(priorityCritical, new GUIContent("Critical", "Accent color for Critical priority tasks."));
            }
        );

        // ===== Accent Rules =====
        DrawSection(
            "Accent Rules",
            "Control how task accent colors are decided.",
            () =>
            {
                EditorGUILayout.PropertyField(forceAccentFromPriority,
                    new GUIContent("Force Accent From Priority",
                        "If enabled: every task accent is automatically taken from the priority colors.\n" +
                        "If disabled: tasks can have a custom accent color in the board window."));

                EditorGUILayout.Space(4);

                if (forceAccentFromPriority.boolValue)
                {
                    EditorGUILayout.HelpBox(
                        "Accent is forced from Priority.\n" +
                        "• Task accent will always match the selected Priority.\n" +
                        "• The 'Accent' field in the Add/Edit task UI is hidden.",
                        MessageType.Info
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Custom accents are enabled.\n" +
                        "• You can pick a custom Accent color per task in the board window.",
                        MessageType.None
                    );
                }
            }
        );

        // ===== Behavior =====
        DrawSection(
            "Behavior",
            "Interaction rules in the board window.",
            () =>
            {
                EditorGUILayout.PropertyField(allowDragBetweenColumns,
                    new GUIContent("Allow Drag Between Columns",
                        "If disabled, tasks cannot be dragged across columns.\n" +
                        "They can still be moved via the task context menu (gear icon)."));

                if (!allowDragBetweenColumns.boolValue)
                {
                    EditorGUILayout.HelpBox(
                        "Drag between columns is disabled.\n" +
                        "Use the gear menu on tasks to move them.",
                        MessageType.Warning
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Drag between columns is enabled.",
                        MessageType.None
                    );
                }
            }
        );

        // ===== Debug: Tasks list =====
        EditorGUILayout.Space(8);
        using (new EditorGUILayout.VerticalScope(_boxStyle))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                tasks.isExpanded = EditorGUILayout.Foldout(tasks.isExpanded,
                    new GUIContent($"Tasks (Debug)   [{tasks.arraySize}]",
                        "Raw serialized list. Normally you manage tasks from the TasksBoard window."),
                    true);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Clear All", "Remove all tasks from this board"), GUILayout.Width(90)))
                {
                    if (EditorUtility.DisplayDialog("Clear All Tasks",
                        "This will remove ALL tasks from this board.\n\nAre you sure?",
                        "Clear", "Cancel"))
                    {
                        tasks.ClearArray();
                    }
                }
            }

            if (tasks.isExpanded)
            {
                EditorGUILayout.HelpBox(
                    "Debug view of the raw task list.\n" +
                    "Recommended workflow: use the TasksBoard window to add/edit tasks.",
                    MessageType.None
                );

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(tasks, true);
                EditorGUI.indentLevel--;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSection(string title, string hint, System.Action draw)
    {
        EditorGUILayout.Space(6);
        using (new EditorGUILayout.VerticalScope(_boxStyle))
        {
            EditorGUILayout.LabelField(title, _subHeaderStyle);

            if (!string.IsNullOrWhiteSpace(hint))
            {
                EditorGUILayout.HelpBox(hint, MessageType.None);
            }

            EditorGUILayout.Space(2);
            draw?.Invoke();
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WBSBoard
{
    [CustomEditor(typeof(WbsBoardAsset))]
    public class WbsBoardAssetEditor : Editor
    {
        private SerializedProperty _boardName;

        private SerializedProperty _headerBg;
        private SerializedProperty _panelBg;
        private SerializedProperty _cardBg;

        private SerializedProperty _todoColor;
        private SerializedProperty _pendingColor;
        private SerializedProperty _progressColor;
        private SerializedProperty _doneColor;

        private SerializedProperty _tasks;

        private void OnEnable()
        {
            _boardName = serializedObject.FindProperty("boardName");

            _headerBg = serializedObject.FindProperty("headerBg");
            _panelBg = serializedObject.FindProperty("panelBg");
            _cardBg = serializedObject.FindProperty("cardBg");

            _todoColor = serializedObject.FindProperty("todoColor");
            _pendingColor = serializedObject.FindProperty("pendingColor");
            _progressColor = serializedObject.FindProperty("progressColor");
            _doneColor = serializedObject.FindProperty("doneColor");

            _tasks = serializedObject.FindProperty("tasks");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var asset = (WbsBoardAsset)target;

            DrawNavigationHelp();
            EditorGUILayout.Space(10);

            DrawBoardSection();
            EditorGUILayout.Space(10);

            DrawColorsSection();
            EditorGUILayout.Space(10);

            DrawTasksSection(); // raw list (optional)
            EditorGUILayout.Space(14);

            DrawBottomActions(asset);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawNavigationHelp()
        {
            EditorGUILayout.HelpBox(
                "How to use:\n" +
                "• Open the board using the button below.\n" +
                "• Add tasks inside the Board Window.\n" +
                "• Up/Down reorder is ALWAYS enabled (stored list order).\n" +
                "• Index can be left empty (0) and filled later (e.g., 1, 1.1, 2.3).\n" +
                "• DependsOn blocks a task until the referenced task is Completed.",
                MessageType.Info
            );
        }

        private void DrawBoardSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Board", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_boardName, new GUIContent("Board Name", "Display name shown in window/launcher."));
            }
        }

        private void DrawColorsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("UI Colors", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_headerBg, new GUIContent("Header", "Top header background color."));
                EditorGUILayout.PropertyField(_panelBg, new GUIContent("Panel", "Background color behind rows."));
                EditorGUILayout.PropertyField(_cardBg, new GUIContent("Row Card", "Row background base color."));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Status Colors", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_todoColor, new GUIContent("To Do", "Accent for ToDo."));
                EditorGUILayout.PropertyField(_pendingColor, new GUIContent("Pending", "Accent for Pending."));
                EditorGUILayout.PropertyField(_progressColor, new GUIContent("In Progress", "Accent for InProgress."));
                EditorGUILayout.PropertyField(_doneColor, new GUIContent("Completed", "Accent for Completed."));
            }
        }

        private void DrawTasksSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Tasks (Raw Data)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Tip: Usually you edit tasks from the Board Window.\nThis list is just raw data.",
                    MessageType.None
                );

                EditorGUILayout.PropertyField(_tasks, new GUIContent("Tasks"), includeChildren: true);
            }
        }

        private void DrawBottomActions(WbsBoardAsset asset)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

                if (GUILayout.Button(new GUIContent("Open Board", "Open this board in the WBS window."), GUILayout.Height(34)))
                {
                    WbsBoardWindow.OpenBoard(asset, newWindow: true);
                }

                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f);
                if (GUILayout.Button(new GUIContent("Delete Board", "Delete this board asset from the project."), GUILayout.Height(30)))
                {
                    string path = AssetDatabase.GetAssetPath(asset);
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (EditorUtility.DisplayDialog("Delete Board",
                            $"Delete board '{asset.boardName}'?\n\nThis deletes asset:\n{path}",
                            "Delete", "Cancel"))
                        {
                            AssetDatabase.DeleteAsset(path);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                        }
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }
    }
}
#endif

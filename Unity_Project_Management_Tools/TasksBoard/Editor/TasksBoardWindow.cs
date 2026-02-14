#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace TasksBoard
{
    public class TasksBoardWindow : EditorWindow
    {
        private const string RootFolder = "Assets/TasksBoard";
        private const string BoardsFolder = "Assets/TasksBoard/Boards";

        [SerializeField] private TasksBoardAsset _board;

        // Top UI
        private VisualElement _topBar;
        private TextField _boardNameField;
        private TextField _searchField;
        private Toggle _showArchivedToggle;

        // Columns
        private VisualElement _todoList;
        private VisualElement _pendingList;
        private VisualElement _progressList;
        private VisualElement _completedList;

        // Layout
        private VisualElement _mainRow;              // ? used for responsive direction
        private VisualElement _leftPanel;            // card
        private ScrollView _leftScroll;              // ? left panel scroll (vertical)
        private ScrollView _kanbanScroll;

        // New Task fields
        private TextField _newTitle;
        private TextField _newDesc;
        private EnumField _newPriority;

        // Accent (only created when not forced)
        private ColorField _newAccent;

        private Toggle _newHasDue;
        private IntegerField _newDueInDays;

        private Toggle _newHasTags;
        private TextField _newTags;

        private Toggle _newHasRefs;
        private VisualElement _newRefsContainer;
        private readonly List<UnityEngine.Object> _newRefs = new();

        // ? Subtasks as LIST UI (not CSV)
        private Toggle _newHasSubtasks;
        private VisualElement _newSubtasksContainer;
        private readonly List<string> _newSubtasks = new();

        // drag between columns
        private string _draggingTaskId;

        // Remember foldouts per task (editor-only)
        private readonly Dictionary<string, bool> _descOpen = new();
        private readonly Dictionary<string, bool> _refsOpen = new();
        private readonly Dictionary<string, bool> _subsOpen = new();

        // ---------------- MENU ----------------

        [MenuItem("Tools/Open Tasks Boards")]
        public static void OpenBoardLauncher() => TasksBoardLauncherWindow.Open();

        public static void OpenBoard(TasksBoardAsset board, bool newWindow)
        {
            if (board == null) return;

            var w = newWindow ? CreateWindow<TasksBoardWindow>() : GetWindow<TasksBoardWindow>();
            w.minSize = new Vector2(820, 440);
            w._board = board;
            w.titleContent = new GUIContent($"TasksBoard - {board.boardName}");
            w.Show();
            w.RebuildAll();
        }

        // ---------------- LIFECYCLE ----------------

        private void OnEnable()
        {
            EnsureFolders();

            if (_board == null)
            {
                rootVisualElement.Clear();
                var msg = new Label("No board assigned.\n\nUse: Tools > TasksBoard > Open Boards");
                msg.style.whiteSpace = WhiteSpace.Normal;
                msg.style.paddingLeft = 12;
                msg.style.paddingTop = 12;
                rootVisualElement.Add(msg);
                return;
            }

            RebuildAll();
        }

        private void RebuildAll()
        {
            if (_board == null) return;

            EnsureAssetNameMatchesBoardName();
            titleContent = new GUIContent($"TasksBoard - {_board.boardName}");

            BuildUI();
            RefreshBoard();

            // ? keep layout responsive (and avoid stacking multiple callbacks)
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            ApplyResponsiveLayout();
        }

        private void OnGeometryChanged(GeometryChangedEvent _)
        {
            ApplyResponsiveLayout();
        }

        // ---------------- FOLDERS / ASSETS ----------------

        private static void EnsureFoldersStatic()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "TasksBoard");

            if (!AssetDatabase.IsValidFolder(BoardsFolder))
                AssetDatabase.CreateFolder(RootFolder, "Boards");
        }

        private void EnsureFolders() => EnsureFoldersStatic();

        private static string MakeSafeFileName(string s)
        {
            var bad = System.IO.Path.GetInvalidFileNameChars();
            foreach (var c in bad) s = s.Replace(c.ToString(), "");
            s = s.Replace("/", "_").Replace("\\", "_").Replace(":", "_").Trim();
            if (string.IsNullOrEmpty(s)) s = "Board";
            return s;
        }

        private static TasksBoardAsset CreateNewBoardAssetStatic(string boardName)
        {
            EnsureFoldersStatic();

            string safe = MakeSafeFileName(string.IsNullOrWhiteSpace(boardName) ? "New Board" : boardName.Trim());
            string path = AssetDatabase.GenerateUniqueAssetPath($"{BoardsFolder}/{safe}.asset");

            var asset = CreateInstance<TasksBoardAsset>();
            asset.boardName = string.IsNullOrWhiteSpace(boardName) ? "New Board" : boardName.Trim();

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

        private void SaveBoard()
        {
            if (_board == null) return;
            EditorUtility.SetDirty(_board);
            AssetDatabase.SaveAssets();
        }

        private void RenameBoardAndAsset(string newBoardName)
        {
            if (_board == null) return;

            newBoardName = (newBoardName ?? "").Trim();
            if (string.IsNullOrEmpty(newBoardName))
            {
                EditorUtility.DisplayDialog("Invalid Name", "Board name cannot be empty.", "OK");
                return;
            }

            _board.boardName = newBoardName;
            EditorUtility.SetDirty(_board);

            string assetPath = AssetDatabase.GetAssetPath(_board);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string safe = MakeSafeFileName(newBoardName);
                string err = AssetDatabase.RenameAsset(assetPath, safe);
                if (!string.IsNullOrEmpty(err))
                    Debug.LogWarning($"RenameAsset warning: {err}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            titleContent = new GUIContent($"TasksBoard - {_board.boardName}");
        }

        private void EnsureAssetNameMatchesBoardName()
        {
            if (_board == null) return;

            string assetPath = AssetDatabase.GetAssetPath(_board);
            if (string.IsNullOrEmpty(assetPath)) return;

            string desired = MakeSafeFileName(_board.boardName);
            if (string.IsNullOrEmpty(desired)) return;

            string currentName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (string.Equals(currentName, desired, StringComparison.OrdinalIgnoreCase))
                return;

            string err = AssetDatabase.RenameAsset(assetPath, desired);
            if (!string.IsNullOrEmpty(err))
                Debug.LogWarning($"RenameAsset warning: {err}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void DeleteCurrentBoard()
        {
            if (_board == null) return;

            string path = AssetDatabase.GetAssetPath(_board);
            if (string.IsNullOrEmpty(path)) return;

            if (!EditorUtility.DisplayDialog("Delete Board",
                    $"Delete board '{_board.boardName}'?\n\nThis deletes asset:\n{path}",
                    "Delete", "Cancel"))
                return;

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Close();
            TasksBoardLauncherWindow.Open();
        }

        // ---------------- UI HELPERS ----------------

        private VisualElement Separator(int top = 10, int bottom = 10)
        {
            var s = new VisualElement();
            s.style.height = 1;
            s.style.marginTop = top;
            s.style.marginBottom = bottom;
            s.style.backgroundColor = new Color(0, 0, 0, 0.35f);
            return s;
        }

        private Label SectionTitle(string text)
        {
            var l = new Label(text);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.opacity = 0.9f;
            l.style.marginTop = 6;
            l.style.marginBottom = 6;
            return l;
        }

        private VisualElement GroupBoxLike()
        {
            var g = new VisualElement();
            g.style.flexDirection = FlexDirection.Row;
            g.style.alignItems = Align.Center;
            g.style.paddingLeft = 10;
            g.style.paddingRight = 10;
            g.style.paddingTop = 6;
            g.style.paddingBottom = 6;
            g.style.borderTopLeftRadius = 12;
            g.style.borderTopRightRadius = 12;
            g.style.borderBottomLeftRadius = 12;
            g.style.borderBottomRightRadius = 12;
            g.style.backgroundColor = new Color(1, 1, 1, 0.04f);
            g.style.borderLeftWidth = 1;
            g.style.borderRightWidth = 1;
            g.style.borderTopWidth = 1;
            g.style.borderBottomWidth = 1;
            g.style.borderLeftColor = new Color(0, 0, 0, 0.35f);
            g.style.borderRightColor = new Color(0, 0, 0, 0.35f);
            g.style.borderTopColor = new Color(0, 0, 0, 0.35f);
            g.style.borderBottomColor = new Color(0, 0, 0, 0.35f);
            return g;
        }

        private VisualElement Card(Color bg)
        {
            var v = new VisualElement();
            v.style.backgroundColor = bg;
            v.style.borderTopLeftRadius = 14;
            v.style.borderTopRightRadius = 14;
            v.style.borderBottomLeftRadius = 14;
            v.style.borderBottomRightRadius = 14;
            v.style.borderLeftWidth = 1;
            v.style.borderRightWidth = 1;
            v.style.borderTopWidth = 1;
            v.style.borderBottomWidth = 1;
            v.style.borderLeftColor = new Color(0, 0, 0, 0.4f);
            v.style.borderRightColor = new Color(0, 0, 0, 0.4f);
            v.style.borderTopColor = new Color(0, 0, 0, 0.4f);
            v.style.borderBottomColor = new Color(0, 0, 0, 0.4f);
            return v;
        }

        // ? Unity 6 safe icon button (no obsolete unityBackgroundScaleMode, no BackgroundSize.Contain)
        private Button IconButton(string unityIconName, string fallbackText, Action onClick, int w = 24, int h = 22)
        {
            var btn = new Button(onClick);
            btn.style.width = w;
            btn.style.height = h;

            var icon = EditorGUIUtility.IconContent(unityIconName)?.image as Texture2D;
            if (icon != null)
            {
                btn.text = "";
                btn.style.backgroundImage = new StyleBackground(icon);
                btn.style.unityBackgroundImageTintColor = new Color(1, 1, 1, 0.9f);
                btn.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                btn.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                btn.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
                // background-size differs between versions; leaving it unset is safe.
            }
            else btn.text = fallbackText;

            return btn;
        }

        private void ApplyResponsiveLayout()
        {
            if (_leftPanel == null || _mainRow == null || _kanbanScroll == null) return;

            float w = position.width;

            bool narrow = w < 980f;
            bool veryNarrow = w < 720f;

            // ? Top bar wrap
            if (_topBar != null)
            {
                _topBar.style.flexWrap = Wrap.Wrap;

                // Unity 6 has no rowGap / columnGap in C#,
                // so spacing must be simulated with margins on children.
                foreach (var child in _topBar.Children())
                {
                    child.style.marginBottom = 6;
                    child.style.marginRight = 8;
                }
            }


            // ? Main layout: side-by-side on wide, stacked on very narrow
            _mainRow.style.flexDirection = veryNarrow ? FlexDirection.Column : FlexDirection.Row;

            // ? Left panel sizing
            if (veryNarrow)
            {
                _leftPanel.style.width = Length.Percent(100);
                _leftPanel.style.minWidth = 0;
                _leftPanel.style.marginRight = 0;
                _leftPanel.style.marginBottom = 10;
                _leftPanel.style.flexShrink = 0;
            }
            else
            {
                _leftPanel.style.width = narrow ? 260 : 340;
                _leftPanel.style.minWidth = 250;
                _leftPanel.style.marginRight = 12;
                _leftPanel.style.marginBottom = 0;
                _leftPanel.style.flexShrink = 0;
            }

            // ? Kanban scroll must be allowed to shrink (prevents overlap/overflow)
            _kanbanScroll.style.flexGrow = 1;
            _kanbanScroll.style.minWidth = 0;

            // ? Make top inputs adapt instead of forcing fixed widths
            if (_boardNameField != null) _boardNameField.style.width = veryNarrow ? Length.Percent(100) : 220;
            if (_searchField != null) _searchField.style.width = veryNarrow ? Length.Percent(100) : 220;

            // ? Ensure left panel scroll always usable
            if (_leftScroll != null)
                _leftScroll.style.maxHeight = veryNarrow ? 420 : StyleKeyword.Auto;
        }

        // ---------------- BUILD UI ----------------

        private void BuildUI()
        {
            rootVisualElement.Clear();

            _topBar = new VisualElement();
            _topBar.style.flexDirection = FlexDirection.Row;
            _topBar.style.alignItems = Align.Center;
            _topBar.style.paddingLeft = 10;
            _topBar.style.paddingRight = 10;
            _topBar.style.paddingTop = 8;
            _topBar.style.paddingBottom = 8;
            _topBar.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            _topBar.style.borderBottomWidth = 1;
            _topBar.style.borderBottomColor = new Color(0, 0, 0, 0.35f);

            var title = new Label($"TasksBoard - {_board.boardName}");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.marginRight = 10;
            _topBar.Add(title);

            // Board group
            var boardGroup = GroupBoxLike();
            boardGroup.style.marginRight = 10;
            boardGroup.style.flexGrow = 1;
            boardGroup.style.flexShrink = 1;
            boardGroup.style.minWidth = 320;

            _boardNameField = new TextField { value = _board.boardName };
            _boardNameField.style.width = 220;

            var renameBtn = new Button(() => RenameBoardAndAsset(_boardNameField.value)) { text = "Rename" };
            renameBtn.style.marginLeft = 6;

            var pingBtn = new Button(() =>
            {
                EditorGUIUtility.PingObject(_board);
                Selection.activeObject = _board;
            })
            { text = "Ping" };
            pingBtn.style.marginLeft = 6;

            var deleteBtn = new Button(DeleteCurrentBoard) { text = "Delete" };

            // Make it red
            deleteBtn.style.backgroundColor = new StyleColor(Color.red);
            deleteBtn.style.color = Color.white; // optional: better contrast
            deleteBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            deleteBtn.style.borderTopLeftRadius = 4;
            deleteBtn.style.borderTopRightRadius = 4;
            deleteBtn.style.borderBottomLeftRadius = 4;
            deleteBtn.style.borderBottomRightRadius = 4;
            deleteBtn.style.paddingLeft = 8;
            deleteBtn.style.paddingRight = 8;

            deleteBtn.style.marginLeft = 6;

            boardGroup.Add(new Label("Board Name") { style = { opacity = 0.8f, marginRight = 6 } });
            boardGroup.Add(_boardNameField);
            boardGroup.Add(renameBtn);
            boardGroup.Add(pingBtn);
            boardGroup.Add(deleteBtn);

            _topBar.Add(boardGroup);

            // Search group
            var searchGroup = GroupBoxLike();
            searchGroup.style.marginRight = 10;
            searchGroup.style.flexGrow = 1;
            searchGroup.style.flexShrink = 1;
            searchGroup.style.minWidth = 320;

            _searchField = new TextField();
            _searchField.style.width = 220;
            _searchField.RegisterValueChangedCallback(_ => RefreshBoard());

            var clearBtn = new Button(() => { _searchField.value = ""; RefreshBoard(); }) { text = "Clear" };
            clearBtn.style.marginLeft = 6;

            _showArchivedToggle = new Toggle("Archived");
            _showArchivedToggle.style.marginLeft = 10;
            _showArchivedToggle.RegisterValueChangedCallback(_ => RefreshBoard());

            searchGroup.Add(new Label("Search") { style = { opacity = 0.8f, marginRight = 6 } });
            searchGroup.Add(_searchField);
            searchGroup.Add(clearBtn);
            searchGroup.Add(_showArchivedToggle);

            _topBar.Add(searchGroup);

            _topBar.Add(new VisualElement { style = { flexGrow = 1 } });

            var openOtherBtn = new Button(TasksBoardLauncherWindow.Open) { text = "Open Other Board..." };
            var saveBtn = new Button(SaveBoard) { text = "Save" };
            saveBtn.style.marginLeft = 6;

            _topBar.Add(openOtherBtn);
            _topBar.Add(saveBtn);

            // Main
            _mainRow = new VisualElement();
            _mainRow.style.flexDirection = FlexDirection.Row;
            _mainRow.style.flexGrow = 1;
            _mainRow.style.paddingLeft = 10;
            _mainRow.style.paddingRight = 10;
            _mainRow.style.paddingTop = 10;
            _mainRow.style.paddingBottom = 10;

            _leftPanel = BuildCreatePanel();
            _leftPanel.style.width = 340;
            _leftPanel.style.minWidth = 250;
            _leftPanel.style.marginRight = 12;
            _leftPanel.style.flexShrink = 0;

            _kanbanScroll = new ScrollView(ScrollViewMode.Horizontal);
            _kanbanScroll.style.flexGrow = 1;
            _kanbanScroll.style.minWidth = 0;

            var kanbanRow = new VisualElement();
            kanbanRow.style.flexDirection = FlexDirection.Row;

            var colTodo = BuildColumn("TO DO", _board.todoColor, out _todoList, TaskStatus.ToDo);
            var colPending = BuildColumn("PENDING", _board.pendingColor, out _pendingList, TaskStatus.Pending);
            var colProgress = BuildColumn("IN PROGRESS", _board.inProgressColor, out _progressList, TaskStatus.InProgress);
            var colDone = BuildColumn("COMPLETED", _board.completedColor, out _completedList, TaskStatus.Completed);

            colTodo.style.marginRight = 10;
            colPending.style.marginRight = 10;
            colProgress.style.marginRight = 10;

            kanbanRow.Add(colTodo);
            kanbanRow.Add(colPending);
            kanbanRow.Add(colProgress);
            kanbanRow.Add(colDone);

            _kanbanScroll.Add(kanbanRow);

            _mainRow.Add(_leftPanel);
            _mainRow.Add(_kanbanScroll);

            rootVisualElement.Add(_topBar);
            rootVisualElement.Add(_mainRow);
        }

        private VisualElement BuildCreatePanel()
        {
            var panel = Card(new Color(0.10f, 0.10f, 0.10f));
            panel.style.paddingLeft = 10;
            panel.style.paddingRight = 10;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 10;

            // ? Scroll container for left panel
            _leftScroll = new ScrollView(ScrollViewMode.Vertical);
            _leftScroll.style.flexGrow = 1;
            _leftScroll.style.minWidth = 0;
            _leftScroll.style.paddingLeft = 6;
            _leftScroll.style.paddingRight = 6;
            _leftScroll.style.paddingTop = 6;
            _leftScroll.style.paddingBottom = 6;

            var header = new Label("New Task");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 14;
            header.style.marginBottom = 8;

            _leftScroll.Add(header);

            // --- Basics
            _leftScroll.Add(SectionTitle("Basics"));

            _newTitle = new TextField("Title");
            _newTitle.style.marginBottom = 6;

            _newDesc = new TextField("Description") { multiline = true };
            _newDesc.style.height = 80;

            _newPriority = new EnumField("Priority", TaskPriority.Medium);

            _leftScroll.Add(_newTitle);
            _leftScroll.Add(_newDesc);
            _leftScroll.Add(_newPriority);

            // Accent (only when not forced)
            _newAccent = null;
            if (!_board.forceAccentFromPriority)
            {
                _newAccent = new ColorField("Accent") { value = new Color(0.25f, 0.65f, 1f) };
                _leftScroll.Add(_newAccent);
            }

            _leftScroll.Add(Separator(10, 10));

            // --- Due Date
            _leftScroll.Add(SectionTitle("Due Date"));

            _newHasDue = new Toggle("Has Due Date");
            _newDueInDays = new IntegerField("Due in (days)") { value = 3 };
            _newDueInDays.SetEnabled(false);
            _newHasDue.RegisterValueChangedCallback(e => _newDueInDays.SetEnabled(e.newValue));

            _leftScroll.Add(_newHasDue);
            _leftScroll.Add(_newDueInDays);

            _leftScroll.Add(Separator(10, 10));

            // --- Tags
            _leftScroll.Add(SectionTitle("Tags"));

            _newHasTags = new Toggle("Has Tags") { value = false };
            _newTags = new TextField("Tags (comma)") { value = "ui, gameplay" };
            _newTags.SetEnabled(false);
            _newHasTags.RegisterValueChangedCallback(e => _newTags.SetEnabled(e.newValue));

            _leftScroll.Add(_newHasTags);
            _leftScroll.Add(_newTags);

            _leftScroll.Add(Separator(10, 10));

            // --- References
            _leftScroll.Add(SectionTitle("References"));

            _newHasRefs = new Toggle("Has References") { value = false };
            _newRefsContainer = new VisualElement();
            _newRefsContainer.style.marginTop = 6;
            _newRefsContainer.style.marginBottom = 6;

            var addRefBtn = new Button(() =>
            {
                _newRefs.Add(null);
                RebuildNewRefsUI();
            })
            { text = "Add Reference" };
            addRefBtn.style.marginTop = 4;

            _newHasRefs.RegisterValueChangedCallback(e =>
            {
                _newRefsContainer.SetEnabled(e.newValue);
                addRefBtn.SetEnabled(e.newValue);
            });

            _newRefsContainer.SetEnabled(false);
            addRefBtn.SetEnabled(false);

            _leftScroll.Add(_newHasRefs);
            _leftScroll.Add(_newRefsContainer);
            _leftScroll.Add(addRefBtn);

            _leftScroll.Add(Separator(10, 10));

            // --- Subtasks
            _leftScroll.Add(SectionTitle("Subtasks"));

            _newHasSubtasks = new Toggle("Has Subtasks") { value = false };
            _newSubtasksContainer = new VisualElement();
            _newSubtasksContainer.style.marginTop = 6;
            _newSubtasksContainer.style.marginBottom = 6;

            var addSubBtn = new Button(() =>
            {
                _newSubtasks.Add("New subtask");
                RebuildNewSubtasksUI();
            })
            { text = "Add Subtask" };
            addSubBtn.style.marginTop = 4;

            _newHasSubtasks.RegisterValueChangedCallback(e =>
            {
                _newSubtasksContainer.SetEnabled(e.newValue);
                addSubBtn.SetEnabled(e.newValue);
            });

            _newSubtasksContainer.SetEnabled(false);
            addSubBtn.SetEnabled(false);

            _leftScroll.Add(_newHasSubtasks);
            _leftScroll.Add(_newSubtasksContainer);
            _leftScroll.Add(addSubBtn);

            _leftScroll.Add(Separator(12, 12));

            // ? Add button (clearer)
            var addBtn = new Button(AddTask) { text = "+ Add Task" };
            addBtn.style.height = 38;
            addBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            addBtn.style.fontSize = 12;
            addBtn.style.marginTop = 2;
            addBtn.style.backgroundColor = new Color(0.25f, 0.65f, 1f, 0.85f);
            addBtn.style.color = Color.black;
            addBtn.style.borderTopLeftRadius = 10;
            addBtn.style.borderTopRightRadius = 10;
            addBtn.style.borderBottomLeftRadius = 10;
            addBtn.style.borderBottomRightRadius = 10;

            _leftScroll.Add(addBtn);

            panel.Add(_leftScroll);

            RebuildNewRefsUI();
            RebuildNewSubtasksUI();
            return panel;
        }

        private void RebuildNewRefsUI()
        {
            if (_newRefsContainer == null) return;
            _newRefsContainer.Clear();

            if (_newRefs.Count == 0)
            {
                var hint = new Label("No references added.");
                hint.style.opacity = 0.65f;
                hint.style.marginTop = 2;
                _newRefsContainer.Add(hint);
                return;
            }

            for (int i = 0; i < _newRefs.Count; i++)
            {
                int idx = i;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 6;

                var field = new ObjectField
                {
                    objectType = typeof(UnityEngine.Object),
                    allowSceneObjects = true
                };
                field.style.flexGrow = 1;
                field.value = _newRefs[idx];
                field.RegisterValueChangedCallback(e => _newRefs[idx] = e.newValue as UnityEngine.Object);

                var remove = new Button(() =>
                {
                    _newRefs.RemoveAt(idx);
                    RebuildNewRefsUI();
                })
                { text = "X" };
                remove.style.width = 26;
                remove.style.marginLeft = 6;

                row.Add(field);
                row.Add(remove);
                _newRefsContainer.Add(row);
            }
        }

        private void RebuildNewSubtasksUI()
        {
            if (_newSubtasksContainer == null) return;
            _newSubtasksContainer.Clear();

            if (_newSubtasks.Count == 0)
            {
                var hint = new Label("No subtasks.");
                hint.style.opacity = 0.65f;
                hint.style.marginTop = 2;
                _newSubtasksContainer.Add(hint);
                return;
            }

            for (int i = 0; i < _newSubtasks.Count; i++)
            {
                int idx = i;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 6;

                var tf = new TextField();
                tf.style.flexGrow = 1;
                tf.value = _newSubtasks[idx] ?? "";
                tf.RegisterValueChangedCallback(e => _newSubtasks[idx] = e.newValue);

                var remove = new Button(() =>
                {
                    _newSubtasks.RemoveAt(idx);
                    RebuildNewSubtasksUI();
                })
                { text = "X" };
                remove.style.width = 26;
                remove.style.marginLeft = 6;

                row.Add(tf);
                row.Add(remove);
                _newSubtasksContainer.Add(row);
            }
        }

        private VisualElement BuildColumn(string title, Color pillColor, out VisualElement list, TaskStatus status)
        {
            var col = Card(new Color(0.08f, 0.08f, 0.08f));
            col.style.flexGrow = 1;
            col.style.flexShrink = 0;
            col.style.minWidth = position.width < 720 ? 220 : 260;
            col.style.paddingLeft = 10;
            col.style.paddingRight = 10;
            col.style.paddingTop = 10;
            col.style.paddingBottom = 10;

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.marginBottom = 10;

            var pill = new Label(title);
            pill.style.unityFontStyleAndWeight = FontStyle.Bold;
            pill.style.fontSize = 12;
            pill.style.paddingLeft = 10;
            pill.style.paddingRight = 10;
            pill.style.paddingTop = 4;
            pill.style.paddingBottom = 4;
            pill.style.borderTopLeftRadius = 999;
            pill.style.borderTopRightRadius = 999;
            pill.style.borderBottomLeftRadius = 999;
            pill.style.borderBottomRightRadius = 999;
            pill.style.backgroundColor = pillColor;
            pill.style.color = Color.black;

            head.Add(pill);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            list = new VisualElement();
            list.style.flexDirection = FlexDirection.Column;
            scroll.Add(list);

            col.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                if (_board == null || !_board.allowDragBetweenColumns) return;
                if (!string.IsNullOrEmpty(_draggingTaskId))
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            });

            col.RegisterCallback<DragPerformEvent>(_ =>
            {
                if (_board == null || !_board.allowDragBetweenColumns) return;
                if (string.IsNullOrEmpty(_draggingTaskId)) return;

                var task = _board.tasks.FirstOrDefault(x => x != null && x.id == _draggingTaskId);
                if (task != null)
                {
                    task.status = status;
                    task.Touch();
                    SaveBoard();
                    RefreshBoard();
                }
                _draggingTaskId = null;
            });

            col.Add(head);
            col.Add(scroll);
            return col;
        }

        // ---------------- TASKS ----------------

        private void AddTask()
        {
            if (_board == null) return;

            var title = (_newTitle.value ?? "").Trim();
            if (string.IsNullOrEmpty(title))
            {
                EditorUtility.DisplayDialog("Missing title", "Please enter a title.", "OK");
                return;
            }

            var t = new TasksBoardItem
            {
                title = title,
                description = _newDesc.value ?? "",
                status = TaskStatus.ToDo,
                priority = (TaskPriority)_newPriority.value,
                archived = false,
                createdUtcTicks = DateTime.UtcNow.Ticks,
                updatedUtcTicks = DateTime.UtcNow.Ticks,

                hasDueDate = _newHasDue.value,
                hasTags = _newHasTags.value,
                hasReferences = _newHasRefs.value,
                hasSubtasks = _newHasSubtasks.value
            };

            t.EnsureLists();

            // Accent
            t.accent = _board.forceAccentFromPriority
                ? PriorityColor(t.priority)
                : (_newAccent != null ? _newAccent.value : new Color(0.25f, 0.65f, 1f));

            if (t.hasDueDate)
                t.DueUtc = DateTime.UtcNow.AddDays(Mathf.Max(0, _newDueInDays.value));

            if (t.hasTags)
                t.tags = ParseTags(_newTags.value);
            else
                t.tags.Clear();

            if (t.hasReferences)
                t.references = _newRefs.Where(o => o != null).ToList();
            else
                t.references.Clear();

            if (t.hasSubtasks)
            {
                t.subtasks = _newSubtasks
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else t.subtasks.Clear();

            _board.tasks.Insert(0, t);
            SaveBoard();
            RefreshBoard();

            // reset form
            _newTitle.value = "";
            _newDesc.value = "";

            _newHasSubtasks.value = false;
            _newSubtasks.Clear();
            RebuildNewSubtasksUI();
        }

        private List<string> ParseTags(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(',')
                .Select(s => (s ?? "").Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void RefreshBoard()
        {
            if (_board == null) return;

            EnsureAssetNameMatchesBoardName();

            _todoList?.Clear();
            _pendingList?.Clear();
            _progressList?.Clear();
            _completedList?.Clear();

            foreach (var t in _board.tasks)
                t?.EnsureLists();

            IEnumerable<TasksBoardItem> tasks = _board.tasks;

            if (_showArchivedToggle != null && !_showArchivedToggle.value)
                tasks = tasks.Where(t => !t.archived);

            var search = (_searchField?.value ?? "").Trim();
            if (!string.IsNullOrEmpty(search))
            {
                tasks = tasks.Where(t =>
                    (t.title ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (t.description ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (t.hasTags && t.tags != null && t.tags.Any(tag => (tag ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (t.hasSubtasks && t.subtasks != null && t.subtasks.Any(st => (st ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                );
            }

            foreach (var t in tasks)
            {
                if (_board.forceAccentFromPriority)
                    t.accent = PriorityColor(t.priority);

                var card = BuildTaskCard(t);

                switch (t.status)
                {
                    case TaskStatus.ToDo: _todoList.Add(card); break;
                    case TaskStatus.Pending: _pendingList.Add(card); break;
                    case TaskStatus.InProgress: _progressList.Add(card); break;
                    case TaskStatus.Completed: _completedList.Add(card); break;
                }
            }
        }

        private void ReorderInBoardList(TasksBoardItem t, int direction)
        {
            if (_board == null || t == null) return;

            int i = _board.tasks.FindIndex(x => x != null && x.id == t.id);
            if (i < 0) return;

            int j = i;
            while (true)
            {
                j += direction;
                if (j < 0 || j >= _board.tasks.Count) return;
                if (_board.tasks[j] != null && _board.tasks[j].status == t.status)
                    break;
            }

            (_board.tasks[i], _board.tasks[j]) = (_board.tasks[j], _board.tasks[i]);
            t.Touch();
            SaveBoard();
            RefreshBoard();
        }

        private VisualElement BuildTaskCard(TasksBoardItem t)
        {
            var card = Card(new Color(0.13f, 0.13f, 0.13f));
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.marginBottom = 8;

            if (!_descOpen.ContainsKey(t.id)) _descOpen[t.id] = false;
            if (!_refsOpen.ContainsKey(t.id)) _refsOpen[t.id] = false;
            if (!_subsOpen.ContainsKey(t.id)) _subsOpen[t.id] = false;

            var accent = new VisualElement();
            accent.style.height = 3;
            accent.style.backgroundColor = t.accent;
            accent.style.marginBottom = 8;
            accent.style.borderTopLeftRadius = 6;
            accent.style.borderTopRightRadius = 6;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var title = new Label(t.title ?? "(Untitled)");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 12;
            title.style.flexGrow = 1;

            var badge = new Label(PriorityText(t.priority));
            badge.style.paddingLeft = 8;
            badge.style.paddingRight = 8;
            badge.style.paddingTop = 2;
            badge.style.paddingBottom = 2;
            badge.style.borderTopLeftRadius = 999;
            badge.style.borderTopRightRadius = 999;
            badge.style.borderBottomLeftRadius = 999;
            badge.style.borderBottomRightRadius = 999;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.fontSize = 10;
            badge.style.backgroundColor = PriorityColor(t.priority);
            badge.style.color = Color.black;

            var upBtn = IconButton("d_scrollup", "Up", () => ReorderInBoardList(t, -1));
            upBtn.style.marginLeft = 6;

            var downBtn = IconButton("d_scrolldown", "Dn", () => ReorderInBoardList(t, +1));
            downBtn.style.marginLeft = 4;

            var gearBtn = IconButton("SettingsIcon", "Edit", () => ShowContextMenu(t), 26, 22);
            gearBtn.style.marginLeft = 6;

            row.Add(title);
            row.Add(badge);
            row.Add(upBtn);
            row.Add(downBtn);
            row.Add(gearBtn);

            card.Add(accent);
            card.Add(row);

            var due = new Label(DueText(t));
            due.style.marginTop = 8;
            due.style.fontSize = 10;
            due.style.opacity = 0.85f;
            due.style.backgroundColor = new Color(1, 1, 1, 0.06f);
            due.style.paddingLeft = 8;
            due.style.paddingRight = 8;
            due.style.paddingTop = 2;
            due.style.paddingBottom = 2;
            due.style.borderTopLeftRadius = 999;
            due.style.borderTopRightRadius = 999;
            due.style.borderBottomLeftRadius = 999;
            due.style.borderBottomRightRadius = 999;
            card.Add(due);

            // Description
            if (!string.IsNullOrWhiteSpace(t.description))
            {
                var fd = new Foldout { text = "Description", value = _descOpen[t.id] };
                fd.RegisterValueChangedCallback(e => _descOpen[t.id] = e.newValue);

                var desc = new Label(t.description);
                desc.style.whiteSpace = WhiteSpace.Normal;
                desc.style.opacity = 0.85f;
                desc.style.marginTop = 6;

                fd.Add(desc);
                card.Add(fd);
            }

            // ? Subtasks (list of strings)
            if (t.hasSubtasks && t.subtasks != null && t.subtasks.Count > 0)
            {
                var fs = new Foldout { text = $"Subtasks ({t.subtasks.Count})", value = _subsOpen[t.id] };
                fs.RegisterValueChangedCallback(e => _subsOpen[t.id] = e.newValue);

                var box = new VisualElement();
                box.style.marginTop = 6;

                foreach (var st in t.subtasks)
                {
                    if (string.IsNullOrWhiteSpace(st)) continue;
                    var lbl = new Label("• " + st);
                    lbl.style.opacity = 0.9f;
                    lbl.style.whiteSpace = WhiteSpace.Normal;
                    box.Add(lbl);
                }

                fs.Add(box);
                card.Add(fs);
            }

            // References
            if (t.hasReferences && t.references != null && t.references.Count > 0)
            {
                var fr = new Foldout { text = $"References ({t.references.Count})", value = _refsOpen[t.id] };
                fr.RegisterValueChangedCallback(e => _refsOpen[t.id] = e.newValue);

                var refsBox = new VisualElement();
                refsBox.style.marginTop = 6;

                foreach (var obj in t.references)
                {
                    if (obj == null) continue;

                    var rRow = new VisualElement();
                    rRow.style.flexDirection = FlexDirection.Row;
                    rRow.style.alignItems = Align.Center;
                    rRow.style.marginBottom = 4;

                    var name = new Label(obj.name);
                    name.style.flexGrow = 1;
                    name.style.opacity = 0.9f;
                    name.style.fontSize = 11;

                    var ping = new Button(() =>
                    {
                        EditorGUIUtility.PingObject(obj);
                        Selection.activeObject = obj;
                    })
                    { text = "Ping" };
                    ping.style.height = 20;

                    rRow.Add(name);
                    rRow.Add(ping);
                    refsBox.Add(rRow);
                }

                fr.Add(refsBox);
                card.Add(fr);
            }

            // Tags
            if (t.hasTags && t.tags != null && t.tags.Count > 0)
            {
                var tagsRow = new VisualElement();
                tagsRow.style.flexDirection = FlexDirection.Row;
                tagsRow.style.flexWrap = Wrap.Wrap;
                tagsRow.style.marginTop = 8;

                foreach (var tag in t.tags.Take(8))
                {
                    var chip = new Label(tag);
                    chip.style.fontSize = 10;
                    chip.style.backgroundColor = new Color(1, 1, 1, 0.06f);
                    chip.style.paddingLeft = 8;
                    chip.style.paddingRight = 8;
                    chip.style.paddingTop = 2;
                    chip.style.paddingBottom = 2;
                    chip.style.borderTopLeftRadius = 999;
                    chip.style.borderTopRightRadius = 999;
                    chip.style.borderBottomLeftRadius = 999;
                    chip.style.borderBottomRightRadius = 999;
                    chip.style.marginRight = 6;
                    chip.style.marginBottom = 6;
                    tagsRow.Add(chip);
                }

                card.Add(tagsRow);
            }

            // Drag start
            card.RegisterCallback<MouseDownEvent>(e =>
            {
                if (_board == null || !_board.allowDragBetweenColumns) return;
                if (e.button != 0) return;

                _draggingTaskId = t.id;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.StartDrag("Task");
            });

            return card;
        }

        private void ShowContextMenu(TasksBoardItem t)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Edit..."), false, () =>
                TasksBoardEditPopup.Open(t, _board, () =>
                {
                    t.Touch();
                    SaveBoard();
                    RefreshBoard();
                }));

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Move/To Do"), false, () => Move(t, TaskStatus.ToDo));
            menu.AddItem(new GUIContent("Move/Pending"), false, () => Move(t, TaskStatus.Pending));
            menu.AddItem(new GUIContent("Move/In Progress"), false, () => Move(t, TaskStatus.InProgress));
            menu.AddItem(new GUIContent("Move/Completed"), false, () => Move(t, TaskStatus.Completed));

            menu.AddSeparator("");

            if (!t.archived)
                menu.AddItem(new GUIContent("Archive"), false, () => { t.archived = true; t.Touch(); SaveBoard(); RefreshBoard(); });
            else
                menu.AddItem(new GUIContent("Unarchive"), false, () => { t.archived = false; t.Touch(); SaveBoard(); RefreshBoard(); });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Delete Task", $"Delete '{t.title}'?", "Delete", "Cancel"))
                {
                    _board.tasks.RemoveAll(x => x != null && x.id == t.id);
                    SaveBoard();
                    RefreshBoard();
                }
            });

            menu.ShowAsContext();
        }

        private void Move(TasksBoardItem t, TaskStatus status)
        {
            t.status = status;
            t.Touch();
            SaveBoard();
            RefreshBoard();
        }

        private Color PriorityColor(TaskPriority p)
        {
            if (_board == null) return Color.white;

            return p switch
            {
                TaskPriority.Low => _board.priorityLow,
                TaskPriority.Medium => _board.priorityMedium,
                TaskPriority.High => _board.priorityHigh,
                _ => _board.priorityCritical
            };
        }

        private string PriorityText(TaskPriority p)
        {
            return p switch
            {
                TaskPriority.Low => "LOW",
                TaskPriority.Medium => "MED",
                TaskPriority.High => "HIGH",
                _ => "CRIT"
            };
        }

        private string DueText(TasksBoardItem t)
        {
            if (!t.hasDueDate) return "No due date";

            var dueLocal = t.DueUtc.ToLocalTime();
            var days = (dueLocal.Date - DateTime.Now.Date).Days;

            if (days < 0) return $"Overdue {Math.Abs(days)}d — {dueLocal:yyyy-MM-dd}";
            if (days == 0) return $"Due today — {dueLocal:yyyy-MM-dd}";
            return $"{days}d left — {dueLocal:yyyy-MM-dd}";
        }

        // ---------------- EDIT POPUP ----------------

        public class TasksBoardEditPopup : EditorWindow
        {
            private TasksBoardItem _task;
            private TasksBoardAsset _board;
            private Action _onApply;

            private bool _loaded;

            private string _title;
            private string _desc;
            private TaskStatus _status;
            private TaskPriority _priority;

            private Color _accent;

            private bool _hasDue;
            private int _dueDays;

            private bool _hasTags;
            private string _tagsCsv;

            private bool _hasRefs;
            private List<UnityEngine.Object> _refs;

            // ? Subtasks as list of strings
            private bool _hasSubtasks;
            private List<string> _subs;

            private bool _archived;

            public static void Open(TasksBoardItem task, TasksBoardAsset board, Action onApply)
            {
                var w = CreateInstance<TasksBoardEditPopup>();
                w.titleContent = new GUIContent("Edit Task");
                w._task = task;
                w._board = board;
                w._onApply = onApply;
                w.minSize = new Vector2(480, 620);
                w.maxSize = new Vector2(740, 1000);
                w.ShowUtility();
            }

            private void OnEnable() => _loaded = false;

            private void LoadFromTaskIfNeeded()
            {
                if (_loaded) return;
                if (_task == null) return;

                _task.EnsureLists();

                _title = _task.title ?? "";
                _desc = _task.description ?? "";
                _status = _task.status;
                _priority = _task.priority;
                _accent = _task.accent;

                _hasDue = _task.hasDueDate;
                _dueDays = _task.hasDueDate
                    ? Mathf.Max(0, (_task.DueUtc.ToLocalTime().Date - DateTime.Now.Date).Days)
                    : 3;

                _hasTags = _task.hasTags;
                _tagsCsv = (_task.tags != null && _task.tags.Count > 0) ? string.Join(", ", _task.tags) : "";

                _hasRefs = _task.hasReferences;
                _refs = (_task.references != null) ? new List<UnityEngine.Object>(_task.references) : new List<UnityEngine.Object>();

                _hasSubtasks = _task.hasSubtasks;
                _subs = (_task.subtasks != null) ? new List<string>(_task.subtasks) : new List<string>();

                _archived = _task.archived;

                _loaded = true;
            }

            private void OnGUI()
            {
                if (_task == null)
                {
                    EditorGUILayout.HelpBox("Task missing.", MessageType.Warning);
                    return;
                }

                LoadFromTaskIfNeeded();

                EditorGUILayout.LabelField("Edit Task", EditorStyles.boldLabel);
                EditorGUILayout.Space(6);

                _title = EditorGUILayout.TextField("Title", _title);

                EditorGUILayout.LabelField("Description");
                _desc = EditorGUILayout.TextArea(_desc, GUILayout.Height(80));

                _status = (TaskStatus)EditorGUILayout.EnumPopup("Status", _status);
                _priority = (TaskPriority)EditorGUILayout.EnumPopup("Priority", _priority);

                bool forceFromPriority = _board != null && _board.forceAccentFromPriority;
                if (!forceFromPriority)
                {
                    _accent = EditorGUILayout.ColorField("Accent", _accent);
                }
                else
                {
                    EditorGUILayout.HelpBox("Accent is forced from Priority in this Board.", MessageType.Info);
                }

                EditorGUILayout.Space(8);

                _hasDue = EditorGUILayout.Toggle("Has Due Date", _hasDue);
                using (new EditorGUI.DisabledScope(!_hasDue))
                {
                    _dueDays = Mathf.Max(0, EditorGUILayout.IntField("Due in days", _dueDays));
                }

                EditorGUILayout.Space(8);

                _hasTags = EditorGUILayout.Toggle("Has Tags", _hasTags);
                using (new EditorGUI.DisabledScope(!_hasTags))
                {
                    _tagsCsv = EditorGUILayout.TextField("Tags (comma)", _tagsCsv);
                }

                EditorGUILayout.Space(8);

                _hasRefs = EditorGUILayout.Toggle("Has References", _hasRefs);
                using (new EditorGUI.DisabledScope(!_hasRefs))
                {
                    EditorGUILayout.LabelField("References");
                    if (_refs == null) _refs = new List<UnityEngine.Object>();

                    for (int i = 0; i < _refs.Count; i++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            _refs[i] = EditorGUILayout.ObjectField(_refs[i], typeof(UnityEngine.Object), true);
                            if (GUILayout.Button("X", GUILayout.Width(22)))
                            {
                                _refs.RemoveAt(i);
                                GUIUtility.ExitGUI();
                            }
                        }
                    }

                    if (GUILayout.Button("Add Reference"))
                        _refs.Add(null);
                }

                EditorGUILayout.Space(8);

                // ? Subtasks list UI
                _hasSubtasks = EditorGUILayout.Toggle("Has Subtasks", _hasSubtasks);
                using (new EditorGUI.DisabledScope(!_hasSubtasks))
                {
                    EditorGUILayout.LabelField("Subtasks");
                    if (_subs == null) _subs = new List<string>();

                    for (int i = 0; i < _subs.Count; i++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            _subs[i] = EditorGUILayout.TextField(_subs[i] ?? "");
                            if (GUILayout.Button("X", GUILayout.Width(22)))
                            {
                                _subs.RemoveAt(i);
                                GUIUtility.ExitGUI();
                            }
                        }
                    }

                    if (GUILayout.Button("Add Subtask"))
                        _subs.Add("New subtask");
                }

                EditorGUILayout.Space(8);
                _archived = EditorGUILayout.Toggle("Archived", _archived);

                EditorGUILayout.Space(14);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Cancel"))
                        Close();

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Apply", GUILayout.Height(24)))
                    {
                        _task.title = (_title ?? "").Trim();
                        _task.description = _desc ?? "";
                        _task.status = _status;
                        _task.priority = _priority;
                        _task.archived = _archived;

                        _task.hasDueDate = _hasDue;
                        if (_hasDue)
                            _task.DueUtc = DateTime.UtcNow.AddDays(Mathf.Max(0, _dueDays));

                        _task.hasTags = _hasTags;
                        _task.tags = _hasTags ? ParseTags(_tagsCsv) : new List<string>();

                        _task.hasReferences = _hasRefs;
                        _task.references = _hasRefs ? _refs.Where(o => o != null).ToList() : new List<UnityEngine.Object>();

                        _task.hasSubtasks = _hasSubtasks;
                        _task.subtasks = _hasSubtasks
                            ? (_subs ?? new List<string>())
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Select(s => s.Trim())
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList()
                            : new List<string>();

                        if (_board != null && _board.forceAccentFromPriority)
                        {
                            _task.accent = _priority switch
                            {
                                TaskPriority.Low => _board.priorityLow,
                                TaskPriority.Medium => _board.priorityMedium,
                                TaskPriority.High => _board.priorityHigh,
                                _ => _board.priorityCritical
                            };
                        }
                        else _task.accent = _accent;

                        _task.Touch();
                        _onApply?.Invoke();
                        Close();
                    }
                }
            }

            private List<string> ParseTags(string csv)
            {
                if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
                return csv.Split(',')
                    .Select(s => (s ?? "").Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        // ---------------- LAUNCHER WINDOW ----------------

        public class TasksBoardLauncherWindow : EditorWindow
        {
            private List<TasksBoardAsset> _boards = new();
            private TextField _search;
            private ScrollView _list;

            public static void Open()
            {
                EnsureFoldersStatic();
                var w = GetWindow<TasksBoardLauncherWindow>(true, "TasksBoard - Launcher", true);
                w.minSize = new Vector2(520, 380);
                w.Show();
                w.Rebuild();
            }

            private void OnEnable() => Rebuild();

            private void Rebuild()
            {
                rootVisualElement.Clear();

                var top = new VisualElement();
                top.style.flexDirection = FlexDirection.Row;
                top.style.alignItems = Align.Center;
                top.style.paddingLeft = 10;
                top.style.paddingRight = 10;
                top.style.paddingTop = 10;
                top.style.paddingBottom = 10;

                _search = new TextField();
                _search.style.flexGrow = 1;
                _search.RegisterValueChangedCallback(_ => RefreshList());

                var refreshBtn = new Button(() => { LoadBoards(); RefreshList(); }) { text = "Refresh" };
                refreshBtn.style.marginLeft = 8;

                var createBtn = new Button(() =>
                {
                    var created = CreateNewBoardAssetStatic("New Board");
                    LoadBoards();
                    RefreshList();
                    TasksBoardWindow.OpenBoard(created, newWindow: true);
                })
                { text = "Create New" };
                createBtn.style.marginLeft = 8;

                top.Add(new Label("Search") { style = { marginRight = 6, opacity = 0.85f } });
                top.Add(_search);
                top.Add(refreshBtn);
                top.Add(createBtn);

                _list = new ScrollView();
                _list.style.flexGrow = 1;
                _list.style.paddingLeft = 10;
                _list.style.paddingRight = 10;
                _list.style.paddingBottom = 10;

                rootVisualElement.Add(top);
                rootVisualElement.Add(_list);

                LoadBoards();
                RefreshList();
            }

            private void LoadBoards()
            {
                _boards.Clear();
                var guids = AssetDatabase.FindAssets("t:TasksBoardAsset", new[] { BoardsFolder });
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var asset = AssetDatabase.LoadAssetAtPath<TasksBoardAsset>(path);
                    if (asset != null) _boards.Add(asset);
                }

                _boards = _boards
                    .OrderBy(b => (b.boardName ?? b.name ?? "").ToLowerInvariant())
                    .ToList();
            }

            private void RefreshList()
            {
                _list.Clear();

                string q = (_search?.value ?? "").Trim();
                IEnumerable<TasksBoardAsset> items = _boards;
                if (!string.IsNullOrEmpty(q))
                {
                    items = items.Where(b =>
                        (b.boardName ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (b.name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                if (!items.Any())
                {
                    var empty = new Label("No boards found.");
                    empty.style.opacity = 0.75f;
                    _list.Add(empty);
                    return;
                }

                foreach (var b in items)
                {
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.marginBottom = 8;
                    row.style.paddingTop = 8;
                    row.style.paddingBottom = 8;
                    row.style.paddingLeft = 8;
                    row.style.paddingRight = 8;
                    row.style.backgroundColor = new Color(1, 1, 1, 0.04f);
                    row.style.borderTopLeftRadius = 10;
                    row.style.borderTopRightRadius = 10;
                    row.style.borderBottomLeftRadius = 10;
                    row.style.borderBottomRightRadius = 10;

                    var name = new Label(string.IsNullOrWhiteSpace(b.boardName) ? b.name : b.boardName);
                    name.style.flexGrow = 1;
                    name.style.unityFontStyleAndWeight = FontStyle.Bold;

                    var openBtn = new Button(() => TasksBoardWindow.OpenBoard(b, newWindow: true)) { text = "Open" };
                    var pingBtn = new Button(() =>
                    {
                        EditorGUIUtility.PingObject(b);
                        Selection.activeObject = b;
                    })
                    { text = "Ping" };
                    pingBtn.style.marginLeft = 6;

                    row.Add(name);
                    row.Add(openBtn);
                    row.Add(pingBtn);

                    _list.Add(row);
                }
            }
        }
    }
}
#endif
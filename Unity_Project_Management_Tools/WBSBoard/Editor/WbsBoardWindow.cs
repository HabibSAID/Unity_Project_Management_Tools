#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WBSBoard
{
    /// <summary>
    /// Main board window (Excel-like table).
    /// Reorder is ALWAYS enabled (stored list order).
    /// Index is optional (can be empty).
    /// Foldout is FAST (no full Refresh rebuild).
    /// </summary>
    public class WbsBoardWindow : EditorWindow
    {
        private const string RootFolder = "Assets/WBSBoard";
        private const string BoardsFolder = "Assets/WBSBoard/Boards";

        [SerializeField] private WbsBoardAsset _board;

        private VisualElement _topBar;
        private TextField _boardNameField;
        private TextField _searchField;

        private ScrollView _scroll;
        private VisualElement _table;

        private bool _geometryHooked;

        // Column widths
        private const float W_INDEX = 75;
        private const float W_TITLE = 210;
        private const float W_DESC = 330;
        private const float W_HOURS = 80;
        private const float W_DAYS = 80;
        private const float W_STATUS = 170; // wider because of pill
        private const float W_DEP = 90;
        private const float W_ACTIONS = 210;

        // Indent per hierarchy level (subtasks)
        private const float INDENT_PER_LEVEL = 26f;

        // ---------------- MENU ----------------
        public static void OpenBoard(WbsBoardAsset board, bool newWindow)
        {
            if (board == null) return;

            var w = newWindow ? CreateWindow<WbsBoardWindow>() : GetWindow<WbsBoardWindow>();
            w.minSize = new Vector2(1100, 560);
            w._board = board;
            w.titleContent = new GUIContent($"WBS - {board.boardName}");
            w.Show();
            w.RebuildAll();
        }

        [MenuItem("Tools/Open WBS Boards")]
        public static void OpenLauncherMenu()
        {
            WbsBoardLauncherWindow.Open();
        }

        private void OnEnable()
        {
            EnsureFolders();

            if (_board == null)
            {
                rootVisualElement.Clear();
                var msg = new Label("No board assigned.\n\nUse: Tools > WBS Board > Open Boards Launcher");
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

            foreach (var t in _board.tasks) TouchRecursive(t);

            titleContent = new GUIContent($"WBS - {_board.boardName}");

            BuildUI();
            Refresh();

            if (!_geometryHooked)
            {
                _geometryHooked = true;
                rootVisualElement.RegisterCallback<GeometryChangedEvent>(_ => ApplyResponsiveLayout());
            }
            ApplyResponsiveLayout();
        }

        private void TouchRecursive(WbsTask t)
        {
            if (t == null) return;
            t.TouchId();
            if (t.subtasks != null)
                foreach (var s in t.subtasks) TouchRecursive(s);
        }

        // ---------------- folders ----------------
        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "WBSBoard");

            if (!AssetDatabase.IsValidFolder(BoardsFolder))
                AssetDatabase.CreateFolder(RootFolder, "Boards");
        }

        private static string MakeSafeFileName(string s)
        {
            var bad = Path.GetInvalidFileNameChars();
            foreach (var c in bad) s = s.Replace(c.ToString(), "");
            s = s.Replace("/", "_").Replace("\\", "_").Replace(":", "_").Trim();
            if (string.IsNullOrEmpty(s)) s = "Board";
            return s;
        }

        private void SaveBoard()
        {
            if (_board == null) return;
            EditorUtility.SetDirty(_board);
            AssetDatabase.SaveAssets();
        }

        private void RenameBoardAndAsset(string newName)
        {
            if (_board == null) return;

            newName = (newName ?? "").Trim();
            if (string.IsNullOrEmpty(newName))
            {
                EditorUtility.DisplayDialog("Invalid Name", "Board name cannot be empty.", "OK");
                return;
            }

            _board.boardName = newName;
            EditorUtility.SetDirty(_board);

            string path = AssetDatabase.GetAssetPath(_board);
            if (!string.IsNullOrEmpty(path))
            {
                string safe = MakeSafeFileName(newName);
                string err = AssetDatabase.RenameAsset(path, safe);
                if (!string.IsNullOrEmpty(err))
                    Debug.LogWarning($"RenameAsset warning: {err}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            titleContent = new GUIContent($"WBS - {_board.boardName}");
        }

        private void DeleteBoard()
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
            WbsBoardLauncherWindow.Open();
        }

        // ---------------- RESPONSIVE ----------------
        private void ApplyResponsiveLayout()
        {
            if (_topBar == null) return;

            float w = position.width;
            bool narrow = w < 980f;
            bool veryNarrow = w < 740f;

            _topBar.style.flexWrap = Wrap.Wrap;

            foreach (var child in _topBar.Children())
            {
                child.style.marginBottom = 6;
                child.style.marginRight = 8;
            }

            if (_boardNameField != null) _boardNameField.style.width = veryNarrow ? Length.Percent(100) : (narrow ? 180 : 240);
            if (_searchField != null) _searchField.style.width = veryNarrow ? Length.Percent(100) : (narrow ? 180 : 240);

            if (_scroll != null) _scroll.style.minWidth = 0;
        }

        // ---------------- UI ----------------
        private void BuildUI()
        {
            rootVisualElement.Clear();

            // Top bar
            _topBar = new VisualElement();
            _topBar.style.flexDirection = FlexDirection.Row;
            _topBar.style.alignItems = Align.Center;
            _topBar.style.paddingLeft = 10;
            _topBar.style.paddingRight = 10;
            _topBar.style.paddingTop = 8;
            _topBar.style.paddingBottom = 8;
            _topBar.style.backgroundColor = _board.headerBg;
            _topBar.style.borderBottomWidth = 1;
            _topBar.style.borderBottomColor = new Color(0, 0, 0, 0.35f);

            var title = new Label($"WBS - {_board.boardName}");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.marginRight = 10;
            _topBar.Add(title);

            // Board group
            var boardGroup = GroupBoxLike();
            boardGroup.style.flexGrow = 1;
            boardGroup.style.flexShrink = 1;
            boardGroup.style.minWidth = 360;

            _boardNameField = new TextField { value = _board.boardName };
            _boardNameField.style.width = 240;
            _boardNameField.tooltip = "Rename the board (also renames the asset file).";

            var renameBtn = new Button(() => RenameBoardAndAsset(_boardNameField.value)) { text = "Rename" };
            renameBtn.style.marginLeft = 6;

            var pingBtn = new Button(() =>
            {
                EditorGUIUtility.PingObject(_board);
                Selection.activeObject = _board;
            })
            { text = "Ping" };
            pingBtn.style.marginLeft = 6;

            var deleteBtn = new Button(DeleteBoard) { text = "Delete" };
            deleteBtn.style.marginLeft = 6;
            deleteBtn.style.backgroundColor = new StyleColor(Color.red);
            deleteBtn.style.color = Color.white;
            deleteBtn.style.unityFontStyleAndWeight = FontStyle.Bold;

            boardGroup.Add(new Label("Board") { style = { opacity = 0.85f, marginRight = 6 } });
            boardGroup.Add(_boardNameField);
            boardGroup.Add(renameBtn);
            boardGroup.Add(pingBtn);
            boardGroup.Add(deleteBtn);
            _topBar.Add(boardGroup);

            // Search group
            var searchGroup = GroupBoxLike();
            searchGroup.style.flexGrow = 1;
            searchGroup.style.flexShrink = 1;
            searchGroup.style.minWidth = 320;

            _searchField = new TextField();
            _searchField.style.width = 240;
            _searchField.tooltip = "Search by title/description/index/dependsOn.";
            _searchField.RegisterValueChangedCallback(_ => Refresh());

            var clearBtn = new Button(() => { _searchField.value = ""; Refresh(); }) { text = "Clear" };
            clearBtn.style.marginLeft = 6;

            searchGroup.Add(new Label("Search") { style = { opacity = 0.85f, marginRight = 6 } });
            searchGroup.Add(_searchField);
            searchGroup.Add(clearBtn);
            _topBar.Add(searchGroup);

            var openOther = new Button(WbsBoardLauncherWindow.Open) { text = "Open Other Board..." };

            // BIG BLUE +TASK button
            var addRoot = new Button(AddRootTask) { text = "+ Task" };
            addRoot.style.height = 30;
            addRoot.style.unityFontStyleAndWeight = FontStyle.Bold;
            addRoot.style.fontSize = 12;
            addRoot.style.backgroundColor = new Color(0.25f, 0.65f, 1f, 0.95f);
            addRoot.style.color = Color.black;
            addRoot.style.borderTopLeftRadius = 8;
            addRoot.style.borderTopRightRadius = 8;
            addRoot.style.borderBottomLeftRadius = 8;
            addRoot.style.borderBottomRightRadius = 8;
            addRoot.style.paddingLeft = 14;
            addRoot.style.paddingRight = 14;
            addRoot.tooltip = "Add a new root task (Index is left empty).";

            var saveBtn = new Button(SaveBoard) { text = "Save" };
            saveBtn.tooltip = "Save the board asset.";

            var refreshBtn = new Button(Refresh) { text = "Refresh" };
            refreshBtn.tooltip = "Rebuild the visible table (use after big changes).";

            _topBar.Add(openOther);
            _topBar.Add(addRoot);
            _topBar.Add(saveBtn);
            _topBar.Add(refreshBtn);

            // Table scroll
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;
            _scroll.style.minWidth = 0;
            _scroll.style.backgroundColor = _board.panelBg;
            _scroll.style.paddingLeft = 10;
            _scroll.style.paddingRight = 10;
            _scroll.style.paddingTop = 10;
            _scroll.style.paddingBottom = 10;

            _table = new VisualElement();
            _table.style.flexDirection = FlexDirection.Column;

            _scroll.Add(BuildHeaderRow());
            _scroll.Add(_table);

            rootVisualElement.Add(_topBar);
            rootVisualElement.Add(_scroll);
        }

        // Excel-style vertical separator
        private VisualElement VSep()
        {
            var s = new VisualElement();
            s.style.width = 1;
            s.style.alignSelf = Align.Stretch;
            s.style.backgroundColor = new Color(0, 0, 0, 0.25f);
            s.style.marginLeft = 6;
            s.style.marginRight = 6;
            return s;
        }

        private VisualElement BuildHeaderRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.marginBottom = 10;
            row.style.backgroundColor = new Color(1, 1, 1, 0.04f);
            row.style.borderTopLeftRadius = 12;
            row.style.borderTopRightRadius = 12;
            row.style.borderBottomLeftRadius = 12;
            row.style.borderBottomRightRadius = 12;
            row.style.borderLeftWidth = 1;
            row.style.borderRightWidth = 1;
            row.style.borderTopWidth = 1;
            row.style.borderBottomWidth = 1;
            row.style.borderLeftColor = new Color(0, 0, 0, 0.35f);
            row.style.borderRightColor = new Color(0, 0, 0, 0.35f);
            row.style.borderTopColor = new Color(0, 0, 0, 0.35f);
            row.style.borderBottomColor = new Color(0, 0, 0, 0.35f);

            // indent space for indent + foldout arrow area
            row.Add(new Label("") { style = { width = 26 + 6 + INDENT_PER_LEVEL } });

            row.Add(HeaderCell("Index", W_INDEX)); row.Add(VSep());
            row.Add(HeaderCell("Title", W_TITLE)); row.Add(VSep());
            row.Add(HeaderCell("Description", W_DESC)); row.Add(VSep());
            row.Add(HeaderCell("Hours", W_HOURS)); row.Add(VSep());
            row.Add(HeaderCell("Days", W_DAYS)); row.Add(VSep());
            row.Add(HeaderCell("Status", W_STATUS)); row.Add(VSep());
            row.Add(HeaderCell("Depends", W_DEP)); row.Add(VSep());
            row.Add(HeaderCell("Actions", W_ACTIONS));

            return row;
        }

        private Label HeaderCell(string text, float width)
        {
            var l = new Label(text);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.opacity = 0.9f;
            l.style.width = width;
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

        // ---------------- data ----------------
        private void AddRootTask()
        {
            if (_board == null) return;

            Undo.RecordObject(_board, "Add Task");

            _board.tasks.Add(new WbsTask
            {
                index = 0f, // empty
                title = "New Task",
                description = "",
                estimateHours = 0,
                estimateDays = 0,
                dependsOn = 0,
                status = WbsStatus.ToDo,
                foldout = true
            });

            SaveBoard();
            Refresh();
        }

        private void Refresh()
        {
            if (_board == null || _table == null) return;

            foreach (var t in _board.tasks) TouchRecursive(t);

            _table.Clear();

            string q = (_searchField?.value ?? "").Trim();

            // keep stored order always
            IEnumerable<WbsTask> items = _board.tasks;

            if (!string.IsNullOrEmpty(q))
            {
                items = items.Where(t =>
                    (t.title ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (t.description ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.index.ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.dependsOn.ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                );
            }

            foreach (var t in items)
                _table.Add(BuildTaskBlock(t, level: 0));
        }

        // ? FAST foldout approach: children container is shown/hidden without Refresh()
        private VisualElement BuildTaskBlock(WbsTask t, int level)
        {
            var block = new VisualElement();
            block.style.flexDirection = FlexDirection.Column;

            var childrenContainer = new VisualElement();
            childrenContainer.style.flexDirection = FlexDirection.Column;

            var row = BuildRow(t, level, childrenContainer);

            block.Add(row);
            block.Add(childrenContainer);

            if (t.subtasks != null && t.subtasks.Count > 0)
            {
                foreach (var s in t.subtasks) // keep stored order
                    childrenContainer.Add(BuildTaskBlock(s, level + 1));
            }

            bool hasChildren = t.subtasks != null && t.subtasks.Count > 0;
            childrenContainer.style.display = (hasChildren && t.foldout) ? DisplayStyle.Flex : DisplayStyle.None;

            return block;
        }

        // ? IMPORTANT: third param is the children container (NOT bool)
        private VisualElement BuildRow(WbsTask t, int level, VisualElement childrenContainer)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.marginBottom = 6;

            row.style.backgroundColor = RowTintForStatus(t.status);

            row.style.borderTopLeftRadius = 12;
            row.style.borderTopRightRadius = 12;
            row.style.borderBottomLeftRadius = 12;
            row.style.borderBottomRightRadius = 12;
            row.style.borderLeftWidth = 1;
            row.style.borderRightWidth = 1;
            row.style.borderTopWidth = 1;
            row.style.borderBottomWidth = 1;
            row.style.borderLeftColor = new Color(0, 0, 0, 0.35f);
            row.style.borderRightColor = new Color(0, 0, 0, 0.35f);
            row.style.borderTopColor = new Color(0, 0, 0, 0.35f);
            row.style.borderBottomColor = new Color(0, 0, 0, 0.35f);

            bool blocked = IsBlocked(t);
            if (blocked)
                row.style.backgroundColor = new Color(0.25f, 0.08f, 0.08f, 0.85f);

            var indent = new VisualElement();
            indent.style.width = level * INDENT_PER_LEVEL;
            row.Add(indent);

            // Foldout arrow (FAST - no Refresh)
            var foldHost = new VisualElement();
            foldHost.style.width = 26;
            foldHost.style.marginRight = 6;

            bool hasChildren = t.subtasks != null && t.subtasks.Count > 0;

            var fold = new Foldout { text = "", value = t.foldout };
            fold.style.marginTop = 0;
            fold.style.marginBottom = 0;
            fold.style.paddingLeft = 0;
            fold.style.paddingRight = 0;
            fold.SetEnabled(hasChildren);

            fold.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(_board, "Toggle Foldout");
                t.foldout = e.newValue;

                // instant show/hide children
                if (childrenContainer != null)
                    childrenContainer.style.display = (hasChildren && t.foldout) ? DisplayStyle.Flex : DisplayStyle.None;

                SaveBoard();
            });

            foldHost.Add(fold);
            row.Add(foldHost);

            row.Add(MakeIndexCell(t)); row.Add(VSep());
            row.Add(MakeTitleCell(t)); row.Add(VSep());
            row.Add(MakeDescCell(t)); row.Add(VSep());
            row.Add(MakeHoursCell(t)); row.Add(VSep());
            row.Add(MakeDaysCell(t)); row.Add(VSep());
            row.Add(MakeStatusCell(t)); row.Add(VSep());
            row.Add(MakeDependsCell(t)); row.Add(VSep());
            row.Add(MakeActionsCell(t));

            return row;
        }

        private Color RowTintForStatus(WbsStatus s)
        {
            Color baseC = StatusColor(s);
            Color bg = _board.cardBg;
            return Color.Lerp(bg, baseC, 0.18f);
        }

        private VisualElement MakeIndexCell(WbsTask t)
        {
            var tf = new TextField();
            tf.style.width = W_INDEX;

            tf.SetValueWithoutNotify(t.index <= 0f ? "" : t.index.ToString("0.###"));
            tf.tooltip = "Optional. Examples: 1, 1.1, 2.3 (empty = not set).";

            tf.RegisterValueChangedCallback(ev =>
            {
                Undo.RecordObject(_board, "Edit Index");

                string s = (ev.newValue ?? "").Trim();
                if (string.IsNullOrEmpty(s))
                    t.index = 0f;
                else if (float.TryParse(s, out float v))
                    t.index = v;

                SaveBoard();
            });

            return tf;
        }

        private VisualElement MakeTitleCell(WbsTask t)
        {
            var title = new TextField { value = t.title ?? "" };
            title.style.width = W_TITLE;

            title.RegisterValueChangedCallback(ev =>
            {
                Undo.RecordObject(_board, "Edit Title");
                t.title = ev.newValue;
                SaveBoard();
            });

            return title;
        }

        private VisualElement MakeDescCell(WbsTask t)
        {
            var desc = new TextField { value = t.description ?? "" };
            desc.style.width = W_DESC;

            desc.RegisterValueChangedCallback(ev =>
            {
                Undo.RecordObject(_board, "Edit Description");
                t.description = ev.newValue;
                SaveBoard();
            });

            return desc;
        }

        private VisualElement MakeHoursCell(WbsTask t)
        {
            var hours = new FloatField { value = t.estimateHours };
            hours.style.width = W_HOURS;
            hours.tooltip = "Estimated hours (>= 0).";

            hours.RegisterValueChangedCallback(ev =>
            {
                Undo.RecordObject(_board, "Edit Hours");
                t.estimateHours = Mathf.Max(0f, ev.newValue);
                SaveBoard();
            });

            return hours;
        }

        private VisualElement MakeDaysCell(WbsTask t)
        {
            var days = new FloatField { value = t.estimateDays };
            days.style.width = W_DAYS;
            days.tooltip = "Estimated days (>= 0).";

            days.RegisterValueChangedCallback(ev =>
            {
                Undo.RecordObject(_board, "Edit Days");
                t.estimateDays = Mathf.Max(0f, ev.newValue);
                SaveBoard();
            });

            return days;
        }

        private VisualElement MakeStatusCell(WbsTask t)
        {
            var host = new VisualElement();
            host.style.flexDirection = FlexDirection.Row;
            host.style.alignItems = Align.Center;
            host.style.width = W_STATUS;

            var status = new EnumField(t.status);
            status.style.flexGrow = 1;
            status.tooltip = "Task status (affects row tint + pill color).";

            var pill = new Label(StatusText(t.status));
            pill.style.marginLeft = 6;
            pill.style.paddingLeft = 8;
            pill.style.paddingRight = 8;
            pill.style.paddingTop = 2;
            pill.style.paddingBottom = 2;
            pill.style.borderTopLeftRadius = 999;
            pill.style.borderTopRightRadius = 999;
            pill.style.borderBottomLeftRadius = 999;
            pill.style.borderBottomRightRadius = 999;
            pill.style.unityFontStyleAndWeight = FontStyle.Bold;
            pill.style.fontSize = 10;
            pill.style.backgroundColor = StatusColor(t.status);
            pill.style.color = Color.black;

            status.RegisterValueChangedCallback(_ =>
            {
                Undo.RecordObject(_board, "Edit Status");
                t.status = (WbsStatus)status.value;
                SaveBoard();

                // this DOES need a refresh so row tint + pill update across UI
                Refresh();
            });

            host.Add(status);
            host.Add(pill);
            return host;
        }

        private static string StatusText(WbsStatus s)
        {
            return s switch
            {
                WbsStatus.ToDo => "TODO",
                WbsStatus.Pending => "PEND",
                WbsStatus.InProgress => "PROG",
                _ => "DONE"
            };
        }

        private VisualElement MakeDependsCell(WbsTask t)
        {
            var dep = new FloatField { value = t.dependsOn };
            dep.style.width = W_DEP;
            dep.tooltip = "0 = none. Otherwise depends on that Index (e.g., 1.0).";

            dep.RegisterValueChangedCallback(ev =>
            {
                Undo.RecordObject(_board, "Edit Dependency");
                t.dependsOn = Mathf.Max(0f, ev.newValue);
                SaveBoard();

                // blocked state affects row color -> refresh
                Refresh();
            });

            return dep;
        }

        private static Button IconArrowButton(string iconName, Action onClick)
        {
            var b = new Button(onClick);
            b.style.width = 28;
            b.style.height = 22;

            var tex = EditorGUIUtility.IconContent(iconName)?.image as Texture2D;
            if (tex != null)
            {
                b.text = "";
                b.style.backgroundImage = new StyleBackground(tex);
                b.style.unityBackgroundImageTintColor = new Color(1, 1, 1, 0.95f);
                b.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                b.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                b.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            }
            else
            {
                b.text = "^";
            }

            return b;
        }

        private VisualElement MakeActionsCell(WbsTask t)
        {
            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.width = W_ACTIONS;

            var up = IconArrowButton("d_scrollup", () => MoveInParent(t, -1));
            up.tooltip = "Move up (reorder).";

            var dn = IconArrowButton("d_scrolldown", () => MoveInParent(t, +1));
            dn.style.marginLeft = 4;
            dn.tooltip = "Move down (reorder).";

            var addSub = new Button(() =>
            {
                Undo.RecordObject(_board, "Add Subtask");

                if (t.subtasks == null) t.subtasks = new List<WbsTask>();

                t.subtasks.Add(new WbsTask
                {
                    index = 0f,
                    title = "New Subtask",
                    description = "",
                    estimateHours = 0,
                    estimateDays = 0,
                    dependsOn = 0,
                    status = WbsStatus.ToDo,
                    foldout = true
                });

                t.foldout = true;
                SaveBoard();
                Refresh();
            })
            { text = "+Sub" };
            addSub.style.marginLeft = 8;
            addSub.tooltip = "Add a subtask under this task.";

            var del = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("Delete", $"Delete '{t.title}' ?", "Delete", "Cancel"))
                    return;

                Undo.RecordObject(_board, "Delete Task");
                RemoveTaskById(_board.tasks, t.id);
                SaveBoard();
                Refresh();
            })
            { text = "Delete" };
            del.style.marginLeft = 6;
            del.tooltip = "Delete this task (and all subtasks).";

            actions.Add(up);
            actions.Add(dn);
            actions.Add(addSub);
            actions.Add(del);

            return actions;
        }

        private Color StatusColor(WbsStatus s)
        {
            return s switch
            {
                WbsStatus.ToDo => _board.todoColor,
                WbsStatus.Pending => _board.pendingColor,
                WbsStatus.InProgress => _board.progressColor,
                _ => _board.doneColor
            };
        }

        private bool IsBlocked(WbsTask t)
        {
            if (_board == null) return false;
            if (t.dependsOn <= 0f) return false;

            var all = new List<WbsTask>();
            CollectAll(_board.tasks, all);

            var dep = all.FirstOrDefault(x => Mathf.Abs(x.index - t.dependsOn) < 0.0001f);
            if (dep == null) return true;
            return dep.status != WbsStatus.Completed;
        }

        private void CollectAll(List<WbsTask> list, List<WbsTask> outList)
        {
            if (list == null) return;
            foreach (var t in list)
            {
                outList.Add(t);
                if (t.subtasks != null && t.subtasks.Count > 0)
                    CollectAll(t.subtasks, outList);
            }
        }

        private void MoveInParent(WbsTask t, int dir)
        {
            if (TryMoveInList(_board.tasks, t, dir))
            {
                SaveBoard();
                Refresh();
            }
        }

        private bool TryMoveInList(List<WbsTask> list, WbsTask target, int dir)
        {
            int i = list.FindIndex(x => x != null && x.id == target.id);
            if (i >= 0)
            {
                int j = i + dir;
                if (j < 0 || j >= list.Count) return false;

                (list[i], list[j]) = (list[j], list[i]);
                return true;
            }

            foreach (var t in list)
            {
                if (t?.subtasks == null) continue;
                if (TryMoveInList(t.subtasks, target, dir))
                    return true;
            }

            return false;
        }

        private bool RemoveTaskById(List<WbsTask> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].id == id)
                {
                    list.RemoveAt(i);
                    return true;
                }

                if (list[i]?.subtasks != null && RemoveTaskById(list[i].subtasks, id))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Boards launcher window (list all boards, create/open/ping).
    /// </summary>
    public class WbsBoardLauncherWindow : EditorWindow
    {
        private const string RootFolder = "Assets/WBSBoard";
        private const string BoardsFolder = "Assets/WBSBoard/Boards";

        private List<WbsBoardAsset> _boards = new();
        private TextField _search;
        private ScrollView _list;

        public static void Open()
        {
            EnsureFoldersStatic();
            var w = GetWindow<WbsBoardLauncherWindow>(true, "WBS Board - Launcher", true);
            w.minSize = new Vector2(520, 380);
            w.Show();
            w.Rebuild();
        }

        private void OnEnable() => Rebuild();

        private static void EnsureFoldersStatic()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "WBSBoard");

            if (!AssetDatabase.IsValidFolder(BoardsFolder))
                AssetDatabase.CreateFolder(RootFolder, "Boards");
        }

        private static string MakeSafeFileName(string s)
        {
            var bad = Path.GetInvalidFileNameChars();
            foreach (var c in bad) s = s.Replace(c.ToString(), "");
            s = s.Replace("/", "_").Replace("\\", "_").Replace(":", "_").Trim();
            if (string.IsNullOrEmpty(s)) s = "Board";
            return s;
        }

        private static WbsBoardAsset CreateNewBoardAssetStatic(string boardName)
        {
            EnsureFoldersStatic();

            string safe = MakeSafeFileName(string.IsNullOrWhiteSpace(boardName) ? "New Board" : boardName.Trim());
            string path = AssetDatabase.GenerateUniqueAssetPath($"{BoardsFolder}/{safe}.asset");

            var asset = ScriptableObject.CreateInstance<WbsBoardAsset>();
            asset.boardName = string.IsNullOrWhiteSpace(boardName) ? "New Board" : boardName.Trim();

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

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
            _search.tooltip = "Search boards by name.";
            _search.RegisterValueChangedCallback(_ => RefreshList());

            var refreshBtn = new Button(() => { LoadBoards(); RefreshList(); }) { text = "Refresh" };
            refreshBtn.style.marginLeft = 8;

            var createBtn = new Button(() =>
            {
                var created = CreateNewBoardAssetStatic("New Board");
                LoadBoards();
                RefreshList();
                WbsBoardWindow.OpenBoard(created, newWindow: true);
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
            var guids = AssetDatabase.FindAssets("t:WbsBoardAsset", new[] { BoardsFolder });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var asset = AssetDatabase.LoadAssetAtPath<WbsBoardAsset>(path);
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
            IEnumerable<WbsBoardAsset> items = _boards;
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

                var openBtn = new Button(() => WbsBoardWindow.OpenBoard(b, newWindow: true)) { text = "Open" };

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
#endif

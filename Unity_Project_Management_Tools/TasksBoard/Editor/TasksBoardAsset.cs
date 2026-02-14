using System;
using System.Collections.Generic;
using UnityEngine;

namespace TasksBoard
{
    public enum TaskStatus { ToDo, Pending, InProgress, Completed }
    public enum TaskPriority { Low, Medium, High, Critical }

    [Serializable]
    public class TasksBoardItem
    {
        public string id = Guid.NewGuid().ToString("N");
        public string title;
        [TextArea] public string description;

        public TaskStatus status = TaskStatus.ToDo;
        public TaskPriority priority = TaskPriority.Medium;

        public bool hasDueDate;
        public long dueDateUtcTicks;

        public bool hasTags = true;
        public List<string> tags = new List<string>();

        public bool hasReferences;
        public List<UnityEngine.Object> references = new List<UnityEngine.Object>();

        // ? Subtasks = simple strings (like tags)
        public bool hasSubtasks;
        public List<string> subtasks = new List<string>();

        // Accent
        public Color accent = new Color(0.25f, 0.65f, 1f, 1f);

        public bool archived;

        public long createdUtcTicks = DateTime.UtcNow.Ticks;
        public long updatedUtcTicks = DateTime.UtcNow.Ticks;

        public DateTime DueUtc
        {
            get => new DateTime(dueDateUtcTicks, DateTimeKind.Utc);
            set => dueDateUtcTicks = value.ToUniversalTime().Ticks;
        }

        public DateTime CreatedUtc => new DateTime(createdUtcTicks, DateTimeKind.Utc);
        public DateTime UpdatedUtc => new DateTime(updatedUtcTicks, DateTimeKind.Utc);

        public void Touch() => updatedUtcTicks = DateTime.UtcNow.Ticks;

        public void EnsureLists()
        {
            tags ??= new List<string>();
            references ??= new List<UnityEngine.Object>();
            subtasks ??= new List<string>();
        }
    }

    [CreateAssetMenu(menuName = "TasksBoard/Board", fileName = "TasksBoard")]
    public class TasksBoardAsset : ScriptableObject
    {
        [Header("Board")]
        public string boardName = "TasksBoard";
        public List<TasksBoardItem> tasks = new List<TasksBoardItem>();

        [Header("Kanban Column Colors (Header Pills)")]
        public Color todoColor = new Color(0.70f, 0.55f, 1f, 1f);
        public Color pendingColor = new Color(0.35f, 0.70f, 1f, 1f);
        public Color inProgressColor = new Color(1f, 0.75f, 0.25f, 1f);
        public Color completedColor = new Color(0.25f, 0.90f, 0.55f, 1f);

        [Header("Priority Accent Colors")]
        public Color priorityLow = new Color(0.35f, 0.75f, 1f, 1f);
        public Color priorityMedium = new Color(1f, 0.8f, 0.25f, 1f);
        public Color priorityHigh = new Color(1f, 0.5f, 0.25f, 1f);
        public Color priorityCritical = new Color(1f, 0.25f, 0.3f, 1f);

        [Header("Accent Rules")]
        [Tooltip("If true, task accent is always the priority color (ignores per-task accent).")]
        public bool forceAccentFromPriority = true;

        [Header("Behavior")]
        [Tooltip("If false, tasks cannot be dragged between columns.")]
        public bool allowDragBetweenColumns = true;
    }
}

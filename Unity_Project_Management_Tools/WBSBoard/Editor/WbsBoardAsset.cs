using System;
using System.Collections.Generic;
using UnityEngine;

namespace WBSBoard
{
    public enum WbsStatus { ToDo, Pending, InProgress, Completed }

    [Serializable]
    public class WbsTask
    {
        [HideInInspector] public string id = Guid.NewGuid().ToString("N");

        public float index;        // 1, 1.1, 2.3 ...
        public float dependsOn;    // 0 = none, else depends on that index

        public string title;
        [TextArea(2, 8)] public string description;

        public float estimateHours;
        public float estimateDays;

        public WbsStatus status = WbsStatus.ToDo;

        public List<WbsTask> subtasks = new List<WbsTask>();
        public bool foldout = true;

        public void TouchId()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString("N");
        }
    }

    [CreateAssetMenu(menuName = "WBS Board/WBS Board", fileName = "WbsBoard")]
    public class WbsBoardAsset : ScriptableObject
    {
        [Header("Board")]
        public string boardName = "WBS Board";

        [Header("UI Colors")]
        public Color headerBg = new Color(0.12f, 0.12f, 0.12f, 1f);
        public Color panelBg = new Color(0.09f, 0.09f, 0.09f, 1f);
        public Color cardBg = new Color(0.13f, 0.13f, 0.13f, 1f);

        [Header("Status Colors")]
        public Color todoColor = new Color(0.25f, 0.65f, 1f, 1f);
        public Color pendingColor = new Color(1f, 0.76f, 0.25f, 1f);
        public Color progressColor = new Color(0.55f, 1f, 0.35f, 1f);
        public Color doneColor = new Color(0.75f, 0.75f, 0.75f, 1f);

        public List<WbsTask> tasks = new List<WbsTask>();
    }
}

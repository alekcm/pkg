using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapEditorPrototype
{
    public class EditorUndoRedoSystem : MonoBehaviour
    {
        [SerializeField] private MapSaveSystem mapSaveSystem;
        [SerializeField] private int maxStates = 100;
        [SerializeField] private bool enableKeyboardShortcuts = true;

        private readonly Stack<string> undoStack = new Stack<string>();
        private readonly Stack<string> redoStack = new Stack<string>();

        public event Action StateChanged;

        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;

        private void Start()
        {
            ResetHistoryToCurrentState();
        }

        private void Update()
        {
            if (!enableKeyboardShortcuts)
            {
                return;
            }

            bool ctrlHeld = InputHelper.GetKey(KeyCode.LeftControl) || InputHelper.GetKey(KeyCode.RightControl);
            if (!ctrlHeld)
            {
                return;
            }

            if (InputHelper.GetKeyDown(KeyCode.Z))
            {
                Undo();
            }
            else if (InputHelper.GetKeyDown(KeyCode.Y))
            {
                Redo();
            }
        }

        public void ResetHistoryToCurrentState()
        {
            undoStack.Clear();
            redoStack.Clear();
            NotifyStateChanged();
        }

        public void RecordStateBeforeChange()
        {
            if (mapSaveSystem == null)
            {
                return;
            }

            RecordSnapshot(mapSaveSystem.CaptureCurrentStateJson());
        }

        public void RecordSpecificSnapshot(string snapshot)
        {
            RecordSnapshot(snapshot);
        }

        public void Undo()
        {
            if (!CanUndo || mapSaveSystem == null)
            {
                return;
            }

            string currentSnapshot = mapSaveSystem.CaptureCurrentStateJson();
            string previousSnapshot = undoStack.Pop();
            redoStack.Push(currentSnapshot);
            mapSaveSystem.LoadFromJson(previousSnapshot);
            NotifyStateChanged();
        }

        public void Redo()
        {
            if (!CanRedo || mapSaveSystem == null)
            {
                return;
            }

            string currentSnapshot = mapSaveSystem.CaptureCurrentStateJson();
            string nextSnapshot = redoStack.Pop();
            undoStack.Push(currentSnapshot);
            mapSaveSystem.LoadFromJson(nextSnapshot);
            TrimUndoStack();
            NotifyStateChanged();
        }

        private void RecordSnapshot(string snapshot)
        {
            if (string.IsNullOrEmpty(snapshot))
            {
                return;
            }

            if (undoStack.Count == 0 || undoStack.Peek() != snapshot)
            {
                undoStack.Push(snapshot);
                TrimUndoStack();
            }

            redoStack.Clear();
            NotifyStateChanged();
        }

        private void TrimUndoStack()
        {
            if (maxStates <= 0 || undoStack.Count <= maxStates)
            {
                return;
            }

            string[] snapshots = undoStack.ToArray();
            undoStack.Clear();
            int countToKeep = Mathf.Min(maxStates, snapshots.Length);
            for (int i = countToKeep - 1; i >= 0; i--)
            {
                undoStack.Push(snapshots[i]);
            }
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }
    }
}

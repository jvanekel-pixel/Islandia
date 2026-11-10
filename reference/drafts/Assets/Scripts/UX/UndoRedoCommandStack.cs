using System;
using System.Collections.Generic;
using UnityEngine;

namespace IslandBuilder.UX
{
    public enum CommandActionType : byte
    {
        SpawnObject = 0,
        DeleteObject = 1,
        TransformObject = 2,
        ScatterBatch = 3
    }

    public struct TransformSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    public struct IslandCommandRecord
    {
        public CommandActionType ActionType;
        public int BatchGroupId;
        public GameObject TargetObject;
        public TransformSnapshot BeforeState;
        public TransformSnapshot AfterState;
    }

    public sealed class UndoRedoCommandStack : MonoBehaviour
    {
        public static UndoRedoCommandStack Instance { get; private set; }

        private const int MaxHistoryCapacity = 256;

        private readonly IslandCommandRecord[] _ringBuffer = new IslandCommandRecord[MaxHistoryCapacity];
        private int _headIndex;
        private int _undoCount;
        private int _redoCount;
        private int _currentBatchId = 1;

        public int AvailableUndoCount => _undoCount;
        public int AvailableRedoCount => _redoCount;

        public event Action<int, int> OnHistoryStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public int BeginCommandBatch()
        {
            _currentBatchId++;
            return _currentBatchId;
        }

        public void RecordCommand(IslandCommandRecord record)
        {
            _ringBuffer[_headIndex] = record;
            _headIndex = (_headIndex + 1) % MaxHistoryCapacity;

            if (_undoCount < MaxHistoryCapacity)
            {
                _undoCount++;
            }

            // Recording a new action invalidates redo branch
            _redoCount = 0;
            OnHistoryStateChanged?.Invoke(_undoCount, _redoCount);
        }

        public void UndoLastAction()
        {
            if (_undoCount <= 0) return;

            int peekIdx = (_headIndex - 1 + MaxHistoryCapacity) % MaxHistoryCapacity;
            int targetBatch = _ringBuffer[peekIdx].BatchGroupId;

            do
            {
                _headIndex = (_headIndex - 1 + MaxHistoryCapacity) % MaxHistoryCapacity;
                _undoCount--;
                _redoCount++;

                ExecuteInverse(_ringBuffer[_headIndex]);

                if (_undoCount <= 0) break;
                int prevIdx = (_headIndex - 1 + MaxHistoryCapacity) % MaxHistoryCapacity;
                if (targetBatch == 0 || _ringBuffer[prevIdx].BatchGroupId != targetBatch) break;
            }
            while (true);

            OnHistoryStateChanged?.Invoke(_undoCount, _redoCount);
        }

        public void RedoLastAction()
        {
            if (_redoCount <= 0) return;

            int targetBatch = _ringBuffer[_headIndex].BatchGroupId;

            do
            {
                ExecuteForward(_ringBuffer[_headIndex]);

                _headIndex = (_headIndex + 1) % MaxHistoryCapacity;
                _undoCount++;
                _redoCount--;

                if (_redoCount <= 0) break;
                if (targetBatch == 0 || _ringBuffer[_headIndex].BatchGroupId != targetBatch) break;
            }
            while (true);

            OnHistoryStateChanged?.Invoke(_undoCount, _redoCount);
        }

        private static void ExecuteInverse(IslandCommandRecord rec)
        {
            if (rec.TargetObject == null) return;

            switch (rec.ActionType)
            {
                case CommandActionType.SpawnObject:
                case CommandActionType.ScatterBatch:
                    // Soft-deactivate so Redo restores the exact instance with 0 Instantiate() cost
                    rec.TargetObject.SetActive(false);
                    break;

                case CommandActionType.DeleteObject:
                    rec.TargetObject.SetActive(true);
                    break;

                case CommandActionType.TransformObject:
                    rec.TargetObject.transform.SetPositionAndRotation(rec.BeforeState.Position, rec.BeforeState.Rotation);
                    rec.TargetObject.transform.localScale = rec.BeforeState.Scale;
                    break;
            }
        }

        private static void ExecuteForward(IslandCommandRecord rec)
        {
            if (rec.TargetObject == null) return;

            switch (rec.ActionType)
            {
                case CommandActionType.SpawnObject:
                case CommandActionType.ScatterBatch:
                    rec.TargetObject.SetActive(true);
                    break;

                case CommandActionType.DeleteObject:
                    rec.TargetObject.SetActive(false);
                    break;

                case CommandActionType.TransformObject:
                    rec.TargetObject.transform.SetPositionAndRotation(rec.AfterState.Position, rec.AfterState.Rotation);
                    rec.TargetObject.transform.localScale = rec.AfterState.Scale;
                    break;
            }
        }
    }
}

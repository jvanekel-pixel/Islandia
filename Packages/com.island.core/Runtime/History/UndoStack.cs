using System;
using System.Collections.Generic;

namespace Island.Core.History
{
    /// <summary>
    /// What has been done and what has been taken back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two lists rather than the ring buffer this replaces. A ring is the obvious shape for
    /// bounded history and it was where three of the four defects in the draft lived: batches
    /// could be split by the write head overtaking them, entries that fell off the end were never
    /// released, and the redo branch was "invalidated" by zeroing a counter while the stale
    /// entries stayed in the buffer and could be walked back into. Lists make eviction explicit,
    /// which is the operation that has to be right.
    /// </para>
    /// <para>
    /// History is bounded by <em>memory</em>, not by a number of steps. A terrain stroke carries a
    /// compressed region delta that can be tens of kilobytes while a move carries two transforms;
    /// counting steps would either hold a hundred megabytes of terrain or throw away a hundred
    /// cheap moves. Both bounds exist, and either one trips eviction.
    /// </para>
    /// <para>
    /// Eviction removes whole batches. Half a batch is worse than none of it: undoing it would
    /// leave the world in a state it was never in.
    /// </para>
    /// </remarks>
    public sealed class UndoStack
    {
        /// <summary>
        /// How many steps history keeps. The spec says 128; the draft's own constant said 256 and
        /// its comment said 128, which is the kind of disagreement that decides nothing until it
        /// decides the wrong thing.
        /// </summary>
        public const int DefaultMaxCommands = 128;

        /// <summary>
        /// How much memory history may hold, in bytes. Sits inside the 96 MB ADR-10 gives pets,
        /// media and UI together, which is where undo's share comes from.
        /// </summary>
        public const long DefaultMaxBytes = 24L * 1024L * 1024L;

        private readonly List<Entry> _done = new List<Entry>();
        private readonly List<Entry> _undone = new List<Entry>();

        private int _batchDepth;
        private int _batchId;
        private int _nextBatchId = 1;

        public UndoStack() : this(DefaultMaxCommands, DefaultMaxBytes) { }

        public UndoStack(int maxCommands, long maxBytes)
        {
            if (maxCommands < 1)
                throw new ArgumentOutOfRangeException(nameof(maxCommands), "History must hold at least one step.");
            if (maxBytes < 1)
                throw new ArgumentOutOfRangeException(nameof(maxBytes), "History must be allowed some memory.");

            MaxCommands = maxCommands;
            MaxBytes = maxBytes;
        }

        public int MaxCommands { get; }
        public long MaxBytes { get; }

        /// <summary>How many steps can be taken back. A batch counts as the steps it holds.</summary>
        public int UndoDepth => _done.Count;

        public int RedoDepth => _undone.Count;

        /// <summary>What history is holding, by the commands' own accounting.</summary>
        public long BytesHeld { get; private set; }

        /// <summary>Raised after anything changes, so a wrist gauge or history list can refresh.</summary>
        public event Action Changed;

        /// <summary>The label of the step <see cref="Undo"/> would take back, or null.</summary>
        public string NextUndoLabel => _done.Count > 0 ? _done[_done.Count - 1].Command.Label : null;

        public string NextRedoLabel => _undone.Count > 0 ? _undone[_undone.Count - 1].Command.Label : null;

        // ---- batching -------------------------------------------------------------------------

        /// <summary>
        /// Everything recorded until the matching <see cref="EndBatch"/> undoes as one step.
        /// </summary>
        /// <remarks>
        /// Nests. Only the outermost pair opens and closes a batch, so a tool that batches
        /// internally can be called from another tool that batches without producing two steps
        /// where the person performed one action.
        /// </remarks>
        public void BeginBatch()
        {
            if (_batchDepth == 0) _batchId = _nextBatchId++;
            _batchDepth++;
        }

        public void EndBatch()
        {
            if (_batchDepth == 0)
                throw new InvalidOperationException("EndBatch without a matching BeginBatch.");
            _batchDepth--;
            if (_batchDepth == 0) _batchId = 0;
        }

        /// <summary>Whether a batch is open. For assertions in tools that must not leave one open.</summary>
        public bool InBatch => _batchDepth > 0;

        // ---- recording ------------------------------------------------------------------------

        /// <summary>
        /// A step has happened. The command is not executed — the caller has already done the
        /// work; this is the record of how to take it back.
        /// </summary>
        public void Record(IUndoableCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            // Doing something new means the future that was taken back will never happen. The
            // commands holding it have to be released here, not merely forgotten.
            DropRedoBranch();

            _done.Add(new Entry(command, _batchId));
            BytesHeld += command.Bytes;

            Evict();
            Changed?.Invoke();
        }

        // ---- undo and redo --------------------------------------------------------------------

        /// <summary>Takes back the last step, or the last batch. False when there is nothing to take back.</summary>
        public bool Undo()
        {
            if (_batchDepth > 0)
                throw new InvalidOperationException("Cannot undo while a batch is open.");
            if (_done.Count == 0) return false;

            var batch = TrailingRun(_done);
            for (var i = 0; i < batch; i++)
            {
                var entry = _done[_done.Count - 1];
                _done.RemoveAt(_done.Count - 1);
                entry.Command.Undo();
                _undone.Add(entry);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Does the last taken-back step again, or the last batch.</summary>
        public bool Redo()
        {
            if (_batchDepth > 0)
                throw new InvalidOperationException("Cannot redo while a batch is open.");
            if (_undone.Count == 0) return false;

            var batch = TrailingRun(_undone);
            for (var i = 0; i < batch; i++)
            {
                var entry = _undone[_undone.Count - 1];
                _undone.RemoveAt(_undone.Count - 1);
                entry.Command.Redo();
                _done.Add(entry);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// How many entries at the end of a list belong to the same step.
        /// </summary>
        /// <remarks>
        /// One, unless the last entry is part of a batch, in which case every consecutive entry
        /// sharing its id. Batch id 0 means "on its own", so a run of unbatched steps is never
        /// mistaken for one batch.
        /// </remarks>
        private static int TrailingRun(List<Entry> entries)
        {
            var last = entries[entries.Count - 1];
            if (last.BatchId == 0) return 1;

            var run = 0;
            for (var i = entries.Count - 1; i >= 0 && entries[i].BatchId == last.BatchId; i--) run++;
            return run;
        }

        // ---- forgetting -----------------------------------------------------------------------

        /// <summary>
        /// An object has been destroyed for good. Drop every step that mentions it, because
        /// undoing one would reach for something that is no longer there.
        /// </summary>
        /// <remarks>
        /// Drops the whole batch a mentioning step belongs to, for the same reason eviction does:
        /// undoing the rest of it would produce a state the world was never in.
        /// </remarks>
        public void Forget(long instanceId)
        {
            if (instanceId == Save.InstanceIds.None) return;

            var before = _done.Count + _undone.Count;
            ForgetIn(_done, instanceId);
            ForgetIn(_undone, instanceId);
            if (_done.Count + _undone.Count != before) Changed?.Invoke();
        }

        private void ForgetIn(List<Entry> entries, long instanceId)
        {
            var doomed = new HashSet<int>();
            var loose = new List<int>();

            for (var i = 0; i < entries.Count; i++)
            {
                if (!Mentions(entries[i].Command, instanceId)) continue;
                if (entries[i].BatchId == 0) loose.Add(i);
                else doomed.Add(entries[i].BatchId);
            }

            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (!loose.Contains(i) && !doomed.Contains(entries[i].BatchId)) continue;
                Release(entries[i]);
                entries.RemoveAt(i);
            }
        }

        private static bool Mentions(IUndoableCommand command, long instanceId)
        {
            var subjects = command.Subjects;
            if (subjects == null) return false;
            for (var i = 0; i < subjects.Length; i++) if (subjects[i] == instanceId) return true;
            return false;
        }

        /// <summary>Whether any step in history mentions this object.</summary>
        public bool Touches(long instanceId)
        {
            if (instanceId == Save.InstanceIds.None) return false;
            foreach (var entry in _done) if (Mentions(entry.Command, instanceId)) return true;
            foreach (var entry in _undone) if (Mentions(entry.Command, instanceId)) return true;
            return false;
        }

        // ---- clearing and eviction ------------------------------------------------------------

        /// <summary>Throws all history away, releasing everything it holds.</summary>
        public void Clear()
        {
            if (_done.Count == 0 && _undone.Count == 0) return;

            foreach (var entry in _done) Release(entry);
            foreach (var entry in _undone) Release(entry);
            _done.Clear();
            _undone.Clear();
            BytesHeld = 0;
            Changed?.Invoke();
        }

        private void DropRedoBranch()
        {
            if (_undone.Count == 0) return;
            foreach (var entry in _undone) Release(entry);
            _undone.Clear();
        }

        /// <summary>
        /// Trims the oldest history until both bounds are met.
        /// </summary>
        /// <remarks>
        /// The newest batch is never evicted, even when it alone is over the memory bound. Being
        /// unable to take back the thing you just did is worse than holding one oversized delta,
        /// and the alternative — refusing to record it — is the same failure wearing a different
        /// coat.
        /// </remarks>
        private void Evict()
        {
            while (_done.Count > 1 && (_done.Count > MaxCommands || BytesHeld > MaxBytes))
            {
                var run = LeadingRun(_done);

                // Never evict the whole of history: that would drop the step just recorded.
                if (run >= _done.Count) break;

                for (var i = 0; i < run; i++)
                {
                    Release(_done[0]);
                    _done.RemoveAt(0);
                }
            }
        }

        /// <summary>How many entries at the front of the list belong to the same step.</summary>
        private static int LeadingRun(List<Entry> entries)
        {
            var first = entries[0];
            if (first.BatchId == 0) return 1;

            var run = 0;
            for (var i = 0; i < entries.Count && entries[i].BatchId == first.BatchId; i++) run++;
            return run;
        }

        private void Release(Entry entry)
        {
            BytesHeld -= entry.Command.Bytes;
            if (BytesHeld < 0) BytesHeld = 0;
            entry.Command.Discard();
        }

        private readonly struct Entry
        {
            public Entry(IUndoableCommand command, int batchId)
            {
                Command = command;
                BatchId = batchId;
            }

            public IUndoableCommand Command { get; }

            /// <summary>Zero means this step stands alone.</summary>
            public int BatchId { get; }
        }
    }
}

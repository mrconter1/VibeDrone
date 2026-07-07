using System.Collections.Generic;

namespace OpenDrone
{
    // Linear undo/redo over full level snapshots (JSON strings). Reset() seeds the current state;
    // Record() commits a new state after each edit (dropping any redo branch); Undo()/Redo() step
    // through. Pure C# so it can be unit-tested. Capped so long sessions don't grow unbounded.
    public sealed class EditHistory
    {
        private const int MaxStates = 200;

        private readonly List<string> _states = new();
        private int _index = -1;

        public bool CanUndo => _index > 0;
        public bool CanRedo => _index >= 0 && _index < _states.Count - 1;
        public int Count => _states.Count;

        public void Reset(string state)
        {
            _states.Clear();
            _states.Add(state);
            _index = 0;
        }

        // Commit a new current state. Truncates the redo branch and appends.
        public void Record(string state)
        {
            if (_index < 0) { Reset(state); return; }
            if (state == _states[_index]) return;   // no-op edit: don't clutter the history

            if (_index < _states.Count - 1) _states.RemoveRange(_index + 1, _states.Count - _index - 1);
            _states.Add(state);
            _index++;

            if (_states.Count > MaxStates) { _states.RemoveAt(0); _index--; }
        }

        // Step back; returns the state to restore, or null if nothing to undo.
        public string? Undo() => CanUndo ? _states[--_index] : null;

        // Step forward; returns the state to restore, or null if nothing to redo.
        public string? Redo() => CanRedo ? _states[++_index] : null;
    }
}

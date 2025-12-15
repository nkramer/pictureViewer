using System;
using System.Collections.Generic;

namespace Folio.Utilities;
// Manages undo/redo functionality using XML snapshots of the application state.
public class UndoRedoManager {
    private Stack<string> undoStack = new Stack<string>();
    private Stack<string> redoStack = new Stack<string>();
    private Func<string> createSnapshot;
    private Action<string> restoreSnapshot;

    // createSnapshot - Callback to create a snapshot of current state (returns XML string)
    // restoreSnapshot - Callback to restore a snapshot (takes XML string)
    public UndoRedoManager(Func<string> createSnapshot, Action<string> restoreSnapshot) {
        this.createSnapshot = createSnapshot ?? throw new ArgumentNullException(nameof(createSnapshot));
        this.restoreSnapshot = restoreSnapshot ?? throw new ArgumentNullException(nameof(restoreSnapshot));
    }

    public void RecordSnapshot() {
        undoStack.Push(createSnapshot());
        redoStack.Clear();
    }

    public bool CanUndo {
        get { return undoStack.Count > 0; }
    }

    public bool CanRedo {
        get { return redoStack.Count > 0; }
    }

    public void Undo() {
        if (!CanUndo) return;
        redoStack.Push(createSnapshot());
        restoreSnapshot(undoStack.Pop());
    }

    public void Redo() {
        if (!CanRedo) return;
        undoStack.Push(createSnapshot());
        restoreSnapshot(redoStack.Pop());
    }

    // Clears all undo/redo history.
    public void Clear() {
        undoStack.Clear();
        redoStack.Clear();
    }
}

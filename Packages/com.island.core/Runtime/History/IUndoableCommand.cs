namespace Island.Core.History
{
    /// <summary>
    /// One step a person can take back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Commands live in <c>Island.Core</c> and know nothing about Unity. A command that edits
    /// placed objects names them by <see cref="Subjects"/> — the instance ids from
    /// <see cref="Save.InstanceIds"/> — and the Unity layer resolves those to whatever it is
    /// holding. The version this replaces stored <c>GameObject</c> references directly, which
    /// made undo untestable without an editor and left dangling references whenever something
    /// was destroyed behind its back.
    /// </para>
    /// <para>
    /// <see cref="Bytes"/> and <see cref="Discard"/> exist because history is not free. A terrain
    /// stroke's delta can be tens of kilobytes, so the stack bounds itself by memory rather than
    /// by a count of steps, and anything that owns a resource is told when it will never be used
    /// again. The version this replaces hid deleted objects instead of destroying them and never
    /// destroyed the ones that fell off the end of its ring buffer, so a long session leaked
    /// every object it had ever deleted.
    /// </para>
    /// </remarks>
    public interface IUndoableCommand
    {
        /// <summary>What this step was, for the history list. Short, and in the person's terms.</summary>
        string Label { get; }

        /// <summary>
        /// Roughly how much memory this command holds, so the stack can bound itself.
        /// </summary>
        /// <remarks>
        /// An estimate is fine; it decides when history is trimmed, not correctness. It must not
        /// change over the life of the command, or the stack's running total drifts.
        /// </remarks>
        long Bytes { get; }

        /// <summary>
        /// The instance ids this command affects, so history can be asked about an object and so
        /// that destroying one can clear the steps that mention it. Empty for anything with no
        /// instance, such as a terrain stroke.
        /// </summary>
        long[] Subjects { get; }

        /// <summary>Put the world back as it was before this step.</summary>
        void Undo();

        /// <summary>Do this step again.</summary>
        void Redo();

        /// <summary>
        /// This command has left history and will never be undone or redone again. Release
        /// whatever it owns — the object a delete was holding back from destruction, the buffers
        /// a terrain delta allocated.
        /// </summary>
        /// <remarks>
        /// Called exactly once per command, whether it fell off the end of history, was dropped
        /// because a new step replaced the redo branch, was forgotten with its subject, or the
        /// stack was cleared. Must tolerate being called on a command that was never undone.
        /// </remarks>
        void Discard();
    }
}

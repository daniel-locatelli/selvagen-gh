using System.Drawing;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Contract used by <see cref="SelvagenActionAttributes"/> to paint
    /// and dispatch clicks on an in-canvas action button (Upload, Delete, etc.)
    /// without knowing the concrete component type.
    /// </summary>
    public interface ISelvagenActionButton
    {
        /// <summary>Label shown on the idle button (e.g. "Upload", "Delete").</summary>
        string ActionLabel { get; }

        /// <summary>Label shown while the action is running (e.g. "Uploading...", "Deleting...").</summary>
        string ActionLabelRunning { get; }

        /// <summary>True while the action is in-flight; disables further clicks and swaps the label.</summary>
        bool IsRunning { get; }

        /// <summary>Top of the button's vertical gradient.</summary>
        Color ButtonGradientTop { get; }

        /// <summary>Bottom of the button's vertical gradient.</summary>
        Color ButtonGradientBottom { get; }

        /// <summary>Called when the user clicks the button.</summary>
        void RequestAction();
    }
}

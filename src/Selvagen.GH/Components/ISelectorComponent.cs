using System.Collections.Generic;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Non-generic surface that <see cref="SelvagenSelectorAttributes"/> uses
    /// to render the inline dropdown without knowing the concrete item type.
    /// </summary>
    public interface ISelectorComponent
    {
        /// <summary>The text shown inside the dropdown rectangle right now.</summary>
        string CurrentDisplayText { get; }

        /// <summary>True if there is at least one cached item available to pick.</summary>
        bool HasItems { get; }

        /// <summary>Item id+display-name pairs in display order. Empty if none cached.</summary>
        IEnumerable<(string Id, string Name)> GetMenuItems();

        /// <summary>The currently-picked item id, or null/empty if nothing picked.</summary>
        string SelectedId { get; }

        /// <summary>Pick an item by id. No-op when id matches current selection.</summary>
        void SetSelectedId(string id);
    }
}

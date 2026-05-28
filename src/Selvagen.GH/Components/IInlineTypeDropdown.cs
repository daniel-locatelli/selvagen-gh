namespace Selvagen.GH.Components
{
    /// <summary>
    /// Optional contract a component can implement to get a small dropdown
    /// painted above its action button by <see cref="SelvagenActionAttributes"/>.
    /// </summary>
    public interface IInlineTypeDropdown
    {
        /// <summary>The list of selectable string options (kept short — fits in a context menu).</summary>
        string[] DropdownOptions { get; }

        /// <summary>Current selection. Setter is invoked when the user picks a new option.</summary>
        string DropdownSelected { get; set; }
    }
}

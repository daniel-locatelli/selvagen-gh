namespace Selvagen.GH.Components
{
    public interface IFilterDropdownComponent
    {
        string[] FilterOptions { get; }
        string[] FilterDisplayNames { get; }
        string SelectedFilter { get; set; }
    }
}

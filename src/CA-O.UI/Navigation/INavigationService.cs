namespace CAO.UI.Navigation;

public interface INavigationService
{
    void Navigate(string route);
    bool CanNavigate(string route);
    string? CurrentRoute { get; }
    void Select(string route);
}

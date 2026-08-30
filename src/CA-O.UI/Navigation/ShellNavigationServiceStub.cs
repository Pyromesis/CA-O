namespace CAO.UI.Navigation;

public sealed class ShellNavigationServiceStub : INavigationService
{
    public string? CurrentRoute { get; private set; }
    public bool CanNavigate(string route) => RouteTable.Resolve(route) != null;
    public void Navigate(string route) => CurrentRoute = route;
    public void Select(string route) => CurrentRoute = route;
}

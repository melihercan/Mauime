namespace DemoApp;

/// <summary>
/// A Shell with one page in it.
///
/// The page is added here rather than through a <c>ContentTemplate</c> so it can come from
/// dependency injection: <see cref="MainPage"/> takes its view model as a constructor argument, and
/// a DataTemplate would construct it with Activator instead.
///
/// There is a Shell at all because a bare ContentPage as the window content sends MAUI down its
/// element-based navigation root, which crashed on Android with
/// "No view found for id ... for fragment NavigationRootManager_ElementBasedFragment".
/// </summary>
public partial class AppShell : Shell
{
    public AppShell(MainPage page)
    {
        InitializeComponent();

        Items.Add(new ShellContent { Title = "Me.Toolkit.Maui Demo", Content = page });
    }
}

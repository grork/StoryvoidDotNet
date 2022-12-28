using Microsoft.UI.Xaml.Navigation;

namespace Codevoid.Storyvoid.Pages;

/// <summary>
/// Simple page that is displayed while signing out is in progress.
/// </summary>
public sealed partial class SigningOutPage : Page
{
    public SigningOutPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        this.Spinner.IsActive = true;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        this.Spinner.IsActive = false;
        base.OnNavigatedFrom(e);
    }
}

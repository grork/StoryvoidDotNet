using Codevoid.Storyvoid.Utilities;
using Codevoid.Storyvoid.ViewModels;
using Microsoft.UI.Xaml.Navigation;

namespace Codevoid.Storyvoid.Pages;

[UseSystemBackdrop]
public sealed partial class LoginPage : Page
{
    public LoginPage() => this.InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        this.Control.ViewModel = (Authenticator)e.Parameter;
        base.OnNavigatedTo(e);
    }
}

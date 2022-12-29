using Codevoid.Storyvoid.ViewModels;
using Microsoft.UI.Xaml.Navigation;

namespace Codevoid.Storyvoid.Pages;

/// <summary>
/// Page to list articles, allow folder switching etc. Note that the actual
/// folder contents are rendered by <see cref="ArticleListControl"/>.
/// </summary>
public sealed partial class ArticleListPage : Page
{
    public ArticleList? ViewModel { get; private set; }

    public ArticleListPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        this.ViewModel = (ArticleList)e.Parameter;
        base.OnNavigatedTo(e);
    }
}

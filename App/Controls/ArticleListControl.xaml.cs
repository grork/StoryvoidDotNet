using Codevoid.Storyvoid.ViewModels;

namespace Codevoid.Storyvoid.Controls;

/// <summary>
/// Presents a list of articles in a list, allowing them to be interacted with
/// </summary>
public sealed partial class ArticleListControl : UserControl
{
    public ArticleList? ViewModel { get; set; }

    public ArticleListControl()
    {
        this.InitializeComponent();
    }
}

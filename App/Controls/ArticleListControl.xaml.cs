using Codevoid.Storyvoid.ViewModels;

namespace Codevoid.Storyvoid.Controls;

/// <summary>
/// Presents a list of articles in a list, allowing them to be interacted with
/// </summary>
public sealed partial class ArticleListControl : UserControl
{
    public ArticleList ViewModel { get; private set; }

    public ArticleListControl(ArticleList articleList)
    {
        this.ViewModel = articleList;
        this.InitializeComponent();
    }
}

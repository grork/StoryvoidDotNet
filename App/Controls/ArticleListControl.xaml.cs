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

    /// <summary>
    /// Called whenever an element is about to be presented, even if it has been
    /// previously presented. This leads, potentially, to redundant work.
    /// </summary>
    private void ItemsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        var listItem = args.Element as ArticleListItem;
        if(listItem == null)
        {
            return;
        }

        // Move the commands from the viewmodel into the list item so that the
        // control can leverage the commands in its UI
        listItem.LikeCommand = this.ViewModel?.LikeCommand;
        listItem.UnlikeCommand = this.ViewModel?.UnlikeCommand;
    }
}

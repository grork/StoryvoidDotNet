using Codevoid.Storyvoid.ViewModels;
using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;

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
        
        // Create the current sort menu, and start reacting to changes on the
        // view model.
        this.CreateSortMenuItems(this.ViewModel.Sorts);
        this.ViewModel.PropertyChanged += this.OnPropertyChanged;
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        // Stop listening for changes from the view model, and reset our sorts
        // in case this view is cached.
        this.ViewModel!.PropertyChanged -= this.OnPropertyChanged;
        this.SortsFlyout.Items.Clear();

        base.OnNavigatedFrom(e);
    }

    private void OnPropertyChanged(object? source, PropertyChangedEventArgs args)
    {
        switch(args.PropertyName)
        {
            case nameof(this.ViewModel.CurrentSort):
                this.UpdateSortMenuItemsCheckedState();
                break;
        }
    }

    /// <summary>
    /// Creates the sort menu items. This is because the contents of a
    /// MenuFlyout can't be data bound easily. As the single use case, it seems
    /// like it's best to just apply these to the menu flyout.
    /// </summary>
    /// <param name="sorts">Sorts to create in the menu</param>
    private void CreateSortMenuItems(IReadOnlyCollection<SortOption> sorts)
    {
        var menu = this.SortsFlyout;
        foreach(var sort in sorts)
        {
            var menuItem = new ToggleMenuFlyoutItem()
            {
                Text = sort.Label,
                Tag = sort,
                IsChecked = (sort == this.ViewModel!.CurrentSort)
            };

            menuItem.Click += MenuItem_Click;

            this.SortsFlyout.Items.Add(menuItem);
        }
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as ToggleMenuFlyoutItem)?.Tag as SortOption;
        if(tag == null)
        {
            return;
        }

        // Triggers the sort to change.
        this.ViewModel!.CurrentSort = tag;
    }

    /// <summary>
    /// Because we're using a toggle item to indicate the current sort in the
    /// flyout, we need to update the checked state. Since we're *also*
    /// generaring the menu items (<see cref="CreateSortMenuItems(IReadOnlyCollection{SortOption})"/>
    /// we can't use databinding. It seems simpler to just loop through when the
    /// sort changes and update the IsChecked state
    /// </summary>
    private void UpdateSortMenuItemsCheckedState()
    {
        var currentSort = this.ViewModel!.CurrentSort;
        foreach(var menuItem in this.SortsFlyout.Items)
        {
            var toggleMenuItem = menuItem as ToggleMenuFlyoutItem;
            if(toggleMenuItem == null)
            {
                continue;
            }

            toggleMenuItem.IsChecked = ((SortOption)toggleMenuItem.Tag) == currentSort;
        }
    }
}

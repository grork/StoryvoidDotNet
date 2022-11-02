using Codevoid.Storyvoid.ViewModels;
using Windows.Storage;

namespace Codevoid.Storyvoid.App.Implementations;

/// <summary>
/// Settings for the Article List
/// </summary>
internal class ArticleListSettings : IArticleListSettings
{
    private static readonly string CONTAINER_KEY = "article-list";
    private static readonly string SORT_KEY = "sort";

    private ApplicationDataContainer container;
    private string sortIdentifier = ArticleList.DefaultSortIdentifier;


    internal ArticleListSettings()
    {
        this.container = ApplicationData.Current.LocalSettings.CreateContainer(CONTAINER_KEY, ApplicationDataCreateDisposition.Always);

        // Restore the value if we have a sort already
        if (this.container.Values.TryGetValue(SORT_KEY, out var value))
        {
            this.sortIdentifier = (string)value;
        }
    }

    public string SortIdentifier
    {
        get => sortIdentifier;
        set
        {
            this.sortIdentifier = value;
            this.container.Values[SORT_KEY] = value;
        }
    }
}

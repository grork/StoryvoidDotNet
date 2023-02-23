using System.ComponentModel;

namespace Codevoid.Storyvoid.Controls;

/// <summary>
/// Displays a <see cref="DatabaseArticle"/> in the UI. Supports being 'recycled'
/// by the ItemsRepeater
/// </summary>
public sealed partial class ArticleListItem : UserControl, INotifyPropertyChanged
{
    private DatabaseArticle? _model = null;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// The <see cref="DatabaseArticle"/> to display
    /// </summary>
    public DatabaseArticle? Model
    {
        get => _model;
        set
        {
            this._model = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Model)));
        }
    }

    public ArticleListItem() => this.InitializeComponent();
}
using Microsoft.UI.Xaml.Automation.Peers;
using System.ComponentModel;
using System.Windows.Input;
using Strings = Codevoid.Storyvoid.Resources;

namespace Codevoid.Storyvoid.Controls;

/// <summary>
/// Displays a <see cref="DatabaseArticle"/> in the UI. Supports being 'recycled'
/// by the ItemsRepeater
/// </summary>
public sealed partial class ArticleListItem : UserControl, INotifyPropertyChanged
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ArticleListItemAutomationPeer(this);

    private sealed class ArticleListItemAutomationPeer : FrameworkElementAutomationPeer
    {
        private readonly ArticleListItem owner;

        public ArticleListItemAutomationPeer(ArticleListItem owner) : base(owner) => this.owner = owner;

        protected override int GetPositionInSetCore()
          => ((ItemsRepeater)owner.Parent)?.GetElementIndex(this.owner) + 1 ?? base.GetPositionInSetCore();

        protected override int GetSizeOfSetCore()
          => ((ItemsRepeater)owner.Parent)?.ItemsSourceView?.Count ?? base.GetSizeOfSetCore();

        protected override string GetClassNameCore() => nameof(ArticleListItemAutomationPeer);

        protected override string GetNameCore() => Strings.ArticleListItem_Automation_Name;
    }

    private DatabaseArticle? _model = null;
    private ICommand? _likeCommand = null;
    private ICommand? _unlikeCommand = null;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// The <see cref="DatabaseArticle"/> to display
    /// </summary>
    public DatabaseArticle? Model
    {
        get => this._model;
        set
        {
            this._model = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Model)));
        }
    }

    /// <summary>
    /// Command that will like the article this control represents.
    /// </summary>
    public ICommand? LikeCommand
    {
        get => this._likeCommand;
        set
        {
            this._likeCommand = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LikeCommand)));
        }
    }

    /// <summary>
    /// Command that will *un*like the article this control represents.
    /// </summary>
    public ICommand? UnlikeCommand
    {
        get => this._unlikeCommand;
        set
        {
            this._unlikeCommand = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnlikeCommand)));
        }
    }

    public ArticleListItem() => this.InitializeComponent();
}
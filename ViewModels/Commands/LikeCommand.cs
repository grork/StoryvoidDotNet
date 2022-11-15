namespace Codevoid.Storyvoid.ViewModels.Commands;

/// <summary>
/// Like an article
/// </summary>
internal class LikeCommand : ArticleCommand
{
    internal LikeCommand(IArticleDatabase database) : base(database)
    { }

    /// <inheritdoc/>
    protected override bool CoreCanExecute(DatabaseArticle article)
    {
        return base.CoreCanExecute(article) && !article.Liked;
    }

    /// <inheritdoc/>
    protected override void CoreExecute(DatabaseArticle article)
    {
        this.database.LikeArticle(article.Id);
    }
}

/// <summary>
/// Unlike an article
/// </summary>
internal class UnlikeCommand : ArticleCommand
{
    internal UnlikeCommand(IArticleDatabase database) : base(database)
    { }

    /// <inheritdoc/>
    protected override bool CoreCanExecute(DatabaseArticle article)
    {
        return base.CoreCanExecute(article) && article.Liked;
    }

    /// <inheritdoc/>
    protected override void CoreExecute(DatabaseArticle article)
    {
        this.database.UnlikeArticle(article.Id);
    }
}

using System.Windows.Input;

namespace Codevoid.Storyvoid.ViewModels.Commands;

/// <summary>
/// Base class to simplify implementing commands that work against articles.
/// Handles <see ref="CanExecute" /> behaviour for enablement when a valid
/// article has been supplied.
/// 
/// Additionally only called <see ref="CoreExecute" /> if the command can be
/// executed.
/// </summary>
internal abstract class ArticleCommand : ICommand
{
    protected IArticleDatabase database;

    /// <summary>
    /// Required constructor to be called by derived classes to provide a
    /// database instance to work with
    /// </summary>
    /// <param name="database">Database instance to use for commands</param>
    protected ArticleCommand(IArticleDatabase database) => this.database = database;

    /// <summary>
    /// Raises the <see ref="CanExecuteChanged" /> event to allow consumers to
    /// recompute their state based on <see ref="CanExecute" />
    /// </summary>
    public void RaiseCanExecuteChanged() => this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    #region ICommand Implementation
    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter)
    {
        var article = parameter as DatabaseArticle;
        if(article is null)
        {
            return false;
        }

        return this.CoreCanExecute(article);
    }

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        var article = parameter as DatabaseArticle;
        if(article is null)
        {
            return;
        }

        if(!this.CoreCanExecute(article))
        {
            return;
        }

        this.CoreExecute(article);
    }
    #endregion

    /// <summary>
    /// Derived classes can override this to provide custom logic on deciding
    /// if they should be enabled or not
    /// </summary>
    /// <param name="articleId">Article being checked for execution</param>
    /// <returns>True if can be executed, false otherwise</returns>
    protected virtual bool CoreCanExecute(DatabaseArticle article)
    {
        return true;
    }

    /// <summary>
    /// Required to be overridden. Actually performs the work of the command.
    /// Only called if <see ref="CanExecute" /> is true.
    /// </summary>
    /// <param name="articleId">Article to operate on</param>
    protected abstract void CoreExecute(DatabaseArticle article);
}
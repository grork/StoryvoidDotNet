namespace Codevoid.Storyvoid;

public sealed class ArticleNotFoundException : Exception
{
    public readonly long ArticleId;
    public ArticleNotFoundException(long id) : base($"Article {id} not found")
    {
        this.ArticleId = id;
    }
}

public sealed class FolderNotFoundException : Exception
{
    public readonly long LocalFolderId;
    public FolderNotFoundException(long localFolderId) : base($"Folder {localFolderId} not found")
    {
        this.LocalFolderId = localFolderId;
    }
}

public sealed class LocalOnlyStateExistsException : Exception
{
    public readonly long ArticleId;
    public LocalOnlyStateExistsException(long articleId)
        : base($"Local Only State already present for {articleId}")
    {
        this.ArticleId = articleId;
    }
}

public sealed class LocalOnlyStateNotFoundException : Exception
{
    public readonly long ArticleId;
    public LocalOnlyStateNotFoundException(long articleId)
        : base($"Local Only State was not present for {articleId}")
    {
        this.ArticleId = articleId;
    }
}

public sealed class DuplicatePendingFolderDeleteException : Exception
{
    public readonly long ServiceId;
    public DuplicatePendingFolderDeleteException(long serviceId)
        : base($"A pending folder delete was already present for {serviceId}")
    {
        this.ServiceId = serviceId;
    }
}

public sealed class DuplicatePendingFolderAddException : Exception
{
    public readonly long LocalFolderId;
    public DuplicatePendingFolderAddException(long localFolderId)
        : base($"A pending folder add was already present for {localFolderId}")
    {
        this.LocalFolderId = localFolderId;
    }
}

public sealed class DuplicatePendingArticleAddException : Exception
{
    public readonly Uri ArticleUrl;
    public DuplicatePendingArticleAddException(Uri articleUrl)
        : base($"A pending article add was already present for {articleUrl}")
    {
        this.ArticleUrl = articleUrl;
    }
}

public sealed class DuplicatePendingArticleDeleteException : Exception
{
    public readonly long ArticleId;
    public DuplicatePendingArticleDeleteException(long articleId)
        : base($"A pending article delete was already present for {articleId}")
    {
        this.ArticleId = articleId;
    }
}

public sealed class DuplicatePendingArticleStateChangeException : Exception
{
    public readonly long ArticleId;
    public DuplicatePendingArticleStateChangeException(long articleId)
        : base($"A pending article state change was already present for {articleId}")
    {
        this.ArticleId = articleId;
    }
}

public sealed class DuplicatePendingArticleMoveException : Exception
{
    public readonly long ArticleId;
    public DuplicatePendingArticleMoveException(long articleId)
        : base($"A pending article move was already present for {articleId}")
    {
        this.ArticleId = articleId;
    }
}

public sealed class FolderHasPendingArticleMoveException : Exception
{
    public readonly long LocalFolderId;
    public FolderHasPendingArticleMoveException(long localFolderId)
        : base($"Folder {localFolderId} has a pending article move")
    {
        this.LocalFolderId = localFolderId;
    }
}
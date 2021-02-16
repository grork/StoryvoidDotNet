using System;

namespace Codevoid.Storyvoid
{
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
}
using System;

namespace Codevoid.Storyvoid
{
    public sealed class BookmarkNotFoundException : Exception
    {
        public readonly long BookmarkId;
        public BookmarkNotFoundException(long id) : base($"Bookmark {id} not found")
        {
            this.BookmarkId = id;
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
}
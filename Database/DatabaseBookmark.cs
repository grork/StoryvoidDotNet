using System;
using System.Data;

namespace Codevoid.Storyvoid
{
    public class DatabaseBookmark
    {
        private DatabaseBookmark()
        {
            this.Title = String.Empty;
            this.Url = new Uri("unset://unset");
            this.Hash = String.Empty;
        }

        public long Id { get; private set; }
        public Uri Url { get; private set; }
        public string Title { get; private set; }
        public string? Description { get; private set; }
        public float Progress { get; private set; }
        public DateTime ProgressTimestamp { get; private set; }
        public string Hash { get; private set; }
        public bool Liked { get; private set; }

        public static DatabaseBookmark FromRow(IDataReader row)
        {
            return new DatabaseBookmark();
        }
    }
}

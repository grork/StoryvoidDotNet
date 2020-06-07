using System;
using System.Data;

namespace Codevoid.Storyvoid
{
    public class DatabaseFolder
    {
        private DatabaseFolder()
        {
            this.Title = String.Empty;
        }

        public long LocalId { get; private set; }
        public long? ServiceId { get; private set; }
        public string Title { get; private set; }
        public long Position { get; private set; }
        public bool SyncToMobile { get; private set; }
        public bool IsOnService => (this.ServiceId != null);

        internal static DatabaseFolder FromRow(IDataReader row)
        {
            var folder = new DatabaseFolder()
            {
                Title = row.GetString("title"),
                LocalId = row.GetInt64("local_id"),
                Position = row.GetInt64("position"),
            };

            var rawSyncToMobile = row.GetInt64("sync_to_mobile");
            folder.SyncToMobile = (rawSyncToMobile != 0);

            // Service ID might be null so need to check if it's null before
            // requesting it from the row
            if(!row.IsDBNull("service_id"))
            {
                folder.ServiceId = row.GetInt64("service_id");
            }

            return folder;
        }
    }
}

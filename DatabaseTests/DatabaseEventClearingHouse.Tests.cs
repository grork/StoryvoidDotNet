using System.Data;
using System.Diagnostics.CodeAnalysis;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class DatabaseEventClearingHouseTests
{
    private class MockReader : IDataReader
    {
        (string Name, object? Data)[] data;

        internal MockReader((string Name, object? Data)[] data)
        {
            this.data = data;
        }

        private T GetTheData<T>(int index)
        {
            return (T)(this.data[index].Data!);
        }

        public bool GetBoolean(int i)
        {
            return this.GetTheData<bool>(i);
        }

        public DateTime GetDateTime(int i)
        {
            return this.GetTheData<DateTime>(i);
        }
        public float GetFloat(int i)
        {
            return this.GetTheData<float>(i);
        }

        public long GetInt64(int i)
        {
            return this.GetTheData<long>(i);
        }

        public bool IsDBNull(int i)
        {
            return (this.data[i].Data == null);
        }

        public string GetString(int i)
        {
            return this.GetTheData<string>(i);
        }

        public int GetOrdinal(string name)
        {
            for (var index = 0; index < this.data.Length; index += 1)
            {
                if (this.data[index].Name == name)
                {
                    return index;
                }
            }

            return -1;
        }

        public string GetName(int i)
        {
            return this.data[i].Name;
        }

        public int FieldCount
        {
            get
            {
                return data.Length;
            }
        }

        #region Not Needed
        public object this[int i] => throw new NotImplementedException();
        public object this[string name] => throw new NotImplementedException();
        public int Depth => throw new NotImplementedException();
        public bool IsClosed => throw new NotImplementedException();
        public int RecordsAffected => throw new NotImplementedException();
        public void Close() => throw new NotImplementedException();
        public void Dispose() => throw new NotImplementedException();
        public byte GetByte(int i) => throw new NotImplementedException();
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotImplementedException();
        public char GetChar(int i) => throw new NotImplementedException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotImplementedException();
        public IDataReader GetData(int i) => throw new NotImplementedException();
        public string GetDataTypeName(int i) => throw new NotImplementedException();
        public decimal GetDecimal(int i) => throw new NotImplementedException();
        public double GetDouble(int i) => throw new NotImplementedException();
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        public Type GetFieldType(int i) => throw new NotImplementedException();
        public Guid GetGuid(int i) => throw new NotImplementedException();
        public short GetInt16(int i) => throw new NotImplementedException();
        public int GetInt32(int i) => throw new NotImplementedException();
        public DataTable? GetSchemaTable() => throw new NotImplementedException();
        public object GetValue(int i) => throw new NotImplementedException();
        public int GetValues(object[] values) => throw new NotImplementedException();
        public bool NextResult() => throw new NotImplementedException();
        public bool Read() => throw new NotImplementedException();
        #endregion
    }

    private DatabaseEventClearingHouse clearingHouse;
    public DatabaseEventClearingHouseTests()
    {
        this.clearingHouse = new DatabaseEventClearingHouse();
    }

    private DatabaseArticle GetArticle()
    {
        var reader = new MockReader(new (string Name, object? Data)[]
        {
            ("id", 1L),
            ("url", "https://www.codevoid.net"),
            ("title", "Sample"),
            ("read_progress", 0.1F),
            ("read_progress_timestamp", DateTime.Now),
            ("hash", "1234"),
            ("liked", false),
            ("description", null)
        });

        return DatabaseArticle.FromRow(reader);
    }

    private DatabaseFolder GetFolder()
    {
        var reader = new MockReader(new (string, object?)[]
        {
            ("should_sync", 1L),
            ("service_id", 10L),
            ("title", "Sample Folder"),
            ("local_id", 9L),
            ("position", 99L)
        });

        return DatabaseFolder.FromRow(reader);
    }

    [Fact]
    public void CanRaiseFolderAddedAddedEvent()
    {
        var folder = this.GetFolder();
        DatabaseFolder? eventFolder = null;
        this.clearingHouse.FolderAdded += (_, added) => eventFolder = added;
        this.clearingHouse.RaiseFolderAdded(folder);

        Assert.NotNull(eventFolder);
        Assert.Equal(folder, eventFolder);
    }

    [Fact]
    public void CanRaiseFolderDeletedEvent()
    {
        var folder = this.GetFolder();
        DatabaseFolder? eventFolder = null;
        this.clearingHouse.FolderDeleted += (_, deleted) => eventFolder = deleted;
        this.clearingHouse.RaiseFolderDeleted(folder);

        Assert.NotNull(eventFolder);
        Assert.Equal(folder, eventFolder);
    }

    [Fact]
    public void CanRaiseFolderUpdatedEvent()
    {
        var folder = this.GetFolder();
        DatabaseFolder? eventFolder = null;
        this.clearingHouse.FolderUpdated += (_, updated) => eventFolder = updated;
        this.clearingHouse.RaiseFolderUpdated(folder);

        Assert.NotNull(eventFolder);
        Assert.Equal(folder, eventFolder);
    }

    [Fact]
    public void CanRaiseArticleAddedEvent()
    {
        var article = this.GetArticle();
        DatabaseArticle? eventArticle = null;
        this.clearingHouse.ArticleAdded += (_, added) => eventArticle = added;
        this.clearingHouse.RaiseArticleAdded(article);

        Assert.NotNull(eventArticle);
        Assert.Equal(article, eventArticle);
    }

    [Fact]
    public void CanRaiseArticleDeletedEvent()
    {
        var article = this.GetArticle();
        DatabaseArticle? eventArticle = null;
        this.clearingHouse.ArticleDeleted += (_, deleted) => eventArticle = deleted;
        this.clearingHouse.RaiseArticleDeleted(article);

        Assert.NotNull(eventArticle);
        Assert.Equal(article, eventArticle);
    }

    [Fact]
    public void CanRaiseArticleUpdatedEvent()
    {
        var article = this.GetArticle();
        DatabaseArticle? eventArticle = null;
        this.clearingHouse.ArticleUpdated += (_, updated) => eventArticle = updated;
        this.clearingHouse.RaiseArticleUpdated(article);

        Assert.NotNull(eventArticle);
        Assert.Equal(article, eventArticle);
    }

    [Fact]
    public void CanRaiseArticleMovedEvent()
    {
        const long FOLDER_ID = 99L;
        var article = this.GetArticle();
        DatabaseArticle? eventArticle = null;
        long? eventTo = null;

        this.clearingHouse.ArticleMoved += (_, payload) => (eventArticle, eventTo) = payload;
        this.clearingHouse.RaiseArticleMoved(article, FOLDER_ID);

        Assert.NotNull(eventArticle);
        Assert.Equal(article, eventArticle);

        Assert.True(eventTo.HasValue);
        Assert.Equal(FOLDER_ID, eventTo!.Value);
    }
}
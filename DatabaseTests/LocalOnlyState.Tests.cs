﻿using System.Data;
using System.Diagnostics.CodeAnalysis;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public class LocalOnlyStateTests : IDisposable
{
    private IDbConnection connection;
    private IArticleDatabase db;
    private IList<DatabaseArticle> sampleArticles = new List<DatabaseArticle>();

    public LocalOnlyStateTests()
    {
        this.connection = TestUtilities.GetConnection();
        this.ResetArticleDatabaseWrapper();
        this.sampleArticles = this.PopulateDatabaseWithArticles();
    }

    public void Dispose()
    {
        this.connection.Close();
        this.connection.Dispose();
    }

    [MemberNotNull(nameof(db))]
    private void ResetArticleDatabaseWrapper(IDatabaseEventSource? clearingHouse = null)
    {
        this.db = new ArticleDatabase(this.connection, clearingHouse);
    }

    public IList<DatabaseArticle> PopulateDatabaseWithArticles()
    {
        var unreadFolder = WellKnownLocalFolderIds.Unread;
        var article1 = this.db.AddArticleToFolder(TestUtilities.GetRandomArticle(), unreadFolder);
        var article2 = this.db.AddArticleToFolder(TestUtilities.GetRandomArticle(), unreadFolder);
        var article3 = this.db.AddArticleToFolder(TestUtilities.GetRandomArticle(), unreadFolder);

        return new List<DatabaseArticle> { article1, article2, article3 };
    }

    private static DatabaseLocalOnlyArticleState GetSampleLocalOnlyState(long articleId)
    {
        return new DatabaseLocalOnlyArticleState()
        {
            ArticleId = articleId,
            AvailableLocally = true,
            FirstImageLocalPath = new($"localimage://local/{articleId}"),
            FirstImageRemoteUri = new($"remoteimage://remote/{articleId}"),
            LocalPath = new($"localfile://local/{articleId}"),
            ExtractedDescription = string.Empty,
            ArticleUnavailable = true,
            IncludeInMRU = false
        };
    }

    [Fact]
    public void RequestingLocalStateForMissingArticleReturnsNothing()
    {
        var result = this.db.GetLocalOnlyStateByArticleId(this.sampleArticles.First().Id);
        Assert.Null(result);
    }

    [Fact]
    public void CanAddLocalStateForArticle()
    {
        var data = new DatabaseLocalOnlyArticleState()
        {
            ArticleId = this.sampleArticles.First().Id,
        };

        var result = this.db.AddLocalOnlyStateForArticle(data);
        Assert.Equal(data.ArticleId, result.ArticleId);
        Assert.Equal(data, result);
    }

    [Fact]
    public void AddingLocalStateForMissingArticleThrowNotFoundException()
    {
        var data = new DatabaseLocalOnlyArticleState()
        {
            ArticleId = 99
        };

        var ex = Assert.Throws<ArticleNotFoundException>(() => this.db.AddLocalOnlyStateForArticle(data));
        Assert.Equal(data.ArticleId, ex.ArticleId);
    }

    [Fact]
    public void AddingLocalStateWhenAlreadyPresentThrowsDuplicateException()
    {
        var data = new DatabaseLocalOnlyArticleState()
        {
            ArticleId = this.sampleArticles.First().Id,
        };

        _ = this.db.AddLocalOnlyStateForArticle(data);
        var ex = Assert.Throws<LocalOnlyStateExistsException>(() => this.db.AddLocalOnlyStateForArticle(data));
        Assert.Equal(data.ArticleId, ex.ArticleId);
    }

    [Fact]
    public void CanReadLocalStateForArticle()
    {
        var articleId = this.sampleArticles.First().Id;
        var extractedDescription = "SampleExtractedDescription";
        var data = LocalOnlyStateTests.GetSampleLocalOnlyState(articleId) with
        { ExtractedDescription = extractedDescription };

        _ = this.db.AddLocalOnlyStateForArticle(data);
        var result = (this.db.GetLocalOnlyStateByArticleId(articleId))!;
        Assert.Equal(data, result);
    }

    [Fact]
    public void ReadingArticleIncludesLocalStateWhenPresent()
    {
        var articleId = this.sampleArticles.First().Id;
        var extractedDescription = "SampleExtractedDescription";

        var data = LocalOnlyStateTests.GetSampleLocalOnlyState(articleId) with
        { ExtractedDescription = extractedDescription };

        _ = this.db.AddLocalOnlyStateForArticle(data);
        var article = (this.db.GetArticleById(articleId))!;
        Assert.True(article.HasLocalState);
        Assert.Equal(data, article.LocalOnlyState!);
    }

    [Fact]
    public void ListingArticlesWithPartialLocalStateReturnsLocalStateCorrectly()
    {
        var articleId = this.sampleArticles.First().Id;
        var articleData = LocalOnlyStateTests.GetSampleLocalOnlyState(articleId);

        _ = this.db.AddLocalOnlyStateForArticle(articleData);
        var articles = this.db.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);

        var articleWithLocalState = (from a in articles
                                     where a.Id == articleId
                                     select a).Single();
        Assert.True(articleWithLocalState!.HasLocalState);
        Assert.Equal(articleData, articleWithLocalState.LocalOnlyState!);

        var articlesWithoutLocalState = (from a in articles
                                         where (a.Id != articleId) && !a.HasLocalState
                                         select a).Count();
        Assert.Equal(articles.Count() - 1, articlesWithoutLocalState);
    }

    [Fact]
    public void ListingArticlesWhichAllHaveLocalStateCorrectlyReturnsLocalState()
    {
        var addedLocalState = new List<DatabaseLocalOnlyArticleState>();

        foreach (var a in this.sampleArticles)
        {
            var data = LocalOnlyStateTests.GetSampleLocalOnlyState(a.Id);
            _ = this.db.AddLocalOnlyStateForArticle(data);

            addedLocalState.Add(data);
        }

        var articles = this.db.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        var articlesWithLocalState = (from a in articles
                                      where a.HasLocalState
                                      select a);
        Assert.Equal(addedLocalState.Count(), articlesWithLocalState.Count());

        foreach (var localState in addedLocalState)
        {
            var matchingArticle = (from ma in articlesWithLocalState
                                   where ma.Id == localState.ArticleId
                                   select ma).Single();
            Assert.NotNull(matchingArticle);
            Assert.True(matchingArticle.HasLocalState);
            Assert.Equal(localState, matchingArticle.LocalOnlyState);
        }
    }

    [Fact]
    public void ListingArticlesArtclesWithoutLocalOnlyStateOnlyReturnsArticlesWithoutLocalOnlyState()
    {
        var addedLocalState = new List<DatabaseLocalOnlyArticleState>();

        foreach (var a in this.sampleArticles.Take(2))
        {
            var data = LocalOnlyStateTests.GetSampleLocalOnlyState(a.Id);
            _ = this.db.AddLocalOnlyStateForArticle(data);

            addedLocalState.Add(data);
        }

        var articles = this.db.ListArticlesWithoutLocalOnlyState();
        Assert.Equal(this.sampleArticles.Count - 2, articles.Count());

        foreach (var article in articles)
        {
            Assert.False(article.HasLocalState);
        }
    }

    [Fact]
    public void CanRemoveLocalOnlyStateForArticleWhenPresent()
    {
        var state = LocalOnlyStateTests.GetSampleLocalOnlyState(this.sampleArticles.First()!.Id);
        _ = this.db.AddLocalOnlyStateForArticle(state);

        this.db.DeleteLocalOnlyArticleState(state.ArticleId);

        var result = this.db.GetLocalOnlyStateByArticleId(state.ArticleId);
        Assert.Null(result);
    }

    [Fact]
    public void CanRemoveLocalOnlyStateWhenArticleIsPresentButStateIsnt()
    {
        this.db.DeleteLocalOnlyArticleState(this.sampleArticles.First()!.Id);
    }

    [Fact]
    public void WhenRemovingStateForArticleThatIsntPresentNoErrorReturned()
    {
        this.db.DeleteLocalOnlyArticleState(999L);
    }

    [Fact]
    public void CanUpdateSingleFieldLocalOnlyStateWithExistingState()
    {
        var state = LocalOnlyStateTests.GetSampleLocalOnlyState(this.sampleArticles.First()!.Id);
        _ = this.db.AddLocalOnlyStateForArticle(state);

        var newState = state with
        {
            ExtractedDescription = "I have been updated"
        };

        var updated = this.db.UpdateLocalOnlyArticleState(newState);
        var readFromDatabase = this.db.GetLocalOnlyStateByArticleId(newState.ArticleId);

        Assert.NotEqual(state, updated);
        Assert.Equal(newState, updated);
        Assert.Equal(newState, readFromDatabase);
    }

    [Fact]
    public void CanUpdateAllFieldsFieldLocalOnlyStateWithExistingState()
    {
        var state = LocalOnlyStateTests.GetSampleLocalOnlyState(this.sampleArticles.First()!.Id);
        _ = this.db.AddLocalOnlyStateForArticle(state);

        var newState = state with
        {
            AvailableLocally = state.AvailableLocally!,
            FirstImageLocalPath = new Uri("localPathNew", UriKind.Relative),
            FirstImageRemoteUri = new Uri("remotePathNew", UriKind.Relative),
            LocalPath = new Uri("localPathNew", UriKind.Relative),
            ExtractedDescription = "Extracted with care",
            ArticleUnavailable = !state.ArticleUnavailable,
            IncludeInMRU = !state.IncludeInMRU
        };

        var updated = this.db.UpdateLocalOnlyArticleState(newState);
        var readFromDatabase = this.db.GetLocalOnlyStateByArticleId(newState.ArticleId);

        Assert.NotEqual(state, updated);
        Assert.Equal(newState, updated);
        Assert.Equal(newState, readFromDatabase);
    }

    [Fact]
    public void UpdatingLocalOnlyStateWhenNoLocalStatePresentThrowsException()
    {
        var state = LocalOnlyStateTests.GetSampleLocalOnlyState(this.sampleArticles.First()!.Id);
        Assert.Throws<LocalOnlyStateNotFoundException>(() => this.db.UpdateLocalOnlyArticleState(state));
    }

    [Fact]
    public void UpdatingLocalOnlyStateWithInvalidArticleIdThrowException()
    {
        Assert.Throws<ArgumentException>(() => this.db.UpdateLocalOnlyArticleState(new DatabaseLocalOnlyArticleState()));
    }

    [Fact]
    public void DeletingArticleAlsoDeletesLocalOnlyState()
    {
        var state = LocalOnlyStateTests.GetSampleLocalOnlyState(this.sampleArticles.First()!.Id);
        _ = this.db.AddLocalOnlyStateForArticle(state);

        this.db.DeleteArticle(state.ArticleId);

        var result = this.db.GetLocalOnlyStateByArticleId(state.ArticleId);
        Assert.Null(result);
    }

    [Fact]
    public void ListingLikedArticlesReturnsLocalStateIfPresent()
    {
        var articleId = this.sampleArticles.First().Id;
        _ = this.db.AddLocalOnlyStateForArticle(LocalOnlyStateTests.GetSampleLocalOnlyState(articleId));
        _ = this.db.LikeArticle(articleId);

        var likedArticles = this.db.ListLikedArticles();
        Assert.Single(likedArticles);
        var likedArticle = likedArticles.First()!;

        Assert.True(likedArticle.HasLocalState);
        Assert.NotNull(likedArticle.LocalOnlyState);
    }

    [Fact]
    public void ListingAllArticlesInAFolderReturnsLocalStateIfPresent()
    {
        var articleId = this.sampleArticles.First().Id;
        _ = this.db.AddLocalOnlyStateForArticle(LocalOnlyStateTests.GetSampleLocalOnlyState(articleId));

        var articlesInAFolder = this.db.ListAllArticlesInAFolder();
        var articleExpectedToHaveState = articlesInAFolder.Select((a) => a.Article).First((a) => a.Id == articleId)!;

        Assert.True(articleExpectedToHaveState.HasLocalState);
        Assert.NotNull(articleExpectedToHaveState.LocalOnlyState);
    }

    [Fact]
    public void ListingArticlesNotInAFolderReturnsLocalStateIfPresent()
    {
        var articleId = this.sampleArticles.First().Id;
        _ = this.db.AddLocalOnlyStateForArticle(LocalOnlyStateTests.GetSampleLocalOnlyState(articleId));
        this.db.RemoveArticleFromAnyFolder(articleId);

        var articlesInAFolder = this.db.ListArticlesNotInAFolder();
        var articleExpectedToHaveState = articlesInAFolder.First((a) => a.Id == articleId)!;

        Assert.True(articleExpectedToHaveState.HasLocalState);
        Assert.NotNull(articleExpectedToHaveState.LocalOnlyState);
    }

    [Fact]
    public void ArticleUpdatedEventRaisedWhenLocalStateIsAdded()
    {
        var clearingHouse = new DatabaseEventClearingHouse();
        this.ResetArticleDatabaseWrapper(clearingHouse);

        var articleId = this.sampleArticles.First()!.Id;
        DatabaseArticle? article = null;
        clearingHouse.ArticleUpdated += (_, args) => article = args;

        this.db.AddLocalOnlyStateForArticle(LocalOnlyStateTests.GetSampleLocalOnlyState(articleId));

        Assert.NotNull(article);
        Assert.True(article!.HasLocalState);
        Assert.Equal(articleId, article!.Id);
    }

    [Fact]
    public void ArticleUpdatedEventRaisedWhenLocalStateIsUpdated()
    {
        var articleId = this.sampleArticles.First()!.Id;
        var originalLocalState = LocalOnlyStateTests.GetSampleLocalOnlyState(articleId);
        this.db.AddLocalOnlyStateForArticle(originalLocalState);

        var clearingHouse = new DatabaseEventClearingHouse();
        this.ResetArticleDatabaseWrapper(clearingHouse);

        DatabaseArticle? article = null;
        var updatedState = originalLocalState with
        {
            ArticleUnavailable = !originalLocalState.ArticleUnavailable
        };

        clearingHouse.ArticleUpdated += (_, args) => article = args;

        this.db.UpdateLocalOnlyArticleState(updatedState);

        Assert.NotNull(article);
        Assert.True(article!.HasLocalState);
        Assert.Equal(articleId, article!.Id);
        Assert.Equal(updatedState.ArticleUnavailable, article.LocalOnlyState!.ArticleUnavailable);
    }

    [Fact]
    public void ArticleUpdatedEventRaisedWhenLocalStateIsDeleted()
    {
        var articleId = this.sampleArticles.First()!.Id;
        var originalLocalState = LocalOnlyStateTests.GetSampleLocalOnlyState(articleId);
        this.db.AddLocalOnlyStateForArticle(originalLocalState);

        var clearingHouse = new DatabaseEventClearingHouse();
        this.ResetArticleDatabaseWrapper(clearingHouse);

        DatabaseArticle? article = null;

        clearingHouse.ArticleUpdated += (_, args) => article = args;

        this.db.DeleteLocalOnlyArticleState(articleId);

        Assert.NotNull(article);
        Assert.False(article!.HasLocalState);
        Assert.Equal(articleId, article!.Id);
    }
}
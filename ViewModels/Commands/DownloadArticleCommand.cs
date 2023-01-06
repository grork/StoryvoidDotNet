using System;
using Codevoid.Storyvoid.Sync;

namespace Codevoid.Storyvoid.ViewModels.Commands;

internal class DownloadArticleCommand : ArticleCommand
{
    private IArticleDownloader downloader;
    public DownloadArticleCommand(IArticleDownloader downloader, IArticleDatabase database) : base(database)
    {
        this.downloader = downloader;
    }

    protected override async void CoreExecute(DatabaseArticle article)
    {
        try
        {
            await this.downloader.DownloadArticleAsync(article);
        }
        catch (Exception) { }
    }
}
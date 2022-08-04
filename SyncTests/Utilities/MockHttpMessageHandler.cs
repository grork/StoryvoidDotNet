
namespace Codevoid.Test.Storyvoid.Sync;

internal sealed class FileSystemMappedHttpHandler : HttpMessageHandler
{
    private DirectoryInfo root;
    public event EventHandler<Uri>? FileRequested;

    internal FileSystemMappedHttpHandler(DirectoryInfo root)
    { this.root = root; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if(request.RequestUri is null)
        {
            throw new NotSupportedException("Must supply a URL");
        }

        if(!request.RequestUri.IsAbsoluteUri)
        {
            throw new NotSupportedException("Only absolute URIs are supported");
        }

        // Map to a local file path from the relative path. We're just going to
        // assume the *filename* is all we're using.
        var filename = Path.GetFileName(request.RequestUri.AbsolutePath);
        var localPath = Path.Join(this.root.FullName, filename);

        // Map missing local files to 404.
        if(!File.Exists(localPath))
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }

        // Try to extract the extension. Some of our image paths don't have
        // extensions, but the do have a - that indicates the file extension
        var extension = Path.GetExtension(localPath);
        if(extension.Length > 0)
        {
            extension = extension.Substring(1);
        }

        if(String.IsNullOrWhiteSpace(extension))
        {
            var lastHypen = localPath.LastIndexOf('-');
            if(lastHypen == -1)
            {
                throw new InvalidOperationException("File has no extension, nor a - to guess it");
            }

            extension = localPath.Substring(lastHypen + 1);

            if(extension == "svg")
            {
                // If we didn't extract the extension from the file directly,
                // and found it with the hypen, we don't want to respond with a
                // known image format -- some services respond with 'text/plain'
                // for unextensioned SVG since they don't know the file
                extension = "unknown";
            }
        }

        if(this.FileRequested is not null)
        {
            this.FileRequested(this, request.RequestUri);
        }

        if(cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
        }

        HttpResponseMessage fileContentsResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        var fileContents = File.Open(localPath, FileMode.Open, FileAccess.Read);
        HttpContent content = fileContentsResponse.Content = new StreamContent(fileContents);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue($"image/{(extension == "svg" ? "svg+xml" : extension)}");

        return Task.FromResult(fileContentsResponse);
    }
}
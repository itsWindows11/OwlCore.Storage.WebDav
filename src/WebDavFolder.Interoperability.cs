namespace OwlCore.Storage.WebDav;

public partial class WebDavFolder
{
    private async Task<IChildFile> CreateCopyOfInteroperableAsync(
        IFile fileToCopy,
        bool overwrite,
        string newName,
        CreateRenamedCopyOfDelegate fallback,
        CancellationToken cancellationToken)
    {
        var targetPath = WebDavHelpers.CombinePath(Path, newName);

        if (!overwrite && await _webDavClient.GetStorableFromPathAsync(targetPath, cancellationToken) is not null)
            throw new FileAlreadyExistsException("Destination file already exists.");

        try
        {
            using var readStream = await fileToCopy.OpenStreamAsync(FileAccess.Read, cancellationToken);
            var response = await _webDavClient.PutFile(targetPath, readStream, new global::WebDav.PutFileParameters
            {
                CancellationToken = cancellationToken
            });

            if (!response.IsSuccessful)
                return await fallback(this, fileToCopy, overwrite, newName, cancellationToken);

            return await WebDavFile.GetFromWebDavPathAsync(_webDavClient, targetPath, cancellationToken);
        }
        catch
        {
            return await fallback(this, fileToCopy, overwrite, newName, cancellationToken);
        }
    }

    private async Task<IChildFile> MoveFromInteroperableAsync(
        IModifiableFolder sourceFolder,
        IChildFile fileToMove,
        bool overwrite,
        string newName,
        MoveRenamedFromDelegate fallback,
        CancellationToken cancellationToken)
    {
        var copied = await CreateCopyOfInteroperableAsync(fileToMove, overwrite, newName,
            async (folder, file, overwriteValue, copiedName, ct) => await fallback(folder, (IChildFile)file, sourceFolder, overwriteValue, copiedName, ct), cancellationToken);

        await sourceFolder.DeleteAsync(fileToMove, cancellationToken);

        return copied;
    }
}

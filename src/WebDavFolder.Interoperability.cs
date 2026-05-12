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

        if (!overwrite && await _webDavClient.GetStorableFromPathAsync(targetPath, cancellationToken).ConfigureAwait(false) is not null)
            throw new FileAlreadyExistsException("Destination file already exists.");

        try
        {
            using var readStream = await fileToCopy.OpenStreamAsync(FileAccess.Read, cancellationToken).ConfigureAwait(false);
            var response = await _webDavClient.PutFile(targetPath, readStream, new global::WebDav.PutFileParameters
            {
                CancellationToken = cancellationToken
            }).ConfigureAwait(false);

            if (!response.IsSuccessful)
                return await fallback(this, fileToCopy, overwrite, newName, cancellationToken).ConfigureAwait(false);

            return await WebDavFile.GetFromWebDavPathAsync(_webDavClient, targetPath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return await fallback(this, fileToCopy, overwrite, newName, cancellationToken).ConfigureAwait(false);
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
            (folder, file, overwriteValue, copiedName, ct) => fallback(folder, (IChildFile)file, sourceFolder, overwriteValue, copiedName, ct), cancellationToken).ConfigureAwait(false);

        await sourceFolder.DeleteAsync(fileToMove, cancellationToken).ConfigureAwait(false);

        return copied;
    }
}

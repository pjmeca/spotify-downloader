namespace SpotifyDownloader.Services;

public interface IFileOperationCoordinator
{
    Task<TResult> RunWithExclusiveMusicAccess<TResult>(Func<Task<TResult>> operation);
    void RunWithExclusiveMusicAccess(Action operation);
}

public class FileOperationCoordinator : IFileOperationCoordinator
{
    private readonly SemaphoreSlim semaphore = new(1, 1);

    public async Task<TResult> RunWithExclusiveMusicAccess<TResult>(Func<Task<TResult>> operation)
    {
        await semaphore.WaitAsync();
        try
        {
            return await operation();
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void RunWithExclusiveMusicAccess(Action operation)
    {
        semaphore.Wait();
        try
        {
            operation();
        }
        finally
        {
            semaphore.Release();
        }
    }
}

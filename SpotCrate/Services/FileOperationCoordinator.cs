namespace SpotCrate.Services;

public class FileOperationCoordinator
{
    private readonly SemaphoreSlim semaphore = new(1, 1);

    public async Task<TResult> RunWithExclusiveMusicAccess<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<(bool Acquired, TResult? Result)> TryRunWithExclusiveMusicAccess<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            return (false, default);
        }

        try
        {
            return (true, await operation());
        }
        finally
        {
            semaphore.Release();
        }
    }
}

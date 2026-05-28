namespace Personal_Blog_Application.Tests.Helpers
{
    // Disposable temp folder for tests that touch the filesystem (avatar
    // upload). Cleanup is best-effort.
    public sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "blogapp_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ }
            }
        }
    }
}

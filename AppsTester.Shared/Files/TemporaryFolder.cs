using System;
using System.IO;

namespace AppsTester.Shared.Files
{
    public interface ITemporaryFolder : IDisposable
    {
        public string AbsolutePath { get; }
    }

    internal class TemporaryFolder : ITemporaryFolder
    {
        public string AbsolutePath { get; }

        public TemporaryFolder()
        {
            AbsolutePath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(AbsolutePath);
        }

        public override string ToString()
        {
            return AbsolutePath;
        }

        public void Dispose()
        {
            Directory.Delete(AbsolutePath, recursive: true);
        }
    }
}
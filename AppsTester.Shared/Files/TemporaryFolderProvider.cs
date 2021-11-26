namespace AppsTester.Shared.Files
{
    public interface ITemporaryFolderProvider
    {
        ITemporaryFolder Get();
    }
    
    internal class TemporaryFolderProvider : ITemporaryFolderProvider
    {
        public ITemporaryFolder Get()
        {
            return new TemporaryFolder();
        }
    }
}
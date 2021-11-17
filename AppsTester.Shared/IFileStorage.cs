namespace AppsTester.Shared
{
    public interface IFileStorage
    {
        public byte[] GetFileContent(string filename);
    }
}
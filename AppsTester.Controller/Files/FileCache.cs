using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AppsTester.Controller.Files
{
    public class FileCache
    {
        private static string TempDirectory { get; set; }
        private static TimeSpan TTL { get; set; }

        public FileCache(string applicationName, TimeSpan ttl)
        {
            TempDirectory = Path.Combine(Path.GetTempPath(), applicationName, "cache");
            
            if (!Directory.Exists(TempDirectory))
            {
                Directory.CreateDirectory(TempDirectory);
            }

            TTL = ttl;
        }

        public bool IsKeyExists(string key)
        {
            return File.Exists(GetFilePathByKey(key));
        }

        public string ReadString(string key)
        {
            return File.ReadAllText(GetFilePathByKey(key));
        }

        public Task<byte[]> ReadBytesAsync(string key)
        {
            return File.ReadAllBytesAsync(GetFilePathByKey(key));
        }
        
        public FileStream ReadStreamAsync(string key)
        {
            return File.OpenRead(GetFilePathByKey(key));
        }
        
        public async Task WriteAsync(string key, Stream valueStream)
        {
            DeleteOldFiles();

            await using var fileStream = new FileStream(GetFilePathByKey(key), FileMode.Create);
            await valueStream.CopyToAsync(fileStream);
            await fileStream.FlushAsync();
        }
        
        public void Write(string key, string value)
        {
            DeleteOldFiles();
            File.WriteAllText(GetFilePathByKey(key), value);
        }
        
        public void Write(string key, byte[] value)
        {
            DeleteOldFiles();
            File.WriteAllBytes(GetFilePathByKey(key), value);
        }

        private void DeleteOldFiles()
        {
            foreach (var file in Directory.EnumerateFiles(TempDirectory))
            {
                if (DateTime.UtcNow.Subtract(File.GetLastWriteTimeUtc(file)) > TTL)
                {
                    File.Delete(file);
                }
            }
        }

        private string GetFilePathByKey(string key)
        {
            return Path.Combine(TempDirectory, GetMd5StringOfKey(key));
        }

        private string GetMd5StringOfKey(string key)
        {
            var bytes = MD5.Create().ComputeHash(Encoding.Default.GetBytes(key));
            
            var sb = new StringBuilder();
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }
    }
}
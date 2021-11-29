using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace AppsTester.Checker.Android.Apk
{
    internal interface IApkReader
    {
        Task<string> ReadPackageNameAsync(string filename);
    }

    internal class ApkReader : IApkReader
    {
        public async Task<string> ReadPackageNameAsync(string filename)
        {
            if (!File.Exists(filename))
                throw new ApplicationException($"Can't find .apk file in {filename}.");

            using var zipArchive = new ZipArchive(new FileStream(filename, FileMode.Open));

            var androidManifestEntry = zipArchive.Entries.FirstOrDefault(e => e.FullName == "AndroidManifest.xml");
            if (androidManifestEntry == null)
                throw new ApplicationException("Can't find AndroidManifest.xml file in .apk file.");

            await using var androidManifestStream = androidManifestEntry.Open();

            await using var androidManifestMemoryStream = new MemoryStream();
            await androidManifestStream.CopyToAsync(androidManifestMemoryStream);

            var manifestReader = new AndroidManifestReader(androidManifestMemoryStream.ToArray());
            var packageName = manifestReader
                .Manifest
                .XPathSelectElement("/root/manifest")?
                .Attribute("package")?
                .Value;

            if (string.IsNullOrEmpty(packageName))
                throw new ApplicationException("Can't find package name in manifest file.");

            return packageName;
        }
    }
}
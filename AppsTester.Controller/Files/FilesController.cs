using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AppsTester.Controller.Files
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly FileCache _fileCache;
        
        public FilesController(FileCache fileCache)
        {
            _fileCache = fileCache;
        }

        [HttpPost("{key}")]
        public async Task<IActionResult> SaveFileAsync(string key, IFormFile file)
        {
            await _fileCache.WriteAsync(key, file.OpenReadStream());
            return Ok();
        }

        [HttpHead("{key}")]
        public IActionResult CheckFileExistence(string key)
        {
            return StatusCode(_fileCache.IsKeyExists(key) ? (int) HttpStatusCode.Found : (int) HttpStatusCode.NotFound);
        }

        [HttpGet("{key}")]
        public async Task<IActionResult> GetFile(string key)
        {
            if (!_fileCache.IsKeyExists(key))
                return NotFound();
            
            return File(await _fileCache.ReadBytesAsync(key), "application/zip");
        }
    }
}
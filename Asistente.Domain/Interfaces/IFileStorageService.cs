using System.IO;
using System.Threading.Tasks;

namespace Asistente.Domain.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string subDirectory);
    Task<Stream> GetFileAsync(string rutaArchivo);
    Task DeleteFileAsync(string rutaArchivo);
    void ValidateFile(string fileName, Stream stream);
}

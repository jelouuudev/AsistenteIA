using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Options;

namespace Asistente.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly GestorDocumentalConfig _config;

    public FileStorageService(IOptions<GestorDocumentalConfig> config)
    {
        _config = config.Value;
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string subDirectory)
    {
        // Asegurar que la ruta base existe
        var baseDirectory = _config.RutaDocumentos;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DocumentosAI");
        }

        // En Linux, el volumen se monta en /AsistenteIA_Documentos
        if (Environment.OSVersion.Platform != PlatformID.Win32NT && baseDirectory.Contains(':'))
        {
            var afterColon = baseDirectory.Substring(baseDirectory.IndexOf(':') + 1);
            // Limpiar barras iniciales para evitar dobles slashes
            afterColon = afterColon.TrimStart('\\', '/');
            baseDirectory = "/" + afterColon.Replace('\\', '/');
        }

        var targetDir = Path.Combine(baseDirectory, subDirectory);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var filePath = Path.Combine(targetDir, fileName);

        // Guardar archivo
        using var destinationStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        fileStream.Position = 0;
        await fileStream.CopyToAsync(destinationStream);

        return filePath;
    }

    public Task<Stream> GetFileAsync(string rutaArchivo)
    {
        var ruta = NormalizarRuta(rutaArchivo);
        if (!File.Exists(ruta))
        {
            throw new FileNotFoundException("El archivo físico no fue encontrado en el servidor.", rutaArchivo);
        }

        var fileStream = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream>(fileStream);
    }

    public Task DeleteFileAsync(string rutaArchivo)
    {
        var ruta = NormalizarRuta(rutaArchivo);
        if (File.Exists(ruta))
        {
            File.Delete(ruta);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// En contenedores Linux las rutas se guardaron originalmente como Windows
    /// (ej. C:\AsistenteIA_Documentos\doc_1\archivo.txt) pero el volumen se monta en
    /// /AsistenteIA_Documentos. Si la ruta original no existe, se intenta la equivalente Linux.
    /// </summary>
    private static string NormalizarRuta(string ruta)
    {
        if (File.Exists(ruta))
            return ruta;

        // Si estamos en Linux y la ruta es Windows, convertir
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            // Reemplazar backslashes por forward slashes
            var normalized = ruta.Replace('\\', '/');
            
            // Eliminar dobles slashes (excepto al inicio)
            while (normalized.Contains("//"))
                normalized = normalized.Replace("//", "/");
            
            // Si tiene letra de unidad (ej: C:/), convertir a ruta Linux
            if (normalized.Length > 1 && normalized[1] == ':')
            {
                var linux = "/" + normalized.Substring(2);
                if (File.Exists(linux))
                    return linux;
            }
            
            // Si ya es una ruta relativa con forward slashes
            if (File.Exists(normalized))
                return normalized;
        }

        return ruta;
    }

    public void ValidateFile(string fileName, Stream stream)
    {
        // 1. Validar extensión
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!_config.ExtensionesPermitidas.Select(e => e.ToLowerInvariant()).Contains(ext))
        {
            throw new InvalidOperationException($"Extensión no permitida. Las permitidas son: {string.Join(", ", _config.ExtensionesPermitidas)}");
        }

        // 2. Validar tamaño máximo
        var sizeInBytes = stream.Length;
        var maxSizeInBytes = _config.TamanoMaximoMB * 1024L * 1024L;
        if (sizeInBytes > maxSizeInBytes)
        {
            throw new InvalidOperationException($"El archivo excede el tamaño máximo permitido de {_config.TamanoMaximoMB} MB.");
        }

        if (sizeInBytes <= 0)
        {
            throw new InvalidOperationException("El archivo está vacío.");
        }

        // 3. Validar cabecera PDF (Magic Bytes %PDF) para comprobar que no esté corrupto y sea PDF real
        if (ext == ".pdf")
        {
            var initialPosition = stream.Position;
            try
            {
                stream.Position = 0;
                var buffer = new byte[4];
                var read = stream.Read(buffer, 0, 4);
                if (read < 4)
                {
                    throw new InvalidOperationException("El archivo es demasiado pequeño o está corrupto.");
                }

                // %PDF hex signature is 25 50 44 46
                if (buffer[0] != 0x25 || buffer[1] != 0x50 || buffer[2] != 0x44 || buffer[3] != 0x46)
                {
                    throw new InvalidOperationException("El archivo no es un documento PDF válido o está corrupto.");
                }
            }
            finally
            {
                stream.Position = initialPosition;
            }
        }
    }
}

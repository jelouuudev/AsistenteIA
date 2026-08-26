using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services;

public class UsuarioService : IUsuarioService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IRolRepository _rolRepository;
    private readonly IAuditoriaRepository _auditoriaRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;

    public UsuarioService(
        IUsuarioRepository usuarioRepository,
        IRolRepository rolRepository,
        IAuditoriaRepository auditoriaRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService)
    {
        _usuarioRepository = usuarioRepository;
        _rolRepository = rolRepository;
        _auditoriaRepository = auditoriaRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var usuario = await _usuarioRepository.GetByUsernameAsync(request.Usuario);

        if (usuario == null)
        {
            return new LoginResponse
            {
                Exitoso = false,
                Error = "Usuario o contraseña incorrectos."
            };
        }

        // Si el usuario no está activo
        if (!usuario.Activo)
        {
            // Registrar intento fallido para un usuario desactivado
            var sesionFallo = new AuditoriaSesion
            {
                IdUsuario = usuario.IdUsuario,
                FechaInicio = DateTime.UtcNow,
                DireccionIP = request.DireccionIP,
                Navegador = request.Navegador,
                Estado = "Inactivo"
            };
            await _auditoriaRepository.AddSesionAsync(sesionFallo);
            await _unitOfWork.SaveChangesAsync();

            return new LoginResponse
            {
                Exitoso = false,
                Error = "El usuario se encuentra deshabilitado."
            };
        }

        // Validar contraseña
        bool passwordValida = _passwordHasher.VerifyPassword(request.Contrasena, usuario.PasswordHash);

        if (!passwordValida)
        {
            // Registrar intento fallido
            var sesionFallo = new AuditoriaSesion
            {
                IdUsuario = usuario.IdUsuario,
                FechaInicio = DateTime.UtcNow,
                DireccionIP = request.DireccionIP,
                Navegador = request.Navegador,
                Estado = "Fallido"
            };
            await _auditoriaRepository.AddSesionAsync(sesionFallo);
            await _unitOfWork.SaveChangesAsync();

            return new LoginResponse
            {
                Exitoso = false,
                Error = "Usuario o contraseña incorrectos."
            };
        }

        // Crear sesión exitosa
        var sesionExito = new AuditoriaSesion
        {
            IdUsuario = usuario.IdUsuario,
            FechaInicio = DateTime.UtcNow,
            DireccionIP = request.DireccionIP,
            Navegador = request.Navegador,
            Estado = "Exitoso"
        };
        await _auditoriaRepository.AddSesionAsync(sesionExito);

        usuario.FechaUltimoAcceso = DateTime.UtcNow;
        _usuarioRepository.Update(usuario);

        await _unitOfWork.SaveChangesAsync();

        var roles = usuario.UsuarioRoles.Select(ur => ur.Rol?.Nombre ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList();
        var token = _jwtTokenService.GenerarToken(usuario.IdUsuario, usuario.UsuarioNombre, roles);

        return new LoginResponse
        {
            Exitoso = true,
            Usuario = MapToDto(usuario),
            IdSesion = sesionExito.IdSesion,
            Token = token
        };
    }

    public async Task LogoutAsync(int sessionId)
    {
        var sesion = await _auditoriaRepository.GetSesionByIdAsync(sessionId);
        if (sesion != null && sesion.FechaFin == null)
        {
            sesion.FechaFin = DateTime.UtcNow;
            sesion.Estado = "Cerrado";
            _auditoriaRepository.UpdateSesion(sesion);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<UsuarioDto?> ObtenerPorIdAsync(int id)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(id);
        if (usuario == null) return null;
        return MapToDto(usuario);
    }

    public async Task<UsuarioDto?> ObtenerPorNombreAsync(string username)
    {
        var usuario = await _usuarioRepository.GetByUsernameAsync(username);
        if (usuario == null) return null;
        return MapToDto(usuario);
    }

    public async Task<IEnumerable<UsuarioDto>> ObtenerTodosAsync()
    {
        var usuarios = await _usuarioRepository.GetAllAsync();
        return usuarios.Select(MapToDto);
    }

    public async Task<UsuarioDto> CrearUsuarioAsync(CrearUsuarioRequest request, int currentUserId, string ipAddress)
    {
        var existente = await _usuarioRepository.GetByUsernameAsync(request.UsuarioNombre);
        if (existente != null)
        {
            throw new InvalidOperationException($"El nombre de usuario '{request.UsuarioNombre}' ya está en uso.");
        }

        var usuario = new Usuario
        {
            UsuarioNombre = request.UsuarioNombre,
            Nombres = request.Nombres,
            Apellidos = request.Apellidos,
            Correo = request.Correo,
            PasswordHash = _passwordHasher.HashPassword(request.Contrasena),
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        // Asignar Roles
        if (request.Roles != null && request.Roles.Any())
        {
            foreach (var rolNombre in request.Roles)
            {
                var rol = await _rolRepository.GetByNombreAsync(rolNombre);
                if (rol != null)
                {
                    usuario.UsuarioRoles.Add(new UsuarioRol { Rol = rol });
                }
            }
        }

        await _usuarioRepository.AddAsync(usuario);
        await _unitOfWork.SaveChangesAsync();

        // Log auditoría
        await RegistrarActividadInternaAsync(currentUserId, "Usuarios", "Creación", 
            $"Se creó el usuario '{usuario.UsuarioNombre}' con roles: {string.Join(", ", request.Roles ?? new List<string>())}", ipAddress);

        return MapToDto(usuario);
    }

    public async Task<UsuarioDto> ActualizarUsuarioAsync(int id, ActualizarUsuarioRequest request, int currentUserId, string ipAddress)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(id);
        if (usuario == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }

        var oldNombres = usuario.Nombres;
        var oldApellidos = usuario.Apellidos;
        var oldCorreo = usuario.Correo;
        var oldActivo = usuario.Activo;
        var oldRoles = usuario.UsuarioRoles.Select(ur => ur.Rol?.Nombre ?? "").ToList();

        usuario.Nombres = request.Nombres;
        usuario.Apellidos = request.Apellidos;
        usuario.Correo = request.Correo;
        usuario.Activo = request.Activo;

        // Modificar Roles
        usuario.UsuarioRoles.Clear();
        if (request.Roles != null)
        {
            foreach (var rolNombre in request.Roles)
            {
                var rol = await _rolRepository.GetByNombreAsync(rolNombre);
                if (rol != null)
                {
                    usuario.UsuarioRoles.Add(new UsuarioRol { Rol = rol, IdUsuario = usuario.IdUsuario, IdRol = rol.IdRol });
                }
            }
        }

        _usuarioRepository.Update(usuario);
        await _unitOfWork.SaveChangesAsync();

        // Determinar cambios para auditoría
        var cambios = new List<string>();
        if (oldNombres != request.Nombres) cambios.Add("Nombres");
        if (oldApellidos != request.Apellidos) cambios.Add("Apellidos");
        if (oldCorreo != request.Correo) cambios.Add("Correo");
        if (oldActivo != request.Activo) cambios.Add($"Activo a '{request.Activo}'");

        var rolesAgregados = request.Roles?.Except(oldRoles).ToList() ?? new List<string>();
        var rolesRemovidos = oldRoles.Except(request.Roles ?? new List<string>()).ToList();
        if (rolesAgregados.Any()) cambios.Add($"Roles asignados: {string.Join(", ", rolesAgregados)}");
        if (rolesRemovidos.Any()) cambios.Add($"Roles revocados: {string.Join(", ", rolesRemovidos)}");

        string detalleCambios = cambios.Any() ? string.Join(", ", cambios) : "Sin cambios en datos personales o roles";

        await RegistrarActividadInternaAsync(currentUserId, "Usuarios", "Modificación", 
            $"Se modificó al usuario '{usuario.UsuarioNombre}'. Cambios: {detalleCambios}", ipAddress);

        return MapToDto(usuario);
    }

    public async Task DesactivarUsuarioAsync(int id, int currentUserId, string ipAddress)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(id);
        if (usuario == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }

        usuario.Activo = false;
        _usuarioRepository.Update(usuario);
        await _unitOfWork.SaveChangesAsync();

        await RegistrarActividadInternaAsync(currentUserId, "Usuarios", "Desactivación", 
            $"Se desactivó al usuario '{usuario.UsuarioNombre}'", ipAddress);
    }

    public async Task CambiarPasswordAsync(int id, CambiarPasswordRequest request, int currentUserId, string ipAddress)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(id);
        if (usuario == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }

        usuario.PasswordHash = _passwordHasher.HashPassword(request.NuevaContrasena);
        _usuarioRepository.Update(usuario);
        await _unitOfWork.SaveChangesAsync();

        await RegistrarActividadInternaAsync(currentUserId, "Usuarios", "Cambio de contraseña", 
            $"Se cambió la contraseña del usuario '{usuario.UsuarioNombre}'", ipAddress);
    }

    private async Task RegistrarActividadInternaAsync(int userId, string modulo, string accion, string descripcion, string ipAddress)
    {
        var actividad = new AuditoriaActividad
        {
            IdUsuario = userId,
            FechaHora = DateTime.UtcNow,
            Modulo = modulo,
            Accion = accion,
            Descripcion = descripcion,
            DireccionIP = ipAddress
        };
        await _auditoriaRepository.AddActividadAsync(actividad);
        await _unitOfWork.SaveChangesAsync();
    }

    private static UsuarioDto MapToDto(Usuario usuario)
    {
        return new UsuarioDto
        {
            IdUsuario = usuario.IdUsuario,
            UsuarioNombre = usuario.UsuarioNombre,
            Nombres = usuario.Nombres,
            Apellidos = usuario.Apellidos,
            Correo = usuario.Correo,
            Activo = usuario.Activo,
            FechaCreacion = usuario.FechaCreacion,
            FechaUltimoAcceso = usuario.FechaUltimoAcceso,
            Roles = usuario.UsuarioRoles.Select(ur => ur.Rol?.Nombre ?? string.Empty).Where(n => !string.IsNullOrEmpty(n)).ToList()
        };
    }
}

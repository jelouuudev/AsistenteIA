# Documento de Seguridad - Asistente Inteligente Empresarial

## 1. Resumen Ejecutivo

Este documento describe las medidas de seguridad implementadas en el Asistente Inteligente Empresarial durante la ETAPA 3, incluyendo autenticación, autorización, auditoría y protección de datos.

## 2. Autenticación

### 2.1 Mecanismo de Autenticación

El sistema utiliza **autenticación basada en cookies** de ASP.NET Core:

- **Esquema**: Cookie Authentication
- **Duración de sesión**: 2 horas con sliding expiration
- **Ruta de login**: `/Account/Login`
- **Ruta de acceso denegado**: `/Account/AccessDenied`

### 2.2 Hash de Contraseñas

Las contraseñas nunca se almacenan en texto plano. Se utiliza el algoritmo **PBKDF2** (Password-Based Key Derivation Function 2):

#### Especificaciones técnicas

- **Algoritmo**: PBKDF2 con HMAC-SHA256
- **Iteraciones**: 100,000
- **Salt**: 128 bits (generado aleatoriamente por cada contraseña)
- **Longitud del hash**: 256 bits (32 bytes)
- **Formato de almacenamiento**: `Base64(Salt):Base64(Hash)`

#### Implementación

```csharp
public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        // Generate 128-bit salt
        byte[] salt = new byte[128 / 8];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Derive 256-bit subkey using PBKDF2
        byte[] hashedBytes = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: 100000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 256 / 8);

        // Store salt and hash together
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hashedBytes)}";
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] hash = Convert.FromBase64String(parts[1]);

        byte[] testHash = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: 100000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 256 / 8);

        return CryptographicOperations.FixedTimeEquals(hash, testHash);
    }
}
```

#### Seguridad del hash

- **Salt único por contraseña**: Previene ataques de rainbow table
- **100,000 iteraciones**: Aumenta el costo computacional de ataques de fuerza bruta
- **FixedTimeEquals**: Previene ataques de timing side-channel
- **SHA256**: Algoritmo criptográfico seguro y ampliamente aceptado

### 2.3 Requisitos de Contraseña

Las contraseñas deben cumplir los siguientes requisitos:

- Mínimo 8 caracteres
- Al menos una letra mayúscula
- Al menos una letra minúscula
- Al menos un número
- Al menos un carácter especial (!@#$%^&*)

### 2.4 Validación de Credenciales

El proceso de validación incluye:

1. **Verificación de existencia del usuario**
2. **Verificación de estado activo del usuario**
3. **Verificación de contraseña usando PBKDF2**
4. **Registro de intentos fallidos en auditoría**
5. **Creación de sesión exitosa con registro en auditoría**

## 3. Autorización

### 3.1 Roles del Sistema

El sistema implementa tres roles predefinidos:

| Rol | Descripción | Permisos |
|-----|-------------|----------|
| Administrador | Acceso completo al sistema | Chat, Usuarios, Roles, Auditoría |
| Operador | Usuario básico del asistente | Chat |
| Supervisor | Auditoría del sistema | Chat, Auditoría |

### 3.2 Protección de Rutas

Todas las rutas requieren autenticación por defecto:

```csharp
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});
```

**Excepciones** (rutas públicas):
- `/Account/Login` - Página de inicio de sesión
- `/Account/AccessDenied` - Página de acceso denegado

### 3.3 Autorización por Roles

Los controladores y acciones están protegidos con atributos de autorización:

```csharp
[Authorize(Roles = "Administrador")]
public class UsuariosController : Controller
{
    // Solo administradores pueden acceder
}

[Authorize(Roles = "Administrador,Supervisor")]
public class AuditoriaController : Controller
{
    // Administradores y supervisores pueden acceder
}
```

### 3.4 Menú Condicional

El menú de navegación se genera dinámicamente según el rol del usuario:

```csharp
@if (User.IsInRole("Administrador") || User.IsInRole("Operador"))
{
    <li class="nav-item">
        <a class="nav-link-custom" asp-controller="Chat" asp-action="Index">
            <i class="bi bi-chat-dots"></i> Asistente
        </a>
    </li>
}
@if (User.IsInRole("Administrador"))
{
    <li class="nav-item">
        <a class="nav-link-custom" asp-controller="Usuarios" asp-action="Index">
            <i class="bi bi-people"></i> Usuarios
        </a>
    </li>
}
```

## 4. Auditoría

### 4.1 Auditoría de Sesiones

Se registra cada intento de inicio de sesión en la tabla `AuditoriaSesion`:

| Campo | Descripción |
|-------|-------------|
| IdSesion | Identificador único de la sesión |
| IdUsuario | Usuario que intentó iniciar sesión |
| FechaInicio | Fecha y hora del intento |
| FechaFin | Fecha y hora de cierre de sesión (si aplica) |
| DireccionIP | Dirección IP del cliente |
| Navegador | User-Agent del navegador |
| Estado | Exitoso, Fallido, Inactivo, Cerrado |

**Estados de sesión:**
- **Exitoso**: Login exitoso
- **Fallido**: Contraseña incorrecta
- **Inactivo**: Usuario deshabilitado
- **Cerrado**: Sesión cerrada por el usuario

### 4.2 Auditoría de Actividades

Se registran todas las acciones relevantes del sistema en la tabla `AuditoriaActividad`:

| Campo | Descripción |
|-------|-------------|
| IdActividad | Identificador único de la actividad |
| IdUsuario | Usuario que realizó la acción |
| FechaHora | Fecha y hora de la acción |
| Modulo | Módulo afectado (Usuarios, Roles, etc.) |
| Accion | Acción realizada (Creación, Modificación, etc.) |
| Descripcion | Descripción detallada de la acción |
| DireccionIP | Dirección IP del cliente |

**Acciones auditadas:**
- Inicio de sesión
- Cierre de sesión
- Intentos fallidos de login
- Creación de usuarios
- Modificación de usuarios
- Desactivación de usuarios
- Cambio de contraseña
- Asignación de roles
- Creación de roles
- Modificación de roles

### 4.3 Retención de Logs

- **Auditoría en base de datos**: Retención permanente (configurable)
- **Logs de aplicación (Serilog)**: 30 días con rotación diaria

## 5. Protección de Datos

### 5.1 Datos Sensibles

**Contraseñas:**
- Nunca se almacenan en texto plano
- Hash con PBKDF2 + Salt
- No se muestran en logs ni en respuestas de API

**Información de usuario:**
- Correo electrónico almacenado en base de datos
- No se expone en logs de aplicación
- Solo visible para usuarios con permisos

### 5.2 Conexión a Base de Datos

La cadena de conexión utiliza **Windows Authentication** (Trusted_Connection=True):

```
Server=localhost;Database=AsistenteIA;Trusted_Connection=True;TrustServerCertificate=True;
```

**Ventajas:**
- Sin contraseñas en archivos de configuración
- Integración con seguridad de Windows
- Gestión centralizada de credenciales

### 5.3 Configuración de HTTPS

**Recomendación para producción:**
- Habilitar HTTPS en la aplicación
- Configurar certificados SSL/TLS
- Forzar redirección a HTTPS
- Implementar HSTS (HTTP Strict Transport Security)

## 6. Gestión de Usuarios

### 6.1 Creación de Usuarios

Solo los usuarios con rol **Administrador** pueden crear nuevos usuarios.

**Proceso:**
1. Validación de unicidad de nombre de usuario
2. Validación de requisitos de contraseña
3. Hash de contraseña con PBKDF2
4. Asignación de roles
5. Registro en auditoría

### 6.2 Modificación de Usuarios

Solo los usuarios con rol **Administrador** pueden modificar usuarios.

**Proceso:**
1. Verificación de existencia del usuario
2. Modificación de campos permitidos
3. Actualización de roles
4. Registro de cambios en auditoría

### 6.3 Desactivación de Usuarios

**No se permite eliminación física de usuarios.** Los usuarios se desactivan lógicamente:

**Proceso:**
1. Cambio de estado `Activo` a `false`
2. Usuario ya no puede iniciar sesión
3. Datos del usuario se conservan en base de datos
4. Registro en auditoría

### 6.4 Cambio de Contraseña

Solo los usuarios con rol **Administrador** pueden cambiar contraseñas de otros usuarios.

**Proceso:**
1. Validación de requisitos de nueva contraseña
2. Generación de nuevo hash con PBKDF2
3. Actualización en base de datos
4. Registro en auditoría

## 7. Gestión de Roles

### 7.1 Creación de Roles

Solo los usuarios con rol **Administrador** pueden crear roles.

**Proceso:**
1. Validación de unicidad de nombre de rol
2. Creación del rol
3. Registro en auditoría

### 7.2 Modificación de Roles

Solo los usuarios con rol **Administrador** pueden modificar roles.

**Proceso:**
1. Verificación de existencia del rol
2. Modificación de campos permitidos
3. Registro en auditoría

### 7.3 Activación/Desactivación de Roles

Los roles pueden activarse o desactivarse:

**Impacto:**
- Roles desactivados no pueden asignarse a usuarios
- Usuarios con roles desactivados pierden esos permisos
- Registro en auditoría

## 8. Seguridad de la Aplicación

### 8.1 Validación de Entrada

**Validaciones implementadas:**
- FluentValidation para DTOs de entrada
- Validación de longitud de campos
- Validación de formatos (correo electrónico)
- Validación de requisitos de contraseña

### 8.2 Protección contra CSRF

La aplicación utiliza **Anti-Forgery Tokens** en formularios POST:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login(LoginRequest model, string? returnUrl = null)
{
    // ...
}
```

### 8.3 Manejo de Errores

**Middleware de excepciones:**
- Captura excepciones no controladas
- No expone detalles técnicos al usuario final
- Registra errores en Serilog con stack trace
- Devuelve respuestas JSON consistentes

### 8.4 Logging con Serilog

**Eventos registrados:**
- Inicio de aplicación
- Solicitudes HTTP
- Tiempo de respuesta
- Excepciones
- Errores de comunicación con Ollama
- Errores de acceso a SQL Server
- Actividades de seguridad

**Configuración:**
- Console sink para desarrollo
- File sink para producción (rotación diaria)
- Retención de 30 días

## 9. Preparación para Integraciones Futuras

### 9.1 Arquitectura Desacoplada

El sistema está preparado para integrar mecanismos de autenticación corporativa sin modificar la lógica de negocio:

**Interfaces implementadas:**
- `IPasswordHasher`: Permite cambiar el algoritmo de hash
- `IUsuarioService`: Abstracción de lógica de usuarios
- `IAuditoriaService`: Abstracción de auditoría

**Futuras integraciones posibles:**
- Active Directory
- LDAP
- OAuth 2.0
- Azure AD
- SAML 2.0

### 9.2 Proveedores de IA

La interfaz `IAIProvider` permite cambiar el proveedor de IA sin modificar la lógica de negocio:

**Implementación actual:** OllamaProvider
**Futuros proveedores:** OpenAI, Azure OpenAI, Anthropic, etc.

## 10. Recomendaciones de Seguridad

### 10.1 Para Producción

1. **Habilitar HTTPS**
   - Configurar certificado SSL/TLS
   - Forzar redirección a HTTPS
   - Implementar HSTS

2. **Configurar CORS**
   - Limitar orígenes permitidos
   - No usar `AllowAnyOrigin()` en producción

3. **Gestión de contraseñas**
   - Implementar política de expiración de contraseñas
   - Implementar bloqueo después de N intentos fallidos
   - Implementar verificación de contraseñas comprometidas

4. **Auditoría**
   - Configurar alertas para actividades sospechosas
   - Implementar revisión periódica de logs
   - Configurar retención según políticas de la empresa

5. **Base de datos**
   - Usar autenticación SQL Server en lugar de Windows Authentication
   - Encriptar la cadena de conexión
   - Implementar backups regulares
   - Configurar replicación para alta disponibilidad

6. **Aplicación**
   - Configurar firewall a nivel de aplicación (WAF)
   - Implementar rate limiting
   - Configurar headers de seguridad (CSP, X-Frame-Options, etc.)

### 10.2 Para Desarrollo

1. **No usar credenciales de producción**
2. **No commitear archivos de configuración con secrets**
3. **Usar variables de entorno para configuración sensible**
4. **Implementar pre-commit hooks para validar código**

## 11. Cumplimiento de Normativas

### 11.1 GDPR (Reglamento General de Protección de Datos)

**Cumplimiento parcial:**
- ✅ Datos personales almacenados de forma segura
- ✅ Auditoría de acceso a datos
- ✅ Capacidad de desactivación de usuarios (derecho al olvido parcial)
- ⚠️ Falta: Consentimiento explícito de usuarios
- ⚠️ Falta: Política de privacidad
- ⚠️ Falta: Mecanismo de exportación de datos

### 11.2 ISO 27001

**Cumplimiento parcial:**
- ✅ Control de acceso basado en roles
- ✅ Auditoría de actividades
- ✅ Gestión de contraseñas seguras
- ⚠️ Falta: Política de seguridad formal
- ⚠️ Falta: Plan de respuesta a incidentes
- ⚠️ Falta: Formación en seguridad para usuarios

## 12. Conclusiones

El sistema implementa medidas de seguridad robustas para proteger:

- ✅ Autenticación con contraseñas seguras (PBKDF2)
- ✅ Autorización basada en roles
- ✅ Auditoría completa de accesos y actividades
- ✅ Protección de datos sensibles
- ✅ Arquitectura preparada para integraciones futuras

**Estado actual:** La ETAPA 3 establece una base sólida de seguridad que puede extenderse en etapas posteriores para cumplir con normativas más estrictas y requisitos empresariales adicionales.

## 13. Referencias

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [ASP.NET Core Security](https://docs.microsoft.com/en-us/aspnet/core/security/)
- [NIST Digital Identity Guidelines](https://pages.nist.gov/800-63-3/)
- [GDPR Compliance](https://gdpr.eu/)

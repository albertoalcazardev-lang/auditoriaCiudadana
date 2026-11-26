# VyT Systems — Guía de Arquitectura y Desarrollo

## 1. Objetivo de VyT Systems

**VyT Systems** es una plataforma web modular cuyo primer módulo funcional será **Auditoría Ciudadana** (registro, consulta y seguimiento de solicitudes de información pública, administración de instituciones, plazos normativos y tablero de control).

Task 01 conecta los formularios de React con ASP.NET Core Identity y MySQL mediante una sesión por cookie.

---

## 2. Stack Tecnológico Definitivo

| Área | Tecnologías |
| :--- | :--- |
| **Backend** | C#, .NET 10 LTS, ASP.NET Core 10 (Controllers + ProblemDetails + OpenAPI) |
| **ORM y Persistencia** | Entity Framework Core 10 + `MySql.EntityFrameworkCore` (Oracle) sobre **MySQL 8.4 LTS** (`VyTSystems_Dev`) |
| **Validación y Mapeo** | **FluentValidation** (`12.2.0`) · **Mapster** + `Mapster.DependencyInjection` (`10.0.14`) |
| **Ejecución de CQRS** | MediatR `15.0.0` |
| **Frontend** | React 19, TypeScript, Vite, React Router, Tailwind CSS 4, TanStack Query |
| **Arquitectura** | Clean Architecture, Vertical Slice Architecture, CQRS, MVVM con hooks personalizados y SOLID |

---

## 3. Clean Architecture y Estructura Definitiva

```text
VyTSystems/
├── backend/
│   ├── VyTSystems.sln
│   ├── VyTSystems.Domain/
│   │   ├── Entities/                   # Futuras entidades del negocio
│   │   ├── ValueObjects/               # Objetos de valor del negocio
│   │   ├── Common/                     # Entity, ValueObject, DomainException
│   │   └── VyTSystems.Domain.csproj
│   │
│   ├── VyTSystems.Application/
│   │   ├── Abstractions/Persistence/   # IUnitOfWork y contratos de repositorios futuros
│   │   ├── Abstractions/Authentication/ # Verificación de credenciales y acceso solicitado
│   │   ├── Common/
│   │   │   ├── Behaviors/              # ValidationBehavior de MediatR
│   │   │   ├── Validation/             # ValidationError y extensiones para FluentValidation
│   │   │   ├── Mapping/                # MappingConfig (convenciones comunes y escaneo IRegister)
│   │   │   └── Results/                # Result, Result<T> y Error
│   │   ├── Solicitudes/
│   │   │   ├── Crear/
│   │   │   ├── Editar/
│   │   │   ├── Consultar/
│   │   │   ├── Listar/
│   │   │   └── Mapping/                # Perfiles IRegister específicos de Solicitudes
│   │   ├── Instituciones/
│   │   │   └── Mapping/                # Perfiles IRegister específicos de Instituciones
│   │   ├── Auth/Login/                 # Command, Validator, Handler y resultado del login
│   │   ├── Dashboard/                  # Casos de uso futuros de Tablero
│   │   ├── DependencyInjection.cs
│   │   └── VyTSystems.Application.csproj
│   │
│   ├── VyTSystems.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── VyTSystemsDbContext.cs
│   │   │   ├── Configurations/         # Configuraciones Fluent API (IEntityTypeConfiguration<T>)
│   │   │   └── Migrations/             # Migraciones EF Core para MySQL
│   │   ├── Repositories/               # UnitOfWork e implementaciones concretas de repositorios
│   │   ├── Security/                   # Servicios técnicos futuros de seguridad
│   │   ├── Seed/                       # IDataSeeder y DatabaseInitializer (solo Development)
│   │   ├── DependencyInjection.cs
│   │   └── VyTSystems.Infrastructure.csproj
│   │
│   └── VyTSystems.API/
│       ├── Controllers/                # Controladores HTTP delgados
│       ├── Middleware/                 # GlobalExceptionHandler (RFC 7807)
│       ├── Extensions/                 # CorsExtensions y configuración HTTP
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── VyTSystems.API.csproj
│
├── frontend/
│   ├── public/
│   ├── src/
│   │   ├── app/                        # providers/, router/ y App.tsx
│   │   ├── core/                       # api/httpClient.ts y config/env.ts
│   │   ├── features/                   # Módulos MVVM: auth/, solicitudes/, instituciones/, dashboard/
│   │   ├── shared/                     # components/, hooks/ y types/ transversales
│   │   ├── main.tsx
│   │   └── index.css
│   ├── package.json
│   └── vite.config.ts
│
├── .gitignore
├── .editorconfig
├── global.json
└── README.md
```

> Las carpetas de módulos que aún no contienen archivos en Task 00 se irán poblando conforme cada integrante desarrolle su historia de usuario, evitando llenar el repositorio de archivos ficticios o `.gitkeep` innecesarios.

---

## 4. Responsabilidades de cada Capa y Reglas de Dependencias

* **`VyTSystems.Domain`:** contiene exclusivamente entidades, objetos de valor, reglas invariantes y excepciones del negocio. **No depende de ningún otro proyecto ni de EF Core, MySQL o Mapster.**
* **`VyTSystems.Application`:** contiene los casos de uso (*Vertical Slices*), contratos CQRS, validadores con FluentValidation, perfiles de Mapster, resultados tipados (`Result<T>`) e interfaces de persistencia. **Depende únicamente de `Domain`.**
* **`VyTSystems.Infrastructure`:** implementa el acceso a **MySQL** con EF Core (`VyTSystemsDbContext`), configuraciones Fluent API, migraciones, repositorios, seguridad técnica y datos semilla. **Depende de `Application` y `Domain`.**
* **`VyTSystems.API`:** expone Controladores HTTP, middleware de excepciones, CORS y compone la inyección de dependencias. Los `Controllers` no deben incluir SQL ni reglas centrales de negocio. **Depende de `Application` e `Infrastructure`.**

```text
API ──► Application ──► Domain
 │           ▲             ▲
 └───────────┴── Infrastructure
```

---

## 5. Organización Vertical Slices

Siguiendo el estilo modular por acción de negocio, cada historia de usuario agrupa sus piezas directamente bajo `VyTSystems.Application/<Modulo>/<Accion>/`:

```text
VyTSystems.Application/
└── Solicitudes/
    ├── Crear/
    │   ├── CrearSolicitudCommand.cs
    │   ├── CrearSolicitudHandler.cs
    │   └── CrearSolicitudValidator.cs
    ├── Editar/
    │   ├── EditarSolicitudCommand.cs
    │   ├── EditarSolicitudHandler.cs
    │   └── EditarSolicitudValidator.cs
    └── Listar/
        ├── ListarSolicitudesQuery.cs
        ├── ListarSolicitudesDto.cs
        ├── ListarSolicitudesHandler.cs
        └── ListarSolicitudesValidator.cs
```

*(Los ejemplos de código siguientes son exclusivamente educativos dentro de este `README.md` y no existen como archivos reales durante Task 00).*

---

## 6. CQRS con MediatR y FluentValidation

CQRS sigue separando operaciones de escritura y lectura. Desde Task 01, **MediatR sustituye al dispatcher propio**: los Commands y Queries implementan `IRequest<TResponse>` y cada Handler implementa `IRequestHandler<TRequest, TResponse>`. Las clases específicas viven juntas por funcionalidad; no hay carpeta general de CQRS.

Los Controllers envían los casos de uso mediante `ISender.Send(command, cancellationToken)`. `AddApplication()` descubre los Handlers mediante `AddMediatR`, registra los Validators del mismo ensamblado mediante `AddValidatorsFromAssembly` y añade `ValidationBehavior<,>` al pipeline abierto:

1. El Behavior ejecuta secuencialmente `ValidateAsync` para cada `IValidator<TRequest>` y propaga la cancelación.
2. Si hay errores, lanza `FluentValidation.ValidationException` antes del Handler. La API devuelve `400` con `ValidationProblemDetails.errors`: nombres de campos y mensajes, sin valores ingresados.
3. Con entrada válida, MediatR invoca el Handler una sola vez. El Handler no valida de nuevo.

**Login.** `LoginCommand` recibe identificador, contraseña, preferencia de recordar sesión y modo solicitado. Los límites de entrada son 256 caracteres para el identificador y 4096 para la contraseña; no constituyen una política de creación de contraseñas. `LoginHandler` delega la verificación en `IAuthenticationService`. El servicio deberá contrastar la identidad, el estado de la cuenta y sus permisos reales, sin confiar en el modo elegido por el cliente.

`IdentityAuthenticationService` busca con `UserManager` por correo si el identificador contiene `@` y por nombre en caso contrario. `CheckPasswordSignInAsync` respeta los requisitos de confirmación y bloqueo de Identity. Los fallos de contraseña cuentan para el bloqueo en cuentas que lo admitan; se conservan los valores predeterminados de Identity (5 intentos, 5 minutos), sujetos a aprobación. No existe una deshabilitación administrativa independiente en `IdentityUser`.

Mientras no haya flujo de segundo factor, una cuenta con 2FA habilitado no recibe sesión. El adaptador comprueba el rol real con `IsInRoleAsync` y solo devuelve `Authorized` después de que termine `SignInAsync`. Recordar sesión controla la persistencia de la cookie, no los roles. El resultado no garantiza la recepción de la respuesta por el navegador. Application unifica los rechazos por credenciales, cuenta o acceso y distingue los fallos técnicos.

**Integración del host.** Infrastructure registra el adaptador y `SignInManager` por petición. API autentica mediante `Identity.Application` y ejecuta CORS → autenticación → autorización antes de los Controllers. La cookie `__Host-VyTSystems.Auth` es exclusiva del host (sin `Domain`, ruta `/`), `HttpOnly`, `Secure` y `SameSite=Lax`. El ticket tiene una ventana de 8 horas con renovación deslizante. Sin «Recordar sesión» el navegador recibe una cookie de sesión; con la preferencia activa, recibe una fecha de expiración. React no necesita leer ni almacenar su valor.

**Contrato HTTP de login (HTTPS):**

1. `GET /api/auth/csrf` devuelve `200` con `{ "requestToken": "…" }` y establece la cookie antifalsificación `__Host-VyTSystems.Csrf` (`HttpOnly`, `Secure`, `SameSite=Strict`). La respuesta no es almacenable en caché. Conservar el token de petición en memoria y dejar que el navegador administre las cookies.
2. `POST /api/auth/login` recibe JSON con `identifier` (usuario o correo), `password`, `rememberSession` (booleano, por defecto `false`) y `requestedAccess` (`"Administrador"` o `"Consulta"`; también se admiten los valores del enum `1` y `2`). Enviar el token en `X-CSRF-TOKEN` junto con su cookie. Un par ausente o inválido se rechaza antes de MediatR.
3. Éxito: `200` sin cuerpo; Identity emite la cookie de autenticación. Errores: `400` para datos o antifalsificación inválidos, `401` genérico para cualquier rechazo de acceso y `503` para servicio no disponible. Los errores usan Problem Details y las respuestas de login no deben almacenarse en caché.
4. **Después del login, volver a solicitar `GET /api/auth/csrf`** con la cookie de sesión antes de la siguiente operación protegida: el token de petición anterior corresponde a la identidad anónima.

**Consulta y cierre de sesión:**

`AuthController` delega ambas operaciones a `IAuthenticatedSessionService` en `API/Services/Authentication/`. El servicio se resuelve por petición, obtiene el principal desde `IHttpContextAccessor` y cierra la cookie mediante el esquema `Identity.Application`. No consulta la base de datos.

- `GET /api/auth/me` devuelve `200` con `{ "userId": "…", "userName": "…", "roles": ["Consulta"] }`, sin caché. Usa el identificador, nombre y roles del principal autenticado, no el modo solicitado al entrar. Solo enumera `Administrador` y `Consulta`. Sin sesión válida o si faltan el identificador o el nombre, devuelve `401`.
- `POST /api/auth/logout` requiere la cookie de sesión y el encabezado `X-CSRF-TOKEN` con el token obtenido **después del login**, junto con su cookie antifalsificación. Devuelve `204` sin cuerpo e indica al navegador que elimine la cookie de autenticación. Sin sesión devuelve `401`; con sesión pero sin un par CSRF válido, `400`. Tras el cierre se debe descartar el token asociado al usuario y obtener uno nuevo de `GET /api/auth/csrf` como cliente anónimo antes de otro login.
- El cierre elimina la cookie del cliente, no revoca criptográficamente copias previas. Los roles del ticket pueden seguir vigentes hasta que Identity vuelva a validar la identidad y actualice o rechace el ticket, o hasta su expiración. Un cambio de rol en MySQL no se refleja instantáneamente. La API decide el acceso por los roles del principal; `/me` solo los comunica a la interfaz.

**React y operaciones futuras:**

- En desarrollo el navegador usa un único origen HTTPS. Vite conserva `/api` y valida el certificado de Kestrel; no se requieren permisos CORS adicionales. En producción el servidor web debe encaminar `/api` al backend y las rutas de navegación a `index.html`.
- React envía `credentials: 'include'`, renueva el token CSRF en memoria al cambiar de identidad y consulta `/me` al entrar o recargar una ruta de sesión. Las guardas sólo presentan los paneles mínimos autorizados por los roles de la respuesta. Un error de red no se interpreta como cierre de sesión. Proteger las futuras operaciones de escritura también mediante antifalsificación.
- Proteger cada endpoint mediante políticas o `[Authorize(Roles = "...")]` basados en `Administrador` y `Consulta`; no derivar permisos de `RequestedAccess`. En el futuro registro, crear cuentas con `UserManager.CreateAsync` y garantizar `LockoutEnabled = true` para las sujetas al bloqueo (`AllowedForNewUsers` está activo por defecto).
- En despliegue, persistir el anillo de claves de ASP.NET Core Data Protection fuera del repositorio y del sistema de archivos efímero del contenedor. Restringir el acceso al proceso y cifrar las claves en reposo. Las réplicas de un mismo entorno deben compartir el anillo y el mismo nombre de aplicación; separar los entornos y conservar claves anteriores durante la rotación para no invalidar tickets vigentes.

**Licencia.** Para MediatR 15 se utiliza Community. Suministra la clave al proceso backend mediante la variable `VYT_MEDIATR_LICENSE_KEY`, desde la configuración privada del entorno. No la guardes en el repositorio ni la imprimas.

---

## Mapster — Configuración y convenciones de mapeo

> **Nota importante:** los fragmentos de código de esta sección (`Solicitud`, `SolicitudDto`, `SolicitudMappingConfig`) son **ejemplos exclusivamente educativos dentro del `README.md`** y no existen como clases reales dentro de `Application` ni `Domain` durante Task 00.

### 7.1. ¿Qué es Mapster y para qué lo utilizamos?
* **¿Qué es Mapster?** Es una biblioteca ligera y de alto rendimiento para C# que automatiza y estandariza la transformación de datos entre objetos en memoria.
* **¿Para qué lo utilizamos en VyT Systems?** Para convertir **Entidades a DTOs**, **modelos de consulta a DTOs** y **objetos de respuesta de `Application`**, reduciendo conversiones repetitivas cuando la transformación es directa y segura.

### 7.2. Diferencia entre Entity Framework Core y Mapster
* **Entity Framework Core (`Infrastructure`):** es el **ORM** responsable de la persistencia en **MySQL**, traducción de LINQ a SQL, transacciones, repositorios y migraciones.
* **Mapster (`Application`):** únicamente **convierte objetos C# en otros objetos C#** ya cargados o proyectados en memoria, sin acceder a bases de datos, sin validar reglas de negocio y sin sustituir a FluentValidation ni a EF Core.

### 7.3. ¿Qué es una Entidad y qué es un DTO?
* **Entidad (`VyTSystems.Domain/Entities/`):** modelo central del negocio con identidad única (`Id`), estado protegido (`private set`) y métodos que hacen cumplir las reglas del negocio. Nunca se devuelve directamente en respuestas HTTP.
* **DTO — *Data Transfer Object* (`VyTSystems.Application/<Modulo>/<Accion>/`):** objeto inmutable (`record`) sin comportamiento de negocio que transporta únicamente los campos necesarios para responder un caso de uso específico (p. ej. `Solicitudes/Listar/SolicitudResumenDto.cs`).

### 7.4. Cómo se transforma una Entidad en un DTO (Mapeo manual vs. Mapster)

Supongamos el siguiente modelo educativo:

```csharp
// Entidad de dominio (ejemplo educativo)
public sealed class Solicitud : Entity<Guid>
{
    public string Folio { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public EstadoSolicitud Estado { get; private set; }
    public DateOnly FechaRegistro { get; private set; }
    public string NotasInternasReservadas { get; private set; } = string.Empty;
}

// DTO de respuesta cercano al caso de uso (ejemplo educativo)
public sealed record SolicitudDto(
    Guid SolicitudId,
    string Folio,
    string Descripcion,
    string EstadoTexto,
    DateOnly FechaRegistro);
```

* **Con mapeo manual (repetido en cada lugar que lo necesite):**
  ```csharp
  var dto = new SolicitudDto(
      SolicitudId: solicitud.Id,
      Folio: solicitud.Folio,
      Descripcion: solicitud.Descripcion,
      EstadoTexto: solicitud.Estado.ToString(),
      FechaRegistro: solicitud.FechaRegistro);
  ```
* **Con Mapster (regla centralizada una sola vez en el módulo):**
  ```csharp
  // En SolicitudMappingConfig (una sola vez por módulo):
  config.NewConfig<Solicitud, SolicitudDto>()
      .Map(dest => dest.SolicitudId, src => src.Id)
      .Map(dest => dest.EstadoTexto, src => src.Estado.ToString());

  // En cualquier Handler que lo requiera:
  var dto = mapper.Map<SolicitudDto>(solicitud);
  ```
  *(Observa que `NotasInternasReservadas` no existe en `SolicitudDto`, evitando exponer datos internos).*

### 7.5. Cómo funcionan `IMapper`, `TypeAdapterConfig` e `IRegister`
* **`TypeAdapterConfig`:** contenedor central donde Mapster guarda las convenciones comunes y los perfiles compilados; se registra **una sola vez como `Singleton`**.
* **`IRegister`:** interfaz de Mapster (`void Register(TypeAdapterConfig config)`) implementada por cada módulo para registrar sus reglas sin acoplarse a otros módulos.
* **`MapsterMapper.IMapper` (`ServiceMapper`):** servicio oficial de `Mapster.DependencyInjection` registrado como **`Scoped`** para inyectarse en los Handlers (usa siempre `mapper.Map<TDto>(...)` en lugar de la extensión estática `.Adapt<TDto>()`, para garantizar que se utilice la instancia `TypeAdapterConfig` registrada en DI y no `GlobalSettings`). **No se debe crear una interfaz `IMapper` personalizada.**

### 7.6. Organización de carpetas y registro en Dependency Injection
1. **Configuraciones comunes (`Application/Common/Mapping/MappingConfig.cs`):** construye una instancia aislada de `TypeAdapterConfig` y ejecuta `config.Scan(applicationAssembly)` (reservado únicamente para convenciones transversales justificadas; nunca debe convertirse en una clase gigante con mapeos de negocio).
2. **Configuraciones específicas de cada módulo (`Application/<Modulo>/Mapping/`):** cada módulo colocará ahí sus clases `IRegister` cuando existan entidades reales:
   * `Application/Solicitudes/Mapping/SolicitudMappingConfig.cs`
   * `Application/Instituciones/Mapping/InstitucionMappingConfig.cs`
3. **DTOs:** permanecen junto al caso de uso que los utiliza (`Application/Solicitudes/Listar/SolicitudResumenDto.cs`).
4. **Registro mediante Dependency Injection (`Application/DependencyInjection.cs`):** utiliza `TryAddSingleton` para `TypeAdapterConfig` y `TryAddScoped<IMapper, ServiceMapper>()`, garantizando una única instancia de configuración y cero registros duplicados. Al crear una clase `IRegister` dentro de `VyTSystems.Application`, `config.Scan(assembly)` la detecta automáticamente al iniciar.

### 7.7. Cómo utilizar Mapster desde un Handler
```csharp
using MapsterMapper;
using MediatR;

public sealed class ConsultarSolicitudHandler(
    ISolicitudRepository repository,
    IMapper mapper) : IRequestHandler<ConsultarSolicitudQuery, Result<SolicitudDto>>
{
    public async Task<Result<SolicitudDto>> Handle(
        ConsultarSolicitudQuery query,
        CancellationToken cancellationToken = default)
    {
        var solicitud = await repository.GetByIdAsync(query.SolicitudId, cancellationToken);
        if (solicitud is null)
        {
            return Result.Failure<SolicitudDto>(
                Error.NotFound("Solicitud.NoEncontrada", "La solicitud solicitada no existe."));
        }

        var dto = mapper.Map<SolicitudDto>(solicitud);
        return Result.Success(dto);
    }
}
```

### 7.8. Cuándo preferir conversión manual y cómo evitar copiar datos sensibles
* **Cuándo usar conversión manual / métodos del dominio:** en toda **creación o modificación de entidades con reglas de negocio** (`Command` → `Entidad`). **Prohibido usar `mapper.Map<Solicitud>(command)` o `mapper.Map(command, solicitud)`** para modificar estados, permisos o identificadores saltándose constructores o métodos del dominio (`Solicitud.Crear(...)`, `solicitud.CambiarEstado(...)`).
* **Evitar fugas o copias sensibles:** definir DTOs cerrados solo con campos públicos, nunca mapear entradas externas sobre propiedades sensibles (hashes, roles, `Id`, auditoría) y usar `.Ignore(...)` cuando sea necesario bloquear explícitamente un miembro.

### 7.9. Cómo aplicar SOLID al mapeo
* **Single Responsibility:** cada `MappingConfig` de módulo atiende únicamente las conversiones de su módulo.
* **Open/Closed:** añadir `InstitucionMappingConfig : IRegister` no obliga a modificar `MappingConfig.cs` ni `SolicitudMappingConfig`.
* **Liskov Substitution:** `ServiceMapper` cumple fielmente el contrato `MapsterMapper.IMapper`.
* **Interface Segregation:** se inyecta `MapsterMapper.IMapper` sin fabricar interfaces globales gigantes con métodos por cada entidad.
* **Dependency Inversion:** `Domain` permanece libre de Mapster e `Infrastructure` no se filtra hacia `Application`.

---

## 8. Repositorios, EF Core 10 con MySQL y Migraciones

### Implementar repositorios
1. Definir la interfaz en `Application/Abstractions/Persistence/` (sin exponer `DbContext`, `DbSet` ni `IQueryable`).
2. Implementar la clase en `Infrastructure/Repositories/` inyectando `VyTSystemsDbContext`.
3. Registrar la interfaz y su implementación en `Infrastructure/DependencyInjection.cs`.

### Configuración segura de credenciales MySQL (`VyTSystems_Dev`)
**Nunca escribas contraseñas en `appsettings.json` ni las subas a GitHub.** Cada desarrollador configura su conexión local hacia `VyTSystems_Dev` usando **User Secrets** de .NET (con un usuario local dedicado distinto de `root`):

```bash
cd "/home/beto/Proyectos_CV/VyTSystems/backend"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=127.0.0.1;Port=3306;Database=VyTSystems_Dev;Uid=vyt_dev;Pwd=TU_PASSWORD_LOCAL;" \
  --project VyTSystems.API/VyTSystems.API.csproj
```
*(También puede suministrarse mediante la variable de entorno `ConnectionStrings__DefaultConnection`).*

### Crear y aplicar migraciones reales (cuando exista el primer modelo aprobado)
Iniciar la API no aplica migraciones ni ejecuta datos semilla, tampoco en `Development`. Revisa la migración generada y aplícala solo mediante una operación manual autorizada.
```bash
cd "/home/beto/Proyectos_CV/VyTSystems/backend"

dotnet ef migrations add <NombreMigracion> \
  --project VyTSystems.Infrastructure/VyTSystems.Infrastructure.csproj \
  --startup-project VyTSystems.API/VyTSystems.API.csproj \
  --context VyTSystemsDbContext \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project VyTSystems.Infrastructure/VyTSystems.Infrastructure.csproj \
  --startup-project VyTSystems.API/VyTSystems.API.csproj \
  --context VyTSystemsDbContext
```

---

## 9. Frontend: Arquitectura MVVM con Hooks Personalizados

Cada módulo funcional dentro de `frontend/src/features/<modulo>/` separará responsabilidades en:

```text
features/solicitudes/
├── pages/        # View: pantallas asociadas a rutas
├── components/   # View: componentes visuales reutilizables del módulo
├── hooks/        # ViewModel: hooks personalizados con estado de presentación y TanStack Query
├── api/          # Servicios HTTP que invocan httpRequest<T> desde src/core/api/httpClient.ts
└── types/        # Model: interfaces y tipos TypeScript del módulo
```

---

## 10. Principios SOLID Obligatorios

* **S — Single Responsibility:** el Behavior coordina la validación; cada Validator define reglas de entrada y cada Handler coordina un caso de uso.
* **O — Open/Closed:** los nuevos Slices, validadores (`AddValidatorsFromAssembly`) y perfiles Mapster (`IRegister`) se incorporan sin modificar la infraestructura base.
* **L — Liskov Substitution:** toda implementación de repositorio o seeder cumple íntegramente el contrato definido.
* **I — Interface Segregation:** interfaces pequeñas por necesidad; el login solo depende de la operación de autenticación.
* **D — Dependency Inversion:** los casos de uso dependen de interfaces en `Application`, jamás de `VyTSystemsDbContext` ni de `MySql.EntityFrameworkCore`.

---

## 11. Ejecución Local de Backend y Frontend

### Backend (`https://localhost:7080`)
```bash
cd "/home/beto/Proyectos_CV/VyTSystems/backend"
dotnet build VyTSystems.sln
dotnet dev-certs https --trust
dotnet run --project VyTSystems.API --launch-profile https
```

### Frontend (`https://localhost:5173`)

Una vez por certificado, exportar el certificado de desarrollo de ASP.NET Core para Vite **fuera del repositorio**, bajo el usuario que ejecuta ambos servidores. Al renovar el certificado repetir la exportación y reiniciar ambos. En Linux, comprobar que el navegador confía en el certificado de `localhost`; no omitir advertencias TLS.

```bash
(umask 077; mkdir -p ~/.config/vyt-systems/https && \
  dotnet dev-certs https --export-path ~/.config/vyt-systems/https/localhost.pem --format PEM --no-password)
```

```bash
cd "/home/beto/Proyectos_CV/VyTSystems/frontend"
npm install
npm run build
npm run dev
```

Con Vite en ejecución, `npx playwright install chromium --only-shell` y `npm run test:auth` (desde `frontend/`) prueban el navegador con respuestas controladas de la API, sin cuentas de MySQL. Si Chromium en Linux usa un almacén NSS independiente, importar en ese perfil **sólo el certificado público de `localhost` como servidor de confianza**, por ejemplo `certutil -A -d sql:"$HOME/.local/share/pki/nssdb" -n "VyT localhost" -t 'P,,' -i "$HOME/.config/vyt-systems/https/localhost.pem"` (ajustar la ruta al almacén NSS del perfil). No importar la clave ni tratar ese certificado de servidor como una CA.

**Prueba manual con MySQL:** en una ventana privada, abrir `/admin/login` desde `https://localhost:5173` e introducir las credenciales de `vyt-test-admin` en el formulario. Verificar `/admin/dashboard`, recargar, intentar `/consulta/dashboard` (acceso denegado), volver al panel y cerrar sesión. Al volver con «Atrás» no debe mostrarse el panel. Repetir desde `/consulta/login` con `vyt-test-consulta` e invertir los destinos. Probar con y sin «Recordar mi sesión»: en el inspector de almacenamiento comprobar únicamente los atributos `HttpOnly`, `Secure`, `SameSite=Lax`, ruta `/` y expiración (sesión o fecha) de `__Host-VyTSystems.Auth`, sin copiar valores. Con credenciales correctas en el formulario del otro rol se espera `401` sin sesión nueva. No exportar capturas, trazas ni archivos HAR con credenciales o cookies.

---

## 12. Flujo Oficial de Ramas en GitHub

Ramas oficiales del repositorio: **`main`**, **`DEV`**, **`QA`** y **`SP1`**.

* **Flujo obligatorio:** `task/<id>-<descripcion>` → **`SP1`** → **`QA`** → **`DEV`** → **`QA`** → **`main`**
* Crear siempre cada tarea desde `SP1` actualizado, verificar que `dotnet build` y `npm run build` compilen sin errores, comprobar que no existan secretos en el árbol y abrir Pull Request hacia `SP1`.

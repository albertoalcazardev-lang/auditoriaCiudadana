# VyT Systems — Guía de Arquitectura y Desarrollo (Task 00)

## 1. Objetivo de VyT Systems

**VyT Systems** es una plataforma web modular cuyo primer módulo funcional será **Auditoría Ciudadana** (registro, consulta y seguimiento de solicitudes de información pública, administración de instituciones, plazos normativos y tablero de control).

Esta **Task 00** entrega exclusivamente el **esqueleto arquitectónico común** para que todos los integrantes del equipo desarrollen sus historias de usuario bajo una estructura unificada, mantenible y alineada con buenas prácticas empresariales.

---

## 2. Stack Tecnológico Definitivo

| Área | Tecnologías |
| :--- | :--- |
| **Backend** | C#, .NET 10 LTS, ASP.NET Core 10 (Controllers + ProblemDetails + OpenAPI) |
| **ORM y Persistencia** | Entity Framework Core 10 + `MySql.EntityFrameworkCore` (Oracle) sobre **MySQL 8.4 LTS** (`VyTSystems_Dev`) |
| **Validación y Mapeo** | **FluentValidation** (`12.2.0`) · **Mapster** + `Mapster.DependencyInjection` (`10.0.14`) |
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
│   │   ├── Common/
│   │   │   ├── CQRS/                   # ICommand, IQuery, Handlers, IDispatcher, RequestDispatcher
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
│   │   ├── Auth/                       # Casos de uso futuros de Autenticación
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

## 6. Desarrollo con CQRS, FluentValidation y Dispatcher

### 6.1. Cómo desarrollar Commands y Queries
* **Command (`ICommand` o `ICommand<TResult>`):** operación que modifica el estado del sistema.
  ```csharp
  public sealed record CrearSolicitudCommand(
      string Folio,
      Guid InstitucionId,
      string Descripcion) : ICommand<Guid>;
  ```
* **Query (`IQuery<TResult>`):** operación de solo lectura sin efectos secundarios.
  ```csharp
  public sealed record ConsultarSolicitudQuery(Guid SolicitudId) : IQuery<SolicitudDetalleDto>;
  ```

### 6.2. Cómo agregar Validators mediante FluentValidation
Cada caso de uso define su validador heredando de `AbstractValidator<T>` en la misma carpeta del Slice. `AddApplication()` registra automáticamente todos los validadores del ensamblado:

```csharp
using FluentValidation;

public sealed class CrearSolicitudValidator : AbstractValidator<CrearSolicitudCommand>
{
    public CrearSolicitudValidator()
    {
        RuleFor(x => x.Folio)
            .NotEmpty().WithMessage("El folio es obligatorio.")
            .MaximumLength(40);

        RuleFor(x => x.InstitucionId)
            .NotEmpty().WithMessage("La institución es obligatoria.");

        RuleFor(x => x.Descripcion)
            .NotEmpty()
            .MinimumLength(10).WithMessage("La descripción requiere al menos 10 caracteres.");
    }
}
```

> **Separación de responsabilidades:**
> * **FluentValidation:** valida forma y consistencia de datos de entrada antes del Handler.
> * **Domain:** protege invariantes fundamentales del negocio dentro de las entidades.
> * **Autorización:** verifica identidad/permisos en la capa HTTP/seguridad, nunca dentro de un validador de formato.

### 6.3. Cómo implementar Handlers
Los Handlers implementan `ICommandHandler<TCommand, TResult>` o `IQueryHandler<TQuery, TResult>`, dependen solo de abstracciones de `Application` y retornan `Result` / `Result<TResult>`:

```csharp
public sealed class CrearSolicitudHandler(
    ISolicitudRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<CrearSolicitudCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        CrearSolicitudCommand command,
        CancellationToken cancellationToken = default)
    {
        var solicitud = Solicitud.Crear(command.Folio, command.InstitucionId, command.Descripcion);

        await repository.AddAsync(solicitud, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(solicitud.Id);
    }
}
```

### 6.4. Cómo funciona el Dispatcher (`RequestDispatcher`)
`RequestDispatcher` (en `Application/Common/CQRS/RequestDispatcher.cs`) implementa `ICommandDispatcher` e `IQueryDispatcher` manteniendo **una única estrategia de ejecución**:
1. Resuelve desde DI todos los `FluentValidation.IValidator<TRequest>` registrados para el Command o Query.
2. Ejecuta todas las validaciones **antes** de resolver el Handler.
3. Si existen errores, corta el flujo inmediatamente devolviendo `Result.ValidationFailure(...)` **sin ejecutar el Handler**.
4. Solo si la entrada es válida, resuelve e invoca el Handler correspondiente.

En un `Controller` de `VyTSystems.API/Controllers/`:
```csharp
[ApiController]
[Route("api/solicitudes")]
public sealed class SolicitudesController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Crear(CrearSolicitudCommand command, CancellationToken ct)
    {
        var result = await commandDispatcher.SendAsync<CrearSolicitudCommand, Guid>(command, ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error, details = result.ValidationErrors });
        }

        return Created($"/api/solicitudes/{result.Value}", new { id = result.Value });
    }
}
```

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

public sealed class ConsultarSolicitudHandler(
    ISolicitudRepository repository,
    IMapper mapper) : IQueryHandler<ConsultarSolicitudQuery, SolicitudDto>
{
    public async Task<Result<SolicitudDto>> HandleAsync(
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

* **S — Single Responsibility:** `DependencyInjection.cs` solo registra servicios; `RequestDispatcher` solo despacha; cada `Validator`, `Handler` y `Repository` tiene una única razón de cambio.
* **O — Open/Closed:** los nuevos Slices, validadores (`AddValidatorsFromAssembly`) y perfiles Mapster (`IRegister`) se incorporan sin modificar la infraestructura base.
* **L — Liskov Substitution:** toda implementación de repositorio o seeder cumple íntegramente el contrato definido.
* **I — Interface Segregation:** interfaces pequeñas y específicas (`ICommandDispatcher` separado de `IQueryDispatcher`, contratos por agregado).
* **D — Dependency Inversion:** los casos de uso dependen de interfaces en `Application`, jamás de `VyTSystemsDbContext` ni de `MySql.EntityFrameworkCore`.

---

## 11. Ejecución Local de Backend y Frontend

### Backend (`http://localhost:5080`)
```bash
cd "/home/beto/Proyectos_CV/VyTSystems/backend"
dotnet build VyTSystems.sln
dotnet run --project VyTSystems.API
```

### Frontend (`http://localhost:5173`)
```bash
cd "/home/beto/Proyectos_CV/VyTSystems/frontend"
npm install
npm run build
npm run dev
```

---

## 12. Flujo Oficial de Ramas en GitHub

Ramas oficiales del repositorio: **`main`**, **`DEV`**, **`QA`** y **`SP1`**.

* **Flujo obligatorio:** `task/<id>-<descripcion>` → **`SP1`** → **`QA`** → **`DEV`** → **`QA`** → **`main`**
* Crear siempre cada tarea desde `SP1` actualizado, verificar que `dotnet build` y `npm run build` compilen sin errores, comprobar que no existan secretos en el árbol y abrir Pull Request hacia `SP1`.

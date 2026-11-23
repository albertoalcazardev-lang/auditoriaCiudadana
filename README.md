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
│   │   │   ├── Mapping/                # MappingConfig (escaneo de perfiles IRegister de Mapster)
│   │   │   └── Results/                # Result, Result<T> y Error
│   │   ├── Solicitudes/                # Vertical Slices: Crear/, Editar/, Consultar/, Listar/
│   │   ├── Instituciones/              # Casos de uso de Instituciones
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

## 7. Entidades, DTOs y Configuración de Mapster

1. **Entidades:** se crean en `VyTSystems.Domain/Entities/` heredando de `Entity<TId>` y encapsulando sus reglas de negocio.
2. **DTOs:** se definen como `record` inmutables junto al caso de uso que los necesita (p. ej. `Application/Solicitudes/Consultar/SolicitudDetalleDto.cs`).
3. **Mapster (`Application/Common/Mapping/`):** `MappingConfig` escanea automáticamente todas las clases que implementen `IRegister` en `Application` e inyecta `IMapper`:
   ```csharp
   using Mapster;

   public sealed class SolicitudMappingConfig : IRegister
   {
       public void Register(TypeAdapterConfig config)
       {
           config.NewConfig<Solicitud, SolicitudDetalleDto>()
               .Map(dest => dest.SolicitudId, src => src.Id);
       }
   }
   ```
   * **Regla:** no mapear propiedades sensibles automáticamente ni usar Mapster para sustituir constructores/fábricas del Dominio, validaciones o repositorios.

---

## 8. Repositorios, EF Core 10 con MySQL y Migraciones

### Implementar repositorios
1. Definir la interfaz en `Application/Abstractions/Persistence/` (sin exponer `DbContext`, `DbSet` ni `IQueryable`).
2. Implementar la clase en `Infrastructure/Repositories/` inyectando `VyTSystemsDbContext`.
3. Registrar la interfaz y su implementación en `Infrastructure/DependencyInjection.cs`.

### Configuración segura de credenciales MySQL (`VyTSystems_Dev`)
**Nunca escribas contraseñas en `appsettings.json` ni las subas a GitHub.** Cada desarrollador configura su conexión local hacia `VyTSystems_Dev` usando **User Secrets** de .NET (con un usuario local dedicado distinto de `root`):

```bash
cd "/home/beto/Proyectos CV/VyTSystems/backend"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=127.0.0.1;Port=3306;Database=VyTSystems_Dev;Uid=vyt_dev;Pwd=TU_PASSWORD_LOCAL;" \
  --project VyTSystems.API/VyTSystems.API.csproj
```
*(También puede suministrarse mediante la variable de entorno `ConnectionStrings__DefaultConnection`).*

### Crear y aplicar migraciones reales (cuando exista el primer modelo aprobado)
```bash
cd "/home/beto/Proyectos CV/VyTSystems/backend"

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
cd "/home/beto/Proyectos CV/VyTSystems/backend"
dotnet build VyTSystems.sln
dotnet run --project VyTSystems.API
```

### Frontend (`http://localhost:5173`)
```bash
cd "/home/beto/Proyectos CV/VyTSystems/frontend"
npm install
npm run build
npm run dev
```

---

## 12. Flujo Oficial de Ramas en GitHub

Ramas oficiales del repositorio: **`main`**, **`DEV`**, **`QA`** y **`SP1`**.

* **Flujo obligatorio:** `task/<id>-<descripcion>` → **`SP1`** → **`QA`** → **`DEV`** → **`QA`** → **`main`**
* Crear siempre cada tarea desde `SP1` actualizado, verificar que `dotnet build` y `npm run build` compilen sin errores, comprobar que no existan secretos en el árbol y abrir Pull Request hacia `SP1`.

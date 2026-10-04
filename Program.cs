/*
    Taller matriculas 
    Integrantes:
    - Marcos Jacome
    - Luis Brieva
*/
using System.Reflection;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// --- Configuración de Swagger ------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API de Matrículas Académicas",
        Version = "v1",
        Description = "Gestión de asignaturas y matrículas por año y período académico. Presentado por Marcos Jacome y Luis Brieva"
    });

    options.SupportNonNullableReferenceTypes();

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

var app = builder.Build();

// --- Middleware de Swagger ---------------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "API de Matrículas Académicas v1");
    options.DocumentTitle = "API de Matriculas Academicas";
});

app.UseHttpsRedirection();

// ===================================================================================
// DATOS EN MEMORIA (List<T>) - no se usa base de datos
// ===================================================================================

var asignaturas = new List<Asignatura>
{
    new Asignatura(101, "Estructuras de Datos", "SIS-101", 3),
    new Asignatura(102, "Programación Web", "SIS-102", 4),
    new Asignatura(103, "Bases de Datos I", "SIS-103", 3)
};

var matriculas = new List<Matricula>();

var estudiantes = new List<Estudiante>
{
    new Estudiante(1, "Laura Gómez", "Ingeniería de Sistemas"),
    new Estudiante(2, "Carlos Pérez", "Ingeniería de Sistemas"),
    new Estudiante(3, "María Rodríguez", "Ingeniería de Sistemas")
};

var api = app.MapGroup("/api");

// ===================================================================================
// ENDPOINTS
// ===================================================================================

// GET /api/asignaturas
api.MapGet("/asignaturas", () =>
{
    var activas = asignaturas.Where(a => a.Activa).ToList();
    return Results.Ok(activas);
})
.Produces<List<Asignatura>>(StatusCodes.Status200OK)
.WithName("ListarAsignaturas")
.WithTags("Asignaturas")
.WithSummary("Lista la oferta académica (solo asignaturas activas).")
.WithDescription("""
    Devuelve únicamente las asignaturas con Activa == true.
    Las asignaturas inactivas (borrado lógico) no aparecen en la oferta,
    pero se conservan en el historial académico de los estudiantes.
    """);

// POST /api/asignaturas
api.MapPost("/asignaturas", (Asignatura request) =>
{
    var nuevoId = asignaturas.Count > 0 ? asignaturas.Max(a => a.Id) + 1 : 1;

    if (string.IsNullOrWhiteSpace(request.Nombre))
        return Results.BadRequest(new ErrorResponse("El nombre de la asignatura es obligatorio."));

    if (string.IsNullOrWhiteSpace(request.Codigo))
        return Results.BadRequest(new ErrorResponse("El código de la asignatura es obligatorio."));

    if (request.Creditos <= 0)
        return Results.BadRequest(new ErrorResponse("Los créditos deben ser mayores que cero."));

    if (asignaturas.Any(a => a.Codigo.Equals(request.Codigo.Trim(), StringComparison.OrdinalIgnoreCase)))
        return Results.BadRequest(new ErrorResponse($"Ya existe una asignatura con el código {request.Codigo}."));

    var nueva = new Asignatura(nuevoId, request.Nombre.Trim(), request.Codigo.Trim(), request.Creditos, true);
    asignaturas.Add(nueva);

    return Results.Created($"/api/asignaturas/{nueva.Id}", nueva);
})
.Produces<Asignatura>(StatusCodes.Status201Created)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.WithName("CrearAsignatura")
.WithTags("Asignaturas")
.WithSummary("Registra una nueva asignatura.")
.WithDescription("""
    Validaciones:
    - Nombre y código obligatorios (400 si están vacíos).
    - Créditos mayores que cero (400 si no).
    - Código no repetido (400 si ya existe).

    El Id enviado en el cuerpo se ignora: lo genera el servidor.
    La asignatura siempre se crea con Activa = true.
    """);

// DELETE /api/asignaturas/{id}
api.MapDelete("/asignaturas/{id:int}", (int id) =>
{
    var indice = asignaturas.FindIndex(a => a.Id == id);

    if (indice == -1)
        return Results.NotFound();

    if (matriculas.Any(m => m.AsignaturaId == id))
    {
        asignaturas[indice] = asignaturas[indice] with { Activa = false };
    }
    else
    {
        asignaturas.RemoveAt(indice);
    }

    return Results.NoContent();
})
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound)
.WithName("EliminarAsignatura")
.WithTags("Asignaturas")
.WithSummary("Elimina físicamente la asignatura o la inactiva si tiene matrículas.")
.WithDescription("""
    - Sin matrículas: eliminación física.
    - Con matrículas: borrado lógico (Activa = false) para conservar el historial académico.

    En ambos casos responde 204 No Content. Si la asignatura no existe, responde 404.
    """);

// POST /api/matriculas
api.MapPost("/matriculas", (Matricula request) =>
{
    if (!estudiantes.Any(e => e.Id == request.EstudianteId))
        return Results.NotFound(new ErrorResponse($"No existe el estudiante con id {request.EstudianteId}."));

    var asignatura = asignaturas.FirstOrDefault(a => a.Id == request.AsignaturaId);
    if (asignatura is null)
        return Results.NotFound(new ErrorResponse($"No existe la asignatura con id {request.AsignaturaId}."));

    if (!asignatura.Activa)
        return Results.BadRequest(new ErrorResponse($"La asignatura {asignatura.Nombre} está inactiva."));

    if (string.IsNullOrWhiteSpace(request.Periodo) || !request.Periodo.StartsWith(request.Anio.ToString()))
        return Results.BadRequest(new ErrorResponse($"El período '{request.Periodo}' no corresponde al año {request.Anio}."));

    var duplicada = matriculas.Any(m =>
        m.EstudianteId == request.EstudianteId &&
        m.AsignaturaId == request.AsignaturaId &&
        m.Anio == request.Anio &&
        m.Periodo == request.Periodo);

    if (duplicada)
        return Results.BadRequest(new ErrorResponse(
            $"El estudiante {request.EstudianteId} ya está matriculado en la asignatura " +
            $"{request.AsignaturaId} para el período {request.Periodo}."));

    var nuevoId = matriculas.Count > 0 ? matriculas.Max(m => m.Id) + 1 : 1;

    var nueva = new Matricula(nuevoId, request.EstudianteId, request.AsignaturaId, request.Anio, request.Periodo);
    matriculas.Add(nueva);

    return Results.Created($"/api/matriculas/{nueva.Id}", nueva);
})
.Produces<Matricula>(StatusCodes.Status201Created)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces<ErrorResponse>(StatusCodes.Status404NotFound)
.WithName("CrearMatricula")
.WithTags("Matriculas")
.WithSummary("Matricula a un estudiante en una asignatura.")
.WithDescription("""
    Valida, en este orden:
    1. El estudiante existe (404 si no).
    2. La asignatura existe (404 si no).
    3. La asignatura está activa (400 si está inactiva).
    4. El período corresponde al año (400 si no).
    5. No hay matrícula duplicada con la misma combinación
       (EstudianteId, AsignaturaId, Anio, Periodo) (400 si ya existe).

    Repetir la asignatura en un período distinto SÍ es válido.
    El Id enviado en el cuerpo se ignora.
    """);

// GET /api/estudiantes/{id}/asignaturas
api.MapGet("/estudiantes/{id:int}/asignaturas", (int id) =>
{
    if (!estudiantes.Any(e => e.Id == id))
        return Results.NotFound(new ErrorResponse($"No existe el estudiante con id {id}."));

    var historial = matriculas
        .Where(m => m.EstudianteId == id)
        .OrderBy(m => m.Anio)
        .ThenBy(m => m.Periodo)
        .Select(m =>
        {
            var asignatura = asignaturas.FirstOrDefault(a => a.Id == m.AsignaturaId);

            return new AsignaturaMatriculada(
                AsignaturaId: m.AsignaturaId,
                Nombre: asignatura?.Nombre ?? "(asignatura eliminada)",
                Codigo: asignatura?.Codigo ?? string.Empty,
                Creditos: asignatura?.Creditos ?? 0,
                Activa: asignatura?.Activa ?? false,
                Anio: m.Anio,
                Periodo: m.Periodo);
        })
        .ToList();

    return Results.Ok(historial);
})
.Produces<List<AsignaturaMatriculada>>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status404NotFound)
.WithName("ListarAsignaturasDeEstudiante")
.WithTags("Estudiantes")
.WithSummary("Historial de asignaturas matriculadas por un estudiante.")
.WithDescription("""
    Devuelve una entrada por cada matrícula del estudiante, ordenadas por año y período.
    Incluye también las asignaturas inactivas (borrado lógico) para preservar el historial.
    Si el estudiante no existe, responde 404.
    """);

app.Run();

// ===================================================================================
// RECORDS (modelo de datos)
// ===================================================================================

/// <summary>Estudiante registrado en la institución.</summary>
/// <param name="Id">Identificador único del estudiante.</param>
/// <param name="Nombre">Nombre completo.</param>
/// <param name="Carrera">Programa académico al que pertenece.</param>
record Estudiante(int Id, string Nombre, string Carrera);

/// <summary>Asignatura ofrecida. Soporta borrado lógico mediante <c>Activa</c>.</summary>
/// <param name="Id">Lo asigna el servidor; se ignora el enviado en el cuerpo.</param>
/// <param name="Nombre">Nombre de la asignatura.</param>
/// <param name="Codigo">Código único (ej.: SIS-104).</param>
/// <param name="Creditos">Número de créditos (mayor que cero).</param>
/// <param name="Activa">false = inactiva (borrado lógico). Siempre es true al crear.</param>
record Asignatura(int Id, string Nombre, string Codigo, int Creditos, bool Activa = true);

/// <summary>Entidad intermedia de la relación N:M Estudiante–Asignatura.</summary>
/// <param name="Id">Lo asigna el servidor.</param>
/// <param name="EstudianteId">Id del estudiante (debe existir).</param>
/// <param name="AsignaturaId">Id de la asignatura (debe existir y estar activa).</param>
/// <param name="Anio">Año académico (ej.: 2026).</param>
/// <param name="Periodo">Período lectivo; debe empezar por el año (ej.: 2026-1).</param>
record Matricula(int Id, int EstudianteId, int AsignaturaId, int Anio = 2026, string Periodo = "2026-1");

/// <summary>Asignatura dentro del historial académico de un estudiante.</summary>
/// <param name="AsignaturaId">Id de la asignatura.</param>
/// <param name="Nombre">Nombre de la asignatura.</param>
/// <param name="Codigo">Código de la asignatura.</param>
/// <param name="Creditos">Número de créditos.</param>
/// <param name="Activa">false si la asignatura fue inactivada (borrado lógico).</param>
/// <param name="Anio">Año en que se matriculó.</param>
/// <param name="Periodo">Período en que se matriculó (ej.: 2026-1).</param>
record AsignaturaMatriculada(int AsignaturaId, string Nombre, string Codigo, int Creditos, bool Activa, int Anio, string Periodo);

/// <summary>Cuerpo estándar de las respuestas de error.</summary>
/// <param name="Error">Mensaje que explica el motivo del error.</param>
record ErrorResponse(string Error);
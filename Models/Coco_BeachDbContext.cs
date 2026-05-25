using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Coco_Beach.Models
{
    public class Coco_BeachDbContext : DbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly ILogger<Coco_BeachDbContext>? _logger;

        public Coco_BeachDbContext(DbContextOptions options, IHttpContextAccessor httpContextAccessor, ILogger<Coco_BeachDbContext>? logger = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public Coco_BeachDbContext(DbContextOptions options)
            : base(options) { }

        public DbSet<rol> rol { get; set; }
        public DbSet<persona> persona { get; set; }
        public DbSet<usuario> usuario { get; set; }
        public DbSet<estado> estado { get; set; }
        public DbSet<recurso> recurso { get; set; }
        public DbSet<reserva> reserva { get; set; }
        public DbSet<auditoria> auditoria { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var auditEntries = new List<AuditEntry>();
            foreach (var entry in ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added ||
                            e.State == EntityState.Modified ||
                            e.State == EntityState.Deleted))
            {
                if (entry.Entity is auditoria) continue;

                string action = entry.State switch
                {
                    EntityState.Added => "INSERT",
                    EntityState.Modified => "UPDATE",
                    EntityState.Deleted => "DELETE",
                    _ => "UNKNOWN"
                };

                Dictionary<string, object?>? oldValues = null;
                Dictionary<string, object?>? newValues = null;

                if (action == "UPDATE")
                {
                    oldValues = await GetOldValuesFromDatabaseAsync(entry, cancellationToken);
                    newValues = GetCurrentValues(entry);
                }
                else if (action == "INSERT")
                {
                    newValues = GetCurrentValues(entry);
                }
                else if (action == "DELETE")
                {
                    oldValues = await GetOldValuesFromDatabaseAsync(entry, cancellationToken);
                }

                auditEntries.Add(new AuditEntry
                {
                    Entry = entry,
                    TableName = entry.Entity.GetType().Name,
                    Action = action,
                    OldValues = oldValues,
                    NewValues = newValues
                });
            }

            if (auditEntries.Count == 0)
                return await base.SaveChangesAsync(cancellationToken);

            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            int result;
            try
            {
                result = await base.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error al guardar entidades principales");
                throw;
            }

            try
            {
                int? usuarioId = await ObtenerUsuarioIdValidoAsync();
                if (usuarioId == null)
                {
                    _logger?.LogWarning("No se pudo obtener usuarioId, auditoría omitida");
                    return result;
                }

                var auditorias = new List<auditoria>();
                foreach (var auditEntry in auditEntries)
                {
                    int entityId = GetEntityId(auditEntry.Entry);
                    string? oldJson = auditEntry.OldValues != null ? JsonSerializer.Serialize(auditEntry.OldValues) : null;
                    string? newJson = auditEntry.NewValues != null ? JsonSerializer.Serialize(auditEntry.NewValues) : null;

                    if (auditEntry.Action == "UPDATE" && oldJson == newJson)
                        continue;

                    auditorias.Add(new auditoria
                    {
                        tabla_afectada = auditEntry.TableName,
                        registroid = entityId,
                        accion = auditEntry.Action,
                        valor_anterior = oldJson,
                        valor_nuevo = newJson,
                        usuarioid = usuarioId.Value,
                        fecha_accion = GetLocalTime()  // ✅ Hora local simple
                    });
                }

                if (auditorias.Any())
                {
                    await this.auditoria.AddRangeAsync(auditorias, cancellationToken);
                    await base.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error no crítico al guardar auditoría");
            }

            return result;
        }

        private Dictionary<string, object?>? GetCurrentValues(EntityEntry entry)
        {
            var values = new Dictionary<string, object?>();
            foreach (var prop in entry.CurrentValues.Properties)
            {
                values[prop.Name] = entry.CurrentValues[prop];
            }
            return values;
        }

        private async Task<Dictionary<string, object?>?> GetOldValuesFromDatabaseAsync(EntityEntry entry, CancellationToken cancellationToken)
        {
            var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
            if (databaseValues == null) return null;

            var values = new Dictionary<string, object?>();
            foreach (var prop in entry.CurrentValues.Properties)
            {
                values[prop.Name] = databaseValues[prop.Name];
            }
            return values;
        }

        private int GetEntityId(EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey()!.Properties.First();
            var id = entry.Property(key.Name).CurrentValue;
            return Convert.ToInt32(id);
        }

        private async Task<int?> ObtenerUsuarioIdValidoAsync()
        {
            var httpContext = _httpContextAccessor?.HttpContext;
            if (httpContext == null) return null;

            var usuarioIdSesion = httpContext.Session.GetInt32("usuarioId");
            if (usuarioIdSesion.HasValue) return usuarioIdSesion.Value;

            var personaId = httpContext.Session.GetInt32("personaId");
            if (personaId.HasValue)
            {
                var usuario = await this.usuario.FirstOrDefaultAsync(u => u.personaid == personaId.Value);
                if (usuario != null)
                {
                    httpContext.Session.SetInt32("usuarioId", usuario.usuarioid);
                    return usuario.usuarioid;
                }
            }

            const int defaultUserId = 1;
            var existeDefault = await this.usuario.AnyAsync(u => u.usuarioid == defaultUserId);
            return existeDefault ? defaultUserId : null;
        }

        // ✅ Método simple y 100% confiable para El Salvador (UTC-6)
        private DateTime GetLocalTime()
        {
            // El Salvador no tiene horario de verano, siempre UTC-6
            return DateTime.UtcNow.AddHours(-6);
        }

        private class AuditEntry
        {
            public EntityEntry Entry { get; set; } = null!;
            public string TableName { get; set; } = null!;
            public string Action { get; set; } = null!;
            public Dictionary<string, object?>? OldValues { get; set; }
            public Dictionary<string, object?>? NewValues { get; set; }
        }
    }
}
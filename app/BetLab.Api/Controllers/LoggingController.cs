using System.Text;
using BetLab.Application.DTOs;
using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoggingController : ControllerBase
    {
        private readonly BetLabDbContext _context;

        public LoggingController(BetLabDbContext context)
        {
            _context = context;
        }

        [HttpPost("session/start")]
        public async Task<ActionResult<StartSessionResponseDto>> StartSession([FromBody] StartSessionRequestDto request)
        {
            if (request.UserId == Guid.Empty)
                return BadRequest("UserId is required.");

            if (string.IsNullOrWhiteSpace(request.Variant))
                return BadRequest("Variant is required.");

            var allowedVariants = new[] { "Ethical", "Dark" };
            if (!allowedVariants.Contains(request.Variant))
                return BadRequest("Variant must be Ethical or Dark.");

            var userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId);
            if (!userExists)
                return NotFound("User not found.");

            var session = new ExperimentSession
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Variant = request.Variant,
                StartedAt = DateTime.UtcNow,
                DeviceType = request.DeviceType,
                Browser = request.Browser
            };

            _context.ExperimentSessions.Add(session);
            await _context.SaveChangesAsync();

            var response = new StartSessionResponseDto
            {
                SessionId = session.Id,
                UserId = session.UserId,
                Variant = session.Variant,
                StartedAt = session.StartedAt
            };

            return Ok(response);
        }

        [HttpPost("event")]
        public async Task<ActionResult> LogEvent([FromBody] LogEventRequestDto request)
        {
            if (request.SessionId == Guid.Empty)
                return BadRequest("SessionId is required.");

            if (request.UserId == Guid.Empty)
                return BadRequest("UserId is required.");

            if (string.IsNullOrWhiteSpace(request.Variant))
                return BadRequest("Variant is required.");

            if (string.IsNullOrWhiteSpace(request.EventType))
                return BadRequest("EventType is required.");

            if (string.IsNullOrWhiteSpace(request.PageName))
                return BadRequest("PageName is required.");

            var session = await _context.ExperimentSessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == request.UserId);

            if (session == null)
                return NotFound("Session not found.");

            var log = new InteractionLog
            {
                SessionId = request.SessionId,
                UserId = request.UserId,
                Variant = request.Variant,
                EventType = request.EventType,
                PageName = request.PageName,
                TargetType = request.TargetType,
                TargetId = request.TargetId,
                OldValue = request.OldValue,
                NewValue = request.NewValue,
                MetadataJson = request.MetadataJson,
                Timestamp = DateTime.UtcNow
            };

            _context.InteractionLogs.Add(log);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Event logged successfully." });
        }

        [HttpPost("session/end")]
        public async Task<ActionResult> EndSession([FromBody] EndSessionRequestDto request)
        {
            if (request.SessionId == Guid.Empty)
                return BadRequest("SessionId is required.");

            if (request.UserId == Guid.Empty)
                return BadRequest("UserId is required.");

            var session = await _context.ExperimentSessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == request.UserId);

            if (session == null)
                return NotFound("Session not found.");

            if (session.EndedAt != null)
                return BadRequest("Session already ended.");

            session.EndedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Session ended successfully.",
                sessionId = session.Id,
                endedAt = session.EndedAt
            });
        }

       
        [HttpGet("logs")]
        public async Task<ActionResult> GetLogs(
            [FromQuery] string? variant = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? eventType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 500)
        {
            pageSize = Math.Clamp(pageSize, 1, 2000);
            page     = Math.Max(1, page);

            var query = _context.InteractionLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(variant))
                query = query.Where(l => l.Variant == variant);

            if (from.HasValue)
                query = query.Where(l => l.Timestamp >= from.Value.ToUniversalTime());

            if (to.HasValue)
                query = query.Where(l => l.Timestamp <= to.Value.ToUniversalTime());

            if (!string.IsNullOrWhiteSpace(eventType))
                query = query.Where(l => l.EventType == eventType);

            var total = await query.CountAsync();
            var logs  = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    l.Id, l.SessionId, l.UserId, l.Variant,
                    l.EventType, l.PageName,
                    l.TargetType, l.TargetId,
                    l.OldValue, l.NewValue, l.MetadataJson,
                    l.Timestamp
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, logs });
        }

    
        [HttpGet("summary")]
        public async Task<ActionResult> GetSummary(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var query = _context.InteractionLogs.AsQueryable();
            if (from.HasValue) query = query.Where(l => l.Timestamp >= from.Value.ToUniversalTime());
            if (to.HasValue)   query = query.Where(l => l.Timestamp <= to.Value.ToUniversalTime());

            // Counts per EventType per Variant
            var eventCounts = await query
                .GroupBy(l => new { l.Variant, l.EventType })
                .Select(g => new { g.Key.Variant, g.Key.EventType, Count = g.Count() })
                .OrderBy(x => x.Variant).ThenByDescending(x => x.Count)
                .ToListAsync();

            // Sesiuni: total + mediu durată (doar sesiunile închise)
            var sessQuery = _context.ExperimentSessions.AsQueryable();
            if (from.HasValue) sessQuery = sessQuery.Where(s => s.StartedAt >= from.Value.ToUniversalTime());
            if (to.HasValue)   sessQuery = sessQuery.Where(s => s.StartedAt <= to.Value.ToUniversalTime());

            // Fetch sessions în memorie pentru a calcula durata (evităm incompatibilități Npgsql)
            var rawSessions = await sessQuery
                .Select(s => new { s.Variant, s.StartedAt, s.EndedAt })
                .ToListAsync();

            var sessionStats = rawSessions
                .GroupBy(s => s.Variant)
                .Select(g => new
                {
                    Variant            = g.Key,
                    TotalSessions      = g.Count(),
                    ClosedSessions     = g.Count(s => s.EndedAt != null),
                    AvgDurationMinutes = g
                        .Where(s => s.EndedAt != null)
                        .Select(s => (s.EndedAt!.Value - s.StartedAt).TotalMinutes)
                        .DefaultIfEmpty(0)
                        .Average()
                })
                .ToList();

            // Utilizatori unici per variantă
            var uniqueUsers = await query
                .GroupBy(l => l.Variant)
                .Select(g => new { Variant = g.Key, UniqueUsers = g.Select(l => l.UserId).Distinct().Count() })
                .ToListAsync();

            return Ok(new { eventCounts, sessionStats, uniqueUsers });
        }

      
        [HttpGet("export/csv")]
        public async Task<ActionResult> ExportCsv(
            [FromQuery] string? variant = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var query = _context.InteractionLogs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(variant)) query = query.Where(l => l.Variant == variant);
            if (from.HasValue) query = query.Where(l => l.Timestamp >= from.Value.ToUniversalTime());
            if (to.HasValue)   query = query.Where(l => l.Timestamp <= to.Value.ToUniversalTime());

            var logs = await query
                .OrderBy(l => l.Timestamp)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Id,SessionId,UserId,Variant,EventType,PageName,TargetType,TargetId,OldValue,NewValue,MetadataJson,Timestamp");

            foreach (var l in logs)
            {
                static string Esc(string? v) => v == null ? "" : $"\"{v.Replace("\"", "\"\"")}\"";
                sb.AppendLine($"{l.Id},{l.SessionId},{l.UserId},{Esc(l.Variant)},{Esc(l.EventType)},{Esc(l.PageName)},{Esc(l.TargetType)},{Esc(l.TargetId)},{Esc(l.OldValue)},{Esc(l.NewValue)},{Esc(l.MetadataJson)},{l.Timestamp:O}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"betlab_logs_{variant?.ToLower() ?? "all"}_{DateTime.UtcNow:yyyyMMdd}.csv";
            return File(bytes, "text/csv", fileName);
        }
    }
}
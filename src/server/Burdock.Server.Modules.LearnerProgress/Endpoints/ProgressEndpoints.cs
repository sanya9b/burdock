using System.Security.Claims;
using System.Text.Json;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Progress;
using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.LearnerProgress.Application;
using Burdock.Server.Modules.LearnerProgress.Domain;
using Burdock.Server.Shared.Errors;
using Burdock.Server.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Burdock.Server.Modules.LearnerProgress.Endpoints;

/// <summary>POST and GET /v1/progress/events (spec 09 §3.5, FR-09-10/11).</summary>
public static class ProgressEndpoints
{
    /// <summary>Progress:MaxBatchBytes (spec 09 §3.15): body size after decompression.</summary>
    public const long MaxBatchBytes = 1024 * 1024;

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(ApiRoutes.Progress.Group).WithTags("Progress").RequireAuthorization(Policies.User);

        group.MapPost("/events", AppendAsync)
            .RequireRateLimiting(Policies.RateLimits.ProgressBatch)
            .WithMetadata(new RequestSizeLimitAttribute(MaxBatchBytes))
            .WithName("AppendProgressEvents")
            .WithSummary("Appends a batch of client events idempotently by id; returns accepted, duplicate and rejected ids and the learner's cursor.");

        group.MapGet("/events", ReadAsync)
            .RequireRateLimiting(Policies.RateLimits.User)
            .WithName("ReadProgressEvents")
            .WithSummary("Events after the cursor in server_seq order; excludeInstallation lets a device skip its own events.");
    }

    private static async Task<IResult> AppendAsync(ClaimsPrincipal principal, PostEventsDto body, AppendEventsUseCase useCase, CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetLearnerId(principal, out var learnerId))
        {
            return Results.Unauthorized();
        }

        if (body?.Events is null)
        {
            return DomainError.Validation("progress.body_missing", "Errors_Progress_BodyMissing").ToProblem();
        }

        var incoming = body.Events.Select(e => new IncomingEvent(e.Id, e.Type, e.SchemaVersion, e.OccurredAt, e.LocalDate, e.InstallationId, e.Payload.ValueKind == JsonValueKind.Undefined ? null : e.Payload.GetRawText())).ToArray();
        var result = await useCase.ExecuteAsync(learnerId, incoming, cancellationToken).ConfigureAwait(false);
        return result.ToResult(outcome => Results.Ok(new PostEventsResultDto(
            outcome.Accepted,
            outcome.Duplicates,
            outcome.Rejected.Select(r => new RejectedEventDto(r.Id, r.Code)).ToArray(),
            outcome.ServerSeq)));
    }

    private static async Task<IResult> ReadAsync(ClaimsPrincipal principal, ReadEventsUseCase useCase, long? since, int? limit, Guid? excludeInstallation, CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetLearnerId(principal, out var learnerId))
        {
            return Results.Unauthorized();
        }

        var page = await useCase.ExecuteAsync(learnerId, since ?? 0, limit, excludeInstallation, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new EventsPageDto(page.Events.Select(ToDto).ToArray(), page.NextSince, page.HasMore));
    }

    private static SyncedEventDto ToDto(ProgressEvent e)
    {
        using var document = JsonDocument.Parse(e.PayloadJson);
        return new SyncedEventDto(e.ServerSeq!.Value, new ProgressEventDto(e.Id, e.Type, e.SchemaVersion, e.OccurredAt, e.LocalDate, e.InstallationId, document.RootElement.Clone()));
    }
}

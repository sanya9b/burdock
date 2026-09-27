namespace Burdock.Domain.SharedKernel;

/// <summary>Classifies an expected failure so adapters can map it to UI states or HTTP status codes.</summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unavailable,
    Unexpected,
}

/// <summary>
/// An expected failure. <see cref="MessageKey"/> is a localization key: Domain never carries user-facing text
/// (spec 01 §3.7; UI strings live in .resx).
/// </summary>
public sealed record DomainError(string Code, string MessageKey, ErrorKind Kind, IReadOnlyDictionary<string, string>? Data = null)
{
    public static DomainError Validation(string code, string messageKey, IReadOnlyDictionary<string, string>? data = null)
        => new(code, messageKey, ErrorKind.Validation, data);

    public static DomainError NotFound(string code, string messageKey, IReadOnlyDictionary<string, string>? data = null)
        => new(code, messageKey, ErrorKind.NotFound, data);

    public static DomainError Conflict(string code, string messageKey, IReadOnlyDictionary<string, string>? data = null)
        => new(code, messageKey, ErrorKind.Conflict, data);

    public static DomainError Forbidden(string code, string messageKey)
        => new(code, messageKey, ErrorKind.Forbidden);

    public static DomainError Unavailable(string code, string messageKey)
        => new(code, messageKey, ErrorKind.Unavailable);

    public static DomainError Unexpected(string code, string messageKey)
        => new(code, messageKey, ErrorKind.Unexpected);
}

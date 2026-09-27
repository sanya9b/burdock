using System.Diagnostics.CodeAnalysis;

namespace Burdock.Domain.SharedKernel;

/// <summary>Outcome of an operation that can fail in an expected way (ADR: explicit UX states over exceptions).</summary>
public readonly struct Result : IEquatable<Result>
{
    private Result(DomainError? error) => Error = error;

    public DomainError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public static Result Success() => new(null);

    public static Result Failure(DomainError error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static Result<T> Success<T>(T value) => Result<T>.FromValue(value);

    public static Result<T> Failure<T>(DomainError error) => Result<T>.FromError(error);

    public TOut Match<TOut>(Func<TOut> onSuccess, Func<DomainError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess() : onFailure(Error);
    }

    public bool Equals(Result other) => Equals(Error, other.Error);

    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    public override int GetHashCode() => Error?.GetHashCode() ?? 0;

    public static bool operator ==(Result left, Result right) => left.Equals(right);

    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    public static implicit operator Result(DomainError error) => Failure(error);

    public override string ToString() => IsSuccess ? "Success" : $"Failure({Error.Code})";
}

/// <summary>Outcome carrying a value on success.</summary>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? _value;

    private Result(T? value, DomainError? error)
    {
        _value = value;
        Error = error;
    }

    public DomainError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    /// <summary>The value; throws when the result is a failure. Prefer <see cref="Match{TOut}"/>.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result is a failure ({Error.Code}); Value is not available.");

    internal static Result<T> FromValue(T value) => new(value, null);

    internal static Result<T> FromError(DomainError error) => new(default, error ?? throw new ArgumentNullException(nameof(error)));

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<DomainError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value!) : onFailure(Error);
    }

    public Result<TOut> Map<TOut>(Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return IsSuccess ? Result<TOut>.FromValue(map(_value!)) : Result<TOut>.FromError(Error);
    }

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return IsSuccess ? bind(_value!) : Result<TOut>.FromError(Error);
    }

    public bool Equals(Result<T> other) => EqualityComparer<T?>.Default.Equals(_value, other._value) && Equals(Error, other.Error);

    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_value, Error);

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    public static implicit operator Result<T>(T value) => FromValue(value);

    public static implicit operator Result<T>(DomainError error) => FromError(error);

    public override string ToString() => IsSuccess ? $"Success({_value})" : $"Failure({Error.Code})";
}

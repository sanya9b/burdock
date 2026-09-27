using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Tests.SharedKernel;

public sealed class ResultTests
{
    private static readonly DomainError SampleError = DomainError.NotFound("lexeme.notFound", "Errors_Lexeme_NotFound");

    [Fact]
    public void Success_carries_value_and_no_error()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldBe(42);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Failure_carries_error_and_hides_value()
    {
        Result<int> result = SampleError;

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SampleError);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Map_and_bind_short_circuit_on_failure()
    {
        Result<int> failure = SampleError;
        Result<int> success = 2;

        failure.Map(x => x * 10).Error.ShouldBe(SampleError);
        success.Map(x => x * 10).Value.ShouldBe(20);
        success.Bind(x => x > 1 ? Result.Success(x.ToString(System.Globalization.CultureInfo.InvariantCulture)) : Result.Failure<string>(SampleError)).Value.ShouldBe("2");
        success.Bind(_ => Result.Failure<string>(SampleError)).Error.ShouldBe(SampleError);
    }

    [Fact]
    public void Match_selects_the_right_branch()
    {
        Result.Success().Match(() => "ok", e => e.Code).ShouldBe("ok");
        Result.Failure(SampleError).Match(() => "ok", e => e.Code).ShouldBe("lexeme.notFound");
    }

    [Fact]
    public void Error_factories_set_the_kind()
    {
        DomainError.Validation("a", "k").Kind.ShouldBe(ErrorKind.Validation);
        DomainError.Conflict("a", "k").Kind.ShouldBe(ErrorKind.Conflict);
        DomainError.Forbidden("a", "k").Kind.ShouldBe(ErrorKind.Forbidden);
        DomainError.Unavailable("a", "k").Kind.ShouldBe(ErrorKind.Unavailable);
        DomainError.Unexpected("a", "k").Kind.ShouldBe(ErrorKind.Unexpected);
    }
}

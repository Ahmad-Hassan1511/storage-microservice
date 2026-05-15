namespace Storage.Application.Common;

public sealed class Result<T>
{
    public T? Value { get; }
    public ApplicationError? Error { get; }
    public bool IsSuccess => Error is null;

    private Result(T value) { Value = value; }
    private Result(ApplicationError error) { Error = error; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(ApplicationError error) => new(error);
}

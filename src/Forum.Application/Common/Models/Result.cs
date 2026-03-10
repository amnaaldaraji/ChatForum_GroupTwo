namespace Forum.Application.Common.Models;

/// <summary>
/// Classifies the type of error returned in a Result, mapped to HTTP status codes in the API layer.
/// </summary>
public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Unauthorized,
    Forbidden,
    Conflict
}

/// <summary>
/// Represents the outcome of an operation, carrying an optional error message and ErrorType.
/// Used by all CQRS handlers to communicate success/failure without throwing exceptions.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, string? error, ErrorType errorType = ErrorType.None)
    {
        if (isSuccess && error != null)
            throw new InvalidOperationException("Success result cannot have an error.");
        if (!isSuccess && error == null)
            throw new InvalidOperationException("Failed result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
        ErrorType = isSuccess ? ErrorType.None : errorType;
    }

    public bool IsSuccess { get; }
    public bool IsFailed => !IsSuccess;
    public string? Error { get; }
    public ErrorType ErrorType { get; }

    public static Result Success() => new (true, null);
    public static Result Failure(string error, ErrorType errorType = ErrorType.Validation) => new (false, error, errorType);
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error, ErrorType errorType = ErrorType.Validation) => Result<T>.Failure(error, errorType);
}

/// <summary>
/// Generic variant of Result that also carries a typed Value on success.
/// </summary>
public class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(true, null)
    {
        _value = value;
    }

    private Result(string error, ErrorType errorType = ErrorType.Validation) : base(false, error, errorType)
    {
        _value = default;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failed result.");

    public new static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(string error, ErrorType errorType = ErrorType.Validation) => new (error, errorType);
}
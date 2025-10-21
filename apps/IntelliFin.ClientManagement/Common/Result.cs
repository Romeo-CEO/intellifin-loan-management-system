namespace IntelliFin.ClientManagement.Common;

/// <summary>
/// Represents the result of an operation with success/failure state
/// </summary>
/// <typeparam name="T">The type of the result value</typeparam>
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string Error { get; }

    protected Result(bool isSuccess, T? value, string error)
    {
        if (isSuccess && value == null)
            throw new ArgumentException("Value cannot be null for a successful result", nameof(value));
        
        if (!isSuccess && string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Error message must be provided for a failed result", nameof(error));

        IsSuccess = isSuccess;
        Value = value;
        Error = error ?? string.Empty;
    }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static Result<T> Success(T value) => new(true, value, string.Empty);

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static Result<T> Failure(string error) => new(false, default, error);

    /// <summary>
    /// Matches the result to one of two functions
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        return IsSuccess && Value != null
            ? onSuccess(Value)
            : onFailure(Error);
    }

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess && Value != null)
            action(Value);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure
    /// </summary>
    public Result<T> OnFailure(Action<string> action)
    {
        if (IsFailure)
            action(Error);
        return this;
    }
}

/// <summary>
/// Represents the result of an operation without a return value
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }

    protected Result(bool isSuccess, string error)
    {
        if (!isSuccess && string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Error message must be provided for a failed result", nameof(error));

        IsSuccess = isSuccess;
        Error = error ?? string.Empty;
    }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static Result Success() => new(true, string.Empty);

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static Result Failure(string error) => new(false, error);

    /// <summary>
    /// Combines multiple results into a single result
    /// </summary>
    public static Result Combine(params Result[] results)
    {
        foreach (var result in results)
        {
            if (result.IsFailure)
                return result;
        }
        return Success();
    }
}

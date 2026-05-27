namespace Personal_Blog_Application.Services.Common
{
    public enum ResultStatus
    {
        Success,
        ValidationError,
        NotFound,
        Forbidden,
        Conflict
    }

    public record OperationResult(
        ResultStatus Status,
        IReadOnlyList<string> Errors,
        IReadOnlyDictionary<string, string>? FieldErrors = null)
    {
        public bool Success => Status == ResultStatus.Success;

        public static OperationResult Ok() =>
            new(ResultStatus.Success, Array.Empty<string>());

        public static OperationResult Fail(params string[] errors) =>
            new(ResultStatus.ValidationError, errors);

        public static OperationResult FailField(string field, string error) =>
            new(ResultStatus.ValidationError,
                Array.Empty<string>(),
                new Dictionary<string, string> { [field] = error });

        public static OperationResult FailFields(IDictionary<string, string> fieldErrors) =>
            new(ResultStatus.ValidationError, Array.Empty<string>(),
                new Dictionary<string, string>(fieldErrors));

        public static OperationResult NotFound() =>
            new(ResultStatus.NotFound, Array.Empty<string>());

        public static OperationResult Forbidden() =>
            new(ResultStatus.Forbidden, Array.Empty<string>());

        public static OperationResult Conflict(string message) =>
            new(ResultStatus.Conflict, new[] { message });
    }

    public record OperationResult<T>(
        ResultStatus Status,
        T? Value,
        IReadOnlyList<string> Errors,
        IReadOnlyDictionary<string, string>? FieldErrors = null)
    {
        public bool Success => Status == ResultStatus.Success;

        public static OperationResult<T> Ok(T value) =>
            new(ResultStatus.Success, value, Array.Empty<string>());

        public static OperationResult<T> Fail(params string[] errors) =>
            new(ResultStatus.ValidationError, default, errors);

        public static OperationResult<T> FailField(string field, string error) =>
            new(ResultStatus.ValidationError, default,
                Array.Empty<string>(),
                new Dictionary<string, string> { [field] = error });

        public static OperationResult<T> NotFound() =>
            new(ResultStatus.NotFound, default, Array.Empty<string>());

        public static OperationResult<T> Forbidden() =>
            new(ResultStatus.Forbidden, default, Array.Empty<string>());
    }
}

namespace Finance.Application.Common.Exceptions;

public sealed class AppException(
    int statusCode,
    string code,
    string title,
    string message,
    TimeSpan? retryAfter = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string Title { get; } = title;
    public TimeSpan? RetryAfter { get; } = retryAfter;

    public static AppException Unauthorized(string code, string message)
        => new(401, code, "Não autorizado", message);

    public static AppException Forbidden(string code, string message)
        => new(403, code, "Acesso negado", message);

    public static AppException NotFound(string code, string message)
        => new(404, code, "Recurso não encontrado", message);

    public static AppException Conflict(string code, string message)
        => new(409, code, "Conflito de dados", message);

    public static AppException Unprocessable(string code, string message)
        => new(422, code, "Operação não permitida", message);
}

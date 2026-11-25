using System.Globalization;
using Finance.Application.Common.Exceptions;
using Finance.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment hostEnvironment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        string traceId = httpContext.TraceIdentifier;

        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

            var validationProblem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dados inválidos",
                Detail = "Corrija os campos informados e tente novamente.",
                Instance = httpContext.Request.Path
            };
            validationProblem.Extensions["code"] = "validation_failed";
            validationProblem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
            return true;
        }

        if (exception is AppException appException)
        {
            logger.LogWarning(
                "Falha controlada ({Code}, HTTP {StatusCode}) em {Path} [TraceId: {TraceId}]",
                appException.Code,
                appException.StatusCode,
                httpContext.Request.Path,
                traceId);

            if (appException.RetryAfter.HasValue)
            {
                int seconds = Math.Max(1, (int)Math.Ceiling(appException.RetryAfter.Value.TotalSeconds));
                httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            }

            var appProblem = new ProblemDetails
            {
                Status = appException.StatusCode,
                Title = appException.Title,
                Detail = appException.Message,
                Instance = httpContext.Request.Path
            };
            appProblem.Extensions["code"] = appException.Code;
            appProblem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = appException.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(appProblem, cancellationToken);
            return true;
        }

        if (exception is DomainException domainException)
        {
            logger.LogWarning(
                "Regra de domínio violada ({Code}) em {Path} [TraceId: {TraceId}]: {Message}",
                domainException.Code,
                httpContext.Request.Path,
                traceId,
                domainException.Message);

            var domainProblem = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Operação não permitida",
                Detail = domainException.Message,
                Instance = httpContext.Request.Path
            };
            domainProblem.Extensions["code"] = domainException.Code;
            domainProblem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await httpContext.Response.WriteAsJsonAsync(domainProblem, cancellationToken);
            return true;
        }

        logger.LogError(
            exception,
            "Erro não tratado em {Method} {Path} [TraceId: {TraceId}]",
            httpContext.Request.Method,
            httpContext.Request.Path,
            traceId);

        var serverProblem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Erro interno no servidor",
            Detail = hostEnvironment.IsDevelopment()
                ? exception.Message
                : "Ocorreu um erro inesperado. Tente novamente.",
            Instance = httpContext.Request.Path
        };
        serverProblem.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(serverProblem, cancellationToken);
        return true;
    }
}

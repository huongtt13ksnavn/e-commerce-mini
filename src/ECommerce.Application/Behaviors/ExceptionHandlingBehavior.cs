using ECommerce.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Behaviors;

public sealed class ExceptionHandlingBehavior<TRequest, TResponse>(
    ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning("Validation failed for {Request}: {Errors}", typeof(TRequest).Name, ex.Errors);
            throw;
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Domain rule violated for {Request}: {Message}", typeof(TRequest).Name, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Request}", typeof(TRequest).Name);
            throw;
        }
    }
}

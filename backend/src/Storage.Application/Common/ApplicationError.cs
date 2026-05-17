namespace Storage.Application.Common;

public abstract record ApplicationError(string Message);
public sealed record NotFoundError(string Message) : ApplicationError(Message);
public sealed record AccessDeniedError(string Message) : ApplicationError(Message);

/// <summary>HttpStatusHint: 400=Bad, 403=Forbidden, 413=TooLarge, 415=UnsupportedMedia, 422=Unprocessable</summary>
public sealed record PolicyViolationError(string Message, int HttpStatusHint) : ApplicationError(Message);
public sealed record IdempotencyConflictError(string Message) : ApplicationError(Message);
public sealed record ChecksumMismatchError(string Message) : ApplicationError(Message);

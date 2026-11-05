namespace Notificaciones.Api.Contracts;

/// <summary>Formato de toda lista paginada: <c>{ totalRecords, page, pageSize, items }</c>.</summary>
public record PagedResponse<T>(long TotalRecords, int Page, int PageSize, IReadOnlyList<T> Items);

/// <summary>Esquema <c>UnreadCount</c>.</summary>
public record UnreadCountResponse(long Count);

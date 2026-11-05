namespace SchoolPlatform.Host.Common;

/// <summary>Expected business rejection: mapped to a problem response with a stable code.</summary>
public class OperationException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;

    public static OperationException Invalid(string message) => new(400, "invalid-request", message);
    public static OperationException NotFound(string message) => new(404, "not-found", message);
    public static OperationException Changed() =>
        new(409, "resource-changed", "Los datos cambiaron. Recarga la página antes de continuar.");
}

public sealed record PagedResult<T>(T[] Items, int Total, int Page, int PageSize);

public static class Versions
{
    public static string Encode(byte[] version) => Convert.ToBase64String(version);

    public static byte[] Decode(string? version)
    {
        if (string.IsNullOrEmpty(version) || version.Length > 24) throw OperationException.Changed();
        try { return Convert.FromBase64String(version); }
        catch (FormatException) { throw OperationException.Changed(); }
    }
}

public static class Reasons
{
    public static string Require(string? value)
    {
        var reason = value?.Trim() ?? "";
        if (reason.Length is < 8 or > 500)
            throw OperationException.Invalid("Describe el motivo del cambio, entre 8 y 500 caracteres.");
        return reason;
    }
}

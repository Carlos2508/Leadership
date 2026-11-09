namespace SchoolPlatform.Host.IdentityAccess;

// School profiles (student/staff/guardian) and family faculties are separate from these installation roles.
public sealed class AccessRole
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
public sealed class AccessRolePermission
{
    public string RoleKey { get; set; } = "";
    public string Permission { get; set; } = "";
}
public sealed class AccountRoleAssignment
{
    public Guid UserId { get; set; }
    public string RoleKey { get; set; } = "";
}
public sealed record RoleView(string Key, string Label, string Description, bool IsActive, string Version, string[] Permissions, int Accounts);
public sealed record SaveRoleRequest(string Label, string? Description, string[] Permissions, bool IsActive, string Reason, string? Version = null);
public sealed record PermissionDefinition(string Key, string Label, string Area);

// The catalogue identifies protected actions implemented by the server. Role composition and assignments live in SQL.
public static class PermissionCatalog
{
    public static readonly PermissionDefinition[] All =
    [
        new(Permissions.ManageAccounts, "Administrar cuentas, roles y permisos", "Administración"),
        new(Permissions.ReadAudit, "Consultar auditoría", "Administración"),
        new(Permissions.PeopleRead, "Consultar estudiantes, padres y personal", "Comunidad escolar"),
        new(Permissions.PeopleManage, "Registrar y editar fichas y vínculos", "Comunidad escolar"),
        new(Permissions.AcademicRead, "Consultar ciclos, inscripciones y cursos", "Académico"),
        new(Permissions.AcademicManage, "Configurar ciclos, cursos y publicaciones", "Académico"),
        new(Permissions.EnrollmentManage, "Administrar inscripciones y traslados", "Académico"),
        new(Permissions.FinanceRead, "Consultar cuentas, recibos y solvencia", "Finanzas"),
        new(Permissions.FinanceCharges, "Configurar y emitir cargos y ajustes", "Finanzas"),
        new(Permissions.FinanceCollect, "Registrar pagos", "Finanzas"),
        new(Permissions.FinanceReverse, "Revertir pagos", "Finanzas"),
        new(Permissions.CommunicationPublish, "Publicar y administrar avisos", "Comunicación"),
        new(Permissions.AttendanceGate, "Registrar entradas y salidas", "Portería"),
        new(Permissions.AttendanceRead, "Consultar reportes de portería", "Portería"),
        new(Permissions.AttendanceManage, "Administrar lectores y anular marcas", "Portería"),
        new(Permissions.InventoryRead, "Consultar artículos, existencias y movimientos", "Inventario y bodega"),
        new(Permissions.InventoryManage, "Administrar artículos y corregir conteos físicos", "Inventario y bodega"),
        new(Permissions.InventoryMove, "Registrar entradas y salidas de artículos", "Inventario y bodega")
    ];
}

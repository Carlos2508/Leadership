namespace SchoolPlatform.Host.Inventory;

public sealed class InventoryProduct
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Location { get; set; } = "";
    public int Minimum { get; set; }
    public int OnHand { get; set; }
    public bool IsActive { get; set; } = true;
    public long LastSequence { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public byte[] CreationHash { get; set; } = [];
    public byte[] Version { get; set; } = [];
}

public sealed class InventoryMovement
{
    // Client-generated attempt id: retrying an uncertain response never moves stock twice.
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public long Sequence { get; set; }
    public Guid ActorId { get; set; }
    public string Kind { get; set; } = "";
    public int Delta { get; set; }
    public int Before { get; set; }
    public int After { get; set; }
    public string Reference { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public byte[] RequestHash { get; set; } = [];
}

public sealed record CreateInventoryProduct(Guid RequestId, string Code, string Name, string? Category,
    string? Location, int Minimum, int InitialQuantity, string Reason);
public sealed record UpdateInventoryProduct(string Code, string Name, string? Category, string? Location,
    int Minimum, bool IsActive, string Version, string Reason);
public sealed record MoveInventoryStock(Guid RequestId, string Kind, int Quantity, string? Reference, string Reason, string Version);
public sealed record InventoryProductView(Guid Id, string Code, string Name, string Category, string Location,
    int Minimum, int OnHand, bool IsActive, bool LowStock, string Version);
public sealed record InventoryMovementView(Guid Id, long Sequence, string Kind, int Delta, int Before, int After,
    string Reference, string Reason, DateTimeOffset OccurredAt, string Actor);
public sealed record InventorySummary(int ActiveProducts, int LowStockProducts, long Units);

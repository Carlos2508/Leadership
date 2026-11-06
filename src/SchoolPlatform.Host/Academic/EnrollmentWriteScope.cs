using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Academic;

// Same lock for browser writes and initial imports. The capacity check and its write are indivisible.
internal sealed class EnrollmentWriteScope(IDbContextTransaction? transaction) : IAsyncDisposable
{
    public static async Task<EnrollmentWriteScope> BeginAsync(SchoolDbContext db, CancellationToken ct)
    {
        var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        try
        {
            await LockAsync(db, ct);
            return new(transaction);
        }
        catch { if (transaction is not null) await transaction.DisposeAsync(); throw; }
    }

    internal static Task LockAsync(SchoolDbContext db, CancellationToken ct) => db.Database.ExecuteSqlRawAsync("""
        DECLARE @result int;
        EXEC @result = sp_getapplock @Resource=N'SchoolPlatform.Enrollment',
            @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=30000;
        IF @result < 0 THROW 51000, 'Enrollment operation busy', 1;
        """, ct);

    public async Task CommitAsync(CancellationToken ct) { if (transaction is not null) await transaction.CommitAsync(ct); }
    public async ValueTask DisposeAsync() { if (transaction is not null) await transaction.DisposeAsync(); }
}

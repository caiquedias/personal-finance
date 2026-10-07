using Moq;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.Tests.Unit.Support;

/// <summary>
/// Captura as gravações de auditoria e a ordem relativa a CommitAsync (a trilha deve entrar
/// na MESMA transação da ação: AddAsync ANTES de CommitAsync) — #402.
/// </summary>
public sealed class AuditTrace
{
    public List<AuditLog> Logs { get; } = new();
    public List<string> Order { get; } = new();

    public static AuditTrace Track(Mock<IAuditLogRepository> audit, Mock<IUnitOfWork> uow)
    {
        var trace = new AuditTrace();
        audit.Setup(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
             .Callback<AuditLog, CancellationToken>((log, _) => { trace.Logs.Add(log); trace.Order.Add("audit"); })
             .Returns(Task.CompletedTask);
        uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
           .Callback<CancellationToken>(_ => trace.Order.Add("commit"))
           .Returns(Task.CompletedTask);
        return trace;
    }
}

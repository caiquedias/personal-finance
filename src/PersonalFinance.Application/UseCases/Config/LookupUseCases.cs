using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Config;

// IDs de seed que o sistema protege — nunca podem ser alterados ou removidos
file static class SeedIds
{
    // PaymentStatus: Pending=1, Paid=2, Cancelled=3, Partial=4
    public static readonly IReadOnlySet<int> PaymentStatus   = new HashSet<int> { 1, 2, 3, 4 };
    // SourceType: Parental=1, Personal=2
    public static readonly IReadOnlySet<int> SourceType      = new HashSet<int> { 1, 2 };
    // FortnightType: First=1, Second=2
    public static readonly IReadOnlySet<int> FortnightType   = new HashSet<int> { 1, 2 };
}

// ══════════════════════════════════════════════════════════════════════════════
// PAYMENT STATUS
// ══════════════════════════════════════════════════════════════════════════════

public sealed class GetPaymentStatusesUseCase
{
    private readonly IPaymentStatusRepository _repository;
    public GetPaymentStatusesUseCase(IPaymentStatusRepository repository) => _repository = repository;

    public async Task<IEnumerable<LookupResponseDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var items = await _repository.GetAllAsync(ct);
        return items.Select(x => new LookupResponseDto(
            x.Id, x.Name, x.Description,
            IsSystemSeed: SeedIds.PaymentStatus.Contains(x.Id)));
    }
}

public sealed class CreatePaymentStatusUseCase
{
    private readonly IPaymentStatusRepository _repository;
    private readonly IUnitOfWork              _uow;

    public CreatePaymentStatusUseCase(IPaymentStatusRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow        = uow;
    }

    public async Task<LookupResponseDto> ExecuteAsync(
        CreatePaymentStatusDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("O nome do status de pagamento é obrigatório.");

        if (await _repository.ExistsByNameAsync(dto.Name.Trim(), ct))
            throw new DomainException($"Já existe um status de pagamento com o nome '{dto.Name}'.");

        var nextId = await _repository.GetNextIdAsync(ct);
        var entity = new PaymentStatus
        {
            Id          = nextId,
            Name        = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty
        };

        await _repository.AddAsync(entity, ct);
        await _uow.CommitAsync(ct);

        return new LookupResponseDto(entity.Id, entity.Name, entity.Description, IsSystemSeed: false);
    }
}

public sealed class UpdatePaymentStatusUseCase
{
    private readonly IPaymentStatusRepository _repository;
    private readonly IUnitOfWork              _uow;

    public UpdatePaymentStatusUseCase(IPaymentStatusRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow        = uow;
    }

    public async Task<LookupResponseDto> ExecuteAsync(
        UpdatePaymentStatusDto dto, CancellationToken ct = default)
    {
        if (SeedIds.PaymentStatus.Contains(dto.Id))
            throw new DomainException("Itens de sistema não podem ser alterados.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("O nome do status de pagamento é obrigatório.");

        var entity = await _repository.GetByIdAsync(dto.Id, ct)
            ?? throw new DomainException("Status de pagamento não encontrado.");

        // Verifica unicidade apenas se o nome mudou
        if (!string.Equals(entity.Name, dto.Name.Trim(), StringComparison.OrdinalIgnoreCase)
            && await _repository.ExistsByNameAsync(dto.Name.Trim(), ct))
            throw new DomainException($"Já existe um status de pagamento com o nome '{dto.Name}'.");

        entity.Name        = dto.Name.Trim();
        entity.Description = dto.Description?.Trim() ?? string.Empty;

        await _repository.UpdateAsync(entity, ct);
        await _uow.CommitAsync(ct);

        return new LookupResponseDto(entity.Id, entity.Name, entity.Description, IsSystemSeed: false);
    }
}

public sealed class DeletePaymentStatusUseCase
{
    private readonly IPaymentStatusRepository _repository;
    private readonly IUnitOfWork              _uow;

    public DeletePaymentStatusUseCase(IPaymentStatusRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow        = uow;
    }

    public async Task ExecuteAsync(int id, CancellationToken ct = default)
    {
        if (SeedIds.PaymentStatus.Contains(id))
            throw new DomainException("Itens de sistema não podem ser excluídos.");

        var entity = await _repository.GetByIdAsync(id, ct)
            ?? throw new DomainException("Status de pagamento não encontrado.");

        await _repository.DeleteAsync(entity.Id, ct);
        await _uow.CommitAsync(ct);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// SOURCE TYPE
// ══════════════════════════════════════════════════════════════════════════════

public sealed class GetSourceTypesUseCase
{
    private readonly ISourceTypeRepository _repository;
    public GetSourceTypesUseCase(ISourceTypeRepository repository) => _repository = repository;

    public async Task<IEnumerable<LookupResponseDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var items = await _repository.GetAllAsync(ct);
        return items.Select(x => new LookupResponseDto(
            x.Id, x.Name, null,
            IsSystemSeed: SeedIds.SourceType.Contains(x.Id)));
    }
}

public sealed class CreateSourceTypeUseCase
{
    private readonly ISourceTypeRepository _repository;
    private readonly IUnitOfWork           _uow;

    public CreateSourceTypeUseCase(ISourceTypeRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow        = uow;
    }

    public async Task<LookupResponseDto> ExecuteAsync(
        CreateSourceTypeDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("O nome do tipo de fonte é obrigatório.");

        if (await _repository.ExistsByNameAsync(dto.Name.Trim(), ct))
            throw new DomainException($"Já existe um tipo de fonte com o nome '{dto.Name}'.");

        var nextId = await _repository.GetNextIdAsync(ct);
        var entity = new SourceType { Id = nextId, Name = dto.Name.Trim() };

        await _repository.AddAsync(entity, ct);
        await _uow.CommitAsync(ct);

        return new LookupResponseDto(entity.Id, entity.Name, null, IsSystemSeed: false);
    }
}

public sealed class UpdateSourceTypeUseCase
{
    private readonly ISourceTypeRepository _repository;
    private readonly IUnitOfWork           _uow;

    public UpdateSourceTypeUseCase(ISourceTypeRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow        = uow;
    }

    public async Task<LookupResponseDto> ExecuteAsync(
        UpdateSourceTypeDto dto, CancellationToken ct = default)
    {
        if (SeedIds.SourceType.Contains(dto.Id))
            throw new DomainException("Itens de sistema não podem ser alterados.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("O nome do tipo de fonte é obrigatório.");

        var entity = await _repository.GetByIdAsync(dto.Id, ct)
            ?? throw new DomainException("Tipo de fonte não encontrado.");

        if (!string.Equals(entity.Name, dto.Name.Trim(), StringComparison.OrdinalIgnoreCase)
            && await _repository.ExistsByNameAsync(dto.Name.Trim(), ct))
            throw new DomainException($"Já existe um tipo de fonte com o nome '{dto.Name}'.");

        entity.Name = dto.Name.Trim();

        await _repository.UpdateAsync(entity, ct);
        await _uow.CommitAsync(ct);

        return new LookupResponseDto(entity.Id, entity.Name, null, IsSystemSeed: false);
    }
}

public sealed class DeleteSourceTypeUseCase
{
    private readonly ISourceTypeRepository _repository;
    private readonly IUnitOfWork           _uow;

    public DeleteSourceTypeUseCase(ISourceTypeRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow        = uow;
    }

    public async Task ExecuteAsync(int id, CancellationToken ct = default)
    {
        if (SeedIds.SourceType.Contains(id))
            throw new DomainException("Itens de sistema não podem ser excluídos.");

        var entity = await _repository.GetByIdAsync(id, ct)
            ?? throw new DomainException("Tipo de fonte não encontrado.");

        await _repository.DeleteAsync(entity.Id, ct);
        await _uow.CommitAsync(ct);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// FORTNIGHT TYPE
// ══════════════════════════════════════════════════════════════════════════════

public sealed class GetFortnightTypesUseCase
{
    private readonly IFortnightTypeRepository _repository;
    public GetFortnightTypesUseCase(IFortnightTypeRepository repository) => _repository = repository;

    public async Task<IEnumerable<LookupResponseDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var items = await _repository.GetAllAsync(ct);
        return items.Select(x => new LookupResponseDto(
            x.Id, x.Name, null,
            IsSystemSeed: SeedIds.FortnightType.Contains(x.Id)));
    }
}

public sealed class CreateFortnightTypeUseCase
{
    private readonly IFortnightTypeRepository _repository;
    private readonly IUnitOfWork              _uow;

    public CreateFortnightTypeUseCase(IFortnightTypeRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow        = uow;
    }

    public async Task<LookupResponseDto> ExecuteAsync(
        CreateFortnightTypeDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("O nome do tipo de quinzena é obrigatório.");

        if (await _repository.ExistsByNameAsync(dto.Name.Trim(), ct))
            throw new DomainException($"Já existe um tipo de quinzena com o nome '{dto.Name}'.");

        var nextId = await _repository.GetNextIdAsync(ct);
        var entity = new FortnightType { Id = nextId, Name = dto.Name.Trim() };

        await _repository.AddAsync(entity, ct);
        await _uow.CommitAsync(ct);

        return new LookupResponseDto(entity.Id, entity.Name, null, IsSystemSeed: false);
    }
}

public sealed class UpdateFortnightTypeUseCase
{
    private readonly IFortnightTypeRepository _repository;
    private readonly IUnitOfWork              _uow;

    public UpdateFortnightTypeUseCase(IFortnightTypeRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow        = uow;
    }

    public async Task<LookupResponseDto> ExecuteAsync(
        UpdateFortnightTypeDto dto, CancellationToken ct = default)
    {
        if (SeedIds.FortnightType.Contains(dto.Id))
            throw new DomainException("Itens de sistema não podem ser alterados.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("O nome do tipo de quinzena é obrigatório.");

        var entity = await _repository.GetByIdAsync(dto.Id, ct)
            ?? throw new DomainException("Tipo de quinzena não encontrado.");

        if (!string.Equals(entity.Name, dto.Name.Trim(), StringComparison.OrdinalIgnoreCase)
            && await _repository.ExistsByNameAsync(dto.Name.Trim(), ct))
            throw new DomainException($"Já existe um tipo de quinzena com o nome '{dto.Name}'.");

        entity.Name = dto.Name.Trim();

        await _repository.UpdateAsync(entity, ct);
        await _uow.CommitAsync(ct);

        return new LookupResponseDto(entity.Id, entity.Name, null, IsSystemSeed: false);
    }
}

public sealed class DeleteFortnightTypeUseCase
{
    private readonly IFortnightTypeRepository _repository;
    private readonly IUnitOfWork              _uow;

    public DeleteFortnightTypeUseCase(IFortnightTypeRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow        = uow;
    }

    public async Task ExecuteAsync(int id, CancellationToken ct = default)
    {
        if (SeedIds.FortnightType.Contains(id))
            throw new DomainException("Itens de sistema não podem ser excluídos.");

        var entity = await _repository.GetByIdAsync(id, ct)
            ?? throw new DomainException("Tipo de quinzena não encontrado.");

        await _repository.DeleteAsync(entity.Id, ct);
        await _uow.CommitAsync(ct);
    }
}

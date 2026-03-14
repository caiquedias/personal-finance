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

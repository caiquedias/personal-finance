using PersonalFinance.Domain.Entities.Shared;
using PersonalFinance.Domain.Exceptions;
using System.Text.RegularExpressions;

namespace PersonalFinance.Domain.Entities.Config;

/// <summary>
/// Categoria de despesa.
/// Pode ser global (UserId nulo, criada pelo admin, visível a todos)
/// ou pertencente a um usuário específico.
/// Cor obrigatoriamente no formato hex #RRGGBB.
/// </summary>
public sealed class Category : EntityBase
{
    private static readonly Regex HexColorRegex = new(
        @"^#[0-9A-Fa-f]{6}$",
        RegexOptions.Compiled);

    // ── Propriedades ──────────────────────────────────────────────────────────

    /// <summary>
    /// Dono da categoria.
    /// NULL = categoria global criada pelo admin.
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>Nome da categoria.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Cor de representação no frontend. Formato: #RRGGBB.</summary>
    public string Color { get; private set; } = default!;

    /// <summary>Identificador de ícone (ex: "home", "car"). Nullable.</summary>
    public string? Icon { get; private set; }

    /// <summary>
    /// Indica que a categoria é visível para todos os usuários.
    /// Categorias globais só podem ser criadas por admins.
    /// </summary>
    public bool IsGlobal { get; private set; }

    // ── EF Core ───────────────────────────────────────────────────────────────
    private Category() { }

    // ── Factories ─────────────────────────────────────────────────────────────

    /// <summary>Cria uma categoria pertencente a um usuário específico.</summary>
    public static Category Create(string name, string color, string? icon, Guid userId)
    {
        if (userId == Guid.Empty)
            throw new DomainException("O UserId da categoria é obrigatório.");

        ValidateName(name);
        ValidateColor(color);

        return new Category
        {
            Name     = name.Trim(),
            Color    = color.Trim(),
            Icon     = icon?.Trim(),
            UserId   = userId,
            IsGlobal = false
        };
    }

    /// <summary>
    /// Cria uma categoria global visível para todos os usuários.
    /// UserId é nulo — pertence ao sistema.
    /// </summary>
    public static Category CreateGlobal(string name, string color, string? icon)
    {
        ValidateName(name);
        ValidateColor(color);

        return new Category
        {
            Name     = name.Trim(),
            Color    = color.Trim(),
            Icon     = icon?.Trim(),
            UserId   = null,
            IsGlobal = true
        };
    }

    // ── Comportamentos ────────────────────────────────────────────────────────

    /// <summary>Atualiza nome, cor e ícone da categoria.</summary>
    public void Update(string name, string color, string? icon)
    {
        ValidateName(name);
        ValidateColor(color);

        Name  = name.Trim();
        Color = color.Trim();
        Icon  = icon?.Trim();
        SetUpdatedAt();
    }

    // ── Validações privadas ───────────────────────────────────────────────────

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O nome da categoria é obrigatório.");

        if (name.Trim().Length > 100)
            throw new DomainException("O nome da categoria não pode exceder 100 caracteres.");
    }

    private static void ValidateColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
            throw new DomainException("A cor da categoria é obrigatória.");

        if (!HexColorRegex.IsMatch(color.Trim()))
            throw new DomainException(
                $"A cor '{color}' não é válida. Use o formato hexadecimal #RRGGBB.");
    }
}

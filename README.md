# Personal Finance System

> **MonkeyBomb** · Caique Dias · Desenvolvedor Sênior

## Stack

- **Backend:** .NET 8 · Web API
- **Frontend:** Angular 21 · Angular CDK · Tailwind CSS
- **Gráficos:** ECharts + ApexCharts
- **Banco:** SQL Server · On-premise · Windows 11
- **ORM:** EF Core (code-first · Migrations)
- **Auth:** JWT + Argon2id
- **Arquitetura:** DDD + Clean Architecture

## Estrutura

```
PersonalFinance.sln
├── src/
│   ├── PersonalFinance.Domain          # Entidades, EntityBase, interfaces
│   ├── PersonalFinance.Application     # Use cases, DTOs, FluentValidation, MediatR
│   ├── PersonalFinance.Infrastructure  # EF Core, DbContext, Migrations, Auth
│   └── PersonalFinance.Api             # Controllers /api/v1, middlewares, DI
└── tests/
    ├── PersonalFinance.Domain.Tests
    ├── PersonalFinance.Application.Tests
    └── PersonalFinance.Api.Tests
```

## Regra de dependência

`Api → Infrastructure → Application → Domain`  
`Domain` não referencia nenhum projeto interno.

## Configuração de ambiente

`JwtSettings:SecretKey` não vem mais hardcoded em `appsettings.json` — precisa ser configurado por fora.

**Dev local — .NET User Secrets:**

```bash
dotnet user-secrets init --project src/PersonalFinance.Api
dotnet user-secrets set "JwtSettings:SecretKey" "<secret-forte-256-bits>" --project src/PersonalFinance.Api
```

Gerar um secret forte: `openssl rand -base64 32`.

**Homolog/Produção (Render):** configurar a variável de ambiente `JwtSettings__SecretKey` (double underscore — convenção do .NET para seções aninhadas) no painel de environment variables do serviço.

> **Segurança:** rotacionar o secret invalida todas as sessões JWT ativas — coordenar janela de deploy.

**MFA (TOTP):** `Auth:Mfa:EncryptionKey` (Base64 de exatamente 32 bytes — `openssl rand -base64 32`) cifra o secret TOTP em repouso (AES-256-GCM) e é validada no startup (a aplicação não sobe sem ela). Dev: `dotnet user-secrets set "Auth:Mfa:EncryptionKey" "<base64-32-bytes>" --project src/PersonalFinance.Api`; Render: variável `Auth__Mfa__EncryptionKey`. A flag `Auth:Mfa:Enforce` (default `false`) controla se o login exige o 2º fator.

> **Segurança:** trocar a `EncryptionKey` torna ilegíveis os secrets TOTP já gravados — os usuários precisariam refazer o setup do MFA.

## Documentação

Ver `PersonalFinance_Contexto.md` para contexto técnico completo.

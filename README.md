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

## Documentação

Ver `PersonalFinance_Contexto.md` para contexto técnico completo.

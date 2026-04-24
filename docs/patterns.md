# Padrões Angular

## Modal (validado)

```typescript
// Template
@if (modalOpen()) {
  <div class="modal-overlay" @backdropAnim (click)="closeModal()"></div>
  <div class="modal-center" @modalAnim>
    <app-sonic-modal [title]="..." (closed)="closeModal()">
      <!-- conteúdo -->
    </app-sonic-modal>
  </div>
}

// Animations no componente
animations: [
  trigger('backdropAnim', [
    transition(':enter', [style({opacity:0}), animate('200ms ease', style({opacity:1}))]),
    transition(':leave', [animate('180ms ease', style({opacity:0}))])
  ]),
  trigger('modalAnim', [
    transition(':enter', [
      style({opacity:0, transform:'scale(0.88) translateY(-16px)'}),
      animate('260ms cubic-bezier(0.34,1.56,0.64,1)',
        style({opacity:1, transform:'scale(1) translateY(0)'}))
    ]),
    transition(':leave', [
      animate('180ms ease-in',
        style({opacity:0, transform:'scale(0.93) translateY(10px)'}))
    ])
  ])
]
```

## Armadilhas conhecidas

| Erro | Causa | Fix |
|------|-------|-----|
| Dois providers EF Core no teste | `IDbContextOptionsConfiguration<T>` persiste | Remover por `GenericTypeArguments.Contains(typeof(AppDbContext))` |
| `async` com `ref int` | C# não permite ref em async | Retornar tuple `(Guid id, bool wasCreated)` |
| `InvalidCastException Int16→Int32` | `PeriodSummaryRaw.Year` como `int` | Usar `short` (smallint) e `byte` (tinyint) |
| `NG8004 No pipe 'number'` | `DecimalPipe` não importado | Adicionar ao `imports[]` do componente |
| Namespace duplicado controller | Dois arquivos com mesma classe | Substituir, nunca adicionar segundo |
| Builder Angular errado | `@angular-devkit/build-angular:application` | Usar `@angular/build:application` |
| TypeScript incompatível com Angular 21 | `~5.6.0` | Usar `~5.9.0` |
| Node.js antigo com Vite | `require()` de ES Module | Node.js ≥ 20 obrigatório |
| `TemplatePortalDirective` não exportado | Removido do CDK | Remover o import |
| Animação não dispara via CDK Portal | `:enter`/`:leave` exigem `@if` | Usar modal inline com `@if` + Angular Animations |
| Interface reescrita perde métodos | ZIP sobrescreve sem consultar código atual | Nunca reescrever interfaces — sempre `str_replace` |
| `MarkAsPaid` rejeita data futura | Validação na entidade | Usar `UtcNow.AddDays(-1)` nos testes |
| `HasData` não popula InMemory | EF Core HasData é SQL only | `SeedLookupData()` manual na factory |
| Produto cartesiano na `vw_PeriodSummary` | JOIN duplo Income + Expense | Subconsultas separadas por entidade |
| B4/C4 sem fórmula no parser Excel | Planilha salva sem fórmulas | Fallback via `TryGetValue()` na célula |

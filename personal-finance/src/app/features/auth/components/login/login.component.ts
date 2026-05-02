import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AuthService } from '../../../../core/auth/auth.service';

const QUOTES: string[] = [
  'A economia é muito maior se eu não comprar.',
  'Minha estratégia financeira? Rezar pro Pix cair.',
  'Classe média: pobre demais pra investir, rico demais pra reclamar.',
  'Juro composto: a única coisa que cresce mais rápido que minhas dívidas.',
  'Guardei dinheiro por 3 meses. Aí o carro quebrou.',
  'Meu plano de aposentadoria é trabalhar até morrer ou ganhar na Mega.',
  'Inflação: a arte de ser mais pobre sem gastar nada extra.',
  'Fundo de emergência: aquele dinheiro que virou emergência antes de chegar a fundo.',
  'O mercado financeiro caiu. Meu salário já estava no chão.',
  'Economizei no café da manhã pra poder pagar o Pix da festa.',
  'Ascensão de classe: trocar de banco mas continuar no cheque especial.',
  'Minha carteira de investimentos tem mais taxa do que saldo.',
  'O Brasil tem a melhor taxa de juros do mundo. Infelizmente pra quem deve.',
  'Cortar assinatura de streaming não vai pagar minha aposentadoria, mas me faz sentir bem.',
  'Meu patrimônio líquido é um número negativo e eu não sei nada de contabilidade.',
  'Avocado toast não é o motivo de você não ter apartamento. A prestação, sim.',
  'Viver dentro do orçamento é incrível. Preciso tentar um dia.',
  'Cartão de crédito: dinheiro do mês que vem resolvendo os problemas do mês passado.',
  'Diversifiquei minha carteira: devo em 3 bancos diferentes.',
  'A riqueza é passada de geração em geração. Na minha família passaram batido.',
  'Meu planejamento financeiro é um Excel com uma única fórmula: =ESPERANÇA()',
  'Nunca gaste mais do que você ganha. Dito por alguém que claramente nunca foi ao mercado.',
  'Poupança: rendimento de 0,5% ao mês, inflação de 0,8%. É quase um investimento.',
  'Reduzi meus gastos supérfluos. Agora sofro de forma mais barata.',
  'Meu sonho é ter problemas de rico. Por enquanto, só tenho os de pobre.',
  'Educação financeira deveria ser obrigatória nas escolas. Mas aí quem pagaria o rotativo?',
  'Aprendi a diferença entre ativo e passivo. Tudo que eu tenho é passivo.',
  'Consultei um planejador financeiro. Ele ficou triste por mim.',
  'Quando dizem "compre na baixa", eles não sabem que minha conta já estava baixíssima.',
  'Bitcoin caiu 80%. Meu salário não sobe nem 8%.',
  'Fiz um orçamento anual. Durou até a segunda semana de janeiro.',
  'A melhor forma de dobrar seu dinheiro é dobrá-lo e guardar no bolso.',
  'Taxa Selic subiu? Ótimo, meu banco vai me cobrar mais pelo empréstimo.',
  'Vivo de salário a salário. Às vezes nem isso.',
  'Planilha financeira: uma obra de arte que ninguém vai atualizar.',
  'Minha renda passiva é o sono entre um trabalho e outro.',
  'Criptomoedas: a oportunidade única de perder dinheiro de forma descentralizada.',
  'Reserve 6 meses de despesas para emergências. Que graça, não tenho nem 6 dias.',
  'Terceiro mundo: onde você paga imposto de primeiro mundo com salário de quinto.',
  'O segredo dos ricos é simples: nascer rico.',
  'Previdência privada: pague agora para descobrir em 30 anos que não deu certo.',
  'Meu padrão de vida melhorou. Agora sei exatamente o quanto não posso pagar.',
  'Economia comportamental: a ciência de provar que você sempre vai tomar a decisão errada com dinheiro.',
  'Herdei uma fortuna do meu pai: R$ 200 em trocados e uma dívida no SPC.',
  'Todo rico que "começou do zero" esqueceu de mencionar os zeros à direita da família.',
  'Meu dinheiro some mais rápido que promessa de político em ano de eleição.',
  'Liberdade financeira: o Instagram tá cheio de gente vendendo como chegar lá por 12 parcelas.',
  'Gastar menos do que ganha parece fácil até o mês ter 31 dias.',
  'Bolsa de valores: onde adultos apostam dinheiro e chamam de "estratégia".',
  'Transferi pra poupança. O banco agradeceu pelos centavos de rendimento.',
];

const QUOTES_STORAGE_KEY = 'login_quotes_usage';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent {
  private readonly auth   = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb     = inject(FormBuilder);

  readonly loading      = signal(false);
  readonly apiError     = signal<string | null>(null);
  readonly showPassword = signal(false);
  readonly currentQuote = signal(this.pickQuote());

  readonly form = this.fb.group({
    email:    ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  pickQuote(): string {
    const raw = sessionStorage.getItem(QUOTES_STORAGE_KEY);
    let usage: Record<number, number> = raw ? JSON.parse(raw) : {};

    let available = QUOTES.map((_, i) => i).filter(i => (usage[i] ?? 0) < 3);

    if (available.length === 0) {
      usage = {};
      available = QUOTES.map((_, i) => i);
    }

    const idx = available[Math.floor(Math.random() * available.length)];
    usage[idx] = (usage[idx] ?? 0) + 1;
    sessionStorage.setItem(QUOTES_STORAGE_KEY, JSON.stringify(usage));
    return QUOTES[idx];
  }

  showError(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  getEmailError(): string {
    const ctrl = this.form.get('email');
    if (ctrl?.hasError('required')) return 'E-mail é obrigatório';
    if (ctrl?.hasError('email'))    return 'E-mail inválido';
    return '';
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.apiError.set(null);

    const { email, password } = this.form.getRawValue();

    this.auth.login({ email: email!, password: password! }).subscribe({
      next: () => this.router.navigate(['/']),
      error: (err) => {
        this.loading.set(false);
        this.apiError.set(err.error?.message ?? 'Credenciais inválidas.');
      }
    });
  }
}

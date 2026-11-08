# Frontend — estoque e notas fiscais

Interface Angular 22 do desafio Korp. Permite cadastrar e editar produtos, ajustar saldos com motivo, criar notas com múltiplos produtos e imprimir notas abertas. Os dados vêm das duas APIs e são persistidos nos bancos do backend.

## Executar localmente

Use Node.js 24.15 ou superior da linha 24 e npm. O projeto mantém as versões resolvidas no `package-lock.json`.

Na raiz do repositório:

```powershell
cd frontend
npm ci
npm start
```

Abra `http://localhost:4200`. Para cadastros e consultas reais, inicie também os dois serviços conforme o [README do backend](../README.md#executar). Eles usam SDK .NET 10, SQL Server LocalDB e os scripts SQL do repositório.

| Aplicação | Endereço local |
|---|---|
| Angular | `http://localhost:4200` |
| StockService | `http://localhost:5213` |
| BillingService | `http://localhost:5048` |

As URLs das APIs estão em [src/environments/environment.ts](src/environments/environment.ts). O navegador chama os serviços diretamente; o CORS do backend permite a origem `http://localhost:4200`.

## Telas e comportamento

| Rota | Funcionalidade |
|---|---|
| `/produtos` | Busca por código ou descrição e paginação |
| `/produtos/novo` | Código, descrição e saldo inicial |
| `/produtos/:id/editar` | Edição e ajuste de saldo com motivo |
| `/notas` | Lista paginada com filtros de número e status |
| `/notas/nova` | Seleção de até 100 produtos distintos e suas quantidades |
| `/notas/:id` | Itens, status e impressão |

A criação da nota envia somente IDs de produtos e quantidades. O Billing atribui a numeração sequencial e o status `Open`; o saldo exibido na seleção é informativo, pois a criação não reserva estoque.

Na impressão, o frontend mantém o indicador de processamento enquanto o Billing confirma a baixa no Stock e fecha a nota. Após receber a nota `Closed`, aguarda a renderização e abre o diálogo de impressão do navegador. O CSS de impressão apresenta a nota em A4 e oculta a navegação e os controles. Cancelar esse diálogo não reabre a nota nem devolve estoque.

Se a impressão falhar, a interface consulta uma vez o estado atual da nota. Uma nova tentativa exige estado confirmado como aberto; notas fechadas não permitem outra solicitação de impressão. Se a criação perder a resposta, o formulário orienta consultar a lista antes de iniciar outro cadastro.

### Sugestão de descrição por IA

O cadastro e a edição de produtos oferecem **Melhorar descrição** para textos de 1 a 200 caracteres. A interface chama somente `POST /api/products/improve-description` no StockService, com `{ description }`, e apresenta o resultado sem modificar o formulário. **Usar sugestão** preenche o campo; **Descartar** mantém o texto original. O usuário ainda precisa confirmar o formulário para salvar o produto.

Enquanto a sugestão é gerada, novos pedidos ficam bloqueados e o campo continua editável. Uma edição invalida o resultado anterior e impede que uma resposta atrasada seja aplicada a um texto diferente. Falhas de geração aparecem junto ao campo, sem bloquear o cadastro ou exigir recarga do produto.

A chave e o modelo são configurados exclusivamente no backend, conforme o [README do projeto](../README.md#melhorar-descrições-com-ia-opcional). Sem configuração, apenas a melhoria por IA fica indisponível. A descrição é enviada ao OpenRouter/NVIDIA; endpoints gratuitos podem registrar o uso, portanto não envie dados pessoais ou confidenciais. [Detalhes do modelo](https://openrouter.ai/nvidia/nemotron-3.5-lightning%3Afree/pricing).

## Build e testes unitários

Execute dentro de `frontend`:

```powershell
npm run build
npm run test:ci
```

`npm test` mantém os testes em modo de desenvolvimento. A suíte usa Vitest, jsdom, TestBed e substitutos dos serviços; os testes HTTP usam as ferramentas de teste do Angular. São cobertos contratos, validações, concorrência de edição, bloqueio de envios duplicados e recuperação do estado da nota. A melhoria de descrição cobre apresentação, aceitação e descarte da sugestão, edições durante o pedido e falhas sem impedir o cadastro. Esses testes não iniciam APIs, chamam a IA real nem comprovam persistência em SQL Server.

A validação com serviços e bancos reais segue o [roteiro manual](../spec/validacao-manual.md).

## Organização e interface

`src/app/features/products` e `src/app/features/invoices` reúnem páginas, modelos e serviços HTTP por funcionalidade. `src/app/shared` contém cabeçalhos, estados de carregamento/erro, paginação, ícones, validações e tratamento de erros. As rotas carregam cada página sob demanda.

Os componentes são standalone e usam `OnPush`, signals para estado local e Reactive Forms. RxJS coordena buscas e chamadas HTTP. Angular Material fornece paginação e indicadores de carregamento; Lucide fornece os ícones SVG.

A skill `frontend-design` orientou a direção Swiss: branco `#FFFFFF`, cinza neutro `#F7F7F8`, azul `#002FA7`, Helvetica Neue/Helvetica/Arial, linhas finas e contagens reais destacadas. O layout se adapta à largura da tela, mantém foco visível e respeita a preferência por movimento reduzido.

Veja o [detalhamento técnico](../spec/detalhamento-tecnico.md) para os tópicos exigidos na apresentação do desafio.

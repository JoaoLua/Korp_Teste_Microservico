# Detalhamento técnico — estoque e faturamento

Este documento descreve a implementação para os tópicos de apresentação dos [requisitos do desafio](requisitos.md). As instruções de execução estão no [README do projeto](../README.md) e no [README do frontend](../frontend/README.md).

## Arquitetura e fluxo principal

O Angular conversa por HTTP/JSON com dois microsserviços ASP.NET Core em .NET 10. O StockService controla produtos, saldos e movimentações; o BillingService controla notas e seus itens. Cada serviço tem seu próprio banco SQL Server e as camadas Domain, Application, Infrastructure e API.

O cadastro da nota envia produtos e quantidades ao Billing, que consulta o Stock para registrar código e descrição dos itens e gera a numeração sequencial. Na impressão, o Billing solicita a baixa ao Stock e somente fecha a nota após a confirmação. O frontend recebe o estado fechado e abre a impressão do navegador. A criação da nota não reserva estoque.

## Angular: componentes, estado e ciclo de vida

O frontend usa Angular 22 com componentes standalone e rotas carregadas sob demanda. A execução é zoneless, sem `zone.js`. Os componentes usam `ChangeDetectionStrategy.OnPush`, signals para dados e indicadores de operação, e `computed` para valores derivados, como a possibilidade de imprimir uma nota.

Os pontos de ciclo de vida efetivamente utilizados são:

- `ngOnInit`: dispara consultas iniciais na edição do produto, na lista e no cadastro de notas e nos detalhes da nota.
- `DestroyRef` com `takeUntilDestroyed`: encerra inscrições quando o componente é destruído. A limpeza das inscrições é feita por esse mecanismo, sem implementar `ngOnDestroy` manualmente.
- `afterNextRender`: em `InvoiceDetail`, aguarda a atualização da tela com a nota fechada antes de chamar `BrowserPrint`, que encapsula `window.print()`.

O componente de ícones usa `effect` para atualizar o SVG quando seu nome muda. Reactive Forms fornece formulários tipados; no cadastro da nota, um `FormArray` guarda produtos distintos e quantidades inteiras positivas. Validações também são executadas no backend.

## RxJS e bibliotecas visuais

`HttpClient` retorna Observables. Nas buscas de produtos, `debounceTime(300)` aguarda uma pausa de digitação e `distinctUntilChanged` evita consultas iguais consecutivas. `switchMap` troca a consulta ativa, impedindo que uma resposta antiga substitua o resultado da busca atual. `catchError` transforma falhas em feedback de tela e `finalize` encerra indicadores nos fluxos que o utilizam.

As operações de cadastro e impressão bloqueiam novos envios enquanto estão pendentes. Não há repetição automática dos POSTs. Uma falha de impressão gera uma consulta GET do estado da nota para orientar a próxima ação do usuário.

Angular Material é usado no paginador e no indicador de carregamento. O paginador traduz o índice visual de página, iniciado em zero, para o contrato das APIs, iniciado em um. Lucide fornece os ícones SVG. Os demais elementos são HTML semântico com SCSS: tabelas, formulários, botões, avisos e layout responsivo.

A skill `frontend-design` orientou o visual Swiss: superfícies `#FFFFFF` e `#F7F7F8`, azul `#002FA7`, Helvetica Neue/Helvetica/Arial e divisórias de 1 px. Contagens reais ganham destaque tipográfico; telas vazias e de erro não usam dados fictícios. CSS de impressão oculta controles, configura A4 e preserva a leitura dos itens.

## Backend, persistência e dependências

O backend usa ASP.NET Core para controllers, injeção de dependência, CORS, tratamento de exceções e OpenAPI. Entity Framework Core, com o provedor SQL Server, faz o mapeamento e acesso aos dados. O Billing usa um cliente `HttpClient` para consultar produtos e solicitar baixas no Stock.

Os pacotes C# são gerenciados pelo NuGet nos arquivos `.csproj`; o frontend usa npm e `package-lock.json`. A implementação não utiliza Go. Os scripts SQL em `scripts/` são a fonte do esquema; o startup das APIs não cria nem altera tabelas.

As transações são locais a cada banco. A baixa de estoque persiste saldos, movimentações e confirmação da operação em conjunto. O identificador da nota reconhece uma baixa já concluída e evita novo desconto quando o usuário repete a impressão após uma falha. A numeração é atualizada na mesma transação que cria a nota. Edições e ajustes de produtos usam `rowVersion` para identificar alterações concorrentes.

## Melhoria opcional de descrições

O recurso foi acrescentado após o fluxo obrigatório, por solicitação do usuário. `ProductApi` envia a descrição para `POST /api/products/improve-description` no StockService. O controller chama `DescriptionImprovementService`, que valida entrada e saída de 1 a 200 caracteres e depende da interface `IDescriptionImprover`. Na Infrastructure, `OpenRouterDescriptionImprover` usa `HttpClient` para chamar `/api/v1/chat/completions` do OpenRouter, sem SDK adicional ou acesso ao banco.

A instrução pede revisão de clareza e ortografia em português, preservando as características informadas e sem inventar informações. O modelo é `nvidia/nemotron-3.5-lightning:free`; a requisição limita `provider.max_price` a zero para entrada, saída e chamada. Não há fallback pago nem retry automático. A chave só existe na configuração do backend; o [README](../README.md#melhorar-descrições-com-ia-opcional) descreve User Secrets e variáveis de ambiente.

O frontend usa signals separados para carregamento, sugestão e erro da IA. O resultado é exibido como texto, nunca como HTML, e só substitui o campo ao clicar em **Usar sugestão**. O banco é alterado apenas ao salvar o formulário. `valueChanges` invalida a sugestão ao editar; uma revisão numérica também descarta respostas de pedidos anteriores, inclusive quando o usuário altera o campo e depois volta ao texto original. `takeUntilDestroyed` encerra a inscrição ao sair da tela.

O timeout padrão é de 30 segundos, limitado entre 5 e 60. Respostas incompletas, formato inesperado ou descrições vazias/longas são recusadas com 502. Falta de chave ou modelo, limites de uso, erro de rede e timeout geram 503 na melhoria de descrição. Essas falhas preservam o texto original e não ativam o bloqueio de gravação incerta usado pelo cadastro.

A descrição é enviada ao OpenRouter/NVIDIA. Endpoints gratuitos podem registrar o uso e não devem receber informações pessoais ou confidenciais; esse aviso também aparece no formulário. [Preço e condições do endpoint](https://openrouter.ai/nvidia/nemotron-3.5-lightning%3Afree/pricing).

## Uso de LINQ

LINQ aparece em operações concretas do projeto:

- `StockStore`: `Where` busca produtos por código/descrição; `OrderBy`, `Skip` e `Take` ordenam e paginam; `Select` cria as respostas.
- `BillingStore`: filtros de número/status, carregamento dos itens e paginação das notas; `ToDictionary` associa produtos consultados aos dados registrados em cada item.
- `InvoiceService` e `StockDeductionService`: `GroupBy` consolida produtos repetidos, `Sum` soma quantidades e `OrderBy` mantém uma ordem estável.

As consultas baseadas no EF são executadas no SQL Server; a consolidação dos itens recebidos e a montagem das respostas também usam LINQ em memória.

## Erros e recuperação

As APIs centralizam exceções em implementações de `IExceptionHandler` e retornam `ProblemDetails`, incluindo `code` e `traceId`. Erros de validação retornam 400, recurso inexistente 404, conflitos 409, saldo insuficiente 422 e indisponibilidade conhecida 503. Falhas inesperadas retornam 500 com mensagem genérica e detalhes registrados nos logs.

O frontend interpreta `detail`, mensagens de validação e `title` quando ele contém uma mensagem legível. Também diferencia falha de conexão de respostas HTTP e associa os erros de campos ao formulário de produto. Um conflito de `rowVersion` exige recarregar os dados e revisar a edição.

Após erro de impressão, a interface consulta o estado atual uma vez: nota fechada mantém o bloqueio; nota aberta permite tentativa manual após resolver a causa; estado desconhecido exige nova consulta. Isso evita assumir que uma resposta perdida significa que a baixa não aconteceu. Cancelar o diálogo físico de impressão não altera a operação já concluída no backend.

## Verificação e apresentação

O frontend usa testes unitários com Vitest, jsdom e TestBed; substitutos dos serviços isolam os componentes, e ferramentas HTTP do Angular verificam os contratos sem acessar APIs reais. O backend usa xUnit e fakes para regras de aplicação, além de um `HttpMessageHandler` falso para verificar o cliente OpenRouter e respostas de erro sem acessar a rede. Os comandos são `npm run test:ci`, dentro de `frontend`, e `dotnet test KorpBackend.slnx`, na raiz.

Após a inclusão da IA, os builds passaram e foram aprovados 52 testes no frontend e 55 no backend (42 no Stock e 13 no Billing). Uma chamada real ao endpoint de melhoria também retornou uma sugestão válida com o modelo configurado, sem gravar um produto.

A demonstração deve cobrir cadastro de produto, nota com múltiplos itens, impressão com atualização do saldo, bloqueio da nota fechada e recuperação após indisponibilidade do Stock. O [roteiro manual](validacao-manual.md) descreve a conferência com os serviços e bancos reais. Testes unitários não substituem essa verificação de persistência e comunicação.

Como complemento, demonstre **Melhorar descrição**, revise o texto e aceite ou descarte a sugestão. A disponibilidade do provedor gratuito não é requisito para executar o restante da apresentação.

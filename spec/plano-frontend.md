# Plano de execução do frontend

Atualizado em 09/09/2026. Status: implementado em `frontend/`. As etapas abaixo permanecem como referência de organização e critérios de conclusão. Consulte o [registro de validação](validacao-frontend.md) e o [detalhamento técnico](detalhamento-tecnico.md).

## 1. Objetivo e limites

Entregar uma interface Angular simples, organizada e fácil de explicar em um desafio para vaga júnior. A prioridade é concluir o fluxo obrigatório: cadastrar produtos, criar notas com vários itens, imprimir/fechar uma nota e demonstrar falha e recuperação entre os serviços.

Os [requisitos oficiais](requisitos.md) continuam sendo a referência. Este plano e a [especificação técnica](spec.md) podem ser ajustados quando identificarmos inconsistências ou oportunidades de simplificação, sem alterar os requisitos obrigatórios.

O plano original deixou de fora autenticação, dashboard, NgRx, microfrontends, SSR, filas, polling, testes E2E, IA, geração de PDF no backend e regras fiscais reais. Depois de concluir o fluxo obrigatório, a IA foi acrescentada por solicitação do usuário, conforme o complemento na seção 6. Os demais limites permanecem. Também não inventar preços, impostos ou totais monetários: esses dados não existem nos contratos atuais.

## 2. Base técnica adotada

| Escolha | Aplicação no projeto |
|---|---|
| Angular 22, componentes standalone e Router | Aplicação em `frontend/`, organizada por produtos e notas, sem módulos ou camadas extras desnecessárias |
| Reactive Forms tipados | Validação dos cadastros e `FormArray` para os itens da nota |
| HttpClient e RxJS | Serviços HTTP tipados; busca de produtos e tratamento de respostas |
| Angular Material e SCSS | Uma única biblioteca visual para formulários, tabelas, paginação, diálogos e feedback |
| Vitest e ferramentas de teste do Angular | Testes unitários, sem iniciar APIs nem conectar ao banco |

Angular 22 foi adotado, com versões compatíveis de Angular, CLI e Material e dependências resolvidas no `package-lock.json`. Os comandos usam o CLI local. Node `v24.19.0` e npm `11.17.0` estão disponíveis; o Node atende à faixa documentada para Angular 22. Referência: [compatibilidade oficial](https://angular.dev/reference/versions).

Reactive Forms atende aos formulários sem acrescentar outra biblioteca. Para os testes, aproveitar o Vitest usado nos novos projetos do CLI. Referências: [Reactive Forms](https://angular.dev/guide/forms/reactive-forms) e [testes no Angular](https://angular.dev/guide/testing).

Usar estado local nos componentes, serviços HTTP pequenos e funções auxiliares somente quando houver reutilização real. RxJS pode cuidar da busca com `debounceTime`, `distinctUntilChanged` e `switchMap` em consultas GET; não cancelar/substituir gravações dessa forma nem aplicar retry automático em POST/PUT. Usar `AsyncPipe` ou descarte de inscrições quando necessário, evitando assinaturas permanentes.

**Ambiente verificado:** o SDK .NET 10.0.401 voltou a estar disponível. O backend compilou e a integração foi conferida com SQL Server LocalDB em bancos temporários de validação. Os bancos StockDb e BillingDb foram preservados.

**Direção visual:** a skill frontend-design orientou o estilo Swiss, com branco, cinza neutro, azul `#002FA7`, Helvetica e linhas finas. As contagens reais e o número da nota aparecem em destaque. Material é usado no paginador e no carregamento; Lucide fornece os ícones, e os formulários/tabelas usam HTML semântico com SCSS.

## 3. Telas e organização

Interface em português, com navegação principal **Produtos / Notas**, formulários com rótulos claros, mensagens próximas aos campos e estados de carregamento, lista vazia e erro. Usar texto além de cores para distinguir “Aberta” e “Fechada”, manter navegação por teclado e adaptar formulários/tabelas para telas menores.

| Rota | Conteúdo |
|---|---|
| `/produtos` | Lista, pesquisa por código/descrição, paginação e acesso ao cadastro/edição |
| `/produtos/novo` | Cadastro com código, descrição e saldo inicial |
| `/produtos/:id/editar` | Edição do cadastro; não altera saldo |
| `/notas` | Lista com número, status, data e filtros |
| `/notas/nova` | Seleção de vários produtos, quantidades e remoção de itens antes de salvar |
| `/notas/:id` | Detalhes, situação da nota, impressão e feedback de falhas |

Redirecionar a rota inicial para produtos e apresentar uma página simples para rotas não encontradas. Na organização de arquivos, usar `features/products` e `features/invoices`, cada uma com seus componentes, modelos e serviço HTTP. Reservar `shared` apenas para elementos realmente usados nas duas funcionalidades, como leitura de erros de API. URLs ficam na configuração de ambiente, não espalhadas nos componentes.

## 4. Comunicação com os serviços

O Angular acessa os dois serviços por HTTP. Ele consulta/cadastra produtos no Stock e consulta/cria/imprime notas no Billing. **Somente o Billing solicita a baixa ao Stock.** A interface não acessa bancos nem chama `/api/stock/deduct`.

Desenvolvimento local: Angular em `http://localhost:4200`, Stock em `http://localhost:5213` e Billing em `http://localhost:5048`. O CORS existente já permite essa origem do Angular. Usar inicialmente as URLs HTTP dos perfis locais e duas configurações de URL-base; não precisamos introduzir gateway ou proxy.

| Serviço | Chamadas usadas na primeira entrega |
|---|---|
| Stock | `GET /api/products`, `GET /api/products/{id}`, `POST /api/products`, `PUT /api/products/{id}` |
| Billing | `GET /api/invoices`, `GET /api/invoices/{id}`, `POST /api/invoices`, `POST /api/invoices/{id}/print` |

Os DTOs usam JSON `camelCase`, IDs como strings GUID e datas UTC. Mostrar datas no horário local do navegador e traduzir apenas os rótulos dos status, mantendo `Open`/`Closed` nas chamadas. A paginação das APIs começa em 1; converter o índice do paginador visual, que começa em 0.

## 5. Etapas de execução

### Etapa 1 — Criar a base navegável

- Criar o projeto Angular em `frontend/`, com routing, SCSS e sem SSR.
- Configurar Material, layout, rotas e URLs dos serviços.
- Configurar apresentação em português, incluindo textos do paginador.
- Manter os comandos padrão de desenvolvimento, build e testes no `package.json`.

**Pronto quando:** a aplicação inicia na porta 4200, Produtos e Notas são acessíveis pelo menu e o build funciona. Ainda não apresentar telas vazias como funcionalidades concluídas.

### Etapa 2 — Criar a comunicação HTTP

- Definir os modelos e os serviços de produtos e notas conforme os DTOs existentes, sem geração automática de clientes ou camada genérica de repositórios.
- Implementar parâmetros de busca, filtros e paginação; omitir filtros vazios.
- Criar uma função pequena para extrair mensagens de ProblemDetails e erros por campo.
- Diferenciar erro de rede, validação, registro não encontrado e falha do serviço; não exibir stack traces nem objetos JSON ao usuário.
- Iniciar os testes unitários dos serviços com respostas HTTP simuladas.

**Pronto quando:** os testes verificam URLs, métodos, parâmetros e payloads dos serviços, sem fazer chamadas reais. A validação com as APIs reais acontece assim que o ambiente backend estiver disponível.

### Etapa 3 — Entregar produtos

- Listar produtos com busca no servidor e paginação. Reiniciar a página ao alterar a busca.
- Cadastrar código, descrição e saldo inicial; desabilitar submissão enquanto salva.
- Validar código obrigatório até 50 caracteres, descrição obrigatória até 200 e saldo inteiro entre 0 e `2.147.483.647`; espaços em branco não contam como preenchimento.
- Editar código e descrição enviando o `rowVersion` obtido na consulta, sem incluir saldo no PUT.
- Preservar `minimumStock` e `category` recebidos do servidor caso não sejam exibidos no formulário de edição, evitando apagar metadados existentes.
- Em código duplicado, orientar a correção. Em conflito de versão, solicitar recarga e revisão; não sobrescrever silenciosamente as alterações de outra operação.

**Pronto quando:** cadastrar, buscar e editar funcionam com persistência real; a listagem mostra os dados atualizados e os erros aparecem de forma compreensível.

### Etapa 4 — Entregar cadastro e consulta de notas

- Listar notas por número, status e data; oferecer filtros por número exato e status.
- Criar formulário com `FormArray`, busca/paginação de produtos e seleção de múltiplos itens. Não tratar a primeira página da API como o catálogo inteiro.
- Manter os produtos já selecionados ao pesquisar outra página. Preferir uma linha por produto, permitindo editar a quantidade e remover a linha.
- Exigir pelo menos um item, no máximo 100 linhas e quantidades inteiras positivas dentro do limite de `int` do backend.
- Enviar somente `{ items: [{ productId, quantity }] }`. Número sequencial e status inicial são definidos pelo Billing.
- Após salvar, navegar para o detalhe e usar código/descrição retornados na nota, pois são snapshots e não devem mudar com uma edição posterior do produto.

**Pronto quando:** é possível criar e consultar uma nota aberta com vários produtos; os dados permanecem após recarregar a página. O saldo apresentado na seleção é informativo: criar uma nota não reserva estoque, e a validação definitiva ocorre na impressão.

### Etapa 5 — Entregar impressão e recuperação de falhas

- Exibir “Imprimir” somente para nota aberta; proteger também o handler contra chamadas enquanto estiver processando.
- No clique, mostrar carregamento, desabilitar a ação e enviar um único `POST /api/invoices/{id}/print`, sem corpo e sem `Idempotency-Key`.
- No sucesso 200, substituir os dados da tela pela nota fechada retornada. A próxima consulta de produtos deve buscar os saldos atuais, sem manter cache desatualizado.
- Como acabamento já previsto na especificação, preparar uma visualização imprimível simples com `@media print`, ocultando navegação e botões. Após renderizar o resultado confirmado, abrir o diálogo com `window.print()`; não depender de popup aberto depois da chamada assíncrona. Esse acabamento não deve bloquear a entrega do fluxo obrigatório de baixa, fechamento e feedback.
- Não chamar o POST nem abrir automaticamente o diálogo ao simplesmente acessar uma nota fechada. Não oferecer uma nova ação de impressão para notas fechadas.
- Explicar que fechar/baixar ocorre no servidor antes do diálogo do navegador; cancelar o diálogo não reabre a nota nem estorna estoque.

Tratamento de retorno e retomada:

| Situação | Comportamento da interface |
|---|---|
| 422 — saldo insuficiente | Informar falta de saldo; não mostrar sucesso ou baixar valores localmente |
| 409 — nota já fechada ou conflito | Consultar novamente a nota e mostrar o estado confirmado |
| 503, erro de rede ou falha inesperada | Informar a falha e consultar a nota: uma resposta perdida não prova que a operação não aconteceu |
| Consulta após erro retorna `Open` | Permitir tentativa manual na mesma nota após resolver a causa |
| Consulta após erro retorna `Closed` | Mostrar a nota fechada e não repetir o POST |
| Consulta após erro também falha | Mostrar estado não confirmado e ação “Consultar novamente”; manter impressão indisponível até confirmar o estado |

Fazer uma consulta de atualização após erro, sem polling. A repetição manual usa o mesmo ID da nota; não recriar uma nota para recuperar a impressão. A proteção contra baixa duplicada continua sendo responsabilidade do backend.

**Pronto quando:** saldo 10 com nota de 2 resulta em saldo 8 e nota fechada; nova impressão fica bloqueada. A interrupção do Stock mostra feedback e, após o retorno do serviço, a tentativa manual conclui a mesma nota sem baixa duplicada.

### Etapa 6 — Validar e preparar a apresentação

- Escrever os testes junto com cada etapa, fechando aqui os cenários pendentes: validações de formulários, montagem dos payloads, paginação, mensagens de API e estados do botão de impressão.
- Cobrir sucesso, falha, consulta de recuperação e bloqueio de submissão simultânea. Usar `HttpTestingController` para simular respostas, não servidores ou bancos reais. Referência: [testes de HTTP no Angular](https://angular.dev/guide/http/testing).
- Executar build e testes unitários em modo não interativo. Não adicionar Cypress, Playwright, suíte E2E ou meta artificial de cobertura.
- Validar manualmente cadastro, persistência após recarga, nota com vários itens, impressão, saldo insuficiente, falha/retorno do Stock e conflitos básicos. Aproveitar o [roteiro existente](validacao-manual.md), acrescentando os passos pelas telas.
- Conferir teclado, rótulos, mensagens, lista vazia e comportamento em uma tela menor.
- Atualizar README com instalação, execução e testes do frontend. Preparar o detalhamento técnico: ciclos de vida realmente usados, RxJS, biblioteca visual, comunicação HTTP e tratamento de falhas. Não introduzir hooks apenas para citá-los no vídeo.

**Pronto quando:** build e testes unitários passam, o fluxo manual com as APIs reais está validado e a documentação permite executar e demonstrar a aplicação.

## 6. Complementos, somente depois do fluxo principal

O ajuste de saldo foi implementado como painel na edição do produto e permite demonstrar a recuperação de estoque insuficiente. A tela de histórico de movimentações continua opcional e não foi implementada.

O ajuste chama `POST /api/products/{id}/adjustments` com saldo final desejado, motivo obrigatório de até 200 caracteres e `rowVersion`. Não é uma quantidade a somar. Isso não deve atrasar o fluxo de notas e impressão; a demonstração inicial pode repor saldo pelo endpoint existente.

**Complemento posterior implementado:** melhoria de descrição no cadastro e na edição de produtos. O botão chama `POST /api/products/improve-description` com `{ description }` e mostra a resposta para **Usar sugestão** ou **Descartar**. A operação não salva o produto. Editar invalida a sugestão e respostas antigas, pedidos simultâneos são bloqueados e erros de IA não impedem o cadastro normal. Chave e modelo ficam exclusivamente no backend; a tela informa que o texto é enviado ao provedor externo e não deve conter dados pessoais ou confidenciais.

Esse complemento recebeu testes unitários de contrato e comportamento: a suíte do frontend passou com 52 testes e o build foi aprovado. Os comandos e a configuração opcional estão no [README](../README.md#melhorar-descrições-com-ia-opcional).

## 7. Inconsistências e cuidados identificados

- **Erros com formatos ligeiramente diferentes:** Stock usa `title` como mensagem, enquanto Billing usa `detail` e deixa o código em `title`. O frontend deve priorizar `detail`, depois um `title` legível, além de `errors` por campo e uma mensagem genérica segura. Podemos padronizar as APIs depois, mas isso não bloqueia as telas.
- **Impressão não equivale a confirmação da impressora:** o backend confirma baixa e fechamento, não que papel foi impresso. A visualização do navegador é uma conveniência simples, sem emissão fiscal ou PDF obrigatório.
- **Cadastros não têm idempotência:** após timeout ao criar produto ou nota, não repetir automaticamente o POST. Orientar a consultar a lista antes de tentar cadastrar novamente.
- **Edição não é ajuste de estoque:** o formulário de edição não deve enviar saldo ao PUT; usar a operação específica se o complemento for implementado.
- **Integração conferida:** cadastros, baixa, fechamento, saldo insuficiente e falha/retorno do Stock foram verificados nas telas com persistência real. O [registro de validação](validacao-frontend.md) detalha os resultados e limites da conferência.

As telas do fluxo obrigatório foram implementadas com os contratos existentes. A melhoria de descrição acrescentou somente seu endpoint específico no StockService. O próximo passo é o candidato revisar o fluxo e preparar a apresentação com o detalhamento técnico.

# Backend — escopo simplificado

Atualizado em 09/09/2026 após a decisão de adequar o projeto ao desafio de uma vaga júnior. Mantemos .NET 10 e os [requisitos oficiais](requisitos.md).

## Decisão

Usar dois serviços pequenos, comunicação HTTP direta e testes unitários. Não usar Outbox, worker, polling, tentativas numeradas ou um simulador E2E.

O Billing consulta produtos no Stock ao criar notas. Na impressão, chama a baixa de estoque e fecha a nota quando recebe confirmação. O cliente aguarda a resposta HTTP e mostra carregamento durante a chamada.

## O que permanece

- Produtos persistidos, código único, saldos e histórico de movimentações.
- Notas abertas com múltiplos itens, snapshots e numeração sequencial transacional.
- Baixa de todos os produtos em uma transação local, sem saldo negativo.
- Baixa confirmada identificada pelo ID da nota para não repetir o débito após falha.
- RowVersion na edição/ajuste de produtos.
- Controllers finos, casos de uso, EF, validação, ProblemDetails e logs.
- Testes unitários de regras e serviços usando fakes.

Cada serviço acessa exclusivamente seu próprio banco. Não há transação distribuída.

## Tratamento de falhas

| Situação | Resultado |
|---|---|
| Saldo insuficiente | 422, sem baixa parcial; repor e repetir a impressão da mesma nota |
| Stock fora do ar ou timeout | 503 com mensagem; tentar novamente após restabelecer o serviço |
| Stock confirmou, mas Billing não fechou | Repetir a mesma nota recupera a baixa salva e conclui o fechamento |
| Nota já fechada | 409; consultar os dados, sem outra baixa |
| Produto alterado simultaneamente | 409 por RowVersion; recarregar os dados |

A retomada é manual, pelo mesmo endpoint de impressão. Não existe fila para concluir notas automaticamente após reiniciar os serviços. Uma nota pode permanecer aberta com estoque já baixado até essa retomada; isso deve ser explicado na demonstração.

## Etapas

- [x] Simplificar impressão e remover infraestrutura assíncrona.
- [x] Simplificar a identificação da baixa para usar apenas InvoiceId.
- [x] Substituir E2E por testes unitários de produtos, notas e tratamento de falhas (35 aprovados na etapa de simplificação).
- [x] Atualizar scripts de instalação nova e documentação, preservando bancos existentes.
- [x] Compilar e executar os testes unitários.
- [x] Concluir a conferência das APIs reais pelo roteiro manual (na entrega posterior do frontend).
- [x] Integrar o Angular com as respostas síncronas.

Comandos e contratos atuais estão no [README](../README.md). A conferência de persistência e HTTP real está no [roteiro manual](validacao-manual.md), com os resultados da integração no [registro de validação do frontend](validacao-frontend.md).

## Complemento posterior: descrições por IA

Depois das etapas originais e da entrega do Angular, o usuário solicitou a melhoria opcional de descrições. A integração implementada usa `POST /api/products/improve-description`, um caso de uso na Application e `HttpClient` na Infrastructure para chamar o OpenRouter. A chave fica no backend; o modelo gratuito e o preço máximo zero são configurados sem adicionar dependências de SDK, tabelas ou processos.

O endpoint recebe e retorna `{ description }`, limita os textos a 200 caracteres e não salva produtos. A interface apresenta a sugestão para revisão e confirmação. Falhas da IA não impedem o cadastro normal. Testes unitários usam fakes, inclusive `HttpMessageHandler`, sem rede ou banco. Após esse complemento, o backend compilou e seus 55 testes passaram; a chamada real de melhoria também foi conferida. Consulte o [detalhamento técnico](detalhamento-tecnico.md#melhoria-opcional-de-descrições).

## Fora do escopo atual

PDF no servidor, mensageria, recuperação automática em background, autenticação completa, integração fiscal governamental, cancelamento de notas e observabilidade avançada. Não são necessários para demonstrar o fluxo obrigatório solicitado. A IA saiu desta lista após a solicitação posterior do usuário e permanece opcional para executar a aplicação.

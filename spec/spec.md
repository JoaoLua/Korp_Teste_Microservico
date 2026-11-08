# Especificação — estoque e faturamento

Versão simplificada em 09/09/2026 para o contexto do desafio de uma vaga júnior. Os [requisitos oficiais](requisitos.md) permanecem inalterados.

## Objetivo

Cadastrar produtos e notas fiscais, imprimir notas abertas, fechar a nota e atualizar os saldos dos produtos. Demonstrar também uma falha de comunicação entre os serviços e sua recuperação.

## Tecnologias

- Backend: C#, ASP.NET Core e .NET 10.
- Persistência: Entity Framework Core e SQL Server LocalDB.
- Testes automatizados: xUnit no backend e Vitest no frontend, com substitutos dos serviços e sem banco ou rede.
- Frontend: Angular 22, componentes standalone, Reactive Forms, HttpClient, RxJS e estado local com signals.
- Interface: Angular Material para paginação/carregamento, ícones Lucide e SCSS com direção visual Swiss.
- IA opcional: OpenRouter via `HttpClient` no StockService para sugerir descrições de produtos.

## Organização

StockService e BillingService são independentes. Cada um mantém as camadas Domain, Application, Infrastructure e API. O Stock acessa StockDb; o Billing acessa BillingDb. A integração é HTTP/JSON, nunca uma consulta ao banco do outro serviço.

O Billing armazena código e descrição dos produtos como snapshots nos itens da nota. Alterar um produto não altera notas já criadas.

## Dados

| Banco | Tabelas usadas |
|---|---|
| StockDb | Products, StockMovements, StockDeductionOperations |
| BillingDb | Invoices, InvoiceItems, InvoiceSequence |

Os IDs são GUID; InvoiceSequence usa uma única linha com Id=1. Datas são UTC. O status da nota é Open (Aberta) ou Closed (Fechada). Código do produto é único; saldos não podem ser negativos; quantidades dos itens devem ser positivas.

StockDeductionOperations guarda a confirmação da baixa, única por InvoiceId. Uma nova falha por falta de saldo não cria uma confirmação; após reposição, o pedido pode ser repetido. Campos de compatibilidade permitem reconhecer registros da versão anterior, sem exigir que dados existentes sejam apagados.

Bancos já existentes podem conter tabelas antigas de impressão/Outbox e colunas adicionais. Elas não são utilizadas pelas novas APIs e não são criadas numa instalação nova.

## Regras principais

### Produtos

Código, descrição e saldo inicial são obrigatórios. O cadastro com saldo positivo registra uma entrada no histórico. Edição de cadastro não altera saldo; ajustes usam saldo-alvo, motivo e RowVersion. Versão antiga retorna conflito.

Após concluir o fluxo obrigatório, a melhoria de descrição por IA foi adicionada a pedido do usuário. `POST /api/products/improve-description` recebe e retorna `{ description }`, com texto de 1 a 200 caracteres após remover espaços externos. O caso de uso não altera o banco. A interface permite aceitar ou descartar a sugestão, invalida resultados após uma edição e só grava o produto quando o usuário confirma o formulário.

O modelo configurado é `nvidia/nemotron-3.5-lightning:free`, com preço máximo zero por entrada, saída e requisição, sem fallback pago. A chave permanece no backend, em User Secrets ou variável de ambiente. A descrição é enviada ao OpenRouter/NVIDIA; endpoints gratuitos podem registrar o uso, portanto não enviar dados pessoais ou confidenciais. [Informações do provedor](https://openrouter.ai/nvidia/nemotron-3.5-lightning%3Afree/pricing).

### Notas

Uma nota exige pelo menos um produto. Itens repetidos são consolidados, sem permitir overflow da quantidade. O Billing consulta os produtos no Stock, gera o número sequencial e salva a nota aberta e seus itens numa transação. Criar uma nota não reserva estoque. Não há edição ou exclusão de notas nesta versão.

### Impressão

O cliente chama POST /api/invoices/{id}/print e espera a resposta. O Billing verifica se a nota está aberta, solicita a baixa ao Stock e fecha a nota após confirmação. Sucesso retorna 200 com a nota fechada. Nota já fechada retorna 409.

O Stock valida todos os saldos antes de alterá-los; movimentos e resultado da baixa são gravados junto com os saldos numa única transação local. O ID da nota identifica o pedido: repetir uma baixa concluída retorna sua confirmação, sem novo débito. Conteúdo divergente para a mesma nota é recusado.

A tela deve exibir carregamento durante o POST. Não existem operationId, polling de impressão, header externo Idempotency-Key ou endpoint separado de retry.

## Falhas e retomada

- Dados inválidos: 400.
- Registro não encontrado: 404.
- Nota fechada, conteúdo conflitante ou versão antiga: 409.
- Estoque insuficiente: 422; não baixar nenhum item e manter a nota aberta.
- Resposta inválida ou incompleta da IA: 502, preservando a descrição original.
- Serviço/banco indisponível ou timeout: 503 com mensagem segura.
- Erro inesperado: 500, sem expor stack trace ao cliente.

A recuperação acontece por uma nova chamada de impressão da mesma nota após resolver a causa. Quando o Stock já confirmou a baixa, a nova chamada recupera esse resultado e tenta concluir o fechamento. Não há retry automático nem execução em segundo plano.

Após erro, o cliente consulta a nota: uma resposta perdida não prova que a operação falhou. Cancelar o diálogo de impressão física não estorna estoque.

A IA tem timeout padrão de 30 segundos, configurável entre 5 e 60. Ausência de configuração, limite do serviço gratuito, falha de rede e timeout retornam 503 apenas na operação de melhoria. O cadastro continua disponível, sem repetição automática da chamada de IA.

## Testes e demonstração

Testes unitários cobrem validações, consolidação de itens e decisões dos casos de uso, incluindo não fechar a nota quando o estoque não confirma a baixa. A melhoria de descrição usa um substituto do provedor na Application e `HttpMessageHandler` falso para testar o cliente HTTP da Infrastructure, sem rede nem banco. Eles não comprovam transações ou comunicação real entre serviços.

A demonstração usa o [roteiro manual](validacao-manual.md). O [README](../README.md) contém os comandos de execução e contratos.

## Frontend implementado e entrega

- Telas de produtos, cadastro, edição e ajuste de saldo com motivo.
- Melhoria opcional de descrição, com sugestão revisada pelo usuário antes de salvar.
- Listagem, filtros, cadastro com múltiplos produtos e detalhes de notas.
- Indicadores de carregamento, validações, bloqueio de envio duplicado e mensagens de erro.
- Visualização imprimível após fechamento confirmado e recuperação manual após falha.
- [Detalhamento técnico](detalhamento-tecnico.md) e [registro de validação](validacao-frontend.md).

Pendentes do candidato: vídeo de apresentação e preparação do repositório para entrega.

A IA é opcional e já está implementada; PDF no backend continua opcional e não foi implementado. Integração fiscal governamental, cálculos tributários e cancelamento de notas não fazem parte desta implementação.

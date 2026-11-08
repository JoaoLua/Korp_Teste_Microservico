# Korp — Estoque e Faturamento

Aplicação desenvolvida como desafio técnico. Frontend Angular 22 e dois microsserviços ASP.NET Core em **.NET 10**, com Entity Framework Core e SQL Server LocalDB.

- **StockService:** cadastro de produtos, saldos, movimentações e sugestão opcional de descrição por IA.
- **BillingService:** cadastro de notas fiscais, numeração sequencial e fechamento na impressão.

Cada serviço acessa somente seu próprio banco. A comunicação entre eles é HTTP/JSON, sem fila, worker ou transação distribuída.

---

## Alterações Implementadas

### Requisitos Obrigatórios ✅

| Funcionalidade | Status | Detalhes |
|---|---|---|
| Cadastro de Produtos | ✅ Completo | Código, descrição e saldo. Edição e ajuste de saldo com controle de concorrência (`rowVersion`). |
| Cadastro de Notas Fiscais | ✅ Completo | Numeração sequencial, status Aberta/Fechada, múltiplos produtos com quantidades. |
| Impressão de Notas | ✅ Completo | Indicador de processamento, baixa de estoque, fechamento da nota e impressão via navegador. |
| Arquitetura de Microsserviços | ✅ Completo | StockService e BillingService independentes, cada um com seu banco de dados. |
| Tratamento de Falhas | ✅ Completo | Recuperação após indisponibilidade do Stock, feedback claro ao usuário, consulta de estado pós-erro. |
| Persistência em banco de dados | ✅ Completo | SQL Server LocalDB com scripts de criação do esquema. |

### Requisitos Opcionais ✅

| Funcionalidade | Status | Detalhes |
|---|---|---|
| Tratamento de Concorrência | ✅ Implementado | `rowVersion` em edições e ajustes; verificação de saldos antes da baixa em transação única. |
| Uso de Inteligência Artificial | ✅ Implementado | Melhoria de descrição de produtos via OpenRouter (`nvidia/nemotron-3.5-lightning:free`). |
| Idempotência | ✅ Implementado | Baixa identificada pelo ID da nota — repetição após falha recupera resultado já confirmado sem duplicar desconto. |

### Detalhamento das Alterações

#### Backend (.NET 10 / ASP.NET Core)
- Dois microsserviços com camadas Domain, Application, Infrastructure e API (Clean Architecture).
- Entity Framework Core com SQL Server para mapeamento e persistência.
- Transações locais garantindo consistência de saldos, movimentações e baixas.
- Numeração sequencial atômica de notas fiscais.
- Tratamento centralizado de exceções com `ProblemDetails` (códigos `400`, `404`, `409`, `422`, `502`, `503`).
- Logs estruturados sem exposição de detalhes internos.
- Health checks (`/health/live`, `/health/ready`) e documentação OpenAPI.
- 55 testes unitários (42 Stock + 13 Billing) com xUnit e fakes.

#### Frontend (Angular 22)
- Componentes standalone com `ChangeDetectionStrategy.OnPush` e signals (zoneless, sem `zone.js`).
- Formulários reativos com validação client-side e server-side.
- Busca com `debounceTime`, `distinctUntilChanged` e `switchMap` (RxJS).
- Paginação com Angular Material, ícones com Lucide.
- Design system Swiss (superfícies limpas, tipografia Helvetica, azul `#002FA7`).
- CSS de impressão configurado para A4.
- 52 testes unitários com Vitest e TestBed.

#### Integração com IA (opcional)
- `POST /api/products/improve-description` — revisão de clareza e ortografia via OpenRouter.
- Chave configurada apenas no backend (User Secrets / variáveis de ambiente).
- Frontend exibe sugestão separadamente; o usuário decide aceitar ou descartar.
- Sem fallback pago, sem retry automático, sem envio de dados ao banco.

---

## Próximas Etapas

- [ ] Revisar a apresentação e garantir cobertura dos cenários: cadastro de produto, nota com múltiplos itens, impressão com atualização de saldo, bloqueio de nota fechada e recuperação após falha.
- [ ] Demonstrar a funcionalidade de IA (melhorar descrição) durante a apresentação.
- [ ] Gravar o vídeo de apresentação conforme os [requisitos](spec/requisitos.md).
- [ ] Enviar por e-mail para `rh@korp.com.br`:
  - Link público do repositório GitHub
  - Link para o vídeo de apresentação
  - Link do [detalhamento técnico](spec/detalhamento-tecnico.md)

---

## Executar

Pré-requisitos: SDK .NET 10, SQL Server LocalDB e `sqlcmd`. Execute os comandos na raiz do projeto.

Para preparar **bancos novos**:

```powershell
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -b -i scripts/create-databases.sql
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -b -i scripts/create-stock-tables.sql
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -b -i scripts/create-billing-tables.sql
```

```powershell
dotnet build KorpBackend.slnx
dotnet test KorpBackend.slnx
```

Inicie em dois terminais:

```powershell
dotnet run --project src/StockService/StockService --launch-profile http
```

```powershell
dotnet run --project src/BillingService/BillingService --launch-profile http
```

Stock: `http://localhost:5213`. Billing: `http://localhost:5048`.

Como alternativa, `./scripts/run-backend.ps1` inicia as duas APIs; `Ctrl+C` encerra os processos. Logs ficam em `artifacts/backend-local/`.

Cada API tem `/health/live`, `/health/ready` e, em Development, `/openapi/v1.json`. Os arquivos `.http` de cada projeto contêm exemplos de chamadas.

### Frontend

Com Node.js 24.15 ou superior, abra um terceiro terminal na raiz:

```powershell
cd frontend
npm ci
npm start
```

Abra [http://localhost:4200](http://localhost:4200). As URLs dos serviços ficam em `frontend/src/environments/environment.ts`.

```powershell
npm run build
npm run test:ci
```

Detalhes no [README do frontend](frontend/README.md) e no [detalhamento técnico](spec/detalhamento-tecnico.md).

### Melhorar descrições com IA (opcional)

Configure a chave do OpenRouter usando User Secrets:

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "SUA_CHAVE" --project src/StockService/StockService
```

| Variável | Configuração |
|---|---|
| `OpenRouter__ApiKey` | Chave da API do OpenRouter |
| `OpenRouter__Model` | Modelo; padrão `nvidia/nemotron-3.5-lightning:free` |
| `OpenRouter__TimeoutSeconds` | Timeout; padrão 30s (5–60) |

A chave não deve entrar em `appsettings.json`, no Angular ou no repositório. Sem chave, os demais endpoints continuam funcionando.

## Como a impressão funciona

1. A interface chama `POST /api/invoices/{id}/print` e mantém o indicador de carregamento.
2. O Billing verifica se a nota está aberta e chama o Stock para baixar os itens.
3. O Stock verifica todos os saldos antes de alterar qualquer produto. Saldo, movimentos e comprovante são salvos na mesma transação.
4. O Billing fecha a nota e responde **200** com a nota `Closed`.

Se faltar saldo: **422**, nota permanece aberta. Se o Stock estiver indisponível: **503**, tente novamente quando voltar. Nota já fechada: **409**. Repetição após falha recupera a baixa já confirmada sem duplicar.

## Endpoints

### Estoque

| Método | Rota | Uso |
|---|---|---|
| GET | `/api/products` | Listagem e busca |
| GET | `/api/products/{id}` | Produto e saldo |
| GET | `/api/products/lookup?ids=GUID&ids=GUID` | Consulta em lote |
| POST | `/api/products` | Cadastro e saldo inicial |
| POST | `/api/products/improve-description` | Sugestão de descrição por IA |
| PUT | `/api/products/{id}` | Edição de cadastro |
| POST | `/api/products/{id}/adjustments` | Ajuste de saldo |
| GET | `/api/products/{id}/movements` | Histórico |
| POST | `/api/stock/deduct` | Baixa interna por nota |
| GET | `/api/stock/deductions/{invoiceId}` | Resultado da baixa |

### Faturamento

| Método | Rota | Uso |
|---|---|---|
| GET | `/api/invoices` | Listagem com filtros |
| GET | `/api/invoices/{id}` | Nota e seus itens |
| POST | `/api/invoices` | Criação com número sequencial |
| POST | `/api/invoices/{id}/print` | Baixa e fechamento |

## Testes

```powershell
dotnet test KorpBackend.slnx        # 55 testes backend
cd frontend && npm run test:ci       # 52 testes frontend
```

A persistência e a comunicação real são conferidas pelo [roteiro manual](spec/validacao-manual.md). Os [requisitos oficiais](spec/requisitos.md) foram preservados.

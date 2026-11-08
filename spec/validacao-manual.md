# Roteiro manual do backend

Este roteiro confere o fluxo real e serve para demonstrar o desafio. Não é uma suíte E2E: são chamadas que podem ser feitas pelo PowerShell, Postman ou arquivos `.http` dos projetos.

Inicie as duas APIs em **terminais separados**, conforme o README. Os exemplos abaixo criam dados de demonstração que permanecem nos bancos; não executam exclusões. Use uma base local de demonstração.

## 1. Cadastro e impressão

Em outro terminal PowerShell:

```powershell
$stock = 'http://localhost:5213'
$billing = 'http://localhost:5048'
$suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)

$product = Invoke-RestMethod "$stock/api/products" -Method Post -ContentType 'application/json' -Body (@{
    code = "DEMO-$suffix"
    description = 'Produto de demonstracao'
    balance = 10
} | ConvertTo-Json)

$invoice = Invoke-RestMethod "$billing/api/invoices" -Method Post -ContentType 'application/json' -Body (@{
    items = @(@{ productId = $product.id; quantity = 2 })
} | ConvertTo-Json -Depth 4)

$invoice
$closed = Invoke-RestMethod "$billing/api/invoices/$($invoice.id)/print" -Method Post
$closed
Invoke-RestMethod "$stock/api/products/$($product.id)"
Invoke-RestMethod "$stock/api/products/$($product.id)/movements"
```

Conferir: nota nasce `Open` com número sequencial; impressão responde `200` e `Closed`; saldo passa de 10 para 8; histórico contém a entrada inicial e uma saída de 2.

Repita o POST de impressão dessa nota fechada. Deve retornar `409`, sem mudar o saldo. No PowerShell, respostas de erro aparecem como exceção HTTP; confira o status e o corpo ProblemDetails.

## 2. Falta de saldo sem baixa parcial

```powershell
$empty = Invoke-RestMethod "$stock/api/products" -Method Post -ContentType 'application/json' -Body (@{
    code = "SEM-SALDO-$suffix"
    description = 'Produto para testar falta de saldo'
    balance = 0
} | ConvertTo-Json)

$shortInvoice = Invoke-RestMethod "$billing/api/invoices" -Method Post -ContentType 'application/json' -Body (@{
    items = @(
        @{ productId = $product.id; quantity = 1 }
        @{ productId = $empty.id; quantity = 1 }
    )
} | ConvertTo-Json -Depth 4)

Invoke-RestMethod "$billing/api/invoices/$($shortInvoice.id)/print" -Method Post
```

Esperado: `422` com código `INSUFFICIENT_STOCK`. Consultar a nota confirma `Open`; os saldos continuam 8 e 0. Nenhum item foi baixado parcialmente.

Reponha o produto e repita **a mesma nota**:

```powershell
$current = Invoke-RestMethod "$stock/api/products/$($empty.id)"
Invoke-RestMethod "$stock/api/products/$($empty.id)/adjustments" -Method Post -ContentType 'application/json' -Body (@{
    balance = 3
    reason = 'Reposicao de demonstracao'
    rowVersion = $current.rowVersion
} | ConvertTo-Json)

Invoke-RestMethod "$billing/api/invoices/$($shortInvoice.id)/print" -Method Post
```

Esperado: `200`, nota `Closed`, saldos 7 e 2.

## 3. Falha e recuperação do microsserviço

Com o Stock ligado, crie outra nota:

```powershell
$retryInvoice = Invoke-RestMethod "$billing/api/invoices" -Method Post -ContentType 'application/json' -Body (@{
    items = @(@{ productId = $product.id; quantity = 1 })
} | ConvertTo-Json -Depth 4)
```

1. No terminal do StockService, pressione `Ctrl+C`. Mantenha o Billing ligado.
2. Faça `POST /api/invoices/{retryInvoice.id}/print`. Deve retornar `503` com mensagem explicando a falha.
3. Consulte a nota no Billing: continua aberta e pode ser consultada sem o Stock.
4. Inicie novamente o Stock com o mesmo comando e banco.
5. Repita o POST para a mesma nota. Deve retornar `200`, fechar a nota e baixar apenas uma unidade.

Não é necessário criar outra nota, outra chave ou solicitar um endpoint especial de retry. A retomada depende dessa nova ação do usuário, não de um processo em segundo plano.

## 4. Proteção básica contra repetição

Para demonstrar a confirmação persistida do estoque, reenvie diretamente o comando da primeira nota já fechada:

```powershell
Invoke-RestMethod "$stock/api/stock/deduct" -Method Post -ContentType 'application/json' -Body (@{
    invoiceId = $invoice.id
    items = @(@{ productId = $product.id; quantity = 2 })
} | ConvertTo-Json -Depth 4)
```

Esperado: recuperar `Completed` sem nova saída e sem reduzir o saldo. Esta chamada interna serve apenas para a demonstração: o Angular deve chamar a impressão no Billing, nunca coordenar a baixa.

Isso permite explicar o caso em que o Stock confirmou a baixa, mas o Billing perdeu a resposta: a mesma nota pode recuperar a confirmação. Não é necessário criar um simulador de falhas de rede no projeto.

## 5. Testes unitários

```powershell
dotnet test KorpBackend.slnx
```

Os testes não dependem de SQL Server nem das APIs ligadas. Na apresentação, diferencie regras verificadas pelos testes unitários das verificações manuais de banco e comunicação descritas acima.

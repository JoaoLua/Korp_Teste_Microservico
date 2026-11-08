# Testes unitários

Execute na raiz do projeto, com o SDK .NET 10 instalado:

```powershell
dotnet test KorpBackend.slnx
```

Não é necessário iniciar APIs ou SQL Server. Os projetos usam xUnit e fakes pequenos, sem biblioteca de mocks.

- `StockService.UnitTests`: validações de produtos e ajustes; normalização, consolidação e identificação dos pedidos de baixa.
- `BillingService.UnitTests`: validações das notas, consulta dos produtos e fluxo de impressão diante de sucesso, saldo insuficiente ou indisponibilidade.

Esses testes verificam as regras e a coordenação da camada Application. Os fakes apenas registram chamadas e retornam respostas controladas: não comprovam persistência, transações SQL, concorrência real nem comunicação HTTP. Esses pontos devem ser conferidos pelo roteiro manual do projeto.

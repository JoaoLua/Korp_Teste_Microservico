# Validação do frontend

Conferência realizada em 09/09/2026, após a implementação da interface Angular.

## Build e testes

- `npm run build`: concluído, sem erros ou avisos. Bundle inicial de aproximadamente 328 kB, com as páginas carregadas sob demanda.
- `npm run test:ci`: **52 testes unitários aprovados em 6 arquivos**, incluindo o aprimorador de descrições adicionado posteriormente.
- `dotnet build KorpBackend.slnx --nologo`: concluído com zero erros e zero avisos no SDK 10.0.401.
- `dotnet test KorpBackend.slnx --nologo`: **55 testes unitários aprovados**, sendo 42 do Stock e 13 do Billing.

Os testes frontend cobrem formulários, limites de quantidade, produtos duplicados, contratos HTTP, paginação/filtros, conflitos de versão, resultado incerto de cadastro e recuperação da impressão. Usam Vitest, TestBed e respostas simuladas; não inicializam APIs nem bancos.

## Conferência manual com persistência real

A interface foi operada no navegador com as APIs reais e dois bancos SQL Server LocalDB temporários. Foram usados produtos identificados como dados de validação. Não existe modo de demonstração ou catálogo fictício embutido no frontend.

| Cenário | Resultado observado |
|---|---|
| Formulário de produto vazio | Mensagens de código e descrição obrigatórios; cadastro não enviado |
| Cadastro de dois produtos | Produtos persistidos e exibidos na lista, com saldos iniciais de 10 e 20 |
| Nota com múltiplos itens | Número 1 e status Aberta atribuídos pelo Billing; dois produtos com quantidade 2 |
| Interrupção do Stock antes da impressão | Carregamento, feedback de indisponibilidade e consulta que confirmou a nota aberta |
| Retorno do Stock e tentativa na mesma nota | Nota fechada; saldos passaram de 10 para 8 e de 20 para 18 |
| Nota fechada | A ação de impressão deixou de ser oferecida |
| Quantidade superior ao saldo | Segunda nota permaneceu aberta; resposta de saldo insuficiente, sem baixa |
| Edição de descrição e ajuste de estoque | Cadastro atualizado e saldo reposto de 8 para 10 com motivo |
| Nova tentativa após reposição | A segunda nota fechou e o saldo passou para 1 |
| Descrição do produto alterada depois da criação da nota | A nota manteve a descrição original de seu item |
| Filtro por notas abertas | Exibiu apenas a nota aberta correspondente |
| Interface responsiva | Lista, navegação, cadastro de nota, quantidades e ajuste de estoque conferidos também em viewport de 390 × 844 |

A consulta final ao SQL confirmou duas notas fechadas, duas confirmações de baixa e três movimentações de saída, correspondentes aos três itens das duas notas. Os saldos finais foram 18 e 1.

## Limites e limpeza

### Complemento: melhoria de descrição por IA

Também em 09/09/2026, foi validada a integração opcional pelo OpenRouter. A validação inicial usou o Nemotron 3 Ultra; depois, o modelo padrão foi alterado para `nvidia/nemotron-3.5-lightning:free`, mais leve para descrições curtas. A chave foi configurada somente nos User Secrets locais do StockService.

- Chamada real a `POST /api/products/improve-description`: o texto de teste `parafuso aco inox 10mm caixa com 100 unidades` produziu `Parafuso aço inox 10 mm - caixa com 100 unidades`.
- Uma chamada de diagnóstico ao mesmo modelo confirmou `finish_reason: stop`, `reasoning_tokens: 0` e `cost: 0`. A requisição fixa os tetos de preço de entrada, saída e requisição em zero.
- No navegador, uma resposta inválida/incompleta mostrou o aviso de falha e preservou o texto digitado. Uma nova tentativa manual recebeu a sugestão; “Usar sugestão” substituiu somente o campo, sem cadastrar o produto.
- Entrada vazia foi recusada pelo backend com HTTP 400. Limites, falhas de rede/provedor, timeout e cancelamento são cobertos por testes unitários; descartar, bloquear envio duplicado e ignorar sugestões obsoletas também são cobertos no Angular.
- Não foram gravados nem removidos produtos ou notas durante a validação da IA. O esquema dos bancos não mudou.

Modelos gratuitos estão sujeitos à disponibilidade e a limites do provedor. A tela avisa que a descrição será enviada ao OpenRouter/NVIDIA e não deve conter dados pessoais ou confidenciais. Esta conferência não é uma suíte E2E.

### Conferência anterior dos fluxos de estoque e faturamento

Não foi criada uma suíte E2E. A conferência acima foi manual, usando a interface e verificações pontuais no banco. Os testes unitários cobrem também casos de resposta perdida e falha da consulta de recuperação, sem afirmar que esses casos foram reproduzidos manualmente na rede.

`window.print()` é chamado após a renderização da nota confirmada; existe CSS para apresentação em A4. A saída em uma impressora física não foi validada.

Ao terminar, os dois bancos temporários e seus dados descartáveis foram excluídos sem backup. StockDb e BillingDb foram preservados e reconectados às APIs; ambos continuaram com as contagens originais, de zero produtos e zero notas. A interface inicia com os dados reais desses bancos, sem registros de teste.

Para repetir a demonstração, use os comandos do [README](../README.md), cadastre produtos nas telas, crie uma nota e siga os cenários do [roteiro manual do backend](validacao-manual.md). Use os dois serviços em terminais separados se quiser interromper apenas o Stock e manter o Billing disponível.

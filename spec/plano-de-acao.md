# Plano de ação do projeto

Atualizado em 09/09/2026. Projeto de desafio técnico para vaga júnior, em .NET 10 e Angular.

Os [requisitos oficiais](requisitos.md) são a referência. A [especificação técnica](spec.md) e a implementação podem ser ajustadas para resolver inconsistências sem alterar os requisitos obrigatórios.

## 1. Backend

Manter dois serviços com bancos separados, comunicação HTTP, produtos, notas sequenciais e impressão com baixa de estoque. A arquitetura foi simplificada para retirada de Outbox, workers e testes E2E. Os testes automatizados passam a ser unitários; o fluxo real será demonstrado manualmente.

Detalhes no [plano do backend](plano-backend.md) e comandos no [README](../README.md).

## 2. Angular

Implementação concluída em `frontend/`: produtos, edição/ajuste de saldo, notas, impressão, tratamento de falhas e testes unitários. A ordem de entregas e os critérios permanecem no [plano do frontend](plano-frontend.md); os resultados estão no [registro de validação](validacao-frontend.md).

Etapas atendidas:

- Criar aplicação, rotas e serviços HTTP.
- Implementar listagem, cadastro e edição de produtos.
- Implementar listagem, cadastro e detalhe de notas com múltiplos itens.
- Na impressão, mostrar carregamento enquanto o POST aguarda a baixa e o fechamento.
- Em sucesso, atualizar a nota para Fechada e abrir a versão imprimível.
- Exibir erros de validação, falta de saldo e indisponibilidade de maneira clara.
- Após erro, consultar o estado da nota; permitir repetir o pedido se ela continuar aberta.
- Não enviar número, status ou descrição arbitrários ao criar uma nota.
- Não chamar a baixa diretamente pelo Angular: essa coordenação pertence ao Billing.

Foram usados Reactive Forms, RxJS, Angular Material para paginação/carregamento e Lucide para ícones. A skill frontend-design orientou a identidade visual Swiss.

## 3. Validação e apresentação

- Executar os testes unitários.
- Seguir o [roteiro manual](validacao-manual.md) com as APIs reais.
- Demonstrar cadastro, impressão, atualização de saldo e falha/recuperação do Stock.
- Revisar o [detalhamento técnico](detalhamento-tecnico.md), já preparado com bibliotecas, LINQ, tratamento de erros, RxJS e ciclos de vida efetivamente usados.
- Gravar o vídeo e preparar o repositório para entrega pelo próprio candidato.

## 4. Opcionais

A proteção contra baixa duplicada e concorrência básica já faz parte do backend. Conforme a sequência original, a IA foi acrescentada depois de concluir o fluxo obrigatório e a interface, por solicitação do usuário.

O aprimorador de descrições está implementado no StockService e nos formulários de produtos: gera uma sugestão pelo OpenRouter, permite aceitar/descartar e mantém a gravação sob confirmação do usuário. Configuração e limitações estão no [README](../README.md#melhorar-descrições-com-ia-opcional). PDF no backend e funcionalidades fiscais continuam fora da entrega atual.

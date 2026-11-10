# Expresso · Pedidos de cafeteria

WebAPI REST para cadastrar o cardápio de uma cafeteria e acompanhar cada pedido do balcão até a entrega, com cupom fiscal, controle de estoque e perfis de acesso. Inclui um painel web em Blazor para o balcão.

**.NET 10 · ASP.NET Core · EF Core 10 · PostgreSQL 17 · Blazor · xUnit + Testcontainers + bUnit · Docker · GitHub Actions**

[![CI](https://github.com/MozartSimoes/desafio-expresso-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/MozartSimoes/desafio-expresso-dotnet/actions/workflows/ci.yml)

| Pedido no desafio | Situação |
|---|---|
| Cardápio, pedidos e itens | ✅ |
| As 5 regras de negócio | ✅ todas no domínio e cobertas por testes |
| Integração com API pública | ✅ Open-Meteo (clima) + BrasilAPI (feriados) → "cardápio do dia" |
| Vá além: pico das 8h, toque duplo, nota fiscal, API fora do ar, quem pode o quê, "meu pedido sumiu" | ✅ todos os 6 |
| Testes, Docker Compose, OpenAPI, CI | ✅ 93 testes (unitários, integração com PostgreSQL real e componentes do frontend) |
| Frontend | ✅ Blazor: painel de pedidos, novo pedido, busca, detalhe com histórico e cupom, cardápio |

---

## Sumário

- [Como rodar](#como-rodar)
- [Roteiro rápido no Swagger](#roteiro-rápido-no-swagger)
- [Frontend (Blazor)](#frontend-blazor)
- [Endpoints](#endpoints)
- [Regras de negócio](#regras-de-negócio)
- [Vá além](#vá-além)
- [Decisões e por quê](#decisões-e-por-quê)
- [O que ficou de fora](#o-que-ficou-de-fora)
- [Uso de IA](#uso-de-ia)
- [Estrutura do projeto](#estrutura-do-projeto)

---

## Como rodar

### Opção 1: tudo no Docker (só precisa do Docker)

```bash
docker compose up --build
```

| O quê | Endereço |
|---|---|
| Painel web (Blazor) | **http://localhost:8081** |
| API (Swagger) | **http://localhost:8080/swagger** |

O banco é criado e o cardápio de exemplo é carregado na primeira subida (as migrations rodam na inicialização).

### Opção 2: local (Visual Studio ou `dotnet run`) + banco no Docker

Requer o [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
docker compose up -d postgres         # PostgreSQL na porta 5433
dotnet run --project src/Expresso.Api # API:  http://localhost:5183/swagger
dotnet run --project src/Expresso.Web # Site: http://localhost:5200 (em outro terminal)
```

No Visual Studio, escolha o perfil **"API + Site"** na lista ao lado do botão ▶ e aperte F5: ele sobe os dois projetos (arquivo `Expresso.slnLaunch`).

> O banco usa a porta **5433** para não conflitar com um PostgreSQL que já esteja instalado na 5432.

### Testes

```bash
dotnet test
```

Os testes de integração sobem um PostgreSQL descartável com **Testcontainers**, por isso precisam do Docker rodando. Eles não tocam no banco de desenvolvimento. Os testes do frontend (bUnit) rodam sem Docker.

### Usuários de demonstração

O login é simulado (confirmado com o RH), com usuários fixos no `appsettings.json`:

| Usuário | Senha | Perfil | Pode |
|---|---|---|---|
| `atendente` | `atendente123` | Atendente | Registrar e cancelar pedidos |
| `barista` | `barista123` | Barista | Mudar a situação do pedido |
| `gerente` | `gerente123` | Gerente | Tudo, inclusive cardápio, preços e reenvio de cupom |

No painel web, clique no usuário na tela de login para preencher. No Swagger: `POST /api/auth/login`, copie o `token` e cole no botão **Authorize**.

---

## Roteiro rápido no Swagger

1. `GET /api/menu/today` (sem login): cardápio do dia com clima e feriado de Medianeira-PR.
2. Login como **atendente** → `POST /api/orders`:
   ```json
   { "customerName": "Maria", "items": [ { "productId": 1, "quantity": 2, "notes": "sem açúcar" } ] }
   ```
   O total é calculado pela API.
3. Login como **barista** → `PATCH /api/orders/{id}/status` com `{ "status": "Preparing" }`, depois `Ready` e `Delivered`.
4. `GET /api/orders/{id}/receipt`: o cupom fiscal sai em segundo plano; consulte de novo em alguns segundos.
5. `GET /api/orders/{id}/history`: quem fez cada etapa e quando.

---

## Frontend (Blazor)

Telas para o dia a dia do balcão, em `src/Expresso.Web`:

| Tela | O que faz | Quem vê |
|---|---|---|
| **Painel** | Colunas Recebidos → Preparando → Prontos → Entregues, atualizadas sozinhas a cada 5 s, com o botão da próxima etapa em cada pedido | Todos; os botões mudam conforme o perfil |
| **Novo pedido** | Produtos por categoria, sugestão do dia (clima/feriado) em destaque, salgados esgotados bloqueados, quantidade e observação por item | Atendente e gerente |
| **Buscar pedido** | "Meu pedido sumiu": busca por parte do nome | Todos |
| **Detalhe do pedido** | Itens com o preço do momento, histórico (quem e quando) e situação do cupom fiscal, com reenvio | Todos; reenvio só para o gerente |
| **Cardápio** | Preço, disponibilidade e estoque, editáveis | Gerente edita; os outros só veem |

**Decisões**
- **Blazor com renderização interativa no servidor**: a mesma linguagem e o mesmo ecossistema da API, sem projeto JavaScript à parte. Quem chama a API é o servidor do Blazor, então **não precisa de CORS** e o **token JWT nunca vai para o navegador** (fica na sessão da conexão; recarregar a página pede login de novo).
- **O frontend não referencia o projeto da API**: tem a própria cópia dos contratos (`Api/Models.cs`), para não arrastar EF Core e o backend inteiro para o site. Um **teste de contrato** roda o cliente do site contra a API real, e um campo renomeado quebra o teste, não a tela.
- **As permissões no site só escondem botões**: quem garante é a API. Um barista que chamasse a API diretamente para cancelar receberia 403.
- **Toque duplo também no site**: cada pedido em montagem tem uma `Idempotency-Key`. Se o envio falhar e o atendente clicar de novo, vai a mesma chave, e um pedido novo só ganha chave nova depois do sucesso. O botão também fica desabilitado durante o envio.
- **Atualização por consulta periódica** (5 s) em vez de push: simples e suficiente para o volume de um balcão.
- **API fora do ar**: o site mostra "Não foi possível falar com a API" em vez de quebrar.
- **Estados da tela**: "Carregando…" até a primeira resposta (para não afirmar "Nenhum pedido" antes da hora), lista vazia e erro com mensagem em português. Layout que se ajusta a telas pequenas e acessibilidade básica: `lang="pt-BR"`, campos com `<label>`, erros anunciados com `role="alert"` e confirmações com `role="status"`.

---

## Endpoints

| Método | Rota | Quem pode | O que faz |
|---|---|---|---|
| POST | `/api/auth/login` | público | Devolve o token JWT |
| GET | `/api/products` | público | Cardápio (filtros opcionais: `category`, `available`) |
| GET | `/api/products/{id}` | público | Um produto |
| POST | `/api/products` | gerente | Cadastra produto |
| PUT | `/api/products/{id}` | gerente | Altera produto, preço, disponibilidade e estoque |
| GET | `/api/menu/today` | público | Cardápio do dia (clima + feriado) |
| GET | `/api/orders` | equipe | Últimos 100 pedidos; filtros `status` e `customer` (parte do nome) |
| GET | `/api/orders/{id}` | equipe | Um pedido |
| GET | `/api/orders/{id}/history` | equipe | Histórico de situações: quando e por quem |
| POST | `/api/orders` | atendente, gerente | Registra pedido (aceita o cabeçalho `Idempotency-Key`) |
| PATCH | `/api/orders/{id}/status` | barista, gerente | Avança a situação |
| POST | `/api/orders/{id}/cancel` | atendente, gerente | Cancela (só enquanto Recebido) |
| GET | `/api/orders/{id}/receipt` | equipe | Situação do cupom fiscal |
| POST | `/api/orders/{id}/receipt/retry` | gerente | Reenvia um cupom que falhou |

Erros seguem o padrão **ProblemDetails (RFC 7807)**, com mensagem em português e `traceId`:

| Código | Quando |
|---|---|
| 400 | Dados mal formados ou inválidos (campo obrigatório, tamanho, enum inexistente) |
| 401 / 403 | Sem login / perfil sem permissão |
| 404 | Recurso não existe |
| 409 | Conflito com o estado atual: transição inválida, estoque esgotado, produto duplicado, alteração simultânea |
| 422 | Regra de negócio: produto indisponível, pedido sem itens, quantidade zero |

---

## Regras de negócio

Todas ficam nas entidades de domínio (`Domain/`), e não nos controllers. As propriedades têm `private set`, então não há como montar um pedido inválido por fora.

| Regra | Onde | Como |
|---|---|---|
| 1. Produto indisponível não pode ser pedido | `Order.AddItem` | 422 com o nome do produto |
| 2. Pelo menos um item, quantidade > 0 | `Order.Place` / `AddItem` | 422 com o nome do produto; a regra fica só no domínio, para valer também fora da API |
| 3. A API calcula o total | `Order.Place` | O cliente envia só produto e quantidade; não existe campo de preço ou total na entrada |
| 4. Mudar o preço não afeta pedidos existentes | `OrderItem` | O item guarda uma **cópia** do nome, tamanho e preço no momento do pedido |
| 5. Recebido → Preparando → Pronto → Entregue; cancelar só enquanto Recebido | `Order.ChangeStatus` | Máquina de estados: uma tabela com as transições permitidas; qualquer outra dá 409 |

---

## Vá além

### API fora do ar
O cardápio do dia chama o **Open-Meteo** (clima: dia quente sugere bebidas geladas) e a **BrasilAPI** (feriado: horário especial), em paralelo.

- **Resiliência** (`Microsoft.Extensions.Http.Resilience`): 2 s por tentativa, 1 nova tentativa, 5 s no total e *circuit breaker* (se a API está caindo, para de insistir por um tempo).
- **Cache**: clima por 15 min, feriados por 12 h. No pico, a API externa é chamada poucas vezes.
- **Degradação**: se uma API falha, o cardápio responde mesmo assim, sem aquela informação e com um aviso em `warnings`. Nunca 500.

### Quem pode o quê
JWT com perfis (`Attendant`, `Barista`, `Manager`) e `[Authorize(Roles = ...)]` em cada endpoint, de forma visível no controller. **Seguro por padrão**: uma política global exige login em tudo, e só o que é público tem `[AllowAnonymous]`. Um endpoint novo nasce protegido, não aberto. O gerente pode cobrir qualquer posto.

### "Meu pedido sumiu"
Quando o cliente reclama, a equipe tem três fontes:
- **Busca** por parte do nome (`?customer=mari`, sem diferenciar maiúsculas) e por situação.
- **Histórico** de cada pedido: cada mudança de situação com data, hora e **nome de quem fez** (vem do token).
- **Log** estruturado de tudo, **inclusive dos pedidos recusados**. O caso típico de pedido "sumido" é um pedido que nunca existiu (estoque esgotado, produto indisponível). A linha do log traz o mesmo `traceId` da resposta de erro, então o erro que o atendente viu leva direto ao registro.

### Pico das 8h (estoque)
Salgados de lote (pão de queijo, coxinha) têm estoque. Bebidas, feitas na hora, não têm (`stock: null`).

- **Problema**: dois atendentes leem "resta 1" ao mesmo tempo e os dois vendem.
- **Solução**: a conferência e a baixa são **uma única instrução atômica** no banco (`UPDATE ... SET stock = stock - n WHERE id = @id AND stock >= n`). Se nenhuma linha mudou, esgotou → 409.
- Pedido e baixa de estoque na **mesma transação**: se um item falta, nada é gravado.
- Produtos travados **sempre na mesma ordem** (por id), para evitar *deadlock* entre dois pedidos.
- Cancelar devolve o estoque.
- Teste: 10 vendas simultâneas da última unidade → exatamente 1 venda e 9 recusas.

### Toque duplo (idempotência)
O totem envia um `Idempotency-Key` (um GUID gerado ao começar a finalizar):

| Situação | Resposta |
|---|---|
| 1ª requisição | 201: pedido criado |
| Repetição com a mesma chave | 200: **o mesmo pedido**, sem criar outro nem baixar estoque de novo |
| Mesma chave com pedido diferente | 409: erro do cliente, não é repetição |
| Sem chave (balcão) | Funciona normalmente |

A chave é a **chave primária** de uma tabela, gravada na mesma transação do pedido. Mesmo com dois toques no mesmo milissegundo, o banco aceita só um; o outro desfaz tudo e devolve o pedido do primeiro. Teste: 5 requisições simultâneas → 1 pedido, estoque baixado uma vez.

### Nota fiscal
Todo pedido entregue precisa de cupom, e o emissor é lento e às vezes cai. O balcão não pode depender dele.

- **Padrão outbox**: ao marcar "Entregue", a API grava o cupom como **Pendente** na mesma transação. A resposta sai na hora, sem falar com o emissor.
- **`FiscalReceiptWorker`** (BackgroundService) emite os pendentes. Falha ou demora (timeout de 5 s) → nova tentativa com **espera crescente** (5 s, 10 s, 20 s…, teto de 10 min). Esgotadas as 5 tentativas → **Falhou**, e o gerente pode reenviar.
- `FOR UPDATE SKIP LOCKED`: com várias instâncias da API, cada cupom é processado por uma só.
- Se a API cair logo após a entrega, o cupom não se perde: continua pendente no banco.
- O **emissor é simulado** e configurável sem recompilar:
  ```json
  "FiscalEmitter": { "DelayMilliseconds": 1500, "FailureRate": 0.3 }
  ```
  `FailureRate` vai de 0 (nunca falha) a 1 (sempre fora do ar). No Docker: `FiscalEmitter__FailureRate`. Mudar o `appsettings.json` vale sem reiniciar a API.

---

## Decisões e por quê

**Escopo confirmado com o RH** (perguntei antes de começar):
- Pedido **somente no balcão**: sem viagem ou entrega (por isso não há endereço nem ViaCEP).
- Cliente identificado **só pelo nome**: sem cadastro de clientes.
- O **login pode ser simulado**.

**Arquitetura**
- **Um projeto de API organizado por funcionalidade** (`Features/Orders`, `Features/Products`…), com o domínio separado em `Domain/`. Não usei Clean Architecture com 4 projetos nem CQRS/MediatR: para este tamanho, seriam camadas a mais sem ganho real (KISS). As regras ficam no domínio; se o sistema crescer, separar `Domain` em projeto próprio é simples.
- **Domínio rico**: as entidades protegem as próprias regras, em vez de serviços que alteram propriedades públicas.
- **Erros de negócio como exceções tipadas** (`NotFound`, `Conflict`, `BusinessRule`), convertidas para HTTP em um único lugar (`DomainExceptionHandler`). Os controllers não têm try/catch.
- **`TimeProvider` injetado** em vez de `DateTime.Now`, para testar datas e horários.

**Dados**
- **PostgreSQL + EF Core**: migrations versionadas, aplicadas na inicialização. Enums gravados como texto, para o banco continuar legível.
- **Concorrência otimista** no pedido (coluna de sistema `xmin`): se dois baristas mexem no mesmo pedido, o segundo recebe 409 em vez de sobrescrever o primeiro sem perceber.
- **Cardápio de exemplo carregado por código**, e não com `HasData`: com coluna identity no PostgreSQL, `HasData` não avança a sequência, e o primeiro produto criado pela API daria chave duplicada.
- Índice único em (nome, tamanho), com `NULLS NOT DISTINCT`, para impedir dois "Pão de queijo" sem tamanho.
- Produto vendido não pode ser excluído (chave estrangeira com `Restrict`): marca-se como indisponível.

**Testes**
- **Unitários** para as regras do domínio e **de integração** com o **PostgreSQL real** (Testcontainers), e não SQLite ou banco em memória. Concorrência, transações, `SKIP LOCKED` e índices únicos só são testados de verdade no banco de verdade.
- As APIs externas e o emissor fiscal são substituídos por versões falsas nos testes, para simular falha e lentidão sem depender da internet.
- **Telas do Blazor com bUnit**, usando uma API falsa: botões por perfil, mensagens de erro, produto esgotado, "Carregando…" antes da primeira resposta e a mesma `Idempotency-Key` ao repetir um envio.

**Idioma**
- **Código em inglês, tudo que uma pessoa lê em português**: mensagens de erro, validações, telas, commits e este README. Inglês no código mantém a consistência com o próprio .NET e evita a mistura `GetPedidoAsync` / acentos em identificadores. É uma convenção de time: em um time que escreve o domínio em português, eu seguiria o padrão do time.

**Glossário (código → negócio)**

| Código | Negócio |
|---|---|
| `Product`, `ProductCategory`, `ProductSize` | Produto, categoria, tamanho (P/M/G) |
| `Order`, `OrderItem`, `Notes` | Pedido, item, observação |
| `OrderStatus`: `Received`, `Preparing`, `Ready`, `Delivered`, `Canceled` | Situação: Recebido, Preparando, Pronto, Entregue, Cancelado |
| `OrderStatusChange` | Histórico de situações |
| `FiscalReceipt`: `Pending`, `Issued`, `Failed` | Cupom fiscal: Pendente, Emitido, Falhou |
| `Attendant`, `Barista`, `Manager` | Atendente, barista, gerente |
| `Stock` | Estoque |

---

## O que ficou de fora

| Item | Por quê | Como eu faria |
|---|---|---|
| Autenticação real | O RH confirmou que podia ser simulada | ASP.NET Identity ou um provedor OIDC (Entra ID, Keycloak); senhas com hash; chave de assinatura em cofre de segredos, e não no `appsettings` |
| Painel em tempo real | Consulta a cada 5 s atende ao balcão | SignalR: a API avisa os painéis quando um pedido muda |
| Painel "pedido pronto" para a TV (tela 3 do Anexo) | Priorizei as telas de quem trabalha no balcão (pedido, painel do barista, busca, cardápio) | Página pública, sem login, só leitura, com número e primeiro nome dos pedidos prontos (nada de itens ou valores); endpoint próprio `GET /api/orders/ready` marcado `[AllowAnonymous]` e letras grandes para ver de longe |
| Login que sobrevive ao recarregar a página | A sessão fica na conexão do Blazor, de propósito (o token não vai para o navegador) | Cookie de autenticação HttpOnly no site |
| Emissor fiscal real | Fora do escopo; foi pedido simulado | Implementar `IFiscalEmitter` com o provedor de NFC-e e a referência do cupom como chave de idempotência; contingência offline |
| Paginação | A lista devolve os 100 mais recentes, suficiente para o balcão | Paginação por cursor (`?after=id`) |
| Limpeza das chaves de idempotência | Tabela pequena no volume de uma cafeteria | Tarefa periódica que apaga chaves com mais de 24 h |
| Observabilidade | Logs estruturados no console já atendem ao desafio | OpenTelemetry + Seq ou Application Insights, com busca pelo `traceId` |
| Rate limiting e HTTPS no container | Normalmente ficam no gateway ou proxy reverso | `AddRateLimiter` no login; TLS no proxy |
| Motivo do cancelamento | Não pedido | Campo opcional no `POST /cancel`, gravado no histórico |

---

## Uso de IA

**Ferramenta**: [Claude Code](https://claude.com/claude-code) (Anthropic), como par de programação durante todo o desafio.

**Onde foi usada**: planejamento das etapas, geração de código e testes, migrations, Docker e CI, investigação de erros e escrita da documentação.

**Como trabalhei**: eu defini o escopo (incluindo as perguntas ao RH), conduzi uma etapa por vez, revisei cada mudança, rodei a API no Visual Studio e testei cada funcionalidade manualmente no Swagger e no navegador antes de seguir. Cada commit corresponde a uma etapa revisada.

### O que eu encontrei testando e que a IA não tinha percebido
- **Resposta 422 aparecendo como "Undocumented" no Swagger**: faltava declarar `ProducesResponseType(422)` nos endpoints.
- **Mensagens misturando idiomas** ("de Received para Delivered"): passaram a usar os nomes em português (Recebido, Preparando…).
- **Página do Swagger respondendo 401** depois de ligar o "seguro por padrão": a política global também bloqueava o conteúdo estático do Swagger UI. A IA tinha testado só o `openapi/v1.json`. Corrigido e coberto por teste.
- **Login travando a tela do site com a API desligada**: em vez da mensagem "Não foi possível falar com a API", a página caía em "Reconectando ao servidor". O tratamento de falha de conexão existia em todas as chamadas, menos no login, que usava um caminho próprio. Corrigido, com teste.
- Para confirmar que os testes realmente protegem as regras, troquei `<= 0` por `< 0` na validação da quantidade: o teste do valor-limite falhou na hora.

### O que a IA errou e foi corrigido
- Atributos de validação em `record` com `[property: Required]`: compilava, mas gerava **erro 500** em tempo de execução no ASP.NET MVC. Movidos para o parâmetro.
- **Configuração do JWT**: o gerente recebia 403 ao criar produto, porque o ASP.NET Core renomeia o claim `role` por padrão. Os testes de autorização pegaram; corrigido com `MapInboundClaims = false`.
- **Detalhes internos vazando**: enviar uma situação inexistente devolvia uma mensagem com nomes de tipos internos. Corrigido, com teste garantindo que não vaza.
- **Cardápio ordenado "G, M, P"**: o banco ordenava o tamanho como texto. Corrigido para a ordem do enum.
- **`traceId` do log diferente do da resposta**: foi usado `HttpContext.TraceIdentifier`, mas o ProblemDetails usa `Activity.Current.Id`. Corrigido, com teste comparando os dois.
- Código que não compilava (`?? throw;`), comando de migration com a opção errada (`-c` é *contexto*, não *configuração*) e construtor obsoleto do Testcontainers.
- **Conflito de versões do EF Core** (aviso MSB3277) entre o pacote do Npgsql e o pacote Design: resolvido com referência explícita à versão correta.

### O que foi conferido em vez de aceito
- A migration de concorrência gerou um `AddColumn xmin`, o que pareceria um erro (`xmin` é coluna de sistema do PostgreSQL). Conferi no banco: o Npgsql ignora esse comando, nenhuma coluna é criada e a `xmin` nativa já muda a cada alteração.
- Conferi que os testes de integração usam o banco descartável do container, e não o banco de desenvolvimento.
- Os testes de concorrência (estoque, toque duplo) e os de cupom fiscal, que dependem de tempo, foram repetidos várias vezes seguidas para garantir que não passavam por sorte.
- Os testes do frontend passaram de primeira, o que pede desconfiança. Removi de propósito a checagem de perfil do botão "Cancelar", e o teste do barista falhou na hora. Depois a checagem voltou.
- Os testes do bUnit usam respostas falsas, então não provam que o site entende o JSON real. Por isso foi criado o teste de contrato contra a API de verdade. Ao montá-lo, apareceu um conflito: no .NET 10 a classe `Program` dos projetos web é pública, e o projeto de testes passou a enxergar duas. Resolvido com `extern alias`.

### O que foi decidido diferente da primeira sugestão
- **ViaCEP descartada** como API pública: o RH confirmou que não há entrega. Ficaram clima e feriados, que mudam o cardápio de verdade.
- **Testcontainers exigia uma versão mais nova do Docker** do que a instalada. Em vez de rebaixar a biblioteca, atualizei o Docker Desktop e o WSL.
- **Cancelamento em endpoint próprio**, e não como mais uma situação no `PATCH`: quem cancela (atendente) é diferente de quem prepara (barista), e assim cada permissão fica declarada no seu `[Authorize]`.
- **Nota fiscal com tabela no PostgreSQL (outbox)**, e não com fila externa (RabbitMQ): a mesma garantia de não perder o cupom, sem mais uma infraestrutura para o avaliador subir.

---

## Estrutura do projeto

```
src/Expresso.Api/
  Domain/            Entidades e regras: Product, Order, OrderItem, FiscalReceipt, exceções
  Data/              DbContext, migrations e cardápio de exemplo
  ErrorHandling/     Exceções → ProblemDetails
  Features/
    Auth/            Login simulado, perfis, JWT
    Products/        Cardápio
    Orders/          Pedidos, situações, estoque, idempotência
    Menu/            Cardápio do dia (Open-Meteo + BrasilAPI)
    Receipts/        Cupom fiscal: emissor simulado e worker em segundo plano
src/Expresso.Web/    Painel Blazor
  Api/               Cliente da API, sessão do usuário e textos em português
  Components/Pages/  Login, Painel, Novo pedido, Buscar, Detalhe, Cardápio
tests/Expresso.Tests/
  Domain/, Menu/     Testes unitários
  Integration/       API + PostgreSQL real (Testcontainers) e contrato com o cliente do site
tests/Expresso.Web.Tests/  Telas do Blazor (bUnit) com uma API falsa
```

# Sistema Bancário — Microsserviços (.NET 10)

C# ASP.NET Core Web API com arquitetura de microsserviços.

## Serviços

| Serviço         | Porta | Banco           | Swagger                       |
|-----------------|-------|-----------------|-------------------------------|
| MS-Contas       | 5001  | contas.db       | http://localhost:5001/swagger |
| MS-Transacoes   | 5002  | transacoes.db   | http://localhost:5002/swagger |
| MS-Notificacoes | 5003  | notificacoes.db | http://localhost:5003/swagger |

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10)

## Como executar

Abra 3 terminais e inicie na ordem abaixo:

```bash
cd MS-Contas && dotnet run
cd MS-Notificacoes && dotnet run
cd MS-Transacoes && dotnet run
```

> O banco SQLite é criado automaticamente na primeira execução.

## Endpoints

### MS-Contas (5001)

| Método | Rota               | Descrição                           |
|--------|--------------------|-------------------------------------|
| POST   | /contas            | Criar conta                         |
| GET    | /contas/{id}       | Buscar conta + resumo de transações |
| GET    | /contas/{id}/saldo | Consultar saldo                     |
| PATCH  | /contas/{id}/saldo | Atualizar saldo (interno)           |
| DELETE | /contas/{id}       | Encerrar conta                      |
| GET    | /health            | Health check                        |

### MS-Transacoes (5002)

| Método | Rota                                  | Descrição                                        |
|--------|---------------------------------------|--------------------------------------------------|
| POST   | /transacoes/deposito                  | Realizar depósito                                |
| POST   | /transacoes/saque                     | Realizar saque                                   |
| POST   | /transacoes/transferencia             | Transferência                                    |
| POST   | /transacoes/transferencia-consolidada | Transferência (retorna saldos + status notificação) |
| GET    | /transacoes/{contaId}                 | Extrato                                          |
| GET    | /transacoes/{contaId}/resumo          | Resumo de transações                             |
| GET    | /health                               | Health check                                     |

### MS-Notificacoes (5003)

| Método | Rota                    | Descrição                         |
|--------|-------------------------|-----------------------------------|
| POST   | /notificacoes/enviar    | Enviar notificação                |
| GET    | /notificacoes/{contaId} | Histórico por conta + nome titular |
| GET    | /notificacoes           | Todas as notificações             |
| GET    | /health                 | Health check                      |

## Arquitetura de integração HTTP

Os 3 microserviços formam uma **malha completa (full mesh)**: cada serviço chama os outros dois via HTTP usando `IHttpClientFactory`. Não existe API Gateway — a comunicação é ponto a ponto.

```
┌─────────────────────────────────────────────────────────────┐
│                    MALHA DE INTEGRAÇÃO                      │
│                                                             │
│   ┌──────────────┐          ┌──────────────────────────┐   │
│   │  MS-Contas   │◄─────────│      MS-Transacoes       │   │
│   │   :5001      │──────────►          :5002            │   │
│   └──────┬───────┘  saldo   └────────────┬─────────────┘   │
│          │                               │                  │
│          │ notificação                   │ notificação      │
│          │                               │                  │
│          ▼                               ▼                  │
│   ┌──────────────────────────────────────────────────┐     │
│   │               MS-Notificacoes  :5003              │     │
│   └──────────────────────────────────────────────────┘     │
│          │                                                  │
│          └──────────────────────────► MS-Contas (titular)  │
└─────────────────────────────────────────────────────────────┘
```

### Fluxos por operação

**Criar / Encerrar conta**
```
Cliente ──► MS-Contas ──► MS-Notificacoes
                          (boas-vindas / encerramento)
```

**Buscar conta** (`GET /contas/{id}`)
```
Cliente ──► MS-Contas ──► MS-Transacoes  (resumo: qtd + última transação)
```

**Depósito / Saque**
```
Cliente ──► MS-Transacoes ──► MS-Contas       (consulta saldo)
                          ──► MS-Contas       (atualiza saldo)
                          ──► MS-Notificacoes (avisa titular)
```

**Transferência**
```
Cliente ──► MS-Transacoes ──► MS-Contas  (consulta conta origem)
                          ──► MS-Contas  (debita origem)
                          ──► MS-Contas  (credita destino)
                          ──► MS-Notificacoes (avisa origem)
                          ──► MS-Notificacoes (avisa destino)
```

**Histórico de notificações** (`GET /notificacoes/{contaId}`)
```
Cliente ──► MS-Notificacoes ──► MS-Contas  (busca nome do titular)
```

### Dependências entre serviços

| Serviço         | Chama                          | Razão                              |
|-----------------|--------------------------------|------------------------------------|
| MS-Contas       | MS-Transacoes, MS-Notificacoes | Resumo de transações e notificações |
| MS-Transacoes   | MS-Contas, MS-Notificacoes     | Saldo e notificações               |
| MS-Notificacoes | MS-Contas                      | Nome do titular no histórico       |

> Falhas nas chamadas HTTP são tratadas com `try/catch` — o serviço loga o aviso mas não derruba a operação principal.

## Tecnologias

- ASP.NET Core Web API (.NET 10)
- Entity Framework Core + SQLite
- IHttpClientFactory (comunicação entre serviços)
- Swagger / Swashbuckle

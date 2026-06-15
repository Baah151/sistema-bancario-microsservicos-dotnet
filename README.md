# Sistema Bancário — Microsserviços (.NET 8)

Conversão do projeto Node.js para C# ASP.NET Core Web API com arquitetura de microsserviços.

## Serviços

| Serviço          | Porta | Banco           | Swagger                          |
|------------------|-------|-----------------|----------------------------------|
| MS-Contas        | 5001  | contas.db       | http://localhost:5001/swagger    |
| MS-Transacoes    | 5002  | transacoes.db   | http://localhost:5002/swagger    |
| MS-Notificacoes  | 5003  | notificacoes.db | http://localhost:5003/swagger    |

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)

## Como executar

Abra **3 terminais** separados:

```bash
# Terminal 1 — MS-Contas
cd MS-Contas
dotnet run
```

```bash
# Terminal 2 — MS-Notificacoes (inicie antes do MS-Transacoes)
cd MS-Notificacoes
dotnet run
```

```bash
# Terminal 3 — MS-Transacoes
cd MS-Transacoes
dotnet run
```

> O banco SQLite é criado automaticamente na primeira execução (`EnsureCreated`).

## Endpoints

### MS-Contas (porta 5001)

| Método | Rota                        | Descrição                          |
|--------|-----------------------------|------------------------------------|
| POST   | /contas                     | Criar conta                        |
| GET    | /contas/{id}                | Buscar conta                       |
| GET    | /contas/{id}/saldo          | Consultar saldo                    |
| PATCH  | /contas/{id}/saldo          | Atualizar saldo (uso interno)      |
| DELETE | /contas/{id}                | Encerrar conta                     |
| GET    | /health                     | Health check                       |

### MS-Transacoes (porta 5002)

| Método | Rota                          | Descrição         |
|--------|-------------------------------|-------------------|
| POST   | /transacoes/deposito          | Realizar depósito |
| POST   | /transacoes/saque             | Realizar saque    |
| POST   | /transacoes/transferencia     | Transferência     |
| GET    | /transacoes/{contaId}         | Extrato           |
| GET    | /health                       | Health check      |

### MS-Notificacoes (porta 5003)

| Método | Rota                          | Descrição                   |
|--------|-------------------------------|-----------------------------|
| POST   | /notificacoes/enviar          | Enviar notificação          |
| GET    | /notificacoes/{contaId}       | Histórico por conta         |
| GET    | /notificacoes                 | Todas as notificações       |
| GET    | /health                       | Health check                |

## Fluxo de uma transação

```
Cliente → MS-Transacoes
             ├── GET /contas/{id}         (MS-Contas)
             ├── PATCH /contas/{id}/saldo (MS-Contas)
             ├── INSERT transacao         (transacoes.db)
             └── POST /notificacoes/enviar (MS-Notificacoes — falha tolerada)
```

## Regras de negócio preservadas

- CPF único por conta
- Conta inativa não aceita movimentações
- Saldo insuficiente retorna HTTP 422
- Conta com saldo não pode ser encerrada
- Falha no MS-Notificacoes **não** cancela a transação
- Transferência entre a mesma conta é bloqueada

## Tecnologias

- ASP.NET Core Web API (.NET 8)
- Entity Framework Core + SQLite
- HttpClient (IHttpClientFactory)
- Swagger / Swashbuckle

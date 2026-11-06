# ExampleAuth

Identity Provider corporativo: centraliza usuários e autenticação para os web apps
da empresa via OAuth 2.1 / OpenID Connect, usando [OpenIddict](https://documentation.openiddict.com/)
como servidor de autorização.

## Stack

- .NET 10 / ASP.NET Core
- OpenIddict 7.6 (authorization code + PKCE, client credentials, refresh token)
- ASP.NET Core Identity (usuários, senhas, roles)
- Entity Framework Core 10 + Npgsql (PostgreSQL)

## Estrutura

```
src/ExampleAuth.Server/
  Program.cs                    # wiring de Identity, OpenIddict e pipeline HTTP
  Data/ExampleAuthDbContext.cs      # DbContext (Identity + tabelas do OpenIddict, uuid em ambos)
  Data/DbInitializer.cs         # aplica migrations e faz o bootstrap inicial
  Models/ApplicationUser.cs     # usuário (IdentityUser<Guid> + DisplayName, DisabledAtUtc/DisabledByUserId)
  Models/ClientAccessGrant.cs   # concessão de acesso usuário↔client (licenciamento)
  Services/OpenIddictClientDescriptors.cs  # shape compartilhado de client OAuth por tipo (SPA público / M2M)
  Services/ClientAccessService.cs          # conceder/revogar/checar acesso a um client
  Controllers/                  # /connect/authorize, /connect/token, /connect/userinfo, /connect/logout
  Controllers/ClientUsersController.cs  # API M2M: /api/users, cada client gerencia seus próprios usuários
  Pages/Account/Login.cshtml    # tela de login mínima (Razor Pages)
  Pages/Account/ClientAccessDenied.cshtml   # tela de "sem licença para este sistema"
  Pages/Account/AccountDisabled.cshtml      # tela de "conta desabilitada"
  Pages/Index.cshtml            # dashboard do painel (Administrator ou Support)
  Pages/Clients/                # gestão de clients OAuth via UI (list/create/delete/details+acessos) — Administrator-only
  Pages/Users/                  # gestão de usuários via UI (list/create/edit: nome, role, vínculos, status)
```

## Configuração

O `appsettings.json` guarda apenas o nível de log. Nada específico de ambiente (connection
string, SMTP, URL pública...) nem nenhuma credencial fica nele: em produção (Azure, AWS etc.)
tudo vem de variáveis de ambiente definidas na publicação; em desenvolvimento, de um
`appsettings.Development.json` local (veja abaixo).

Variáveis de ambiente (o `__` mapeia para `:` na configuração hierárquica do ASP.NET Core):

| Variável | Obrigatória | Descrição |
|---|---|---|
| `ConnectionStrings__Default` | sim | Connection string do PostgreSQL, ex.: `Host=localhost;Port=5432;Database=exampleauth;Username=exampleauth;Password=xxx` |
| `Smtp__Host` / `Smtp__Port` / `Smtp__Username` / `Smtp__Password` / `Smtp__FromAddress` | sim, para envio de e-mail | Servidor SMTP usado nos e-mails de confirmação de conta e reset de senha. Opcionais: `Smtp__FromName` (padrão `ExampleAuth`) e `Smtp__UseStartTls` (padrão `true`) |
| `ExampleAuth__PublicBaseUrl` | sim, fora de Development | URL pública do IdP (ex.: `https://auth.example.com`), usada para montar os links dos e-mails sem confiar no header `Host` |
| `ExampleAuth__AllowedHosts` | sim, fora de Development | Hostnames aceitos no header `Host`, separados por `;` (ex.: `auth.example.com`). Qualquer outro `Host` recebe `400` (exceto `/health`) |
| `EXAMPLEAUTH_ADMIN_EMAIL` / `EXAMPLEAUTH_ADMIN_PASSWORD` | não | Se definidas, cria o *primeiro* usuário administrador (`Administrator`) no primeiro start, caso ainda não exista — necessário pra existir alguém que promova os demais (veja abaixo) |
| `EXAMPLEAUTH_SIGNING_CERT_PATH` / `EXAMPLEAUTH_SIGNING_CERT_PASSWORD` | sim, fora de Development | Certificado PFX usado para assinar/criptografar os tokens em produção. Em Development, o OpenIddict gera um certificado de desenvolvimento automaticamente |

### Configuração local (Development)

Copie o template versionado para o seu arquivo pessoal, que é ignorado pelo git, e preencha
com as suas credenciais:

```bash
cd src/ExampleAuth.Server
cp appsettings.Development.example.json appsettings.Development.json
```

O template já vem com valores compatíveis com o `docker-compose.yml` (connection string do
Postgres local). Preencha o bloco `Smtp` se quiser testar o envio de e-mail. Mantenha também
`ExampleAuth:AllowedHosts` e `ExampleAuth:PublicBaseUrl`: sem o primeiro, toda requisição
recebe `400`; sem o segundo, os links dos e-mails ficam sem base. Nunca faça commit do
`appsettings.Development.json`: o `.gitignore` já o ignora, só o `.example` é versionado.

## Rodando localmente

1. Suba um PostgreSQL local via Docker Compose (`docker-compose.yml` na raiz do repo):

   ```bash
   docker compose up -d
   ```

   Isso cria um Postgres 16 em `localhost:5432` (usuário/senha/banco:
   `postgres`/`postgres`/`exampleauth`) com os dados persistidos num volume nomeado
   (`exampleauth_pgdata`). `docker compose down` para o container mantendo os dados;
   `docker compose down -v` também remove o volume, zerando o banco.
2. Instale a ferramenta `dotnet-ef` (uma vez só) e rode as migrations:

   ```bash
   dotnet tool install --global dotnet-ef --version 10.0.10
   export ConnectionStrings__Default="Host=localhost;Port=5432;Database=exampleauth;Username=postgres;Password=postgres"
   cd src/ExampleAuth.Server
   dotnet ef database update
   ```

   Em `Development`, `dotnet ef` também lê o `appsettings.Development.json` (ver acima),
   então não precisa exportar a variável se você já copiou e preencheu esse arquivo.

3. (Opcional) para já nascer com um admin:

   ```bash
   export EXAMPLEAUTH_ADMIN_EMAIL="admin@example.com"
   export EXAMPLEAUTH_ADMIN_PASSWORD="TrocarDepois123!"
   ```

4. Rode a aplicação:

   ```bash
   dotnet run
   ```

   Em `Development`, as migrations são aplicadas automaticamente no start
   (`DbInitializer.RunAsync`), junto com o bootstrap de admin/client acima.

### Rodando pelo VS Code

O repo já tem `.vscode/launch.json` e `.vscode/tasks.json` prontos — abra o painel
"Run and Debug" e escolha **"ExampleAuth.Server (https)"** (builda antes de rodar e abre
o Scalar automaticamente em `/scalar/v1` assim que o Kestrel sobe) ou
**"ExampleAuth.Server (http)"** pra rodar sem TLS.

## Endpoints

- `GET /.well-known/openid-configuration` — discovery OIDC (gerado automaticamente pelo OpenIddict)
- `GET /connect/authorize` — authorization endpoint (redireciona para `/Account/Login` se não autenticado)
- `POST /connect/token` — token endpoint (`authorization_code` + PKCE, `client_credentials`, `refresh_token`)
- `GET/POST /connect/userinfo` — claims do usuário autenticado
- `GET/POST /connect/logout` — encerra a sessão
- `GET /health` — healthcheck simples
- `POST /api/users` — cria um usuário e concede vínculo automático ao client chamador
- `PATCH /api/users/{id}` — edita o `displayName` de um usuário vinculado ao client chamador
- `POST /api/users/{id}/reset-password` — define nova senha de um usuário vinculado ao client chamador
- `DELETE /api/users/{id}/access?actingUserId={guid}` — encerra o vínculo do usuário com o client chamador

`/api/users` é uma API M2M (machine-to-machine): cada client OAuth gerencia só os
seus próprios usuários, autenticado via token `client_credentials` (bearer, esquema
de validação do OpenIddict — **não** o cookie de autenticação da Identity) que
carregue o scope `users.manage`, opt-in por client (marcado na criação em
`/Clients/Create`, não concedido por padrão). Toda ação valida que o usuário alvo
tem um vínculo (`ClientAccessGrant`) com o client chamador antes de tocar nele —
sem vínculo, `404` (não revela se o usuário existe sob outro client). Toda chamada
também exige `actingUserId`, o id ExampleAuth do usuário humano do lado do client que
disparou a ação, só para auditoria (`CreatedByUserId`/`GrantedByUserId`/
`RevokedByUserId`) — `400` se ausente ou inexistente. Contas com a role
`Administrator` são completamente invisíveis por essa API (`404`), mesmo que
tenham (indevidamente) um vínculo com o client.

## Painel administrativo (web)

Além da API, existe uma área web pra gestão sem precisar de curl/Scalar — pensada
pra produção, onde o Scalar não fica disponível:

- `/` — dashboard (contagem de usuários e clients)
- `/Clients` — lista de clients OAuth registrados, com botão de remoção
- `/Clients/Create` — formulário pra registrar um novo client (client id, secret
  opcional — em branco gera um client público —, nome, redirect URIs)
- `/Clients/Details/{clientId}` — detalhe do client + lista de usuários com acesso
  concedido, com formulário pra conceder/revogar
- `/Users` — lista de usuários (email, nome, roles, status)
- `/Users/Edit/{id}` — edição do usuário: nome de exibição (Administrator e Support),
  role (só Administrator, seletor Nenhum/Support/Administrator), status
  ativo/desabilitado (botão só pra Administrator, veja seção própria abaixo) e lista
  de clients aos quais ele tem acesso (edição — conceder/revogar — só pra
  Administrator; Support enxerga a lista, mas sem os controles)

Existem duas roles de equipe: `Administrator` e `Support`. As duas conseguem logar e
acessar `/`, `/Users` e `/Users/Edit/{id}` (edição de nome inclusa) — só
`Administrator` acessa `/Clients/*` e só `Administrator` consegue conceder/revogar
vínculo com client ou alterar a role de outro usuário. Um Administrador nunca
consegue tirar a própria role de Administrator por essa tela (só a de outra pessoa);
não tem limite parecido pra rebaixar/remover a role de qualquer outro usuário,
incluindo outros Administrators. Quem estiver logado sem nenhuma das duas roles cai
em `/Account/AccessDenied`. A primeira conta `Administrator` só nasce via
`EXAMPLEAUTH_ADMIN_EMAIL`/`_PASSWORD` no bootstrap (acima); a partir daí, promover
alguém a `Administrator` ou `Support` é feito por um Administrator existente em
`/Users/Edit/{id}` — não precisa mexer no banco. A API M2M (`/api/users`) usa um
modelo de autorização totalmente diferente — não essas roles, veja a seção
"Endpoints" acima.

## Controle de acesso por client (licenciamento)

Autenticar no ExampleAuth não é o mesmo que ter acesso a um sistema específico: cada
usuário só consegue completar o login (`/connect/authorize`) num client OAuth depois
que um administrador concede esse acesso explicitamente — reflete o modelo comercial
(usuário só usa o Nutriun, por exemplo, depois de contratar a licença dele), não uma
lista de permissões técnica.

- **Nega por padrão**: um client novo começa sem ninguém autorizado; não existe
  "client aberto pra todo mundo".
- **`Administrator` sempre passa**, sem precisar de concessão explícita — `Support`
  não tem esse bypass, precisa de concessão como qualquer usuário comum.
- Quem tenta logar num client sem acesso cai numa tela própria do ExampleAuth
  (`/Account/ClientAccessDenied`) informando qual sistema está bloqueado — o client
  nunca recebe um `code`, então não precisa de nenhuma mudança pra lidar com isso.
- Um administrador concede e revoga acesso em `/Clients/Details/{clientId}` ou
  `/Users/Edit/{id}` (mesma informação, dois pontos de entrada). Um client com
  o scope `users.manage` também concede acesso implicitamente ao criar um usuário
  via `POST /api/users`, e revoga o próprio vínculo via
  `DELETE /api/users/{id}/access` — só para usuários já vinculados a ele mesmo.
- Revogar também derruba qualquer sessão OAuth já emitida pra aquele usuário+client
  (o próximo refresh token falha), não só bloqueia tentativas futuras de login.
- Não afeta `client_credentials` (chamada máquina-a-máquina, sem usuário por trás) —
  só fluxos com um usuário humano (`authorization_code`) passam por essa checagem.
## Desabilitar conta de usuário

Só um `Administrator` pode desabilitar ou reabilitar outro usuário, pela tela
`/Users/Edit/{id}`. É diferente do bloqueio automático por 5 tentativas de senha
erradas (esse expira sozinho em 5 min) — desabilitar é permanente até um
Administrator reverter, e bloqueia a conta por completo, não só o login:

- Bloqueia `/Account/Login` (mensagem "Conta desabilitada"), o fluxo SSO em
  `/connect/authorize` para qualquer client (cai em `/Account/AccountDisabled`) e a
  renovação de token (`refresh_token` em `/connect/token`).
- Ao desabilitar, qualquer refresh token já emitido para o usuário em **qualquer**
  client é revogado na hora (não precisa esperar o próximo uso). Uma sessão/cookie do
  painel admin já aberta no navegador continua valendo até expirar (8h) — mesmo
  racional já aceito hoje para a revogação de vínculo com client, acima.
- Não mexe em nenhum `ClientAccessGrant` — é ortogonal ao licenciamento por client.
- Um `Administrator` não pode desabilitar nem reabilitar a própria conta, só a de
  outro usuário — incluindo outros Administrators.

## Documentação interativa da API (Scalar)

Em `Development`, o OpenAPI é gerado automaticamente e exposto com uma UI interativa:

- `GET /openapi/v1.json` — documento OpenAPI cru
- `GET /scalar/v1` — UI (Scalar) pra explorar e testar `/api/users` (exige um bearer
  token `client_credentials` com scope `users.manage` — o cookie de login não serve
  aqui, ao contrário do resto do painel)

Só cobre as rotas de API (`/api/*`, `/health`); os endpoints OAuth (`/connect/*`) não
aparecem aqui — eles seguem o protocolo OAuth 2.1/OIDC, não fazem sentido testados
como um endpoint REST solto. Diferente do resto do painel, `/api/users` não aceita o
cookie de autenticação da Identity — é preciso obter um token `client_credentials`
(ver seção seguinte) e colar como bearer token na UI do Scalar.

## Testando o fluxo `client_credentials` (sem UI)

```bash
curl -X POST https://localhost:<porta>/connect/token \
  -d grant_type=client_credentials \
  -d client_id=test-client \
  -d client_secret=<segredo> \
  -d scope=api
```

## O que falta para produção

- Certificado de assinatura/encriptação real (`EXAMPLEAUTH_SIGNING_CERT_PATH`)
- `ExampleAuth:PublicBaseUrl` configurado com o hostname público real do deploy (usado para
  montar links de email — reset de senha e confirmação — sem depender do header `Host`
  da requisição) e `ExampleAuth:AllowedHosts` ajustado de acordo
- Rate limiting no login (`/Account/Login`) e no `/connect/token` — hoje só o pedido de
  reset de senha (`/Account/ForgotPassword`) tem rate limiting (por IP)

## Status

Validado ponta a ponta contra um PostgreSQL real: migrations aplicadas, bootstrap de
admin, login via `/Account/Login`, dashboard, criação/listagem/remoção de client OAuth
pelo painel web, confirmação de que um usuário sem a role `Administrator` é bloqueado
no painel (`/Account/AccessDenied`), a API M2M `/api/users` (criação/edição/reset de
senha/revogação de vínculo, isolamento entre clients, bloqueio de contas
`Administrator`), e o fluxo OAuth completo:

- `authorization_code` + PKCE de ponta a ponta — `/connect/authorize` emitindo `code`,
  troca em `/connect/token`, e renovação via `refresh_token`.
- `client_credentials` (machine-to-machine, sem usuário).
- Controle de acesso por client: usuário sem concessão cai em
  `/Account/ClientAccessDenied` sem gerar `code`; após concessão (pela tela do client
  ou do usuário) o login completa normalmente; `Administrator` sempre passa; revogar
  invalida o refresh token já emitido (próxima renovação falha com `invalid_grant`).
- Emails transacionais (confirmação de conta e reset de senha) via SMTP (`SmtpEmailSender`),
  com o link montado a partir de `ExampleAuth:PublicBaseUrl` (configuração), não do header
  `Host` da requisição. Reset de senha usa token dedicado de 1h
  (`PasswordResetTokenProvider`), respeita `IsDisabled`, revoga tokens existentes
  (`RevokeBySubjectAsync`) ao concluir, e é auditado no `LoginAudit` (pedido e conclusão),
  igual ao login. `/Account/ForgotPassword` tem rate limiting de 5 pedidos/15min por IP.

Ainda não testado: um app cliente real (SPA/backend) consumindo o `access_token`
(userinfo, chamadas autenticadas) — só o fluxo de emissão/renovação de token foi
validado, não o consumo por um client de verdade.

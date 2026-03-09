# 🗳️ SVB — Sistema de Votação Biométrica

> Guia completo e didático para programadores iniciantes, estudantes e curiosos.  
> Aqui explicamos **cada componente, cada linha de código e cada conceito** do projeto — desde o que é um microcontrolador até o que é MVVM.

---

## 📋 Índice

1. [O que é o SVB?](#1-o-que-é-o-svb)
2. [Visão geral do sistema](#2-visão-geral-do-sistema)
3. [Os componentes físicos (Hardware)](#3-os-componentes-físicos-hardware)
4. [O que é um ESP32?](#4-o-que-é-um-esp32)
5. [O que é um sensor biométrico?](#5-o-que-é-um-sensor-biométrico)
6. [Comunicação Serial — o "cabo telefônico" dos computadores](#6-comunicação-serial--o-cabo-telefônico-dos-computadores)
7. [A estrutura do software](#7-a-estrutura-do-software)
8. [Parte 1 · O Firmware (código do ESP32)](#8-parte-1--o-firmware-código-do-esp32)
9. [O que é uma API?](#9-o-que-é-uma-api)
10. [Parte 2 · A API em C#](#10-parte-2--a-api-em-c)
11. [O que é Entity Framework e MySQL?](#11-o-que-é-entity-framework-e-mysql)
12. [O que é MVVM?](#12-o-que-é-mvvm)
13. [Parte 3 · A Interface gráfica em .NET MAUI](#13-parte-3--a-interface-gráfica-em-net-maui)
14. [Como tudo se conecta — fluxo completo de um voto](#14-como-tudo-se-conecta--fluxo-completo-de-um-voto)
15. [Como instalar e rodar no PC](#15-como-instalar-e-rodar-no-pc)
16. [Estrutura de pastas do projeto](#16-estrutura-de-pastas-do-projeto)
17. [Protocolo serial detalhado](#17-protocolo-serial-detalhado)
18. [Perguntas frequentes](#18-perguntas-frequentes)

---

## 1. O que é o SVB?

O **SVB (Sistema de Votação Biométrica)** é um sistema completo para realizar votações onde cada pessoa é identificada pela sua **impressão digital** antes de poder votar. Isso garante que:

- Cada pessoa vote **uma única vez**
- Não seja possível votar no lugar de outra pessoa
- Os votos sejam guardados de forma segura numa base de dados

Pense nele como uma urna eletrônica que lê o dedo em vez de um cartão.

---

## 2. Visão geral do sistema

O SVB é composto por **três partes** que comunicam entre si:

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│   [ELEITOR]                                                 │
│      │ coloca o dedo                                        │
│      ▼                                                      │
│  ┌──────────┐    cabo USB / Serial    ┌──────────────────┐  │
│  │  ESP32   │ ◄──────────────────── ► │   API C#         │  │
│  │ + Sensor │    protocolo ASCII       │   (servidor)     │  │
│  │ + LCD    │                         │   ▼              │  │
│  │ + Botões │                         │   MySQL          │  │
│  └──────────┘                         └──────────────────┘  │
│                                              │               │
│                                       ┌──────────────────┐  │
│                                       │  Interface MAUI  │  │
│                                       │  (Windows UI)    │  │
│                                       └──────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

| Parte | Tecnologia | Função |
|---|---|---|
| **Dispositivo físico** | ESP32 + Arduino | Lê a impressão digital, mostra mensagens no ecrã, aceita botões |
| **Servidor (API)** | C# / ASP.NET Core | Guarda os votos, verifica fraudes, expõe dados |
| **Interface gráfica** | .NET MAUI (Windows) | Painel de gestão com resultados, eleitores, monitor serial |

---

## 3. Os componentes físicos (Hardware)

Olhando para o esquema elétrico do projeto, temos:

### ESP32-WROOM
O "cérebro" do dispositivo. Um microcontrolador com WiFi e Bluetooth integrados.

### Sensor Biométrico (I²C)
Sensor de impressão digital. Ligado aos pinos **GPIO16 (RX)** e **GPIO17 (TX)** do ESP32 via UART2.

### LCD 16×2 com I²C (JHD-2X16-I2C)
Ecrã de 16 colunas × 2 linhas onde aparecem as mensagens ao eleitor.  
Ligado via barramento **I²C** (pinos **GPIO21 = SDA** e **GPIO22 = SCL**).

### Botão 1 (BTN_CONFIRM) — GPIO 21
Usado pelo eleitor para **confirmar** o voto.

### Botão 2 (BTN_CANCEL) — GPIO 22
Usado pelo eleitor para **cancelar** a operação.

### LED D1 (LED_OK) — GPIO 25
LED vermelho que pisca em verde quando o voto é bem-sucedido.

### LED D2 (LED_ERROR) — GPIO 26
LED vermelho que pisca quando há erro, acesso negado ou cancelamento.

### Resistores R1–R4 (30kΩ)
Resistências de pull-down/pull-up nos botões — garantem que o pino lê `LOW` quando o botão não está pressionado e `HIGH` quando está.

---

## 4. O que é um ESP32?

O **ESP32** é um microcontrolador — um computador minúsculo que cabe na palma da mão. Ao contrário de um computador normal, ele não tem sistema operativo, ecrã nem teclado. Em vez disso, ele executa um único programa escrito pelo programador (chamado **firmware**).

**Características relevantes:**
- Processador de 240 MHz (muito mais lento que um PC, mas suficiente para controlar hardware)
- 520 KB de RAM
- WiFi e Bluetooth integrados
- Pinos GPIO (General Purpose Input/Output) — pinos físicos onde podemos ligar componentes

**Como programamos o ESP32?**  
Usamos a **Arduino IDE** e escrevemos código em **C++** (simplificado). O código tem sempre duas funções obrigatórias:

```cpp
void setup() {
    // Executado UMA VEZ ao ligar o dispositivo
    // Aqui inicializamos componentes (LCD, sensor, botões…)
}

void loop() {
    // Executado em LOOP INFINITO enquanto o dispositivo estiver ligado
    // Aqui verificamos botões, lemos o sensor, comunicamos com a API…
}
```

---

## 5. O que é um sensor biométrico?

Um sensor biométrico de impressão digital funciona assim:

1. **Captura a imagem** da impressão digital (`getImage`)
2. **Converte a imagem** num modelo matemático (conjunto de pontos característicos do dedo) (`image2Tz`)
3. **Compara esse modelo** com todos os modelos guardados na memória interna do sensor (`fingerFastSearch`)
4. Se encontrar correspondência, devolve o **ID do eleitor** (número de 1 a 127)

O sensor tem memória interna onde guardamos as impressões digitais de cada eleitor. Cada eleitor tem um número (ex: eleitor nº 5 → dedo guardado no slot 5).

---

## 6. Comunicação Serial — o "cabo telefônico" dos computadores

### O que é comunicação serial?

Imaginem que dois dispositivos precisam de "falar" um com o outro. A comunicação serial é como uma linha telefônica — os dados são enviados **bit a bit** (0s e 1s) por um único fio, em sequência.

**UART (Universal Asynchronous Receiver-Transmitter)** é o protocolo mais comum de comunicação serial. Tem dois fios:
- **TX** — Transmit (enviar dados)
- **RX** — Receive (receber dados)

Ao ligar dois dispositivos, o TX de um vai ao RX do outro e vice-versa.

### Parâmetros da comunicação serial

Para que dois dispositivos se "entendam", têm de usar os mesmos parâmetros:

| Parâmetro | Valor usado | O que significa |
|---|---|---|
| **Baud rate** | 115200 | Velocidade: 115 200 bits por segundo |
| **Data bits** | 8 | Cada "pacote" tem 8 bits (= 1 byte = 1 caractere) |
| **Parity** | None | Sem bit de verificação de erro |
| **Stop bits** | 1 | 1 bit de "pausa" entre pacotes |

### No nosso projeto

O ESP32 está ligado ao PC via **cabo USB**. Quando ligamos o cabo, o PC "vê" o ESP32 como uma porta serial (ex: `COM3` no Windows ou `/dev/ttyUSB0` no Linux).

A API C# abre essa porta e "escuta" as mensagens que chegam do ESP32. Assim não precisamos de WiFi — a comunicação é feita pelo cabo USB.

---

## 7. A estrutura do software

```
benedita/
│
├── index.ino          ← Firmware do ESP32 (Arduino/C++)
│
├── api/               ← Servidor API (C# / ASP.NET Core + MySQL)
│   ├── Controllers/   ← Recebe pedidos HTTP (auth, vote, voters)
│   ├── Services/      ← Lógica de negócio + serviço serial
│   ├── Models/        ← Estrutura dos dados (Voter, Vote)
│   ├── Data/          ← Configuração da base de dados
│   └── Program.cs     ← Ponto de entrada / arranque
│
└── ui/                ← Interface gráfica Windows (C# / .NET MAUI)
    ├── Views/         ← Ecrãs da aplicação (XAML)
    ├── ViewModels/    ← Lógica de cada ecrã (MVVM)
    ├── Services/      ← Comunicação com API + serial
    └── Models/        ← Dados usados pela interface
```

---

## 8. Parte 1 · O Firmware (código do ESP32)

O ficheiro `index.ino` é o coração do dispositivo físico. Vamos analisar cada secção:

### Inclusão de bibliotecas

```cpp
#include <Adafruit_Fingerprint.h>   // Biblioteca para controlar o sensor biométrico
#include <LiquidCrystal_I2C.h>      // Biblioteca para controlar o LCD
```

Uma **biblioteca** é um conjunto de código já escrito por outra pessoa que podemos reutilizar. Em vez de escrevermos o código complexo para comunicar com o sensor, usamos a biblioteca `Adafruit_Fingerprint` que já faz isso por nós.

### Definição dos pinos

```cpp
#define RX_FINGER  16   // Pino do ESP32 onde o TX do sensor está ligado
#define TX_FINGER  17   // Pino do ESP32 onde o RX do sensor está ligado
#define BTN_CONFIRM  21 // Pino do botão de confirmar
#define BTN_CANCEL   22 // Pino do botão de cancelar
#define LED_OK    25    // Pino do LED verde (sucesso)
#define LED_ERROR 26    // Pino do LED vermelho (erro)
```

`#define` é uma instrução ao compilador para **substituir o texto**. Sempre que o compilador encontrar `LED_OK`, substitui por `25`. Isso torna o código mais legível — é mais fácil ler `LED_OK` do que o número `25`.

### Protocolo serial

O ESP32 comunica com a API através de mensagens de texto simples terminadas com `\n` (nova linha):

```
ESP32 → API :  CMD:AUTH:5\n       "Pedido de autenticação, eleitor ID 5"
API   → ESP32: RES:AUTH:OK\n      "Eleitor autorizado"

ESP32 → API :  CMD:VOTE:5:OPCAO_A\n   "Registar voto do eleitor 5 na opção A"
API   → ESP32: RES:VOTE:OK\n          "Voto registado com sucesso"
```

### A função `enviarComando`

```cpp
String enviarComando(const String& cmd) {
    Serial.println(cmd);          // Envia o comando para o PC via USB
    Serial.flush();               // Garante que o dado foi enviado antes de continuar

    unsigned long inicio = millis(); // Guarda o tempo actual (em ms)
    String resposta = "";

    while (millis() - inicio < API_TIMEOUT) { // Aguarda até 5 segundos
        if (Serial.available()) {             // Se chegou algum dado
            resposta = Serial.readStringUntil('\n'); // Lê até ao fim da linha
            resposta.trim();                         // Remove espaços e \r\n
            if (resposta.length() > 0) return resposta;
        }
    }
    return "";  // Timeout — a API não respondeu
}
```

`millis()` devolve o número de milissegundos desde que o ESP32 ligou. Subtraindo dois valores conseguimos medir o tempo passado — uma técnica muito comum em microcontroladores.

### O loop principal

```cpp
void loop() {
    // 1. Mostra "Coloque o dedo"
    // 2. Tenta ler a impressão digital
    // 3. Envia CMD:AUTH para a API verificar se o eleitor pode votar
    // 4. Mostra "Confirmar voto?" e aguarda botão
    // 5. Se confirmar: envia CMD:VOTE para a API registar o voto
    // 6. Acende o LED correto
}
```

---

## 9. O que é uma API?

**API** significa *Application Programming Interface* — Interface de Programação de Aplicações.

Pense numa API como um **garçom num restaurante**:
- Você (o cliente = ESP32 ou interface gráfica) faz um pedido
- O garçom (a API) leva o pedido para a cozinha (base de dados)
- A cozinha prepara e o garçom traz o resultado

Você não precisa de saber como a cozinha funciona. Só precisa de saber como fazer o pedido.

**No nosso projeto:**
- O ESP32 "pede" se um eleitor está autorizado → a API verifica na base de dados → responde sim ou não
- A interface gráfica "pede" a lista de eleitores → a API vai à base de dados → devolve a lista

### REST API

A nossa API é do tipo **REST** (*Representational State Transfer*). Usa o protocolo **HTTP** — o mesmo protocolo que os browsers usam para carregar páginas web.

Cada "endpoint" (endereço da API) corresponde a uma operação:

| Método | Endereço | O que faz |
|---|---|---|
| `GET` | `/voters` | Lista todos os eleitores |
| `POST` | `/voters` | Cadastra um novo eleitor |
| `DELETE` | `/voters/5` | Remove o eleitor com ID 5 |
| `POST` | `/auth` | Verifica se o eleitor pode votar |
| `POST` | `/vote` | Registra o voto |
| `GET` | `/vote/results` | Devolve a contagem dos votos |

---

## 10. Parte 2 · A API em C#

### Estrutura geral

A API é um projeto **ASP.NET Core** — um framework da Microsoft para criar APIs e aplicações web em C#. É executada como um processo em background no PC (ou servidor) e "escuta" pedidos HTTP na porta 5000.

Ao mesmo tempo, tem um serviço em background (`SerialHostedService`) que mantém a porta serial aberta e comunica com o ESP32.

### O ficheiro `Program.cs` — ponto de arranque

```csharp
// Cria o "construtor" da aplicação
var builder = WebApplication.CreateBuilder(args);

// Liga à base de dados MySQL
var connStr = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(connStr, ServerVersion.AutoDetect(connStr)));

// Regista os "serviços" (classes que fazem o trabalho)
builder.Services.AddScoped<VoteService>();

// O SerialHostedService é um serviço de background — corre enquanto a API estiver ligada
builder.Services.AddSingleton<SerialHostedService>();
builder.Services.AddHostedService(p => p.GetRequiredService<SerialHostedService>());

// Cria a aplicação e inicia
var app = builder.Build();
app.MapControllers(); // Liga os controllers aos seus endereços HTTP
app.Run();            // Começa a escutar pedidos
```

**O que é "injecção de dependências"?**  
Quando usamos `builder.Services.AddScoped<VoteService>()`, estamos a dizer ao sistema: "quando alguém precisar de um `VoteService`, cria um e entrega". Isso significa que nunca usamos `new VoteService()` manualmente — o sistema faz isso automaticamente. Este padrão chama-se **Injeção de Dependências (DI)** e torna o código mais fácil de testar e manter.

### Os `Controllers` — recebem os pedidos

Um controller é uma classe que responde a pedidos HTTP. Cada método corresponde a um endpoint:

```csharp
[ApiController]          // Marca esta classe como controller de API
[Route("auth")]          // Todos os pedidos a /auth chegam aqui
public class AuthController : ControllerBase
{
    private readonly VoteService _svc;

    // O VoteService é injetado automaticamente pelo sistema DI
    public AuthController(VoteService svc) => _svc = svc;

    [HttpPost]           // Este método responde a POST /auth
    public async Task<IActionResult> Auth([FromBody] AuthRequest req)
    {
        // [FromBody] significa que o JSON do corpo do pedido é convertido para AuthRequest
        var (authorized, reason) = await _svc.AuthorizeAsync(req.FingerId);
        return Ok(new { autorizado = authorized, motivo = reason });
        // Ok() devolve HTTP 200 com o resultado em JSON
    }
}
```

### O `VoteService` — lógica de negócio

O serviço contém a lógica real. Separamos do controller para não misturar "receber pedidos" com "fazer o trabalho":

```csharp
public async Task<(bool Authorized, string Reason)> AuthorizeAsync(int fingerId)
{
    // Vai à base de dados procurar o eleitor com aquele ID de dedo
    var voter = await _db.Voters
        .Include(v => v.Vote)           // Inclui também o voto (se existir)
        .FirstOrDefaultAsync(v => v.FingerId == fingerId);

    if (voter is null)
        return (false, "Eleitor não cadastrado");   // Não está na lista

    if (!voter.CanVote)
        return (false, "Voto já realizado");         // Já votou

    return (true, "OK");
}
```

### O `SerialHostedService` — comunicação com o ESP32

Este serviço é mais complexo. Corre em **background** (como uma segunda thread do programa) e mantém a porta serial aberta:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // Abre a porta serial (ex: COM3 no Windows)
    using var port = new SerialPort(portName, baudRate) { NewLine = "\n" };
    port.Open();

    while (!stoppingToken.IsCancellationRequested)
    {
        // Lê uma linha do ESP32 (ex: "CMD:AUTH:5")
        var line = port.ReadLine().Trim();

        // Processa o comando e obtém a resposta
        var response = await ProcessCommandAsync(line);

        // Envia a resposta de volta ao ESP32 (ex: "RES:AUTH:OK")
        port.WriteLine(response);
    }
}
```

O `CancellationToken` é um mecanismo para parar o serviço de forma ordenada quando a API é encerrada.

---

## 11. O que é Entity Framework e MySQL?

### Base de dados MySQL

O **MySQL** é um sistema de gestão de base de dados relacional — um programa que guarda dados de forma organizada em tabelas (como folhas de cálculo). No nosso projeto temos duas tabelas:

**Tabela `Voters` (Eleitores):**
| Id | FingerId | Name | CanVote | RegisteredAt |
|----|----------|------|---------|--------------|
| 1  | 5        | João Silva | false | 2026-03-01 |
| 2  | 12       | Maria Costa | true | 2026-03-01 |

**Tabela `Votes` (Votos):**
| Id | FingerId | Option | CastAt | VoterId |
|----|----------|--------|--------|---------|
| 1  | 5        | OPCAO_A | 2026-03-09 | 1 |

### Entity Framework Core

Escrever SQL à mão é trabalhoso e propenso a erros:
```sql
SELECT * FROM Voters WHERE FingerId = 5
```

O **Entity Framework Core (EF Core)** permite escrever isso em C#:
```csharp
var voter = await _db.Voters.FirstOrDefaultAsync(v => v.FingerId == 5);
```

O EF Core converte automaticamente essa expressão C# para SQL e executa na base de dados — chamamos a isso um **ORM** (Object-Relational Mapper).

Os `Models` definem a estrutura das tabelas:

```csharp
public class Voter
{
    public int Id { get; set; }       // Chave primária (auto-incremento)
    public int FingerId { get; set; } // ID do sensor biométrico
    public string Name { get; set; }  // Nome do eleitor
    public bool CanVote { get; set; } // Pode votar? (false após votar)
}
```

O `AppDbContext` é a "ponte" entre o C# e a base de dados:

```csharp
public class AppDbContext : DbContext
{
    // DbSet<Voter> representa a tabela "Voters" na base de dados
    public DbSet<Voter> Voters => Set<Voter>();
    public DbSet<Vote>  Votes  => Set<Vote>();
}
```

### String de conexão MySQL

No ficheiro `appsettings.json` configuramos como conectar à base de dados:

```json
"ConnectionStrings": {
    "Default": "Server=localhost;Port=3306;Database=benedita;User=root;Password=SUA_SENHA;"
}
```

- `Server=localhost` — o MySQL está no mesmo computador que a API
- `Port=3306` — porta padrão do MySQL
- `Database=benedita` — nome da base de dados (será criada automaticamente)
- `User=root` — utilizador do MySQL
- `Password=SUA_SENHA` — palavra-passe do MySQL

---

## 12. O que é MVVM?

**MVVM** significa *Model-View-ViewModel* — é um padrão de arquitectura de software para interfaces gráficas.

O problema que o MVVM resolve: em aplicações antigas, o código da interface e o código da lógica estavam misturados. Se quisesses mudar o aspeto visual, tinhas de mexer na lógica, e vice-versa. Isso tornava o código confuso e difícil de manter.

### As três camadas do MVVM

```
┌──────────────┐        ┌──────────────────┐        ┌──────────────┐
│    MODEL     │◄──────►│   VIEWMODEL      │◄──────►│    VIEW      │
│              │        │                  │        │              │
│ Os DADOS     │        │ A LÓGICA de      │        │ O ECRÃ       │
│              │        │ apresentação     │        │ (o que o     │
│ Ex: Voter,   │        │                  │        │ utilizador   │
│ VoteResult   │        │ Ex: lista de     │        │ vê)          │
│              │        │ eleitores,       │        │              │
│ Não sabe     │        │ comandos de      │        │ XAML         │
│ nada da UI   │        │ "Cadastrar",     │        │ Não tem       │  
│              │        │ "Eliminar"…      │        │ lógica        │
└──────────────┘        └──────────────────┘        └──────────────┘
```

### Model (Modelo)

São simplesmente classes que representam os dados:

```csharp
// ui/Models/Voter.cs
public class Voter
{
    public int Id        { get; set; }
    public int FingerId  { get; set; }
    public string Name   { get; set; }
    public bool CanVote  { get; set; }
    // ...
}
```

Nada de lógica, nada de interface — só dados.

### ViewModel

O ViewModel é a camada mais importante do MVVM. Contém a lógica de apresentação. Nunca referencia diretamente botões ou labels — em vez disso, expõe **propriedades** e **comandos** que a View pode ligar (`bind`):

```csharp
// ui/ViewModels/VotersViewModel.cs
public partial class VotersViewModel : ObservableObject
{
    // ObservableCollection notifica a View automaticamente quando a lista muda
    [ObservableProperty]
    private ObservableCollection<Voter> _voters = new();

    // RelayCommand cria um comando que a View pode chamar via binding
    [RelayCommand]
    public async Task LoadAsync()
    {
        var voters = await _api.GetVotersAsync();
        Voters.Clear();
        foreach (var v in voters) Voters.Add(v);
    }
}
```

**O que é `ObservableObject` e `[ObservableProperty]`?**  
Quando uma propriedade muda no ViewModel, a View precisa saber para atualizar o ecrã. `ObservableObject` (do CommunityToolkit.Mvvm) implementa automaticamente a interface `INotifyPropertyChanged` — um mecanismo do .NET que notifica a View. O atributo `[ObservableProperty]` gera automaticamente o código necessário.

### View (XAML)

O XAML (*eXtensible Application Markup Language*) é uma linguagem baseada em XML para definir interfaces gráficas. A View "liga-se" ao ViewModel através de **bindings**:

```xml
<!-- ui/Views/VotersPage.xaml -->
<CollectionView
    ItemsSource="{Binding Voters}"     <!-- Liga a lista ao ViewModel -->
    SelectedItem="{Binding SelectedVoter}">  <!-- Liga o item selecionado -->
</CollectionView>

<Button
    Text="Carregar"
    Command="{Binding LoadCommand}" />   <!-- Liga o botão ao comando LoadAsync -->
```

O `{Binding NomeDaPropriedade}` é o mecanismo que "cola" a View ao ViewModel. Quando `Voters` muda no ViewModel, a lista no ecrã atualiza automaticamente — sem código extra.

### Vantagens do MVVM

| Sem MVVM | Com MVVM |
|---|---|
| Lógica e UI misturadas | Completamente separadas |
| Difícil de testar | ViewModel é testável sem interface |
| Mudança visual exige mexer na lógica | Troca a View sem tocar no ViewModel |
| Código espaguete | Código organizado e previsível |

---

## 13. Parte 3 · A Interface gráfica em .NET MAUI

### O que é .NET MAUI?

**.NET MAUI** (*Multi-platform App UI*) é um framework da Microsoft que permite criar aplicações para Windows, macOS, Android e iOS com **um único código**. No nosso projeto usamos apenas o target Windows.

### Estrutura do projeto MAUI

```
ui/
├── App.xaml / App.xaml.cs         ← Ponto de entrada da aplicação
├── AppShell.xaml / .cs            ← Navegação (menu lateral)
├── MauiProgram.cs                 ← Configuração e DI
│
├── Models/                        ← DTOs (cópias dos modelos da API)
│   ├── Voter.cs
│   └── VoteResult.cs
│
├── Services/
│   ├── ApiService.cs              ← Faz pedidos HTTP à API
│   └── SerialMonitorService.cs    ← Monitoriza a porta serial
│
├── ViewModels/
│   ├── DashboardViewModel.cs      ← Lógica do painel principal
│   ├── VotersViewModel.cs         ← Lógica da gestão de eleitores
│   ├── SerialLogViewModel.cs      ← Lógica do monitor serial
│   └── SettingsViewModel.cs       ← Lógica das configurações
│
└── Views/
    ├── DashboardPage.xaml         ← Ecrã principal com KPIs e resultados
    ├── VotersPage.xaml            ← Lista e cadastro de eleitores
    ├── SerialLogPage.xaml         ← Terminal serial em tempo real
    └── SettingsPage.xaml          ← URL da API, teste de ligação
```

### O `ApiService` — cliente HTTP

```csharp
public class ApiService
{
    private readonly HttpClient _http; // Objeto para fazer pedidos HTTP

    // Obtém lista de eleitores da API
    public async Task<List<Voter>?> GetVotersAsync()
    {
        // GET http://localhost:5000/voters
        // GetFromJsonAsync faz o pedido E converte o JSON para List<Voter>
        return await _http.GetFromJsonAsync<List<Voter>>("voters");
    }

    // Regista novo eleitor
    public async Task<(bool Ok, string Message)> RegisterVoterAsync(int fingerId, string name)
    {
        // POST http://localhost:5000/voters  com body JSON: {"fingerId":5,"name":"João"}
        var res = await _http.PostAsJsonAsync("voters", new { fingerId, name });
        return res.IsSuccessStatusCode
            ? (true, "Eleitor cadastrado!")
            : (false, $"Erro: {await res.Content.ReadAsStringAsync()}");
    }
}
```

### O `SerialMonitorService` — monitor serial

Este serviço permite à interface gráfica ver em tempo real o que está a passar entre o ESP32 e a API:

```csharp
private async Task ReadLoopAsync(CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        var line = await Task.Run(() => _port!.ReadLine(), token);
        // Adiciona ao log com timestamp, direção (RX/TX/SYS) e cor
        AddEntry(line.Trim(), SerialDirection.Rx);
    }
}
```

### O `AppShell` — navegação

O Shell do MAUI fornece automaticamente um menu lateral (`FlyoutMenu`) com as 4 secções da aplicação:

```xml
<Shell FlyoutBehavior="Flyout">
    <FlyoutItem Title="Dashboard">
        <ShellContent ContentTemplate="{DataTemplate views:DashboardPage}" />
    </FlyoutItem>
    <FlyoutItem Title="Eleitores">
        <ShellContent ContentTemplate="{DataTemplate views:VotersPage}" />
    </FlyoutItem>
    <!-- ... -->
</Shell>
```

---

## 14. Como tudo se conecta — fluxo completo de um voto

Vamos seguir um voto do início ao fim:

```
1. ELEITOR coloca o dedo no sensor
         │
         ▼
2. ESP32 — sensor.getImage() → sensor.image2Tz() → sensor.fingerFastSearch()
   Sensor identifica: "Este é o eleitor ID 5"
         │
         ▼
3. ESP32 envia via Serial USB:  "CMD:AUTH:5\n"
         │
         ▼
4. API (SerialHostedService) lê a linha
   → Chama VoteService.AuthorizeAsync(5)
   → VoteService vai à base de dados MySQL:
     SELECT * FROM Voters WHERE FingerId = 5
   → Encontrou! CanVote = true → autorizado
         │
         ▼
5. API envia de volta:  "RES:AUTH:OK\n"
         │
         ▼
6. ESP32 lê a resposta → mostra "Confirmar voto?" no LCD
         │
         ▼
7. ELEITOR pressiona BTN_CONFIRM (GPIO 21)
         │
         ▼
8. ESP32 envia:  "CMD:VOTE:5:OPCAO_A\n"
         │
         ▼
9. API (SerialHostedService) lê a linha
   → Chama VoteService.CastVoteAsync(5, "OPCAO_A")
   → VoteService insere na base de dados:
     INSERT INTO Votes (FingerId, Option, VoterId) VALUES (5, 'OPCAO_A', 1)
     UPDATE Voters SET CanVote = 0 WHERE Id = 1
         │
         ▼
10. API envia de volta:  "RES:VOTE:OK\n"
         │
         ▼
11. ESP32 mostra "Voto registado!" no LCD e pisca LED_OK (GPIO 25)
         │
         ▼
12. Interface MAUI (auto-refresh a cada 10s) faz GET /vote/results
    → API devolve {"OPCAO_A": 1, "OPCAO_B": 0}
    → Dashboard atualiza os gráficos
```

---

## 15. Como instalar e rodar no PC

### Pré-requisitos

| Ferramenta | Para quê | Download |
|---|---|---|
| **MySQL 8.x** | Base de dados | https://dev.mysql.com/downloads/ |
| **.NET 8 SDK** | Compilar e correr a API | https://dot.net |
| **Visual Studio 2022** (Windows) | Compilar a UI MAUI | https://visualstudio.microsoft.com |
| **Arduino IDE 2.x** | Carregar o firmware no ESP32 | https://www.arduino.cc/en/software |
| **Driver CP2102** ou **CH340** | Reconhecer o ESP32 no PC | (vem com a IDE Arduino) |

### Passo 1 — Instalar e configurar o MySQL

1. Instale o MySQL Server
2. Abra o MySQL Workbench (ou linha de comandos)
3. Crie o utilizador/password (ou use o root)
4. **Não precisa de criar a base de dados** — o EF Core cria automaticamente ao arrancar

### Passo 2 — Configurar a API

Abra o ficheiro `api/appsettings.json` e edite a string de conexão com os seus dados:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Port=3306;Database=benedita;User=root;Password=A_SUA_PASSWORD;"
  },
  "Serial": {
    "Port": "COM3",
    "BaudRate": "115200"
  }
}
```

> **Nota sobre a porta serial:**  
> No Windows, as portas são `COM1`, `COM2`, `COM3`…  
> Abra o Gestor de Dispositivos → Portas (COM e LPT) para ver qual é a do ESP32.  
> No Linux são `/dev/ttyUSB0`, `/dev/ttyACM0`…

### Passo 3 — Arrancar a API

Abra um terminal na pasta `api/` e execute:

```bash
dotnet restore          # Descarrega os pacotes NuGet (Pomelo, EF Core, etc.)
dotnet run              # Compila e inicia a API
```

Deverá ver:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5000
info: SerialHostedService[0]
      Serial: abrindo COM3 @ 115200
```

A API está pronta. Pode abrir `http://localhost:5000/swagger` no browser para ver e testar todos os endpoints interactivamente.

### Passo 4 — Carregar o firmware no ESP32

1. Abra a **Arduino IDE**
2. Instale as bibliotecas:
   - `Adafruit Fingerprint Sensor Library` (by Adafruit)
   - `LiquidCrystal I2C` (by Frank de Brabander)
3. Em **Ferramentas → Placa**, selecione `ESP32 Dev Module`
4. Em **Ferramentas → Porta**, selecione a porta do ESP32
5. Abra `index.ino` e clique em **Upload (→)**

### Passo 5 — Registar os eleitores

Antes de votar, é necessário:
1. Guardar a impressão digital no sensor com um sketch de enrolamento (não incluso — veja exemplos da biblioteca Adafruit)
   - Cada eleitor fica guardado num slot (ex: slot 1, slot 2, …)
2. Cadastrá-los na base de dados via API:

```bash
curl -X POST http://localhost:5000/voters \
  -H "Content-Type: application/json" \
  -d '{"fingerId": 1, "name": "João Silva"}'
```

Ou use o Swagger em `http://localhost:5000/swagger`.

### Passo 6 — Abrir a interface gráfica (Windows)

1. Copie a pasta `ui/` para um computador Windows com Visual Studio 2022
2. Copie as fontes OpenSans para `ui/Resources/Fonts/` (conforme `README` nessa pasta)
3. Instale o workload **.NET MAUI** no Visual Studio (via VS Installer)
4. Abra `ui/BeneditaUI.csproj`
5. Selecione **Windows Machine** como target
6. Pressione **F5** para compilar e executar
7. Na página **Configurações**, verifique que a URL da API está correta (`http://IP_DO_PC:5000/`)

### Passo 7 — Votar!

Com tudo ligado:
1. O LCD mostra "Coloque o dedo"
2. O eleitor coloca o dedo
3. O LCD mostra "Verificando..." enquanto a API verifica
4. O LCD mostra "Confirmar voto?" — pressione o botão de confirmar
5. O LED verde pisca — voto registado!
6. A interface MAUI atualiza automaticamente os resultados

---

## 16. Estrutura de pastas do projeto

```
benedita/
│
├── README.md                      ← Este ficheiro
│
├── index.ino                      ← Firmware ESP32 (C++ / Arduino)
│
├── api/                           ← Servidor API
│   ├── BeneditaApi.csproj         ← Definição do projeto C# (pacotes, .NET version)
│   ├── Program.cs                 ← Ponto de entrada / arranque / DI
│   ├── appsettings.json           ← Configurações (MySQL, Serial, Logging)
│   │
│   ├── Controllers/
│   │   ├── AuthController.cs      ← POST /auth
│   │   ├── VoteController.cs      ← POST /vote  |  GET /vote/results
│   │   └── VoterController.cs     ← CRUD /voters
│   │
│   ├── Services/
│   │   ├── VoteService.cs         ← Lógica de autorização e registo de votos
│   │   └── SerialHostedService.cs ← Background service: porta serial ↔ ESP32
│   │
│   ├── Models/
│   │   ├── Voter.cs               ← Entidade eleitor (tabela na BD)
│   │   └── Vote.cs                ← Entidade voto (tabela na BD)
│   │
│   └── Data/
│       └── AppDbContext.cs        ← Configuração do EF Core (tabelas, relações)
│
└── ui/                            ← Interface gráfica Windows
    ├── BeneditaUI.csproj          ← Projeto MAUI (Windows)
    ├── MauiProgram.cs             ← DI + configuração MAUI
    ├── App.xaml / .cs             ← Aplicação (MainPage = AppShell)
    ├── AppShell.xaml / .cs        ← Navigate drawer + registo de rotas
    ├── ServiceHelper.cs           ← Resolve serviços DI fora de construtores
    │
    ├── Models/
    │   ├── Voter.cs               ← DTO espelho (para deserializar JSON da API)
    │   └── VoteResult.cs          ← Resultado por opção com percentagem
    │
    ├── Services/
    │   ├── ApiService.cs          ← HttpClient para a API REST
    │   └── SerialMonitorService.cs← Lê/escreve na porta serial + log visual
    │
    ├── ViewModels/
    │   ├── DashboardViewModel.cs  ← KPIs + resultados + auto-refresh
    │   ├── VotersViewModel.cs     ← CRUD eleitores
    │   ├── SerialLogViewModel.cs  ← Controlo de porta serial + log
    │   └── SettingsViewModel.cs   ← URL API + teste de ping
    │
    ├── Views/
    │   ├── DashboardPage.xaml/.cs ← Painel com cards e barras de progresso
    │   ├── VotersPage.xaml/.cs    ← Lista de eleitores + formulário
    │   ├── SerialLogPage.xaml/.cs ← Terminal estilo darkmode
    │   └── SettingsPage.xaml/.cs  ← Caixa de texto URL + botão testar
    │
    ├── Converters/
    │   └── Converters.cs          ← Conversores XAML (bool→cor, null→bool, …)
    │
    └── Resources/
        ├── Styles/
        │   ├── Colors.xaml        ← Paleta de cores global
        │   └── Styles.xaml        ← Estilos reutilizáveis (botões, cards, …)
        ├── AppIcon/               ← Ícone da aplicação (.svg)
        ├── Splash/                ← Ecrã de carregamento (.svg)
        └── Fonts/                 ← Fontes (.ttf)
```

---

## 17. Protocolo serial detalhado

O protocolo é baseado em texto ASCII, terminado com `\n` (newline). É simples por design — qualquer terminal serial consegue depurá-lo.

### Comandos do ESP32 → API

| Comando | Exemplo | Descrição |
|---|---|---|
| `CMD:PING` | `CMD:PING` | Verifica se a API está activa |
| `CMD:AUTH:<id>` | `CMD:AUTH:5` | Pede autorização para o eleitor com ID biométrico 5 |
| `CMD:VOTE:<id>:<opcao>` | `CMD:VOTE:5:OPCAO_A` | Regista o voto do eleitor 5 na opção A |

### Respostas da API → ESP32

| Resposta | Descrição |
|---|---|
| `RES:PONG` | API está activa (resposta ao PING) |
| `RES:AUTH:OK` | Eleitor autorizado a votar |
| `RES:AUTH:DENIED:Eleitor-nao-cadastrado` | Eleitor não encontrado na BD |
| `RES:AUTH:DENIED:Voto-ja-realizado` | Eleitor já votou |
| `RES:VOTE:OK` | Voto registado com sucesso |
| `RES:VOTE:ERROR:Eleitor-ja-votou` | Tentativa de votar duas vezes |
| `RES:ERROR:FORMATO_INVALIDO` | Comando não reconhecido |

### Notificações da API → ESP32 (proativas)

| Mensagem | Descrição |
|---|---|
| `INFO:VOTER_REGISTERED:<id>` | Um novo eleitor foi cadastrado via interface MAUI |

---

## 18. Perguntas frequentes

**P: O ESP32 precisa de estar sempre ligado ao PC?**  
R: Sim, para comunicar via serial. No futuro pode ser migrado para WiFi (o código original já tinha essa estrutura) ligando à API via HTTP.

**P: Quantos eleitores suporta o sensor biométrico?**  
R: A maioria dos sensores AS608/R307 suporta 127 impressões digitais em simultâneo. A base de dados MySQL não tem limite prático.

**P: É possível usar sem a interface MAUI?**  
R: Sim. A API funciona de forma independente. Pode usar o Swagger (`/swagger`) ou qualquer cliente HTTP (Postman, curl) para gerir eleitores e ver resultados.

**P: Os votos são anônimos?**  
R: No estado actual, a base de dados guarda a associação eleitor→voto (necessário para impedir votos duplos). Para votações onde o anonimato é crítico, seria necessário uma arquitectura diferente (ex: separar a verificação de identidade do registo do voto).

**P: Como instalo o MySQL?**  
R: Descarregue em https://dev.mysql.com/downloads/installer/ e siga o assistente. Escolha "Developer Default". Durante a instalação, defina uma palavra-passe para o utilizador `root` e anote-a — vai precisar no `appsettings.json`.

**P: O que significa `async/await` no código C#?**  
R: É uma forma de escrever código assíncrono de forma legível. Quando uma operação demora tempo (ex: ir à base de dados, fazer um pedido HTTP), usamos `await` para dizer "espera aqui, mas não bloqueies — deixa outros processos correr entretanto". `async` marca o método como assíncrono. Sem isso, a API ficaria "congelada" enquanto esperasse a resposta da base de dados.

**P: O que é um `Task` em C#?**  
R: Um `Task` representa uma operação que pode ainda não ter terminado. É o equivalente C# de uma "promessa" (Promise em JavaScript). `Task<bool>` significa "uma operação que eventualmente vai devolver um bool".

**P: Posso adicionar mais opções de voto?**  
R: Sim. As opções são strings livres. No firmware (`index.ino`), altere `"OPCAO_A"` no `CMD:VOTE` para o nome que quiser. Para uma votação com múltiplas opções, modifique o ESP32 para mostrar as opções no LCD e usar os botões para navegar entre elas.

---

*Desenvolvido como projecto educativo de sistema de votação biométrica embarcada.*  
*Hardware: ESP32-WROOM + AS608 + LCD JHD-2X16-I2C*  
*Software: C++ (Arduino) · C# ASP.NET Core 8 · .NET MAUI 8 · MySQL 8*

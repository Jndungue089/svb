/*
 * Sistema de Votação Biométrica — ESP32
 * Comunicação com a API C# via Serial USB (115200 baud)
 *
 * Protocolo:
 *   ESP32 → API  :  CMD:PING\n
 *   API   → ESP32:  RES:PONG\n
 *
 *   ESP32 → API  :  CMD:AUTH:{finger_id}\n
 *   API   → ESP32:  RES:AUTH:OK:{nome}  |  RES:AUTH:DENIED:{motivo}\n
 *
 *   ESP32 → API  :  CMD:PARTIES\n
 *   API   → ESP32:  RES:PARTIES:{n}|{id}:{sigla}|...\n
 *
 *   ESP32 → API  :  CMD:VOTE:{finger_id}:{party_id}\n
 *   API   → ESP32:  RES:VOTE:OK  |  RES:VOTE:ERROR:{motivo}\n
 *
 *   API   → ESP32:  CMD:ENROLL:{slot}\n          (inicia enrolamento)
 *   ESP32 → API  :  RES:ENROLL:OK:{slot}\n       (sucesso)
 *   ESP32 → API  :  RES:ENROLL:ERROR:{motivo}\n  (falha)
 */

#include <Adafruit_Fingerprint.h>
#include <LiquidCrystal_I2C.h>

// ================= HARDWARE =================
#define RX_FINGER   16
#define TX_FINGER   17

#define BTN_NEXT    21   // navega para próximo partido / confirma enrolamento
#define BTN_CONFIRM 22   // confirma voto

#define LED_OK      25
#define LED_ERROR   26

#define API_TIMEOUT 5000

HardwareSerial fingerSerial(2);
Adafruit_Fingerprint finger(&fingerSerial);
LiquidCrystal_I2C lcd(0x27, 16, 2);

// ================= PARTIDOS =================
struct Partido {
  int    id;
  String sigla;
};

#define MAX_PARTIDOS 16
Partido partidos[MAX_PARTIDOS];
int     numPartidos = 0;

// ================= UTILITÁRIOS =================

void piscarLED(int pino, int vezes = 1, int ms = 200) {
  for (int i = 0; i < vezes; i++) {
    digitalWrite(pino, HIGH); delay(ms);
    digitalWrite(pino, LOW);  delay(ms);
  }
}

void lcdMsg(const char* linha1, const char* linha2 = "") {
  lcd.clear();
  lcd.setCursor(0, 0); lcd.print(linha1);
  lcd.setCursor(0, 1); lcd.print(linha2);
}

// Aguarda debounce e retorna true se pressionado
bool btnPressionado(int pino) {
  if (!digitalRead(pino)) {
    delay(50);
    return !digitalRead(pino);
  }
  return false;
}

// ================= SERIAL PROTOCOL =================

String enviarComando(const String& cmd) {
  Serial.println(cmd);
  Serial.flush();
  unsigned long inicio = millis();
  while (millis() - inicio < API_TIMEOUT) {
    if (Serial.available()) {
      String r = Serial.readStringUntil('\n');
      r.trim();
      if (r.length() > 0) return r;
    }
  }
  return "";
}

bool verificarConexao() {
  return enviarComando("CMD:PING") == "RES:PONG";
}

// ================= PARTIDOS =================

bool carregarPartidos() {
  String res = enviarComando("CMD:PARTIES");
  if (!res.startsWith("RES:PARTIES:")) return false;

  String corpo = res.substring(12); // após "RES:PARTIES:"
  int n = corpo.toInt();
  if (n == 0) return false;

  // Formato: "3|1:MPLA|2:UNITA|3:FNLA"
  numPartidos = 0;
  int pos = corpo.indexOf('|');
  while (pos >= 0 && numPartidos < MAX_PARTIDOS) {
    String entrada = corpo.substring(pos + 1);
    int next = entrada.indexOf('|');
    String item = (next >= 0) ? entrada.substring(0, next) : entrada;

    int sep = item.indexOf(':');
    if (sep > 0) {
      partidos[numPartidos].id    = item.substring(0, sep).toInt();
      partidos[numPartidos].sigla = item.substring(sep + 1);
      partidos[numPartidos].sigla.trim();
      numPartidos++;
    }

    if (next < 0) break;
    corpo = entrada;
    pos   = next - 1;  // reajusta
    pos   = corpo.indexOf('|');
    // Simplificação: iterar a string original
    // Recomeçar parse a partir da posição correcta
    break; // o bloco abaixo re-faz o parse correctamente
  }

  // Parse robusto (reinicia)
  numPartidos = 0;
  int start = corpo.indexOf('|');  // 'corpo' ainda é "3|1:MPLA|2:UNITA"
  // Recarregar a string completa
  corpo = res.substring(12);
  // Saltar o número inicial
  int pipe = corpo.indexOf('|');
  if (pipe < 0) return false;
  String resto = corpo.substring(pipe + 1); // "1:MPLA|2:UNITA|3:FNLA"

  while (resto.length() > 0 && numPartidos < MAX_PARTIDOS) {
    int nextPipe = resto.indexOf('|');
    String item  = (nextPipe >= 0) ? resto.substring(0, nextPipe) : resto;

    int colon = item.indexOf(':');
    if (colon > 0) {
      partidos[numPartidos].id    = item.substring(0, colon).toInt();
      partidos[numPartidos].sigla = item.substring(colon + 1);
      partidos[numPartidos].sigla.trim();
      numPartidos++;
    }

    if (nextPipe < 0) break;
    resto = resto.substring(nextPipe + 1);
  }

  return numPartidos > 0;
}

// ================= DIGITAL =================

int capturarDigital() {
  if (finger.getImage()       != FINGERPRINT_OK) return -1;
  if (finger.image2Tz()       != FINGERPRINT_OK) return -1;
  if (finger.fingerFastSearch()!= FINGERPRINT_OK) return -1;
  return finger.fingerID;
}

// ================= ENROLAMENTO =================

/**
 * Enrola uma nova impressão digital no slot indicado.
 * Captura a digital duas vezes, cria o modelo e armazena.
 * Envia RES:ENROLL:OK:{slot} ou RES:ENROLL:ERROR:{motivo}.
 */
void enrolarDigital(int slot) {
  lcdMsg("Enrolamento", "Coloque o dedo");

  // 1.ª captura
  int p = -1;
  while (p != FINGERPRINT_OK) {
    p = finger.getImage();
    if (p == FINGERPRINT_OK) break;
    if (p == FINGERPRINT_NOFINGER) continue;
    Serial.println("RES:ENROLL:ERROR:IMAGEM_1_FALHOU");
    return;
  }

  if (finger.image2Tz(1) != FINGERPRINT_OK) {
    Serial.println("RES:ENROLL:ERROR:CONVERT_1_FALHOU");
    return;
  }

  lcdMsg("Retire o dedo", "");
  delay(1500);
  while (finger.getImage() != FINGERPRINT_NOFINGER) delay(200);

  lcdMsg("Coloque outra", "vez o dedo");

  // 2.ª captura
  p = -1;
  while (p != FINGERPRINT_OK) {
    p = finger.getImage();
    if (p == FINGERPRINT_OK) break;
    if (p == FINGERPRINT_NOFINGER) continue;
    Serial.println("RES:ENROLL:ERROR:IMAGEM_2_FALHOU");
    return;
  }

  if (finger.image2Tz(2) != FINGERPRINT_OK) {
    Serial.println("RES:ENROLL:ERROR:CONVERT_2_FALHOU");
    return;
  }

  if (finger.createModel() != FINGERPRINT_OK) {
    Serial.println("RES:ENROLL:ERROR:MODELO_NAO_COINCIDE");
    lcdMsg("Digitais nao", "coincidem!");
    piscarLED(LED_ERROR, 3, 200);
    delay(2000);
    return;
  }

  if (finger.storeModel(slot) != FINGERPRINT_OK) {
    Serial.println("RES:ENROLL:ERROR:ARMAZENAMENTO_FALHOU");
    return;
  }

  // Sucesso
  lcdMsg("Digital gravada!", "Slot: " + String(slot));
  piscarLED(LED_OK, 3, 200);
  Serial.println("RES:ENROLL:OK:" + String(slot));
  delay(2000);
}

// ================= AUTENTICAÇÃO =================

bool autenticarNoServidor(int fingerID, String& nomeEleitor) {
  String res = enviarComando("CMD:AUTH:" + String(fingerID));

  if (res.startsWith("RES:AUTH:OK:")) {
    nomeEleitor = res.substring(12);  // extrai o nome
    nomeEleitor = nomeEleitor.substring(0, 16);
    return true;
  }

  int idx = res.indexOf("DENIED:");
  if (idx >= 0) {
    String motivo = res.substring(idx + 7);
    motivo = motivo.substring(0, 16);
    lcdMsg("Nao autorizado", motivo.c_str());
  }
  return false;
}

// ================= SELECCIONAR PARTIDO =================

/**
 * Mostra partidos no LCD e devolve o ID do partido escolhido.
 * BTN_NEXT   = próximo partido (cicla)
 * BTN_CONFIRM = votar neste partido
 * Retorna -1 se cancelado (timeout 60 s).
 */
int seleccionarPartido() {
  if (numPartidos == 0) {
    lcdMsg("Sem partidos!", "");
    delay(2000);
    return -1;
  }

  int idx = 0;
  unsigned long inicio = millis();

  while (millis() - inicio < 60000UL) {
    String linha1 = "< " + partidos[idx].sigla + " >";
    lcdMsg(linha1.c_str(), "OK=Votar N=Prox");

    delay(200); // debounce

    unsigned long t = millis();
    while (millis() - t < 3000) {
      if (btnPressionado(BTN_CONFIRM)) {
        return partidos[idx].id;
      }
      if (btnPressionado(BTN_NEXT)) {
        idx = (idx + 1) % numPartidos;
        break;
      }
      delay(20);
    }
  }

  return -1; // timeout
}

// ================= VOTAR =================

bool registrarVoto(int fingerID, int partyID) {
  String cmd = "CMD:VOTE:" + String(fingerID) + ":" + String(partyID);
  String res = enviarComando(cmd);
  return res.startsWith("RES:VOTE:OK");
}

// ================= SETUP =================

void setup() {
  Serial.begin(115200);

  pinMode(BTN_NEXT,    INPUT_PULLUP);
  pinMode(BTN_CONFIRM, INPUT_PULLUP);
  pinMode(LED_OK,      OUTPUT);
  pinMode(LED_ERROR,   OUTPUT);
  digitalWrite(LED_OK,    LOW);
  digitalWrite(LED_ERROR, LOW);

  lcd.init();
  lcd.backlight();

  fingerSerial.begin(57600, SERIAL_8N1, RX_FINGER, TX_FINGER);
  finger.begin(57600);

  lcdMsg("Bem-vindo ao SVB", "");
  delay(2000);

  // Aguarda API
  lcdMsg("Aguard. API...", "");
  while (!verificarConexao()) delay(1000);

  lcdMsg("Sistema Pronto", "");
  delay(1000);
}

// ================= LOOP =================

void loop() {
  // Verifica se há comandos vindos da API (ex: CMD:ENROLL)
  if (Serial.available()) {
    String cmd = Serial.readStringUntil('\n');
    cmd.trim();

    if (cmd.startsWith("CMD:ENROLL:")) {
      int slot = cmd.substring(11).toInt();
      if (slot < 1 || slot > 127) {
        Serial.println("RES:ENROLL:ERROR:SLOT_INVALIDO");
      } else {
        enrolarDigital(slot);
      }
      return;
    }

    if (cmd.startsWith("INFO:")) {
      lcdMsg("Novo Eleitor:", cmd.substring(5, 21).c_str());
      delay(3000);
      return;
    }
  }

  // Modo votação
  lcdMsg("Coloque o dedo", "");

  int fingerID = capturarDigital();
  if (fingerID < 0) return;

  lcdMsg("Verificando...", "");

  String nomeEleitor;
  if (!autenticarNoServidor(fingerID, nomeEleitor)) {
    piscarLED(LED_ERROR, 3, 200);
    delay(2000);
    return;
  }

  // Mostra nome do eleitor autenticado
  lcdMsg("Bem-vindo!", nomeEleitor.c_str());
  delay(1500);

  // Carrega lista de partidos da API
  lcdMsg("A carregar...", "partidos");
  if (!carregarPartidos()) {
    lcdMsg("Sem partidos", "cadastrados!");
    piscarLED(LED_ERROR, 3, 200);
    delay(2000);
    return;
  }

  // Seleccionar partido
  int partyID = seleccionarPartido();

  if (partyID < 0) {
    lcdMsg("Cancelado", "");
    piscarLED(LED_ERROR, 2, 200);
    delay(2000);
    return;
  }

  // Confirmar voto
  lcdMsg("Registando...", "");

  if (registrarVoto(fingerID, partyID)) {
    lcdMsg("Voto registado!", "Obrigado!");
    piscarLED(LED_OK, 3, 300);
  } else {
    lcdMsg("Erro no server", "Tente novamente");
    piscarLED(LED_ERROR, 5, 150);
  }

  delay(3000);
}

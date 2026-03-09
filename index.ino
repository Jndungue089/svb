/*
 * Sistema de Votação Biométrica — ESP32
 * Comunicação com a API C# via Serial USB (115200 baud)
 *
 * Protocolo:
 *   ESP32 → API  :  CMD:AUTH:<finger_id>\n
 *   API   → ESP32:  RES:AUTH:OK  |  RES:AUTH:DENIED:<motivo>\n
 *
 *   ESP32 → API  :  CMD:VOTE:<finger_id>:<opcao>\n
 *   API   → ESP32:  RES:VOTE:OK  |  RES:VOTE:ERROR:<motivo>\n
 *
 *   ESP32 → API  :  CMD:PING\n
 *   API   → ESP32:  RES:PONG\n
 */

#include <Adafruit_Fingerprint.h>
#include <LiquidCrystal_I2C.h>

// ================= HARDWARE =================
// Sensor biométrico — UART2: RX=GPIO16, TX=GPIO17
#define RX_FINGER  16
#define TX_FINGER  17

// Botões (conforme esquema: GPIO 21 e GPIO 22)
#define BTN_CONFIRM  21
#define BTN_CANCEL   22

// LEDs (D1 = voto OK, D2 = erro/negado)
#define LED_OK    25
#define LED_ERROR 26

// Timeout aguardando resposta da API (ms)
#define API_TIMEOUT 5000

HardwareSerial fingerSerial(2);
Adafruit_Fingerprint finger(&fingerSerial);
LiquidCrystal_I2C lcd(0x27, 16, 2);

bool jaVotouLocal = false;

// ================= UTILITÁRIOS =================

void piscarLED(int pino, int vezes = 1, int ms = 200) {
  for (int i = 0; i < vezes; i++) {
    digitalWrite(pino, HIGH);
    delay(ms);
    digitalWrite(pino, LOW);
    delay(ms);
  }
}

// ================= SERIAL PROTOCOL =================

/**
 * Envia um comando à API e aguarda a resposta.
 * Retorna a linha recebida ou string vazia em caso de timeout.
 */
String enviarComando(const String& cmd) {
  Serial.println(cmd);          // envia para o PC
  Serial.flush();

  unsigned long inicio = millis();
  String resposta = "";

  while (millis() - inicio < API_TIMEOUT) {
    if (Serial.available()) {
      resposta = Serial.readStringUntil('\n');
      resposta.trim();
      if (resposta.length() > 0) return resposta;
    }
  }
  return "";  // timeout
}

bool verificarConexao() {
  String res = enviarComando("CMD:PING");
  return res == "RES:PONG";
}

// ================= DIGITAL =================

int capturarDigital() {
  int p = finger.getImage();
  if (p != FINGERPRINT_OK) return -1;

  p = finger.image2Tz();
  if (p != FINGERPRINT_OK) return -1;

  p = finger.fingerFastSearch();
  if (p != FINGERPRINT_OK) return -1;

  return finger.fingerID;
}

// ================= COMUNICAÇÃO COM API =================

bool autenticarNoServidor(int fingerID) {
  String cmd = "CMD:AUTH:" + String(fingerID);
  String res = enviarComando(cmd);

  if (res.startsWith("RES:AUTH:OK")) return true;

  // Exibe motivo no LCD linha 2 se disponível
  int idx = res.indexOf("DENIED:");
  if (idx >= 0) {
    String motivo = res.substring(idx + 7);
    motivo = motivo.substring(0, 16);   // max 16 chars
    lcd.setCursor(0, 1);
    lcd.print(motivo);
  }
  return false;
}

bool registrarVoto(int fingerID, const String& opcao) {
  String cmd = "CMD:VOTE:" + String(fingerID) + ":" + opcao;
  String res = enviarComando(cmd);
  return res.startsWith("RES:VOTE:OK");
}

// ================= SETUP =================
void setup() {
  Serial.begin(115200);   // comunicação com a API C# via USB

  pinMode(BTN_CONFIRM, INPUT_PULLUP);
  pinMode(BTN_CANCEL,  INPUT_PULLUP);

  pinMode(LED_OK,    OUTPUT);
  pinMode(LED_ERROR, OUTPUT);
  digitalWrite(LED_OK,    LOW);
  digitalWrite(LED_ERROR, LOW);

  lcd.init();
  lcd.backlight();

  fingerSerial.begin(57600, SERIAL_8N1, RX_FINGER, TX_FINGER);
  finger.begin(57600);

  // Aguarda a API ficar disponível
  lcd.clear();
  lcd.print("Aguard. API...");
  while (!verificarConexao()) {
    delay(1000);
  }

  lcd.clear();
  lcd.print("Sistema Pronto");
  delay(1000);
}

// ================= LOOP =================
void loop() {
  // Verifica notificações vindas da API (ex: INFO:VOTER_REGISTERED)
  if (Serial.available()) {
    String info = Serial.readStringUntil('\n');
    info.trim();
    if (info.startsWith("INFO:")) {
      lcd.clear();
      lcd.print(info.substring(5, 21));
      delay(2000);
    }
  }

  if (jaVotouLocal) {
    lcd.clear();
    lcd.print("Voto ja feito");
    piscarLED(LED_OK, 2, 500);
    delay(3000);
    return;
  }

  lcd.clear();
  lcd.print("Coloque o dedo");

  int fingerID = capturarDigital();
  if (fingerID < 0) return;

  lcd.clear();
  lcd.print("Verificando...");

  if (!autenticarNoServidor(fingerID)) {
    lcd.clear();
    lcd.print("Nao autorizado");
    piscarLED(LED_ERROR, 3, 200);
    delay(2000);
    return;
  }

  lcd.clear();
  lcd.print("Confirmar voto?");
  lcd.setCursor(0, 1);
  lcd.print("OK=GPIO21 X=22");

  while (true) {
    if (!digitalRead(BTN_CONFIRM)) {
      lcd.clear();
      lcd.print("Registando...");
      if (registrarVoto(fingerID, "OPCAO_A")) {
        lcd.clear();
        lcd.print("Voto registado!");
        piscarLED(LED_OK, 3, 300);
        jaVotouLocal = true;
      } else {
        lcd.clear();
        lcd.print("Erro no server");
        piscarLED(LED_ERROR, 5, 150);
      }
      delay(3000);
      break;
    }

    if (!digitalRead(BTN_CANCEL)) {
      lcd.clear();
      lcd.print("Cancelado");
      piscarLED(LED_ERROR, 2, 200);
      delay(2000);
      break;
    }
  }
}

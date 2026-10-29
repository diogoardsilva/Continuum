# Continuum

Espelhar e controlar o iPhone a partir do Windows, ao estilo do iPhone Mirroring do macOS.

**Foco atual:** as apps Mensagens, FaceTime, Telefone, Contactos e Fotos no Windows, separadas como no Mac, com os dados do iCloud.com e do iPhone. O espelhamento e o controlo do iPhone ficam para depois.

O iPhone Mirroring da Apple não pode ser imitado diretamente (precisa do Secure Enclave de um Mac e está desativado na UE), por isso o Continuum junta peças que funcionam no Windows:

- **Vídeo**: recetor AirPlay ("Duplicar ecrã" no iPhone).
- **Controlo**: o PC faz de rato e teclado Bluetooth com o AssistiveTouch ligado, ou toques diretos através do WebDriverAgent por USB.
- **Mais tarde**: Mensagens com histórico (a partir do backup local), notificações e modo chamada do FaceTime.

## Estado

Fase 0: testes de viabilidade no hardware real.

| Teste | Ferramenta | Estado |
|---|---|---|
| O Windows consegue fazer de rato Bluetooth para o iPhone? | `tools/BtHidProbe` | à espera de teste |
| Ver o ecrã do iPhone por AirPlay | `Continuum-AirPlay` (UxPlay) | à espera de teste |
| Tocar no iPhone a partir do PC (WebDriverAgent) | `Continuum-USB` (go-ios) | à espera de teste |
| Ler contactos com fotos (iCloud ou backup) | `tools/ContactsTool` | à espera de teste |
| Iniciar sessão no iCloud.com e ler contactos e fotos | `bridge/continuum_icloud.py` (pyicloud) | à espera de teste |
| Copiar do iPhone por cabo só mensagens, chamadas e contactos | `bridge/continuum_iphone.py` (pymobiledevice3) | à espera de teste |

## Descarregar

Cada commit em `main` gera uma [versão de desenvolvimento](../../releases/tag/dev) com:

- `Continuum.BtHidProbe.exe`: o teste do rato Bluetooth.
- `Continuum-win-x64.zip`: **as apps Continuum**: Mensagens, FaceTime, Telefone, Contactos e Fotos, cada uma separada e ao estilo Mac (barra lateral). Extrai e abre `Continuum.exe`; no lançador, carrega em "Adicionar as apps ao menu Iniciar" para cada app ficar com o seu atalho e ícone. Mostram dados de exemplo até copiares os do iPhone com `Continuum.iPhone.exe`.
- `Continuum-AirPlay-win-x64.zip`: o recetor AirPlay (UxPlay) pronto a usar.
- `Continuum.iPhone.exe`: copia do iPhone (por cabo) as mensagens, o histórico de chamadas e os contactos.
- `Continuum.iCloud.exe`: o teste do iCloud.com (sessão, contactos e fotos).
- `Continuum.Contactos.exe`: o teste dos contactos (iCloud ou backup).
- `Continuum-USB-win-x64.zip`: go-ios e o teste de toques pelo WebDriverAgent.
- `WebDriverAgent-unsigned.ipa`: o WebDriverAgent por assinar, para instalar com a tua Apple ID.

## Teste do rato Bluetooth

1. Descarrega e abre `Continuum.BtHidProbe.exe` (se o Windows avisar, "Mais informações" > "Executar mesmo assim").
2. No iPhone liga o AssistiveTouch: Definições > Acessibilidade > Toque > AssistiveTouch.
3. No iPhone abre Definições > Bluetooth e emparelha com o PC.
4. Usa as setas no PC para mover o cursor no iPhone, Espaço para clicar, H para o ecrã principal.

## Teste do ecrã por AirPlay

1. Descarrega e extrai `Continuum-AirPlay-win-x64.zip`.
2. Abre `uxplay.cmd` dentro da pasta. Quando o Windows perguntar pela firewall, permite em redes privadas.
3. Com o iPhone na mesma rede Wi‑Fi, abre o Centro de Controlo > Duplicar ecrã > **Continuum**.
4. O ecrã do iPhone deve aparecer numa janela no PC.

Se o Continuum não aparecer na lista do iPhone, quase sempre é a firewall: confirma que a rede Wi‑Fi está marcada como privada no Windows.

## Teste de toques pelo WebDriverAgent (USB)

Guia completo passo a passo: [docs/INSTALAR-NO-IPHONE.md](docs/INSTALAR-NO-IPHONE.md). Resumo:

1. Instala a app **Apple Devices** da Microsoft Store (ou o iTunes), liga o iPhone por cabo e aceita "Confiar neste computador".
2. Instala o [Sideloadly](https://sideloadly.io), arrasta para lá o `WebDriverAgent-unsigned.ipa`, escolhe a tua Apple ID e carrega em Start.
3. No iPhone: Definições > Geral > VPN e gestão de dispositivos > confia no teu perfil de programador.
4. No iPhone: Definições > Privacidade e segurança > Modo de programador > ligar (o iPhone reinicia).
5. Extrai `Continuum-USB-win-x64.zip` e abre `test-wda.cmd`.

O teste leva o iPhone ao ecrã principal e toca no centro do ecrã.

## Teste do iCloud.com

Abre `Continuum.iCloud.exe`. Pede a Apple ID, a palavra-passe normal e o código de 6 dígitos, exatamente como o iCloud.com no browser. Depois lê os contactos (com fotos) e as 20 fotos mais recentes e guarda-os em `Documentos\Continuum\iCloud`.

A palavra-passe não fica guardada. A sessão confiável fica em `%LOCALAPPDATA%\Continuum\icloud`, para não pedir o código sempre.

## Teste da sincronização do iPhone

1. Instala a app **Apple Devices** (Microsoft Store) ou o iTunes: dá ao Windows o controlador USB do iPhone.
2. Liga o iPhone por cabo, desbloqueia-o e aceita "Confiar".
3. Abre `Continuum.iPhone.exe`. Copia só as mensagens (com anexos), as chamadas e os contactos para `%LOCALAPPDATA%\Continuum\backup`. A primeira vez demora (o iPhone envia o backup inteiro e o Continuum guarda só o que precisa); as seguintes são rápidas.

## Teste dos contactos

Abre `Continuum.Contactos.exe` e escolhe:

1. **iCloud**: precisas da Apple ID e de uma *palavra-passe para apps*, que crias em [account.apple.com](https://account.apple.com) > Início de sessão e segurança > Palavras-passe para apps. Usa o protocolo oficial dos contactos (CardDAV). A palavra-passe não fica guardada.
2. **Backup**: faz primeiro um backup na app Apple Devices (iPhone > Backups > "Fazer backup de todos os dados para este PC", **sem** encriptação).

O teste mostra quantos contactos encontrou e exporta tudo para `Documentos\Continuum\Contactos` (um `contactos.csv` e as fotos).

## Compilar

Requer o .NET 8 SDK. A app usa Avalonia, por isso também compila em Linux; `tools/UiSnapshots` gera imagens de cada ecrã sem precisar de Windows:

```
dotnet build Continuum.sln -c Release
dotnet run --project tools/UiSnapshots -- snapshots
```

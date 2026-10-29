# Continuum

Espelhar e controlar o iPhone a partir do Windows, ao estilo do iPhone Mirroring do macOS.

O iPhone Mirroring da Apple não pode ser imitado diretamente (precisa do Secure Enclave de um Mac e está desativado na UE), por isso o Continuum junta peças que funcionam no Windows:

- **Vídeo**: recetor AirPlay ("Duplicar ecrã" no iPhone).
- **Controlo**: o PC faz de rato e teclado Bluetooth com o AssistiveTouch ligado, ou toques diretos através do WebDriverAgent por USB.
- **Mais tarde**: Mensagens com histórico (a partir do backup local), notificações e modo chamada do FaceTime.

## Estado

Fase 0: testes de viabilidade no hardware real.

| Teste | Ferramenta | Estado |
|---|---|---|
| O Windows consegue fazer de rato Bluetooth para o iPhone? | `tools/BtHidProbe` | à espera de teste |
| Ver o ecrã do iPhone por AirPlay | | por fazer |
| Tocar no iPhone a partir do PC (WebDriverAgent) | | por fazer |

## Descarregar

Cada commit em `main` gera uma [versão de desenvolvimento](../../releases/tag/dev) com:

- `Continuum.BtHidProbe.exe`: o teste do rato Bluetooth.
- `Continuum-win-x64.zip`: a app (por agora só a janela).

## Teste do rato Bluetooth

1. Descarrega e abre `Continuum.BtHidProbe.exe` (se o Windows avisar, "Mais informações" > "Executar mesmo assim").
2. No iPhone liga o AssistiveTouch: Definições > Acessibilidade > Toque > AssistiveTouch.
3. No iPhone abre Definições > Bluetooth e emparelha com o PC.
4. Usa as setas no PC para mover o cursor no iPhone, Espaço para clicar, H para o ecrã principal.

## Compilar

Requer o .NET 8 SDK no Windows:

```
dotnet build Continuum.sln -c Release
```

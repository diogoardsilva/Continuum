# Instalar no iPhone (sem jailbreak)

Tudo o que o Continuum usa funciona num iPhone normal, sem jailbreak.

| Parte | O que é preciso no iPhone |
|---|---|
| Ver o ecrã (AirPlay) | Nada. Usa o "Duplicar ecrã" que já vem no iPhone. |
| Rato Bluetooth | Nada a instalar. Só ligar o AssistiveTouch. |
| Toques por USB (WebDriverAgent) | Instalar uma app de testes assinada com a tua Apple ID. Passos abaixo. |

## WebDriverAgent passo a passo

O WebDriverAgent é uma ferramenta de testes da Apple usada pelo Appium. Instalada com a tua própria Apple ID, é o mesmo que um programador faz para testar uma app no próprio iPhone.

### No PC (uma vez)

1. Instala a app **Apple Devices** a partir da Microsoft Store. Serve para o Windows reconhecer o iPhone por cabo. O iTunes também serve.
2. Instala o **Sideloadly** a partir de [sideloadly.io](https://sideloadly.io) (versão para Windows).
3. Descarrega o `WebDriverAgent-unsigned.ipa` da página [Versão de desenvolvimento](https://github.com/diogoardsilva/Continuum/releases/tag/dev).

### Ligar o iPhone

4. Liga o iPhone ao PC com um cabo USB‑C e desbloqueia-o.
5. Quando o iPhone perguntar "Confiar neste computador?", carrega em **Confiar** e escreve o código.

### Instalar com o Sideloadly

6. Abre o Sideloadly. Em cima deve aparecer o teu iPhone.
7. Arrasta o `WebDriverAgent-unsigned.ipa` para o ícone do ficheiro no Sideloadly.
8. Em "Apple account" escreve a tua Apple ID e carrega em **Start**.
9. Escreve a password da Apple ID e o código de verificação, se te pedir.
   - Podes usar uma Apple ID secundária e gratuita só para isto, se preferires não usar a principal.
10. Espera até aparecer "Done".

### Autorizar no iPhone

11. **Definições > Geral > VPN e gestão de dispositivos**, toca na tua Apple ID em "App de programador" e depois em **Confiar**.
12. **Definições > Privacidade e segurança > Modo de programador**, liga-o. O iPhone reinicia; quando arrancar, confirma **Ligar** e escreve o código.
    - Esta opção só aparece depois de instalares o WebDriverAgent (passo 10).

### Testar

13. Extrai o `Continuum-USB-win-x64.zip` e abre `test-wda.cmd`.
14. O iPhone deve ir ao ecrã principal e tocar no centro do ecrã. Envia-me o texto que aparece na janela.

## Renovar a cada 7 dias

Com uma Apple ID gratuita, a assinatura dura 7 dias. Depois disso o WebDriverAgent deixa de abrir. Para renovar, repete os passos 6 a 10 (não perdes nada). Se usares uma conta de programador paga, dura um ano.

## Problemas comuns

| O que aparece | O que fazer |
|---|---|
| O Sideloadly não vê o iPhone | Confirma que a app Apple Devices está instalada, troca de cabo ou porta e aceita "Confiar" no iPhone. |
| "Maximum number of apps" | Com uma Apple ID gratuita só podes ter 3 apps destas instaladas. Apaga uma que já não uses. |
| "Programador não fidedigno" ao abrir | Falta o passo 11. |
| O teste diz que o Modo de programador está desligado | Falta o passo 12. |
| O teste diz que não encontra o WebDriverAgent | Repete a instalação (passos 6 a 10). |

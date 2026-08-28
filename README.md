# PeakRecon + PeakLab

Mods BepInEx para estudar e destravar o sistema de mapas do PEAK antigo
(testado na v1.35.a, depot antigo via Steam).

- **PeakRecon** — reconhecimento passivo: extrai informação de debug para arquivos de texto.
- **PeakLab** — experimento ativo: campo de **SEED** + botão de aleatório (`?`) na boarding
  pass do aeroporto; com seed definida, re-sorteia as variantes de bioma da ilha ao carregar
  (e, opcionalmente via config, roda `Clear()`+`Generate()` da `LevelGeneration`).
  Espiões Harmony logam quem chama a pipeline de geração em runtime.
  Config em `BepInEx/config/nicolas.peaklab.cfg`. **v0.1 é para jogo solo** — sync de seed
  em multiplayer vem depois.
- **tools/DumpApi** — inspetor offline: imprime a API de qualquer classe do jogo
  direto do `Assembly-CSharp.dll`, sem abrir o jogo.
  Uso: `DumpApi.exe <pasta Managed> <trecho-do-nome> [...]`

Primeira fase de um projeto maior: entender o sistema de mapas do PEAK antigo
(6 cenas fixas, `Level_0`–`Level_5`, rotação diária por `LevelIndex % 6`) e,
com essa informação, construir um gerador/remixador de mapas para versões antigas.

## O que ele faz

Ao carregar o jogo, escreve em `PEAK\BepInEx\recon\`:

- `types.txt` — assinaturas completas (campos, propriedades, métodos, enums) de todas as
  classes do jogo relacionadas a mapa/geração: `MapHandler`, `MapSegment`, `Biome*`,
  `MapGenerator`, `LevelGeneration`, `Campfire`, `Ascent*`, `NextLevel*` etc.
- `scene_<nome>.txt` — a cada cena carregada: instâncias dos componentes de interesse
  (incluindo objetos inativos e prefabs em memória) com valores dos campos serializados,
  e a hierarquia da cena (profundidade limitada).

## Build

Requer apenas o compilador do .NET Framework que já vem com o Windows (`csc.exe`, C# 5)
e os DLLs do próprio jogo como referência. Ajuste o caminho do jogo em `build.bat` e rode:

```
build.bat
```

O script compila `PeakRecon.dll` e copia para `BepInEx\plugins\`.

## Instalação / remoção

Instalar: copiar `PeakRecon.dll` para `PEAK\BepInEx\plugins\`.
Remover: apagar o mesmo arquivo. Não deixa nenhum outro rastro (além da pasta `BepInEx\recon\`).

## Roadmap

1. **Scout (este plugin)** — mapear a API real de geração embarcada no build.
2. **Experimento** — invocar o maquinário existente (`RandomizeBiomeVariants`,
   troca de `MapSegment` entre cenas, seeds custom) via plugin de teste.
3. **Gerador** — plugin final: seed → mapa "novo" determinístico, sincronizável
   entre jogadores da mesma versão.

## Licença

A definir antes da publicação (intenção: open source).

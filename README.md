# PeakLab — mapas novos para o PEAK antigo (v1.35.a)

Conjunto de mods BepInEx que devolve variedade de mapas a versões antigas do PEAK
(desenvolvido e testado na **v1.35.a**, depot antigo baixado via console da Steam).

**O problema:** a v1.35.a tem só 6 ilhas (`Level_0`–`Level_5`) e a rotação diária é
`LevelIndex % 6` — um loop de 6 dias, sempre com o mesmo combo de variantes que veio
assado de fábrica em cada cena.

**A descoberta:** o maquinário de variantes dos devs veio dentro do build, dormente.
Cada cena carrega variantes de área que o daily nunca sorteia — e um segmento de
montanha alternativo inteiro (Alpine ↔ Mesa/deserto) esperando para ser trocado.

## PeakLab (o mod principal)

UI injetada na boarding pass do aeroporto, no quadro de dificuldade:

- **Modo simples:** campo `SEED` (digite um número) + botão `?` (seed aleatória).
  Campo vazio = daily vanilla, comportamento original intocado.
- **Modo avançado** (botão `AVANCADO`): seletores que ciclam opções e persistem no config:
  - `MONTANHA`: Auto / Alpine / Mesa — troca o segmento inteiro
    (`MapHandler.segments[slot] ↔ variantSegments[0]`, com paredes e campfires próprios)
  - `PRAIA`: Auto / Default / SnakeBeach / BlackSand / BlueBeach / RedBeach / JellyHell
  - `SELVA`: Auto / Default / Thorny / SkyJungle / Pillars / Ivy / Lava / Bombs
  - `NEVE`: Auto / Default / Lava / Spiky / GeyserHell (vale quando a montanha é Alpine)

Com seed definida: `Random.InitState(seed)` → `RandomizeBiomeVariants()` (determinístico,
mesma seed = mesmo mapa) + re-sorteio dos `VariantObjectSelector` (micro-variantes do
deserto: TumblerHell, CactusForest, TornadoHell etc.). Pins do avançado são aplicados
por cima do sorteio. Montanha em Auto + seed = a seed decide (50/50).

**v1.x é para jogo solo.** Sync multiplayer (seed via room property) está no roadmap.

`FullRegenerate` no config: **não usar** — `Clear()` remove conteúdo (paredes) que
`Generate()` não reconstrói; a ilha pode nascer vazia no oceano. Mantido para pesquisa.

## Ferramentas de desenvolvimento

- **PeakRecon** — recon passivo: dumpa API de classes de mapa (`types.txt`) e
  instâncias/hierarquia por cena (`scene_*.txt`) em `BepInEx\recon\`.
- **PeakAutoTest** — piloto automático de teste (desligado por padrão): navega
  Pretitle → Title → Play Solo → Airport → embarque sozinho, aplica seed/cena de
  teste, opcionalmente abre a boarding pass (testa a UI) e fecha o jogo. Permitiu
  validar tudo sem intervenção humana.
- **tools/DumpApi** — inspetor offline de `Assembly-CSharp.dll` por reflexão
  (`DumpApi.exe <pasta Managed> <trecho-do-nome>`), sem abrir o jogo.

## Descobertas de pesquisa (resumo)

1. Ilhas são 100% baked no editor; nenhum código de geração roda em runtime no vanilla
   (espiões Harmony em toda a pipeline: zero chamadas durante load normal).
2. O daily antigo é `LevelIndex % 6`; o índice vem do backend
   (`peaklogin.azurewebsites.net`), que ainda responde `VersionOkay: true` para 1.35.
3. `Level_0/2/4` vêm com Mesa baked; `Level_1/3/5` com Alpine. As 6 cenas compartilham
   o mesmo catálogo de variantes (17 marcadores `BiomeVariant` + 6 `VariantObject`).
4. `RandomizeBiomeVariants()` funciona em runtime e é determinístico por seed —
   só nunca é chamado pelo jogo nessa versão.
5. A troca Alpine↔Mesa não tem caminho vivo no vanilla (nem `GetVariantSegmentFromBiome`
   é chamado); feita manualmente pelo PeakLab trocando as entradas dos arrays.
6. `Clear()`/`Generate()` são assimétricos (paredes somem) — full regen inviável por ora.

## Build

Só precisa do compilador que já vem no Windows (`csc.exe`, C# 5) + os DLLs do jogo:

```
build.bat
```

Compila `PeakRecon.dll`, `PeakLab.dll`, `PeakAutoTest.dll` (instala em `BepInEx\plugins`)
e `tools\DumpApi.exe`. Ajuste o caminho do jogo no topo do script.

## Instalação / remoção

Instalar: DLLs em `PEAK\BepInEx\plugins\`. Remover: apagar as DLLs.
Configs em `BepInEx\config\nicolas.peaklab.cfg` (e `nicolas.peakautotest.cfg`).

## Requisitos

- PEAK v1.35.a (depot antigo) + BepInEx 5.4.23+
- Steam aberto e logado (o jogo não inicializa sem SteamAPI)

## Roadmap

- Sync multiplayer da seed/escolhas via Photon room properties
- Polir layout da UI (posições hoje são proporcionais ao box)
- Investigar regeneração de props segura (chamar `Go()` dos steps certos, sem `Clear()` global)
- Harvest de variantes por cena para esconder opções inexistentes

## Licença

A definir antes da publicação (intenção: open source).

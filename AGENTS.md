# AGENTS.md — manual de engenharia do PeakLab

Documento para um agente (ou dev) **sem contexto nenhum** se apropriar deste projeto.
Leia inteiro antes de mexer. Última atualização: 28/08/2026, commit `9ffac32` (v1.0.0 entregue).

## 1. O que é isto

Mods BepInEx que devolvem variedade de mapas ao **PEAK v1.35.a** (versão antiga, baixada
por depot da Steam). Nessa versão o mapa diário é um loop de 6 cenas fixas; nós
reativamos o maquinário de variantes que os devs deixaram dormente no build e
construímos UI para escolher seed/biomas na boarding pass do aeroporto.

Estado atual: **PeakLab 1.0.0 funciona e está validado** (solo). Dono do projeto: Nicolas
(fala PT-BR; comunicação e commits em português). Publicado em
https://github.com/NicolasCaous/PeakLab (MIT).

## 2. Ambiente (específico desta máquina)

| Item | Valor |
|---|---|
| Jogo | `D:\SteamLibrary\steamapps\common\PEAK` (v1.35.a, ver `version.txt`) |
| Origem do jogo | Console Steam: `download_depot 3527290 3527291 5462070481758001186` |
| Repo | `C:\Users\nicolas\Documents\dump\peak-recon` (git, branch `main`) |
| Plugins instalados | `<jogo>\BepInEx\plugins\` (PeakRecon, PeakLab, PeakAutoTest + 3 de terceiros) |
| Configs | `<jogo>\BepInEx\config\nicolas.peaklab.cfg` e `nicolas.peakautotest.cfg` |
| Log do BepInEx | `<jogo>\BepInEx\LogOutput.log` (sobrescrito a cada execução!) |
| Log do Unity | `C:\Users\nicolas\AppData\LocalLow\LandCrab\PEAK\Player.log` (+ `-prev`) |
| Dumps do recon | `<jogo>\BepInEx\recon\` (`types.txt`, `scene_<nome>.txt`) |
| Logs de teste arquivados | `<repo>\testlogs\` (fora do git) |
| Launcher | `C:\Users\nicolas\Desktop\Jogar PEAK.bat` (abre Steam, espera login, abre jogo) |

Regras operacionais críticas:

- **Steam PRECISA estar aberto e logado** ou `SteamAPI_Init()` falha e o jogo abre
  "meio morto" (cascata de NRE no `GameHandler`, jogador preso no aeroporto com loading
  infinito). Esse era o bug original do "quebra em 24h".
- Abrir o jogo **pelo `PEAK.exe`** (nunca pelo Steam). O Steam não atualiza a pasta
  porque o jogo consta como desinstalado (sem `appmanifest_3527290.acf`).
- **Não há dotnet SDK na máquina.** Compilação é com o csc do .NET Framework
  (C# 5!): `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.
- Plugins de terceiros presentes: `peakseedpicker` (força cena, config
  `com.turtledsr.peakseedpicker.cfg`, só `level_0..5` existem aqui), `PeakVersionBypass`,
  `PeakOptimizer`. Não conflitam com os nossos.

## 3. Como o jogo funciona por dentro (tudo verificado empiricamente)

### 3.1 Mapas e rotação diária

- O build tem **6 ilhas**: cenas `Level_0`–`Level_5` (nada de addressables; tudo em
  `PEAK_Data/levelN` + `globalgamemanagers`).
- O backend `https://peaklogin.azurewebsites.net/api/VersionCheck?version=1.35` responde
  `{VersionOkay, LevelIndex, ...}`; `LevelIndex` é um contador de dias. A cena do dia =
  `LevelIndex % 6` (confirmado: índice 439 → `Level_1`). `VersionOkay:true` para 1.35.
- **Nenhum código de geração roda em runtime no vanilla.** Provado com espiões Harmony
  em toda a pipeline (`LevelGeneration.*`, `MapGenerator.*`, `BiomeSelector.Select`,
  `VariantObjectSelector.SelectVariations`, `WallPieceSpawner.Go`,
  `MapHandler.DetectBiomes`, `TodaysBiomes.SetBiomes`): zero chamadas num load normal.
  As ilhas são 100% baked no editor dos devs.

### 3.2 Estrutura de uma cena de ilha

```
Map/
  Biome_1/Beach/Beach_Segment/<variantes com marcador BiomeVariant>
  Biome_2/Jungle/Jungle_Segment/<...>
  Biome_3/Snow/Snow_Segment/<...>        (Alpine)
  Biome_3/Desert/Desert_Segment/<...>    (Mesa; micro-variantes usam VariantObject)
  (Caldera/Volcano/Peak...)
```

- `MapHandler` (singleton, `MapHandler.Instance`): campos `MapSegment[] segments`
  (a trilha real da ilha, ~5 entradas) e `MapSegment[] variantSegments` (segmentos
  alternativos adormecidos — aqui, 1: a montanha não usada).
- `MapHandler.MapSegment`: `_biome (BiomeType)`, `_segmentParent (GameObject)`,
  `_segmentCampfire`, `wallNext/wallPrevious`, `_dayNightProfile`, `hasVariant`,
  `variantBiome`, `isVariant`. O slot da montanha tem `hasVariant=true, variantBiome=Mesa`.
- Enums: `Segment` = Beach, Tropics, Alpine, Caldera, TheKiln, Peak (slots de progresso);
  `Biome.BiomeType` = Shore, Tropics, Alpine, Volcano, Peak, Mesa, Colony.
- **Streaming**: só o segmento atual fica ativo; o `MapHandler` desliga os demais de
  `segments[]` conforme o jogo roda. `variantSegments[]` NUNCA é gerenciado (fica no
  estado em que carregou). Avanço = campfires (`Campfire.advanceToSegment`) →
  `MapHandler.GoToSegment(Segment)`.
- Debug embutido útil: `MapHandler.JumpToSegment(Segment)` (estático, teleporta e ativa
  o slot; funciona offline), `Ascents.UnlockAll()`.

### 3.3 Catálogo de variantes (idêntico nas 6 cenas)

| Área (pai dos marcadores) | Variantes (`BiomeVariant`, filhos diretos do `*_Segment`) |
|---|---|
| `Beach_Segment` | Default, SnakeBeach, BlackSand, BlueBeach, RedBeach, JellyHell |
| `Jungle_Segment` | Default, Thorny, SkyJungle, Pillars, Ivy, Lava, Bombs |
| `Snow_Segment` | Default, Lava, Spiky, GeyserHell |
| `Desert_Segment` (mais fundo) | micro-variantes via `VariantObject`+`VariantObjectSelector`: ScorpionsHell, TumblerHell, CactusForest, CacusHell, DynamiteHell, TornadoHell |

Baked por cena: `Level_0/2/4` vêm com **Mesa** no slot da montanha; `Level_1/3/5` com
**Alpine**. Cada cena vem com UM combo de variantes ligado (ex.: L1 = Lava+Default+Default).
O daily do vanilla nunca re-sorteia nada disso.

### 3.4 As alavancas que funcionam (e as que não)

- `LevelGeneration` (1 por cena): campo `seed`, métodos `Generate()`, `Clear()`,
  `RandomizeBiomeVariants()`. `RandomizeBiomeVariants()` funciona em runtime e é
  determinístico — MAS liga **cascas ocas**: os contêineres de variante vêm VAZIOS de
  fábrica (o conteúdo só foi baked na variante ativa de cada cena). Ligar variante sem
  rodar os geradores dela = ilha pelada (bug real visto pelo Nicolas em 28/08, seed
  454034). **Receita correta (v1.1.0)**: snapshot dos marcadores ativos → escolher/ativar
  a variante (ApplyPin) → `RunGeneratorsUnder()` SOMENTE nos contêineres que mudaram
  OFF→ON (LevelGenStep.Go, WallPieceSpawner.Go, RockSpawner.Go, RockSpawnerGD.spawnObjects,
  BeachSpawner.Spawn, BasicGrassSpawner.Generate). Quem já vinha ativo mantém o bake
  original (zero duplicação). Validado: ~105 geradores por ilha, zero erros, e print
  do Nicolas mostrando a JellyHell populada (via receita B1 global, equivalente).
  A v1.1.0 não usa mais `RandomizeBiomeVariants` no caminho principal (sorteio próprio
  com `System.Random(seed)` sobre pools controlados; ignora os pesos do `BiomeSelector`).
- **Pools de sorteio (decisão de produto do Nicolas)**: default `Padrao` = só variantes
  presentes nos combos baked oficiais da v1.35.a (praia: Default/SnakeBeach/BlackSand/
  RedBeach; selva: Default/Bombs; neve: Default/Lava/Spiky). Opt-in `Todas` (cycler
  `VARIANTES` na UI) libera o catálogo inteiro + re-sorteio das micro-variantes do
  deserto. Racional: manter a experiência canônica da versão como padrão.
- `VariantObjectSelector.SelectVariations()` — mesmo esquema para as micro-variantes
  do deserto. Chamar por instância (4 na cena típica).
- Troca de montanha Alpine↔Mesa: **não existe caminho vivo no vanilla**
  (`GetVariantSegmentFromBiome` nunca é chamado). Fazemos na mão: trocar as entradas
  `segments[slot] ↔ variantSegments[0]`, ajustar `isVariant/hasVariant`, `SetActive`
  nos `_segmentParent`. **STATUS: BETA/BUGADO** — na 1.0.0 o swap fazia o jogador
  nascer no oceano sem ilha (bug real visto pelo Nicolas em 28/08). Causas conhecidas:
  (1) o segmento variante tem ANCESTRAL desligado (`Map/Biome_3/Desert` off nas cenas
  Alpine) — corrigido na 1.0.1 ativando a cadeia de ancestrais; (2) um NRE engolido
  pelo DOTween aparece no Player.log durante a intro com o swap ativo — suspeita de
  tween da cutscene apontando para objeto do segmento desativado; AINDA NÃO RESOLVIDO.
  Na 1.0.1 o swap só roda com pin explícito (a decisão automática por seed foi
  removida) e está marcado beta na UI/config.
- **`Clear()`+`Generate()` NÃO é seguro**: `Clear()` remove conteúdo (paredes de
  escalada) que `Generate()` não reconstrói (`WallPieceSpawner.Go` nunca é chamado
  pelo `Generate()` — 0 hits no espião). Resultado: ilha vazia/oceano. A opção
  `FullRegenerate` existe no config só para pesquisa, com aviso de NÃO USAR.
  Caminho futuro promissor: chamar `Go()` dos steps certos por tipo, sem `Clear()` global.

### 3.5 Fluxo de embarque e UI

- Aeroporto: `AirportCheckInKiosk` (público). `StartGame(int ascent)` →
  `LoadIslandMaster(ascent)` (decide a cena pelo `NextLevelService.Data.CurrentLevelIndex`)
  → `BeginIslandLoadRPC(string sceneName, int ascent)` (público! aceita QUALQUER cena —
  chamar direto funciona offline e pula a UI).
- `BoardingPass : MenuWindow` — a UI do ticket. Campos públicos prontos:
  `ascentTitle`/`ascentDesc` (TMP_Text, fontes do estilo manuscrito),
  `incrementAscentButton` (fonte de clone de botão), `kiosk`, `startGameButton`.
  Métodos: `StartGame()` (público, chamado pelo botão START), `OnOpen()`/`UpdateAscent()`
  (privados). `MenuWindow.Show()` público NÃO dispara `OnOpen` — por isso o PeakLab
  hooka os três (`OnOpen`, `UpdateAscent`, `MenuWindow.Show` filtrado) para injetar UI.
- O box branco da dificuldade ("Ascent") tem ~709×237 unidades de canvas; a UI do
  PeakLab é posicionada por frações do rect dele.

### 3.6 Fim de run e histórico (v1.2.0)

- `EndScreen : MenuWindow` é o recap pós-run universal, com `peakBanner` (vitória),
  `deadBanner` (derrota), `yourFriendsWonBanner` (multiplayer) e `endTime` (TMP com a
  duração). O PeakLab hooka `MenuWindow.Show`+`OnOpen` (postfix filtrado por tipo) e
  faz poll dos banners numa coroutine → grava o desfecho.
- **`MapHandler.JumpToSegment(Segment.Peak)` dispara a vitória REAL** — é o truque que
  permite testar o fluxo de fim de run inteiro via autopilot, sem escalar nada.
- Histórico em `BepInEx\PeakLabHistory.tsv` (TSV com header; uma linha por escalada;
  `EmAndamento` vira `Abandonou` no Load da sessão seguinte). **Não usar `JsonUtility`
  para classes do mod** — serializa `{}` vazio (limitação com assemblies externos);
  por isso o formato é TSV manual.
- **REGRA DE OURO para spawns em runtime (v1.2.3)**: objetos instanciados pelos
  geradores nascem com `PhotonView.ViewID == 0` (no vanilla eram baked e ganhavam ID
  de cena). RPCs neles vão para o vácuo — sintoma real: mala completava o cast e nunca
  abria (seed 724957, ~474 views órfãos numa ilha). Correção: após popular um contêiner,
  varrer `GetComponentsInChildren<PhotonView>(true)` e registrar cada ViewID==0 com
  `PhotonNetwork.AllocateSceneViewID(v)` (fallback `AllocateViewID(v)`). Qualquer
  trabalho futuro de regeneração de props PRECISA repetir esse pós-processo.
- `tools/DumpApi` aceita `@OutroAssembly.dll` como argumento para inspecionar DLLs
  além do Assembly-CSharp (ex.: `@PhotonUnityNetworking.dll PhotonNetwork`).

### Sistema de customização (skins/fits/chapéus) — v1.3.0

- `Customization` (singleton Zorro) tem catálogos `CustomizationOption[]`: `skins`(9),
  `accessories`(20), `eyes`(18), `mouths`(19), `fits`(20), `hats`(27), `sashes`(9).
  Dump completo + texturas em PNG: `PeakRecon 0.2.0` escreve `recon\customization.txt`
  e `recon\tex\*.png` na primeira cena que tiver o singleton.
- **Todo fit é só material+textura** no mesmo mesh (`MainMesh`, shader `W/Character`,
  atlas 1024x1024). Adicionar um fit = clonar uma option (`Object.Instantiate`), trocar
  `fitMaterial` (clone com `mainTexture` própria) e **anexar ao array `fits`** — o
  passaporte (`PassportManager.SetButtons`) lê o catálogo dinamicamente e mostra
  sozinho (grade comporta pelo menos 21). Zerar `requiredAchievement`/`testLocked`
  para nascer desbloqueado. Marcar `hideFlags = DontUnloadUnusedAsset` em option,
  material e texturas (senão `UnloadUnusedAssets` pode coletá-los entre cenas).
- Os chapéus Cap(0) e Beret(1) usam `fitHatMaterial` do fit corrente — combinam com
  qualquer fit novo de graça. `fitMaterialOverrideHat` customiza isso.
- **`overrideHat`/`overrideHatIndex` no fit força um chapéu** (os fits Bundled forçam
  os capuzes 27/28 assim). Chapéus são renderers baked por personagem
  (`CustomizationRefs.playerHats[]`, 29 no vanilla, índice do catálogo = índice do
  array). Chapéu NOVO = clonar o GameObject de um existente em CADA personagem e
  anexar ao array — `PeakLabSkins.ExtendRefs` faz isso (capacete soviético = clone do
  MedicHelmet[7] no índice 29), chamado por postfix em `CharacterCustomization.Awake`,
  prefix em `SetCustomizationForRef` (boneco do passaporte) e sweep no sceneLoaded.
- O rig NÃO tem mesh de calça comprida: `fitMaterialOverridePants` só troca a
  ESTAMPA do renderer `Shorts` (provado com o M_Scout_TropicalPants). Cobertura
  máxima de perna = variante `isSkirt=true` (o Fit_Soviet_Shinel usa isso como
  capote). Renderers reais do personagem: `MainMesh` com 3 slots ([0] pele,
  [1] fitMaterial — inclui camisa E meião, [2] fitMaterialShoes) + `Shorts`/
  `Skirt` (fitPantsMaterial). O boneco do passaporte usa os MESMOS materiais
  (validar nele vale para o personagem, contanto que a DLL seja a mesma).
- A escolha é salva POR ÍNDICE em `PersistentPlayerData` — remover o mod com o fit
  20 equipado deixa o save apontando para fora do catálogo (documentado no config).
- Enum das abas do passaporte: `Customization+Type` (aninhado em `Customization`,
  NÃO em CustomizationOption). Para abrir programaticamente: derivar o enum de
  `AccessTools.Method(typeof(PassportManager), "OpenTab").GetParameters()[0]`.
- Abrir o passaporte via código: `PassportManager.Show()` só ergue o item na mão;
  quem abre a UI de verdade é `Action_Passport.RunAction()` (checar `pm.isOpen`).
- Texturas do mod ficam EMBUTIDAS na DLL (`csc -resource:arquivo.png,NomeLogico`),
  carregadas com `GetManifestResourceStream` + `ImageConversion.LoadImage` — a
  instalação continua sendo um único arquivo.
- `tools/MakeSovietTex.cs` (System.Drawing) gera as texturas a partir dos dumps:
  remap por família de cor em HSV (preserva sombreado pintado), clone-stamp para
  apagar emblemas, estrela desenhada por polígono. Iterar: gerar → build → autopilot
  `TestPassport=true` → ver `recon\passport_test.png` e `recon\dummy_rt.png`.
- **ARMADILHA MORTAL: o boneco do passaporte usa OUTRO MESH.** O personagem real
  veste o `fitMesh` da option (MainMesh 4240 verts, id 438378, NAO legivel por CPU)
  e o `PlayerCustomizationDummy` continua com o MainMesh base (3862 verts, id 43392)
  so trocando materiais. Mesmos nomes, UVs DIFERENTES (ex.: o meiao do personagem
  sampleia a faixa diagonal ate o canto (1023,1023); o do boneco, y598-800).
  Validar textura NO BONECO NAO VALE NADA - validar com selfie do personagem real
  (autotest: `TestPassport` tira `recon\char_selfie_frente/costas.png` com camera
  propria; `UnlitCharacter=true` + atlas-gradiente = leitura de UV do personagem).
- **Pipeline de mapeamento UV** (para saber que região do atlas cai em cada parte
  do corpo): vestir o boneco com `UVGradient_atlas.png` (R=coluna, G=linha, B=0
  como marca d'água), `UnlitDummy=true` no autotest (troca shader por UI/Default →
  cores puras, sem luz da cena; o autotest também afasta a `dummyCamera` p/ corpo
  inteiro), e decodificar o render com `tools/DecodeUV.cs`. ATENÇÃO: filtragem
  bilinear/mip gera decodes fantasma — confirme SEMPRE com um render unlit da
  textura final antes de confiar no mapa.
- **Mapa UV do atlas de fit (verificado no render)**: "quadro" com moldura
  (x718-908, y40-262) = gola + patches do peito (espelhado nos 2 lados); faixa
  superior central (x435-705, y0-58) = topo do peito/gola; região listrada branca
  (x448-972, y595+) = MEIÃO (ribs verticais; não é a saia!); montanha amarela
  (712,450) = emblema das COSTAS; bloco "janela" (x62-348, y282-622) = sem uso
  visível no fit Shorts (provável gorro/saia); gravata = verde-escuro do canto
  inferior esquerdo (x0-437, y840+); meião NÃO vem do material dos sapatos (o
  `Scouts Tex pack 1` é só o sapato em si).

## 4. Componentes do repo

| Arquivo | Papel |
|---|---|
| `PeakLab.cs` | O mod principal (UI + motor). ~500 linhas, C# 5. |
| `PeakLabHistory.cs` | Histórico de escaladas (TSV + modal HISTORICO). |
| `PeakLabSkins.cs` | Fit_Soviet no catálogo + capacete via `ExtendRefs`. Texturas embutidas na DLL. |
| `PeakRecon.cs` | Recon passivo: dumpa API (`types.txt`), cenas (`scene_*.txt`) e customização (`customization.txt` + `tex\*.png`). Sem Harmony. |
| `PeakAutoTest.cs` | Piloto automático de teste (`AirportOnly`, `TestPassport`, seed, screenshots). `Enabled=false` por padrão — SEMPRE devolver para false após testes. |
| `tools/DumpApi.cs` | Inspetor offline de `Assembly-CSharp.dll` por reflexão. Não precisa do jogo aberto. |
| `tools/MakeSovietTex.cs` | Gerador offline das texturas soviéticas a partir dos dumps do PeakRecon. |
| `assets/` | PNGs gerados (atlas/ícone/capacete) que o build embute na PeakLab.dll. |
| `build.bat` | Compila tudo e instala os DLLs no jogo. |
| `testlogs/` | Logs arquivados dos 16 runs de pesquisa (fora do git; fonte das conclusões acima). |

### PeakLab por dentro (ordem importa)

1. `Awake`: binds de config, patches Harmony (UI hooks + `BoardingPass.StartGame` prefix).
2. `StartGamePrefix`: lê o campo de seed da UI → `PendingSeed` (int?; null = vanilla).
3. `OnSceneLoaded(Level_*)` → `ApplyCustomization`:
   a. Sem seed e sem pins → não toca em nada (vanilla puro).
   b. Montanha: pin explícito, ou decidida por `new System.Random(seed).Next(2)` se Auto+seed → `EnsureMountain`.
   c. Com seed: `Random.InitState(seed)` → `LevelGeneration.seed=seed` →
      `RandomizeBiomeVariants()` → `RunVariantSelectors` (deserto).
   d. Pins de área (`ApplyPin`): liga a variante escolhida, desliga irmãs (match por
      `transform.parent.name == "X_Segment"`).
   e. `LogIslandState()` + repetição em t+12s (coroutine) para ver o estado assentado.
4. Timing: `sceneLoaded` dispara depois dos `Awake` da cena e antes dos `Start` —
   janela perfeita (MapHandler.Instance já existe, spawners ainda não rodaram).

Acesso ao jogo: métodos/campos públicos tipados (referenciamos `Assembly-CSharp.dll`);
privados via `AccessTools` (HarmonyLib). `Resources.FindObjectsOfTypeAll` acha inativos
(SEMPRE filtrar `gameObject.scene.IsValid()` — retorna assets/prefabs também).

## 5. Como compilar

`build.bat` faz tudo. Manualmente (a linha completa do PeakLab):

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -t:library -langversion:5 ^
  -out:PeakLab.dll ^
  -r:"<M>\UnityEngine.dll" -r:"<M>\UnityEngine.CoreModule.dll" ^
  -r:"<M>\UnityEngine.UIModule.dll" -r:"<M>\UnityEngine.UI.dll" ^
  -r:"<M>\Unity.TextMeshPro.dll" -r:"<M>\Assembly-CSharp.dll" ^
  -r:"<M>\Zorro.ControllerSupport.dll" -r:"<M>\Zorro.Core.Runtime.dll" ^
  -r:"<M>\Zorro.UI.Runtime.dll" -r:"<M>\netstandard.dll" ^
  -r:"<B>\BepInEx.dll" -r:"<B>\0Harmony.dll" -r:System.dll -r:System.Core.dll ^
  PeakLab.cs
```

(`<M>` = `PEAK_Data\Managed`, `<B>` = `BepInEx\core`.) Depois copiar o `.dll` para
`BepInEx\plugins\`.

Armadilhas de compilação:

- **C# 5 apenas**: nada de `$"..."`, `?.`, `nameof`, out var, membros expression-bodied.
- Erro `CS0012 tipo X definido em assembly não referenciado` → adicionar o dll citado
  (foi assim que entraram Zorro.* e Photon*; `PeakAutoTest` precisa de
  `PhotonUnityNetworking.dll` + `PhotonRealtime.dll` porque tipa o kiosk).
- Warnings CS0618 (APIs obsoletas do Unity) são esperados e inofensivos.

## 6. Workflow de teste automatizado (o superpoder deste projeto)

O jogo pode ser testado SEM humano. Receita:

```bash
# 1. configurar o teste
printf '[AutoTest]\nEnabled = true\nTestSeed = 777\nAscent = 0\nQuitAfterSeconds = 20\nJumpToSegment = \nSceneOverride = Level_1\nTestBoardingUI = false\n' \
  > "/d/SteamLibrary/steamapps/common/PEAK/BepInEx/config/nicolas.peakautotest.cfg"
# (opcional) pins do PeakLab em nicolas.peaklab.cfg

# 2. lançar e esperar o jogo fechar sozinho (~70-90s por run)
cmd //c start "" //D "D:\SteamLibrary\steamapps\common\PEAK" "D:\SteamLibrary\steamapps\common\PEAK\PEAK.exe"
for i in $(seq 1 50); do sleep 5; tasklist //FI "IMAGENAME eq PEAK.exe" | grep -q PEAK.exe || break; done

# 3. arquivar e ler
cp "/d/SteamLibrary/steamapps/common/PEAK/BepInEx/LogOutput.log" testlogs/meu-teste.log
grep -E "PeakLab|AutoTest" testlogs/meu-teste.log
```

Opções do AutoTest: `TestSeed` (vazio = vanilla), `SceneOverride` (`Level_N` embarca
direto nessa cena), `JumpToSegment` (ex.: `Alpine` — teleporta aos 15s, ativa o slot),
`TestBoardingUI` (abre/fecha a boarding pass antes de embarcar — exercita a injeção de UI),
`QuitAfterSeconds`. O fluxo dele: Pretitle avança sozinho → clica `m_playSoloButton` do
`MainMenuMainPage` → `kiosk.StartGame`/`BeginIslandLoadRPC` → quit.

**SEMPRE deixar `Enabled = false` ao terminar** (senão o jogo do Nicolas decola sozinho —
ele já viu "o jogo nascer num oceano infinito" durante uma bateria de testes).

Armadilhas do harness aprendidas na prática:
- **Truncar `LogOutput.log` antes de cada launch** (`: > LogOutput.log`): o BepInEx só
  sobrescreve depois do boot; um poll de grep no arquivo velho "acha" linhas da rodada
  anterior e dispara cedo demais (foi o que estragou screenshots da bateria valid2).
- Screenshot via `tools/screenshot.ps1` captura A TELA: se o Nicolas estiver usando o
  PC (janela na frente), a foto sai do desktop, não do jogo. Combinar com ele antes,
  ou validar por log (posição dos personagens: praia ~y=2; água/void: y<=0 ou queda).

Interpretação de log: entradas multi-linha do BepInEx (tabelas de segmentos, stacks)
não aparecem inteiras num grep simples — usar `grep -A8`. Linhas-chave do PeakLab:
`montanha trocada`, `pin X = Y`, `variantes ativas: ...`, `segmentos:` (tabela com
`(on)/(OFF)` por parent).

## 7. Como investigar mais o jogo

- **API de qualquer classe** (sem abrir o jogo):
  `tools\DumpApi.exe "D:\...\PEAK_Data\Managed" NomeParcial1 NomeParcial2`
- **Estado de cena em runtime**: PeakRecon dumpa toda cena carregada em
  `BepInEx\recon\scene_<nome>.txt` (componentes de interesse com valores de campos +
  hierarquia). Ajustar as listas `TypeKeys`/`SceneDumpTypes` no fonte para ampliar.
- **Quem chama o quê**: adicionar espiões Harmony (prefix logando
  `Environment.StackTrace`) — ver o `SpyPatch` no histórico do git (v0.1 do PeakLab,
  commit `302c884`; removidos na v1.0 para limpar o log).
- Não há decompilador na máquina (sem dotnet para ilspycmd). Corpos de método são caixa
  preta — a técnica do projeto é inferir comportamento por experimento + espião.

## 8. Roadmap com dicas de implementação

0. **Consertar o swap de montanha (prioridade)**: reproduzir com autopilot + log da
   POSIÇÃO do personagem (t+15s: se y ~nível do mar e longe do Beach spawn → nasceu
   na água). Comparar Alpine-baked sem swap vs com swap 1.0.1 (ancestrais corrigidos).
   Investigar o NRE do DOTween na intro (procurar "DOTWEEN" no Player.log); candidatos:
   tween da cutscene do avião referenciando objeto do Snow_Segment desativado, ou
   `CampfireSectionGroundStealer`/`Campfire_Set_Segment` do segmento trocado. Validar
   VISUALMENTE (pedir print ao Nicolas) antes de tirar o selo beta.
1. **Sync multiplayer** (o mais pedido a seguir): no `StartGamePrefix` do host, gravar
   seed+pins em `PhotonNetwork.CurrentRoom.SetCustomProperties`; nos clientes, ler em
   `OnSceneLoaded` antes de aplicar (prioridade sobre config local). Todos precisam do
   mod. Atenção: `Photon.Pun.PhotonNetwork` requer referenciar `PhotonUnityNetworking.dll`.
   Testar solo é impossível — combinar com o Nicolas e um amigo.
2. **Polir UI**: posições em `BuildUI` (frações do box). Pendente: print do Nicolas
   para ajuste fino. Quando o painel avançado abre, pode cobrir a descrição do ascent —
   avaliar esconder `ascentDesc` enquanto aberto.
3. **Re-roll seguro de props**: investigar chamar `Go()`/`Add()` de `PropSpawner`s
   individuais (ou `GoAll()`) SEM `Clear()` global; ou `Clear()` por step exceto walls.
   Os espiões + `scene_Level_N.txt` dizem o que cada step possui.
4. **Filtrar opções por cena**: os harvests (testlogs/harvest-level*.log) mostram o
   catálogo por cena; hoje a UI oferece tudo sempre (pin inexistente só loga warning).
5. **Ocultar "NEVE" quando montanha=Mesa** (só faz efeito com Alpine).

## 9. Convenções do projeto

- Idioma: PT-BR (código com comentários sem acento por segurança de encoding).
- Commits: mensagem em PT + `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Não commitar: DLLs, `testlogs/`, `*.exe` (ver `.gitignore`).
- Publicado em https://github.com/NicolasCaous/PeakLab sob licença **MIT**.
- O jogo é do Nicolas e o mod é para uso pessoal; nada aqui burla compra do jogo
  (ele possui o PEAK na Steam — o SteamAPI exige a conta logada).

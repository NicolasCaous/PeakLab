# PeakLab

Mapas novos para o **PEAK v1.35.a** (versão antiga via depot da Steam).

A v1.35.a tem só 6 ilhas fixas e o mapa diário repete a cada 6 dias, sempre com o mesmo
combo de variantes. O PeakLab reativa o sistema de variantes que veio dormente no build:

- **Seed de mapa** na boarding pass do aeroporto — digite um número ou aperte `?` para
  sortear. A seed gera de novo as pedras, formações, plantas e itens de todos os biomas
  com os geradores do próprio jogo. Mesma seed = mesmo mapa (bom para jogar "o mesmo
  diário" com amigos na mesma versão). O relevo do chão é fixo no jogo e não muda.
- **Modo avançado** — escolha a montanha (**Alpine ou Mesa/deserto** — beta) e as variantes de
  **praia** (SnakeBeach, BlackSand, BlueBeach, RedBeach, JellyHell...), **selva**
  (SkyJungle, Pillars, Ivy, Thorny, Lava, Bombs...) e **neve** (Lava, Spiky, GeyserHell...).
- Campo vazio + tudo em `Auto` = jogo original intocado.

> ⚠️ v1.x é para jogo **solo** — em multiplayer os outros jogadores ainda não recebem
> sua seed. Testado apenas na v1.35.a.

## Requisitos

1. **PEAK v1.35.a** instalado via depot antigo. Resumo (Steam fechado, jogo desinstalado):
   - `Win+R` → `steam://open/console`
   - `download_depot 3527290 3527291 5462070481758001186`
   - copiar o conteúdo baixado para `...\steamapps\common\PEAK`
   - criar `steam_appid.txt` na pasta do jogo com o conteúdo `3527290`
2. **BepInEx 5.4.23+ (win_x64)** extraído na pasta do jogo (rodar o jogo uma vez para gerar as pastas).
3. **Steam aberto e logado** sempre que for jogar — sem isso o jogo abre quebrado
   (preso no aeroporto com loading infinito). Pode deixar o Steam aberto sem medo:
   como o jogo consta como desinstalado, ele não atualiza a pasta.

## Instalação

1. Baixe/compile `PeakLab.dll` (ver [Build](#build-a-partir-do-código)).
2. Copie para `PEAK\BepInEx\plugins\`.
3. Abra o jogo **pelo `PEAK.exe`** (nunca pelo Steam), com o Steam aberto.

Opcionais do repositório (ferramentas de desenvolvimento, não são necessárias para jogar):
`PeakRecon.dll` (extrai dumps de debug) e `PeakAutoTest.dll` (piloto automático de testes,
desligado por padrão).

## Como usar

1. No aeroporto, interaja com o **quiosque de check-in** para abrir a boarding pass.
2. No quadro da dificuldade:
   - **`SEED:`** — digite um número, ou aperte **`?`** para sortear. Vazio = daily normal.
   - **`AVANCADO`** — abre os seletores `MONTANHA` / `PRAIA` / `SELVA` / `NEVE`.
     Clique para ciclar as opções (`AUTO` = deixa a seed/o jogo decidir).
3. Aperte **START**. As escolhas do avançado ficam salvas entre sessões.
4. **`HISTORICO`** (no painel avançado) — abre a retrospectiva das tuas escaladas:
   data, seed, pool, cena, ascent, desfecho (**TERMINOU** / **FALHOU** / **ABANDONOU**)
   e o tempo da run. **Clicar numa linha copia a seed de volta pro campo** — é assim
   que você "salva" e rejoga um mapa que gostou. Os dados ficam em
   `PEAK\BepInEx\PeakLabHistory.tsv` (abre direto no Excel).
5. **Uniformes de soldado soviético** ☭ — abra o **passaporte** (item 1 na mão →
   "open"), aba de **roupas** (camiseta): são os dois últimos da grade —
   **campanha** (calção + meião cáqui) e **capote/shinel** (casacão cumprido). Gimnastyorka
   cáqui com estrela vermelha no peito, gravata vermelha, botas pretas — e o fit
   **equipa sozinho o capacete de aço com estrela** (como os casacos de inverno fazem
   com o capuz). Aviso: outros jogadores só veem o uniforme se também tiverem o mod;
   sem o mod instalado, o save fica apontando para uma roupa que não existe — troque
   de roupa **antes** de desinstalar.

## Configuração

Arquivo: `PEAK\BepInEx\config\nicolas.peaklab.cfg`

| Seção/Chave | Padrão | Efeito |
|---|---|---|
| `[Geracao] RandomizeBiomeVariants` | `true` | Com seed, sorteia as variantes da ilha |
| `[Geracao] PoolDeVariantes` | `Padrao` | `Padrao` = só variantes que os devs usaram nos 6 mapas oficiais; `Todas` = catálogo completo (JellyHell, SkyJungle, BlueBeach...) |
| `[Geracao] RegenerarBiomas` | `true` | Com seed, apaga e gera de novo o conteúdo de todos os biomas. `false` = só troca as variantes e mantém o conteúdo que vem pronto na ilha |
| `[Geracao] Diagnostico` | `false` | Loga o censo dos geradores e a impressão digital de cada bioma (para testes) |
| `[Geracao] FullRegenerate` | `false` | **NÃO USAR** — apaga paredes que não voltam (ilha vazia no oceano) |
| `[Avancado] Montanha/Praia/Selva/Neve` | `Auto` | Espelham os seletores da UI |
| `[Skins] FitSovietico` | `true` | Adiciona o uniforme soviético (com capacete) ao passaporte |

Como funciona por baixo: cada bioma tem dezenas de geradores dos devs (`PropSpawner`
e parentes) que espalham pedras, formações de escalada, plantas e malas sobre o chão.
No jogo original eles só rodaram no editor, e a ilha vem pronta. Com seed, o PeakLab
escolhe as variantes, apaga o que os geradores de cada bioma tinham criado e roda
todos de novo, com o sorteio do Unity iniciado pela seed e pelo nome do bioma. Cada
bioma usa a própria sequência de sorteio, então a mesma seed gera sempre a mesma
ilha. Sem seed, nada é regenerado.

A regeneração acontece durante o carregamento da ilha e acrescenta de 15 a 25
segundos à tela de loading (a selva e o deserto são os biomas mais pesados).

## Solução de problemas

- **Preso no aeroporto, loading infinito, plano não embarca** → o Steam estava fechado
  quando você abriu o jogo. Feche o jogo, abra o Steam, abra o jogo de novo.
- **UI do seed não aparece na boarding pass** → veja `BepInEx\LogOutput.log` e procure
  por `[UI]`; abra uma issue com o log.
- **Escolhi uma variante e não mudou nada** → variantes de `NEVE` só valem com montanha
  Alpine; e a mudança vale para a **próxima** ilha carregada, não a atual.
- **Nasci na água, sem ilha** → você usou `MONTANHA` diferente de `Auto` (recurso beta).
  Volte para `Auto`, feche e reabra o jogo, e reporte numa issue com o
  `BepInEx\LogOutput.log`.

## Build a partir do código

Não precisa de SDK — só do compilador que já vem com o Windows (`csc.exe`) e dos DLLs
do próprio jogo. Ajuste o caminho do jogo no topo de `build.bat` e rode:

```bat
build.bat
```

Compila `PeakRecon.dll`, `PeakLab.dll`, `PeakAutoTest.dll` (e instala em
`BepInEx\plugins\`) + `tools\DumpApi.exe` e `tools\CallGraph.exe`.

## Para desenvolvedores e agentes

Toda a engenharia reversa, arquitetura do jogo, workflow de testes automatizados e
roadmap estão documentados em **[AGENTS.md](AGENTS.md)** — leitura obrigatória antes de
mexer no código.

## Licença

[MIT](LICENSE)

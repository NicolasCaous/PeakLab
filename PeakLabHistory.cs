// PeakLabHistory + FixPhotonViews - parte do PeakLab.dll
// Grava cada embarque (data, seed, cena, pool, variantes, ascent) e o desfecho
// lido dos banners da EndScreen (Terminou/Falhou). JSON em BepInEx\PeakLabHistory.json
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class PeakRunRecord
{
    public string data;      // ISO "yyyy-MM-dd HH:mm"
    public int seed;         // -1 = daily sem seed
    public string cena;      // Level_N
    public string pool;      // Padrao/Todas ("" quando daily puro)
    public int ascent;
    public string variantes; // ex: "JellyHell, Bombs, Lava"
    public string resultado; // EmAndamento / Terminou / Falhou / Abandonou
    public string tempo;     // duracao mostrada pela EndScreen (ex: "23:14")
}

[Serializable]
public class PeakRunHistoryFile
{
    public List<PeakRunRecord> runs = new List<PeakRunRecord>();
}

public static class PeakLabHistory
{
    private static PeakRunHistoryFile _file;
    private static string _path;

    private static GameObject _panel;
    private static TMP_Text _pageLabel;
    private static readonly List<Button> _rowButtons = new List<Button>();
    private static readonly List<TMP_Text> _rowLabels = new List<TMP_Text>();
    private static int _page;
    private const int RowsPerPage = 9;

    // ---------- persistencia ----------

    // formato TSV (uma linha por escalada; abre no Excel):
    // data \t seed \t cena \t pool \t ascent \t variantes \t resultado \t tempo
    public static void Load()
    {
        _path = Path.Combine(Paths.GameRootPath, Path.Combine("BepInEx", "PeakLabHistory.tsv"));
        _file = new PeakRunHistoryFile();
        try
        {
            if (File.Exists(_path))
            {
                string[] lines = File.ReadAllLines(_path);
                foreach (string line in lines)
                {
                    string[] f = line.Split('\t');
                    if (f.Length < 8 || f[0] == "data") continue;
                    PeakRunRecord r = new PeakRunRecord();
                    r.data = f[0];
                    int s;
                    r.seed = int.TryParse(f[1], out s) ? s : -1;
                    r.cena = f[2];
                    r.pool = f[3];
                    int a;
                    r.ascent = int.TryParse(f[4], out a) ? a : 0;
                    r.variantes = f[5];
                    r.resultado = f[6];
                    r.tempo = f[7];
                    _file.runs.Add(r);
                }
            }
        }
        catch (Exception e)
        {
            PeakLabPlugin.Log.LogWarning("[Historico] falha ao ler: " + e.Message);
        }
        // runs que ficaram "EmAndamento" = jogo fechado no meio
        int fixados = 0;
        foreach (PeakRunRecord r in _file.runs)
        {
            if (r.resultado == "EmAndamento") { r.resultado = "Abandonou"; fixados++; }
        }
        if (fixados > 0) Save();
        PeakLabPlugin.Log.LogInfo("[Historico] " + _file.runs.Count + " escalada(s) no historico");
    }

    private static string Clean(string s)
    {
        if (s == null) return "";
        return s.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
    }

    private static void Save()
    {
        try
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("data\tseed\tcena\tpool\tascent\tvariantes\tresultado\ttempo");
            foreach (PeakRunRecord r in _file.runs)
            {
                sb.AppendLine(Clean(r.data) + "\t" + r.seed + "\t" + Clean(r.cena) + "\t" +
                              Clean(r.pool) + "\t" + r.ascent + "\t" + Clean(r.variantes) + "\t" +
                              Clean(r.resultado) + "\t" + Clean(r.tempo));
            }
            File.WriteAllText(_path, sb.ToString());
        }
        catch (Exception e)
        {
            PeakLabPlugin.Log.LogWarning("[Historico] falha ao salvar: " + e.Message);
        }
    }

    public static void StartRun(int seed, string cena, string pool, int ascent, string variantes)
    {
        PeakRunRecord r = new PeakRunRecord();
        r.data = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        r.seed = seed;
        r.cena = cena;
        r.pool = pool;
        r.ascent = ascent;
        r.variantes = variantes;
        r.resultado = "EmAndamento";
        r.tempo = "";
        _file.runs.Add(r);
        _polling = false;
        Save();
        PeakLabPlugin.Log.LogInfo("[Historico] escalada registrada: " +
            (seed >= 0 ? "#" + seed : "daily") + " em " + cena);
    }

    public static void SetOutcome(string resultado, string tempo)
    {
        for (int i = _file.runs.Count - 1; i >= 0; i--)
        {
            if (_file.runs[i].resultado == "EmAndamento")
            {
                _file.runs[i].resultado = resultado;
                _file.runs[i].tempo = tempo ?? "";
                Save();
                PeakLabPlugin.Log.LogInfo("[Historico] desfecho: " + resultado +
                    (string.IsNullOrEmpty(tempo) ? "" : " em " + tempo));
                return;
            }
        }
        PeakLabPlugin.Log.LogWarning("[Historico] desfecho sem escalada em andamento: " + resultado);
    }

    private static bool _polling;

    // le os banners da EndScreen ate um deles aparecer
    public static IEnumerator PollOutcome(EndScreen es)
    {
        if (_polling) yield break;
        _polling = true;
        float t = 0f;
        while (es != null && t < 60f)
        {
            if (es.peakBanner != null && es.peakBanner.activeInHierarchy)
            {
                SetOutcome("Terminou", TimeOf(es));
                _polling = false;
                yield break;
            }
            if (es.deadBanner != null && es.deadBanner.activeInHierarchy)
            {
                SetOutcome("Falhou", TimeOf(es));
                _polling = false;
                yield break;
            }
            if (es.yourFriendsWonBanner != null && es.yourFriendsWonBanner.activeInHierarchy)
            {
                SetOutcome("Terminou (amigos)", TimeOf(es));
                _polling = false;
                yield break;
            }
            t += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }
        _polling = false;
    }

    // o texto es.endTime so recebe o tempo real depois de uma pausa na animacao
    // da EndScreen; antes disso ele mostra o valor de exemplo do prefab (1:32:10).
    // Por isso o tempo vem direto do RunManager, a mesma fonte que a tela usa.
    private static string TimeOf(EndScreen es)
    {
        try
        {
            System.Type rm = HarmonyLib.AccessTools.TypeByName("RunManager");
            object inst = HarmonyLib.AccessTools.Field(rm, "Instance").GetValue(null);
            if (inst == null) return "";
            float secs = (float)HarmonyLib.AccessTools.Field(rm, "timeSinceRunStarted").GetValue(inst);
            int s = Mathf.FloorToInt(secs);
            return (s / 3600) + ":" + ((s / 60) % 60).ToString("00") + ":" + (s % 60).ToString("00");
        }
        catch (Exception e)
        {
            PeakLabPlugin.Log.LogWarning("[Historico] tempo da escalada: " + e.Message);
            return "";
        }
    }

    // ---------- UI ----------

    public static void TogglePanel(BoardingPass bp)
    {
        if (_panel == null || !_panel) BuildPanel(bp);
        bool show = !_panel.activeSelf;
        if (show) { _page = 0; RefreshRows(); }
        _panel.SetActive(show);
    }

    private static void BuildPanel(BoardingPass bp)
    {
        _rowButtons.Clear();
        _rowLabels.Clear();
        Transform canvasRoot = bp.transform; // Canvas_BoardingPass
        float fs = bp.ascentDesc.fontSize;

        // fundo escurecido em tela cheia, sempre por cima do ticket
        _panel = new GameObject("PeakLab_HistoryPanel", typeof(RectTransform));
        _panel.transform.SetParent(canvasRoot, false);
        RectTransform pr = (RectTransform)_panel.transform;
        pr.anchorMin = Vector2.zero;
        pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero;
        pr.offsetMax = Vector2.zero;
        Canvas cv = _panel.AddComponent<Canvas>();
        Canvas parentCv = canvasRoot.GetComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = (parentCv != null ? parentCv.sortingOrder : 0) + 100;
        if (parentCv != null) cv.sortingLayerID = parentCv.sortingLayerID;
        _panel.AddComponent<GraphicRaycaster>();
        Image dim = _panel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        Button dimBtn = _panel.AddComponent<Button>();
        dimBtn.targetGraphic = dim;
        dimBtn.onClick.AddListener(delegate { _panel.SetActive(false); });

        // cartao branco estilo ticket, centralizado
        GameObject card = new GameObject("Card", typeof(RectTransform));
        card.transform.SetParent(_panel.transform, false);
        RectTransform cr = (RectTransform)card.transform;
        cr.anchorMin = new Vector2(0.5f, 0.5f);
        cr.anchorMax = new Vector2(0.5f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f);
        cr.anchoredPosition = Vector2.zero;
        cr.sizeDelta = new Vector2(760f, 820f);
        Image cardImg = card.AddComponent<Image>();
        Image src = null;
        Transform ticketPanel = bp.transform.Find("BoardingPass/Panel");
        if (ticketPanel != null) src = ticketPanel.GetComponent<Image>();
        if (src != null && src.sprite != null)
        {
            cardImg.sprite = src.sprite;
            cardImg.type = src.type;
            cardImg.color = Color.white;
        }
        else cardImg.color = new Color(0.96f, 0.94f, 0.89f, 1f);

        // X de fechar
        Button close = CloneButton(bp, card.transform, "Close", "X", fs);
        RectTransform xr = (RectTransform)close.transform;
        xr.anchorMin = new Vector2(1f, 1f);
        xr.anchorMax = new Vector2(1f, 1f);
        xr.pivot = new Vector2(1f, 1f);
        xr.anchoredPosition = new Vector2(-14f, -14f);
        xr.sizeDelta = new Vector2(56f, 56f);
        close.onClick.AddListener(delegate { _panel.SetActive(false); });

        TMP_Text title = CloneText(bp, card.transform, "Title", "HISTORICO DE ESCALADAS", fs + 6f);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0f, -18f);
        tr.sizeDelta = new Vector2(-48f, 50f);
        title.alignment = TextAlignmentOptions.Center;

        for (int i = 0; i < RowsPerPage; i++)
        {
            Button row = CloneButton(bp, card.transform, "Row" + i, "", fs - 8f);
            RectTransform rr = (RectTransform)row.transform;
            rr.anchorMin = new Vector2(0f, 1f);
            rr.anchorMax = new Vector2(1f, 1f);
            rr.pivot = new Vector2(0.5f, 1f);
            rr.anchoredPosition = new Vector2(0f, -80f - i * 64f);
            rr.sizeDelta = new Vector2(-40f, 56f);
            int rowIndex = i;
            row.onClick.AddListener(delegate { RowClicked(rowIndex); });
            _rowButtons.Add(row);
            TMP_Text lbl = row.transform.Find("Label").GetComponent<TMP_Text>();
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.margin = new Vector4(16f, 0f, 8f, 0f);
            _rowLabels.Add(lbl);
        }

        Button prev = CloneButton(bp, card.transform, "Prev", "<", fs);
        PlaceBottom(prev, new Vector2(24f, 16f), new Vector2(70f, 52f));
        prev.onClick.AddListener(delegate { ChangePage(-1); });
        Button next = CloneButton(bp, card.transform, "Next", ">", fs);
        PlaceBottom(next, new Vector2(666f, 16f), new Vector2(70f, 52f));
        next.onClick.AddListener(delegate { ChangePage(1); });
        _pageLabel = CloneText(bp, card.transform, "Page", "", fs - 4f);
        RectTransform plr = _pageLabel.rectTransform;
        plr.anchorMin = Vector2.zero;
        plr.anchorMax = Vector2.zero;
        plr.pivot = Vector2.zero;
        plr.anchoredPosition = new Vector2(110f, 16f);
        plr.sizeDelta = new Vector2(540f, 52f);
        _pageLabel.alignment = TextAlignmentOptions.Center;

        _panel.SetActive(false);
        PeakLabPlugin.Log.LogInfo("[Historico] painel construido");
    }

    private static void PlaceBottom(Button b, Vector2 pos, Vector2 size)
    {
        RectTransform r = (RectTransform)b.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.zero;
        r.pivot = Vector2.zero;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    private static void ChangePage(int delta)
    {
        int pages = PageCount();
        _page = Mathf.Clamp(_page + delta, 0, pages - 1);
        RefreshRows();
    }

    private static int PageCount()
    {
        int n = _file.runs.Count;
        int pages = (n + RowsPerPage - 1) / RowsPerPage;
        return pages < 1 ? 1 : pages;
    }

    private static void RefreshRows()
    {
        int total = _file.runs.Count;
        for (int i = 0; i < RowsPerPage; i++)
        {
            int idx = total - 1 - (_page * RowsPerPage + i); // mais recente primeiro
            if (idx < 0)
            {
                _rowButtons[i].gameObject.SetActive(false);
                continue;
            }
            _rowButtons[i].gameObject.SetActive(true);
            _rowLabels[i].text = Format(_file.runs[idx]);
        }
        _pageLabel.text = (total == 0) ? "nenhuma escalada ainda" :
            "pagina " + (_page + 1) + "/" + PageCount() + "  (" + total + " escaladas)";
    }

    private static string Format(PeakRunRecord r)
    {
        string quando = r.data.Length >= 16 ?
            r.data.Substring(8, 2) + "/" + r.data.Substring(5, 2) + " " + r.data.Substring(11, 5) : r.data;
        string oQue = (r.seed >= 0) ? "#" + r.seed : "daily";
        string res;
        if (r.resultado == "Terminou") res = "<color=#2e7d32>TERMINOU</color>";
        else if (r.resultado == "Terminou (amigos)") res = "<color=#2e7d32>AMIGOS VENCERAM</color>";
        else if (r.resultado == "Falhou") res = "<color=#c62828>FALHOU</color>";
        else if (r.resultado == "Abandonou") res = "<color=#8d6e63>ABANDONOU</color>";
        else res = "<color=#1565c0>JOGANDO</color>";
        string tempo = string.IsNullOrEmpty(r.tempo) ? "" : " " + r.tempo;
        string pool = string.IsNullOrEmpty(r.pool) ? "" : (r.pool == "Todas" ? " [T]" : " [P]");
        return quando + "  " + oQue + pool + "  " + r.cena.Replace("Level_", "L") +
               " A" + r.ascent + "  " + res + tempo;
    }

    private static void RowClicked(int rowIndex)
    {
        int idx = _file.runs.Count - 1 - (_page * RowsPerPage + rowIndex);
        if (idx < 0 || idx >= _file.runs.Count) return;
        PeakRunRecord r = _file.runs[idx];
        if (r.seed >= 0)
        {
            PeakLabPlugin.SetSeedText(r.seed);
            PeakLabPlugin.Log.LogInfo("[Historico] seed " + r.seed + " copiada para o campo");
        }
    }

    // clones no estilo do ticket (mesmos helpers do PeakLabPlugin, versao local)
    private static TMP_Text CloneText(BoardingPass bp, Transform parent, string name, string text, float size)
    {
        GameObject go = UnityEngine.Object.Instantiate(bp.ascentTitle.gameObject, parent);
        go.name = "PeakLab_H_" + name;
        TMP_Text t = go.GetComponent<TMP_Text>();
        t.text = text;
        t.enableAutoSizing = false;
        t.fontSize = size;
        return t;
    }

    private static Button CloneButton(BoardingPass bp, Transform parent, string name, string label, float size)
    {
        Button b = UnityEngine.Object.Instantiate(bp.incrementAscentButton, parent);
        b.name = "PeakLab_H_" + name;
        b.onClick = new Button.ButtonClickedEvent();
        Transform icon = b.transform.Find("Image");
        if (icon != null) icon.gameObject.SetActive(false);
        GameObject tGO = UnityEngine.Object.Instantiate(bp.ascentTitle.gameObject, b.transform);
        tGO.name = "Label";
        TMP_Text t = tGO.GetComponent<TMP_Text>();
        t.text = label;
        t.color = Color.white;
        t.enableAutoSizing = false;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.richText = true;
        RectTransform tr = (RectTransform)tGO.transform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        return b;
    }
}

// PeakLabSkins - fits customizados no catalogo oficial de customizacao do PEAK.
// O Fit_Soviet e um clone do Fit_Scoutmaster_Shorts com textura recolorida
// (gerada por tools\MakeSovietTex.cs e embutida na DLL como resource) e botas
// pretas do Sailor. Por entrar no catalogo, aparece no passaporte como um fit
// normal do jogo (preview no boneco, persistencia, tudo pela pipeline vanilla).
// ATENCAO: o fit e salvo pelo INDICE (20). Sem o mod instalado, o save aponta
// para um indice fora do catalogo - troque de roupa antes de remover o mod.
using System;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

public static class PeakLabSkins
{
    // indice do capacete clonado em CustomizationRefs.playerHats (vanilla tem 29: 0..28)
    private const int HelmetIndex = 29;

    private static Texture2D _atlas;
    private static Texture2D _icon;
    private static Material _mat;
    private static CustomizationOption _fit;
    private static bool _broken;
    private static Texture2D _helmTex;
    private static Material _helmMat;
    private static bool _helmBroken;

    // idempotente; chamado a cada cena porque o singleton Customization pode
    // ser recriado e ai o array fits volta ao original do asset
    public static void EnsureRegistered(ManualLogSource log)
    {
        if (_broken) return;
        UnityEngine.Object[] cats = Resources.FindObjectsOfTypeAll(typeof(Customization));
        for (int ci = 0; ci < cats.Length; ci++)
        {
            Customization cat = cats[ci] as Customization;
            if (cat == null || cat.fits == null || cat.fits.Length == 0) continue;
            bool has = false;
            CustomizationOption baseFit = null;
            CustomizationOption sailor = null;
            for (int i = 0; i < cat.fits.Length; i++)
            {
                CustomizationOption o = cat.fits[i];
                if (o == null) continue;
                if (o.name == "Fit_Soviet") has = true;
                else if (o.name == "Fit_Scoutmaster_Shorts") baseFit = o;
                else if (o.name == "Fit_Sailor_Shorts") sailor = o;
            }
            if (has) continue;
            if (baseFit == null)
            {
                log.LogWarning("[Skins] Fit_Scoutmaster_Shorts nao encontrado no catalogo");
                continue;
            }
            if (_fit == null && !Build(baseFit, sailor, log)) { _broken = true; return; }
            CustomizationOption[] plus = new CustomizationOption[cat.fits.Length + 1];
            Array.Copy(cat.fits, plus, cat.fits.Length);
            plus[cat.fits.Length] = _fit;
            cat.fits = plus;
            log.LogInfo("[Skins] Fit_Soviet registrado no indice " + (plus.Length - 1) +
                        " (catalogo " + cat.GetInstanceID() + ")");
        }
        // garante o capacete em todo CustomizationRefs ja carregado (instancias e prefabs)
        UnityEngine.Object[] refsAll = Resources.FindObjectsOfTypeAll(typeof(CustomizationRefs));
        for (int i = 0; i < refsAll.Length; i++) ExtendRefs(refsAll[i] as CustomizationRefs);
    }

    // adiciona o capacete sovietico (clone do MedicHelmet, playerHats[7]) como
    // playerHats[29] deste personagem; o Fit_Soviet o forca via overrideHat.
    // Idempotente e tolerante: em layout inesperado simplesmente nao mexe.
    public static void ExtendRefs(CustomizationRefs refs)
    {
        try
        {
            if (refs == null || _helmBroken) return;
            Renderer[] hats = refs.playerHats;
            if (hats == null) return;
            for (int i = 0; i < hats.Length; i++)
            {
                if (hats[i] != null && hats[i].name == "SovietHelmet") return;
            }
            if (hats.Length != HelmetIndex || hats[7] == null) return;
            if (_helmTex == null)
            {
                _helmTex = LoadEmbeddedTex("SovietHelmet_tex.png");
                if (_helmTex == null) { _helmBroken = true; return; }
            }
            if (_helmMat == null)
            {
                _helmMat = new Material(hats[7].sharedMaterial);
                _helmMat.name = "M_SovietHelmet";
                _helmMat.mainTexture = _helmTex;
                _helmMat.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            GameObject src = hats[7].gameObject;
            GameObject cl = UnityEngine.Object.Instantiate(src, src.transform.parent);
            cl.name = "SovietHelmet";
            cl.SetActive(false);
            Renderer rend = cl.GetComponent<Renderer>();
            if (rend == null) rend = cl.GetComponentInChildren<Renderer>(true);
            if (rend == null) { UnityEngine.Object.Destroy(cl); return; }
            rend.sharedMaterial = _helmMat;
            Renderer[] plus = new Renderer[hats.Length + 1];
            Array.Copy(hats, plus, hats.Length);
            plus[hats.Length] = rend;
            refs.playerHats = plus;
        }
        catch (Exception) { }
    }

    private static bool Build(CustomizationOption baseFit, CustomizationOption sailor, ManualLogSource log)
    {
        _atlas = LoadEmbeddedTex("FitSoviet_atlas.png");
        _icon = LoadEmbeddedTex("FitSoviet_icon.png");
        if (_atlas == null || _icon == null)
        {
            log.LogError("[Skins] texturas embutidas nao carregaram - fit desativado");
            return false;
        }
        CustomizationOption o = UnityEngine.Object.Instantiate(baseFit);
        o.name = "Fit_Soviet";
        o.hideFlags = HideFlags.DontUnloadUnusedAsset;
        o.requiredAchievement = ACHIEVEMENTTYPE.NONE;
        o.requiresAscent = false;
        o.testLocked = false;
        o.texture = _icon;
        _mat = new Material(baseFit.fitMaterial);
        _mat.name = "M_Scout_Soviet";
        _mat.mainTexture = _atlas;
        _mat.hideFlags = HideFlags.DontUnloadUnusedAsset;
        o.fitMaterial = _mat;
        if (sailor != null && sailor.fitMaterialShoes != null)
            o.fitMaterialShoes = sailor.fitMaterialShoes; // botas pretas
        // capacete forcado pelo fit (mesmo mecanismo do capuz dos fits Bundled);
        // so liga o override se a textura do capacete realmente carregou
        if (_helmTex == null && !_helmBroken)
        {
            _helmTex = LoadEmbeddedTex("SovietHelmet_tex.png");
            if (_helmTex == null) _helmBroken = true;
        }
        if (!_helmBroken)
        {
            o.overrideHat = true;
            o.overrideHatIndex = HelmetIndex;
        }
        _fit = o;
        return true;
    }

    private static Texture2D LoadEmbeddedTex(string resName)
    {
        try
        {
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName);
            if (s == null) return null;
            byte[] buf = new byte[(int)s.Length];
            int off = 0;
            while (off < buf.Length)
            {
                int n = s.Read(buf, off, buf.Length - off);
                if (n <= 0) break;
                off += n;
            }
            s.Close();
            Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!ImageConversion.LoadImage(t, buf)) return null;
            t.name = resName;
            t.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return t;
        }
        catch (Exception) { return null; }
    }
}

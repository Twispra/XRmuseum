#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

// Explicit trilingual font migration. Called only by the authorized setup,
// in Edit Mode: -executeMethod FontCompletion.RunBatch (omit -quit).
// Rebuilds only the existing SDF asset IN PLACE; never calls LocalizationSetup.
public static class TrilingualFontBaker
{
    const string Root = "Assets/Museum/Features/Localization/Fonts/";
    const string FontPath = Root + "NotoSansSC-MuseumSubset SDF.asset";
    const string SourcePath = Root + "NotoSansSC-MuseumSubset.ttf";
    const string ExpectedGuid = "73d312c39c8c91948afde3c8604a6c94";
    static readonly string AdditionalCharacters = File.ReadAllText(Path.Combine(Application.dataPath, "Museum/Features/Localization/Fonts/characters-required.txt")).TrimEnd();
    const int Size = 4096, Sampling = 110, Padding = 6;
    const GlyphRenderMode RenderMode = GlyphRenderMode.SDFAA;
    static readonly BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Instance;

    [Serializable] public sealed class GlyphEvidence
    {
        public string character, unicode;
        public uint glyphIndex;
        public int x, y, width, height, pixelsAbove127;
        public byte minimum, maximum;
    }
    [Serializable] public sealed class Report
    {
        public string startedUtc, finishedUtc, status, failure, unity, project, output;
        public string method = "TMP 3.0.7 Font Asset Creator low-level LoadFontFace / TryPackGlyphsInAtlas / RenderGlyphsToTexture(byte[]) path. Original asset, atlas and material identities retained; no scene, prefab, table, source font, meta or character-list edits.";
        public string requested = AdditionalCharacters, sourceSha256, assetBeforeSha256, assetAfterSha256, atlasBufferSha256, savedAtlasSha256, guidBefore, guidAfter;
        public string sourceFamily, sourceStyle, requiredCharacters;
        public long fontIdBefore, fontIdAfter, atlasIdBefore, atlasIdAfter, materialIdBefore, materialIdAfter;
        public int previousCharacters, resultingCharacters, glyphCount, sampling = Sampling, padding = Padding, width = Size, height = Size;
        public bool persistedAtlasMatchesBuffer, allRequestedAvailableWithoutFallback, staticPopulation, identitiesPreserved;
        public List<GlyphEvidence> addedGlyphs = new List<GlyphEvidence>();
    }

    public static void RunBatch()
    {
        int exitCode = 1;
        bool createdFolder = false;
        var report = new Report { startedUtc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion, project = Application.dataPath, status = "failed", failure = "" };
        string folder = Argument("-fontCompletionOutput") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../tmp/pr-completion-20261006/font-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
        report.output = Path.GetFullPath(folder);
        try
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Must run in Edit Mode.");
            Require(!Directory.Exists(folder), "Refusing to overwrite an existing evidence directory: " + folder);
            Directory.CreateDirectory(folder);
            createdFolder = true;
            Bake(report, folder);
            report.status = "passed";
            exitCode = 0;
        }
        catch (Exception ex) { report.failure = ex.ToString(); Debug.LogError("FONT_COMPLETION_FAILED " + ex); }
        finally
        {
            report.finishedUtc = DateTime.UtcNow.ToString("o");
            // Existing evidence directories are never overwritten, even on a preflight failure.
            if (createdFolder && !File.Exists(Path.Combine(folder, "report.json")))
                File.WriteAllText(Path.Combine(folder, "report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("FONT_COMPLETION_" + report.status.ToUpperInvariant() + " " + folder);
            if (exitCode != 0) throw new InvalidOperationException(report.failure);
        }
    }

    static void Bake(Report report, string folder)
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assetDisk = Path.Combine(project, FontPath);
        string sourceDisk = Path.Combine(project, SourcePath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Require(font != null, "Existing SDF asset was not loaded.");
        report.guidBefore = AssetDatabase.AssetPathToGUID(FontPath);
        Require(report.guidBefore == ExpectedGuid, "Unexpected font GUID; refusing to replace a different asset.");
        Require(font.atlasPadding == Padding && font.atlasRenderMode == RenderMode, "Unexpected existing atlas settings.");
        Require(font.atlasPopulationMode == AtlasPopulationMode.Static, "Expected the existing static font.");
        Require(font.atlasTextures != null && font.atlasTextures.Length == 1 && font.atlasTextures[0] != null, "Expected one existing atlas.");
        var atlas = font.atlasTextures[0];
        var material = font.material;
        Require(atlas.width == atlas.height && atlas.format == TextureFormat.Alpha8 && atlas.mipmapCount == 1, "Expected a single square Alpha8 atlas without mipmaps.");
        Require(material != null && AssetDatabase.GetAssetPath(atlas) == FontPath && AssetDatabase.GetAssetPath(material) == FontPath, "Atlas and material must be subassets of this font.");
        Require(font.creationSettings.sourceFontFileGUID == AssetDatabase.AssetPathToGUID(SourcePath), "Saved font was not made from the committed source subset.");
        report.fontIdBefore = LocalId(font); report.atlasIdBefore = LocalId(atlas); report.materialIdBefore = LocalId(material);
        report.assetBeforeSha256 = Hash(File.ReadAllBytes(assetDisk));
        report.sourceSha256 = Hash(File.ReadAllBytes(sourceDisk));
        File.Copy(assetDisk, Path.Combine(folder, "before.asset"));
        File.Copy(assetDisk + ".meta", Path.Combine(folder, "before.asset.meta"));

        var previous = font.characterTable.ToDictionary(c => c.unicode, c => c.glyphIndex);
        report.previousCharacters = previous.Count;
        var required = new SortedSet<uint>(previous.Keys);
        foreach (char c in AdditionalCharacters) required.Add(c);
        report.requiredCharacters = string.Concat(required.Select(c => char.ConvertFromUtf32((int)c)));
        File.WriteAllText(Path.Combine(folder, "characters-baked.txt"), report.requiredCharacters);

        Require(FontEngine.InitializeFontEngine() == FontEngineError.Success, "FontEngine initialization failed.");
        Require(FontEngine.LoadFontFace(sourceDisk) == FontEngineError.Success, "Could not load committed source font by file path.");
        Require(FontEngine.SetFaceSize(Sampling) == FontEngineError.Success, "Could not select 110-point face.");
        FaceInfo face = FontEngine.GetFaceInfo();
        Require(face.pointSize == Sampling, "Source face differs from the saved font.");
        report.sourceFamily = face.familyName; report.sourceStyle = face.styleName;
        var byUnicode = new Dictionary<uint, uint>();
        var glyphsByIndex = new Dictionary<uint, Glyph>();
        var toPack = new List<Glyph>();
        var packed = new List<Glyph>();
        // SDFAA = 4165 has no RASTER_MODE_HINTED (8) bit; this is the Creator's flag result.
        // GlyphRasterModes is internal in this Unity build, so do not reference its type.
        GlyphLoadFlags flags = GlyphLoadFlags.LOAD_RENDER | GlyphLoadFlags.LOAD_NO_HINTING;
        foreach (uint unicode in required)
        {
            uint glyphIndex;
            Require(FontEngine.TryGetGlyphIndex(unicode, out glyphIndex) && glyphIndex != 0, "Source missing U+" + unicode.ToString("X4"));
            // The extended source subset has new glyph indices; all saved tables are rebuilt from this same face.
            byUnicode.Add(unicode, glyphIndex);
            if (glyphsByIndex.ContainsKey(glyphIndex)) continue;
            Glyph glyph;
            Require(FontEngine.TryGetGlyphWithIndexValue(glyphIndex, flags, out glyph), "Could not load glyph " + glyphIndex);
            glyph.atlasIndex = 0;
            glyphsByIndex.Add(glyphIndex, glyph);
            if (glyph.glyphRect.width > 0 && glyph.glyphRect.height > 0) toPack.Add(glyph); else packed.Add(glyph);
        }
        var free = new List<GlyphRect> { new GlyphRect(0, 0, Size - 1, Size - 1) };
        var used = new List<GlyphRect>();
        var packMethod = typeof(FontEngine).GetMethod("TryPackGlyphsInAtlas", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
            new[] { typeof(List<Glyph>), typeof(List<Glyph>), typeof(int), typeof(GlyphPackingMode), typeof(GlyphRenderMode), typeof(int), typeof(int), typeof(List<GlyphRect>), typeof(List<GlyphRect>) }, null);
        Require(packMethod != null, "Expected Unity FontEngine packing API is unavailable.");
        packMethod.Invoke(null, new object[] { toPack, packed, Padding, (GlyphPackingMode)0, RenderMode, Size, Size, free, used });
        Require(toPack.Count == 0 && packed.Count == glyphsByIndex.Count, "Not every glyph fitted at 110/6/4096; no product asset has been changed.");
        var render = packed.Where(g => g.glyphRect.width > 0 && g.glyphRect.height > 0).ToList();
        var buffer = new byte[Size * Size];
        var renderMethod = typeof(FontEngine).GetMethod("RenderGlyphsToTexture", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
            new[] { typeof(List<Glyph>), typeof(int), typeof(GlyphRenderMode), typeof(byte[]), typeof(int), typeof(int) }, null);
        Require(renderMethod != null, "Expected Unity FontEngine byte-buffer renderer is unavailable.");
        Require((FontEngineError)renderMethod.Invoke(null, new object[] { render, Padding, RenderMode, buffer, Size, Size }) == FontEngineError.Success, "FontEngine glyph rendering failed.");
        report.atlasBufferSha256 = Hash(buffer);
        // Pixel checks precede every persistent mutation. A populated character table alone is insufficient.
        foreach (var pair in byUnicode)
        {
            var glyph = glyphsByIndex[pair.Value];
            if (glyph.glyphRect.width == 0 || glyph.glyphRect.height == 0)
            {
                Require(char.IsWhiteSpace(char.ConvertFromUtf32((int)pair.Key), 0), "Non-whitespace has an empty glyph rectangle.");
                continue;
            }
            GlyphEvidence evidence = InspectGlyph(pair.Key, glyph, buffer);
            Require(evidence.maximum > 127 && evidence.maximum > evidence.minimum && evidence.pixelsAbove127 > 0, "Empty/constant glyph pixels for " + evidence.unicode);
            if (AdditionalCharacters.Contains(evidence.character)) report.addedGlyphs.Add(evidence);
        }
        Require(report.addedGlyphs.Count > 0, "Required glyphs were not rendered.");
        WritePreview(Path.Combine(folder, "atlas-preview.png"), buffer);

        // Keep the original atlas Texture2D, material, font object and .meta: no delete/recreate.
        // This is the CPU-buffer equivalent of the Creator's Graphics.CopyTexture into its existing atlas.
        SetReadable(atlas, true);
        atlas.Reinitialize(Size, Size, TextureFormat.Alpha8, false);
        atlas.LoadRawTextureData(buffer);
        atlas.Apply(false, false);
        Require(Hash(atlas.GetRawTextureData<byte>().ToArray()) == report.atlasBufferSha256, "CPU atlas did not receive baked bytes.");
        font.faceInfo = face;
        SetField(font, "m_AtlasWidth", Size); SetField(font, "m_AtlasHeight", Size);
        font.glyphTable.Clear(); font.glyphTable.AddRange(packed.OrderBy(g => g.index));
        font.characterTable.Clear();
        foreach (var pair in byUnicode.OrderBy(p => p.Key)) font.characterTable.Add(new TMP_Character(pair.Key, glyphsByIndex[pair.Value]));
        SetField(font, "m_FreeGlyphRects", free); SetField(font, "m_UsedGlyphRects", used); SetField(font, "m_AtlasTextureIndex", 0);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        font.isMultiAtlasTexturesEnabled = false;
        var creation = font.creationSettings;
        creation.pointSizeSamplingMode = 1; creation.pointSize = Sampling; creation.padding = Padding;
        creation.packingMode = 0; creation.atlasWidth = Size; creation.atlasHeight = Size; creation.renderMode = (int)RenderMode;
        creation.characterSetSelectionMode = 7; creation.characterSequence = report.requiredCharacters;
        creation.referencedTextAssetGUID = "";
        font.creationSettings = creation;
        material.SetTexture("_MainTex", atlas); material.SetFloat("_TextureWidth", Size); material.SetFloat("_TextureHeight", Size); material.SetFloat("_GradientScale", Padding + 1);
        SetReadable(atlas, false);
        font.ReadFontAssetDefinition();
        EditorUtility.SetDirty(atlas); EditorUtility.SetDirty(material); EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssetIfDirty(font);
        AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        // Independently verify bytes persisted in the existing text-serialized asset, without relying on cached texture data.
        var hex = Regex.Match(File.ReadAllText(assetDisk), @"(?m)^\s*_typelessdata: ([0-9a-fA-F]+)\s*$");
        Require(hex.Success && hex.Groups[1].Length == buffer.Length * 2, "Saved atlas byte payload has the wrong size or was not text serialized.");
        var savedBytes = new byte[buffer.Length];
        string encoded = hex.Groups[1].Value;
        for (int i = 0; i < savedBytes.Length; i++) savedBytes[i] = (byte)((Hex(encoded[i * 2]) << 4) | Hex(encoded[i * 2 + 1]));
        report.savedAtlasSha256 = Hash(savedBytes);
        report.persistedAtlasMatchesBuffer = report.savedAtlasSha256 == report.atlasBufferSha256;
        Require(report.persistedAtlasMatchesBuffer, "Saved atlas differs from validated rendered bytes.");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        font.ReadFontAssetDefinition();
        report.guidAfter = AssetDatabase.AssetPathToGUID(FontPath);
        report.fontIdAfter = LocalId(font); report.atlasIdAfter = LocalId(font.atlasTextures[0]); report.materialIdAfter = LocalId(font.material);
        report.identitiesPreserved = report.guidAfter == report.guidBefore && report.fontIdAfter == report.fontIdBefore && report.atlasIdAfter == report.atlasIdBefore && report.materialIdAfter == report.materialIdBefore;
        Require(report.identitiesPreserved, "Font/atlas/material identity changed.");
        report.staticPopulation = font.atlasPopulationMode == AtlasPopulationMode.Static && font.sourceFontFile == null;
        Require(report.staticPopulation && font.atlasTextures.Length == 1 && font.atlasTextures[0].width == Size && font.atlasTextures[0].height == Size, "Persisted static atlas settings invalid.");
        // The int overload is a direct dictionary lookup (no fallback and no TryAdd).
        report.allRequestedAvailableWithoutFallback = required.All(c => font.HasCharacter((int)c));
        Require(report.allRequestedAvailableWithoutFallback, "Saved font failed direct character lookup.");
        Require(font.material.GetTexture("_MainTex") == font.atlasTextures[0], "Material lost its atlas reference.");
        Require(File.ReadAllBytes(assetDisk + ".meta").SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "before.asset.meta"))), "Font meta changed.");
        report.resultingCharacters = font.characterTable.Count; report.glyphCount = font.glyphTable.Count;
        report.assetAfterSha256 = Hash(File.ReadAllBytes(assetDisk));
    }

    static GlyphEvidence InspectGlyph(uint unicode, Glyph glyph, byte[] pixels)
    {
        var r = glyph.glyphRect;
        Require(r.x >= 0 && r.y >= 0 && r.x + r.width <= Size && r.y + r.height <= Size, "Glyph rectangle outside atlas.");
        var result = new GlyphEvidence { character = char.ConvertFromUtf32((int)unicode), unicode = "U+" + unicode.ToString("X4"), glyphIndex = glyph.index, x = r.x, y = r.y, width = r.width, height = r.height, minimum = 255 };
        for (int y = r.y; y < r.y + r.height; y++) for (int x = r.x; x < r.x + r.width; x++)
        {
            byte b = pixels[y * Size + x];
            if (b < result.minimum) result.minimum = b;
            if (b > result.maximum) result.maximum = b;
            if (b > 127) result.pixelsAbove127++;
        }
        return result;
    }
    static void WritePreview(string path, byte[] pixels)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.Alpha8, false, true);
        try { texture.LoadRawTextureData(pixels); texture.Apply(false, false); File.WriteAllBytes(path, texture.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
    static void SetField(TMP_FontAsset font, string name, object value)
    {
        var field = typeof(TMP_FontAsset).GetField(name, Fields);
        Require(field != null, "TMP 3.0.7 field missing: " + name);
        field.SetValue(font, value);
    }
    static void SetReadable(Texture2D texture, bool readable)
    {
        // This is the same editor helper used by Font Asset Creator, internal outside TMP's friend assembly.
        var type = Type.GetType("UnityEditor.TextCore.LowLevel.FontEngineEditorUtilities, UnityEditor.TextCoreFontEngineModule", false);
        Require(type != null, "Unity font editor utility type is unavailable.");
        var method = type.GetMethod("SetAtlasTextureIsReadable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Texture2D), typeof(bool) }, null);
        Require(method != null, "Unity font editor readability helper is unavailable.");
        method.Invoke(null, new object[] { texture, readable });
    }
    static long LocalId(UnityEngine.Object item) { string guid; long id; Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out guid, out id), "Missing persistent local ID."); return id; }
    static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    static int Hex(char c) { return c <= '9' ? c - '0' : (c | 32) - 'a' + 10; }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static string Argument(string key) { var args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == key) return args[i + 1]; return null; }
}
#endif

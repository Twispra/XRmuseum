#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Localization.EditorTools
{
    /// <summary>Scoped migration of saved localization assets, never the helmet/gallery builders.</summary>
    public static class TrilingualMuseumSetup
    {
        const string Root="Assets/Museum/Features/Localization";
        const string ScenePath="Assets/Museum/Scenes/MuseumStart.unity";
        [Serializable] public class Copy { public CopyEntry[] entries; }
        [Serializable] public class CopyEntry { public string key,chinese,traditionalChinese,english,note; }
        public static Copy ReadCopy() => JsonUtility.FromJson<Copy>(File.ReadAllText(Root+"/Tables/MuseumTranslations.json"));
        public static void ApplyTable(LocalizationTable table, TMP_FontAsset font)
        {
            var copy=ReadCopy();
            if(copy.entries.Select(r=>r.key).Distinct().Count()!=copy.entries.Length) throw new InvalidOperationException("Duplicate translation key.");
            foreach(var row in copy.entries)
                if(string.IsNullOrEmpty(row.chinese)||string.IsNullOrEmpty(row.traditionalChinese)||string.IsNullOrEmpty(row.english))
                    throw new InvalidOperationException("Incomplete translation: "+row.key);
            var so=new SerializedObject(table);
            so.FindProperty("fontAsset").objectReferenceValue=font;
            var entries=so.FindProperty("entries");entries.arraySize=copy.entries.Length;
            for(int i=0;i<copy.entries.Length;i++)
            {
                var item=entries.GetArrayElementAtIndex(i);var row=copy.entries[i];
                item.FindPropertyRelative("key").stringValue=row.key;
                item.FindPropertyRelative("chinese").stringValue=row.chinese;
                item.FindPropertyRelative("traditionalChinese").stringValue=row.traditionalChinese;
                item.FindPropertyRelative("english").stringValue=row.english;
                item.FindPropertyRelative("note").stringValue=row.note;
            }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(table);
        }
        public static void UpgradeSelector(GameObject root,LocalizationTable table,LanguageChannel channel)
        {
            var buttons=root.GetComponentsInChildren<LanguageButton>(true);
            var chinese=buttons.Single(b=>b.Action==LanguageButton.Choice.Chinese);
            var english=buttons.Single(b=>b.Action==LanguageButton.Choice.English);
            var traditional=buttons.SingleOrDefault(b=>b.Action==LanguageButton.Choice.TraditionalChinese);
            if(!traditional)
            {
                var go=UnityEngine.Object.Instantiate(chinese.gameObject,root.transform);
                go.name="Language Button - Traditional Chinese";
                traditional=go.GetComponent<LanguageButton>();
            }
            var targets=new[]{chinese,traditional,english};
            var actions=new[]{LanguageButton.Choice.Chinese,LanguageButton.Choice.TraditionalChinese,LanguageButton.Choice.English};
            var pressed=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Language-ButtonPressed.mat");
            for(int i=0;i<targets.Length;i++)
            {
                var b=targets[i]; var name=b.name+" Label";
                var label=root.GetComponentsInChildren<TMP_Text>(true).SingleOrDefault(t=>t.name==name);
                if(!label) {var go=new GameObject(name);go.transform.SetParent(root.transform,false);label=go.AddComponent<TextMeshPro>();}
                b.transform.localPosition=new Vector3((i-1)*.66f,.92f,.545f);
                b.transform.localScale=new Vector3(.59f,.20f,.04f);
                label.transform.localPosition=b.transform.localPosition+Vector3.back*.035f;
                label.font=table.FontAsset;label.fontSize=.85f;label.alignment=TextAlignmentOptions.Center;
                label.color=Color.white;label.enableWordWrapping=false;label.rectTransform.sizeDelta=new Vector2(.62f,.20f);
                b.Configure(channel,actions[i],b.GetComponent<Renderer>(),pressed,label);
            }
            var preview=root.GetComponent<LanguagePlayModePreview>();
            if(preview){var so=new SerializedObject(preview);so.FindProperty("enableNumberKeys").boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();}
        }
        public static void FitSavedLabelsBatch()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
                BackupSavedInputs();
                var prefabPath = Root + "/Prefabs/LanguageShowcase.prefab";
                var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                try { FitLabels(prefab.GetComponentsInChildren<TMP_Text>(true)); PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
                var scene = EditorSceneManager.OpenScene(ScenePath);
                FitLabels(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)));
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }
        internal static void FitLabels(System.Collections.Generic.IEnumerable<TMP_Text> labels)
        {
            foreach (var label in labels)
            {
                if (label.name == "Title" && label.transform.parent && label.transform.parent.name.Contains("Language"))
                {
                    label.rectTransform.sizeDelta = new Vector2(2.05f, .4f);
                    label.enableWordWrapping = true; label.enableAutoSizing = true;
                    label.fontSizeMax = 1.4f; label.fontSizeMin = 1f;
                    EditorUtility.SetDirty(label); continue;
                }
                if (label.enableAutoSizing) continue;
                label.enableAutoSizing = true;
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = label.fontSize * .7f;
                EditorUtility.SetDirty(label);
            }
        }
        static void BackupSavedInputs()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or close the dirty scene before migration.");
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../tmp/trilingual-backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(folder);
            foreach (var path in new[] { ScenePath, Root + "/Prefabs/LanguageShowcase.prefab", Root + "/Tables/LocalizationTable.asset",
                Root + "/Fonts/NotoSansSC-MuseumSubset SDF.asset", Root + "/Fonts/NotoSansSC-MuseumSubset.ttf" })
            {
                File.Copy(path, Path.Combine(folder, Path.GetFileName(path)), false);
                File.Copy(path + ".meta", Path.Combine(folder, Path.GetFileName(path) + ".meta"), false);
            }
        }
        public static void RunBatch()
        {
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
                BackupSavedInputs();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                TrilingualFontBaker.RunBatch();
                var table=AssetDatabase.LoadAssetAtPath<LocalizationTable>(Root+"/Tables/LocalizationTable.asset");
                var channel=AssetDatabase.LoadAssetAtPath<LanguageChannel>(Root+"/Tables/LanguageChannel.asset");
                ApplyTable(table,table.FontAsset);AssetDatabase.SaveAssets();
                var prefabPath=Root+"/Prefabs/LanguageShowcase.prefab";
                var prefab=PrefabUtility.LoadPrefabContents(prefabPath);
                try {UpgradeSelector(prefab,table,channel);PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);}
                finally {PrefabUtility.UnloadPrefabContents(prefab);}
                var scene=EditorSceneManager.OpenScene(ScenePath);
                var roots=scene.GetRootGameObjects();
                var selector=roots.Single(g=>g.name=="Language Selection");
                UpgradeSelector(selector,table,channel);
                var service=selector.GetComponent<MuseumLocalizationRoot>();
                if(!service) service=selector.AddComponent<MuseumLocalizationRoot>();service.Configure(table,channel);
                int bindings=0;
                foreach(var label in roots.SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)))
                {
                    if(label.GetComponent<LocalizedText>() || selector.GetComponentsInChildren<LanguageButton>(true)
                        .Any(b=>label.name==b.name+" Label")) continue;
                    var source=label.GetComponent<LocalizedSourceText>();
                    if(!source) source=label.gameObject.AddComponent<LocalizedSourceText>();
                    var english=string.IsNullOrEmpty(source.SourceEnglish)?label.text:source.SourceEnglish;
                    if(!string.IsNullOrEmpty(english)&&!english.All(char.IsDigit)&&table.TranslateEnglish(english,MuseumLanguage.Chinese)==english)
                        throw new InvalidOperationException("Untranslated saved label: "+label.name+" / "+english);
                    source.Configure(table,channel,english);bindings++;
                }
                FitLabels(roots.SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)));
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("TRILINGUAL_SETUP_OK bindings="+bindings+" entries="+table.Entries.Count);
                if(Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
#endif

using System.Collections.Generic;
using Museum.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Museum.Diegetic
{
    public sealed partial class HelmetDiegeticStation
    {
        const float RightButtonColumn = .285f;
        static readonly int FrontOnly = Shader.PropertyToID("_FrontOnly");
        static readonly int FrontLineNormal = Shader.PropertyToID("_FrontLineNormal");
        static readonly int CullModeProperty = Shader.PropertyToID("_CullMode");
        static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        readonly Dictionary<Material, Material> frontTextMaterials = new Dictionary<Material, Material>();
        readonly Dictionary<TMP_Text, Material> knowledgeTextMaterials = new Dictionary<TMP_Text, Material>();
        Renderer knowledgeBacking;
        Material knowledgeBackingMaterial;
        MaterialPropertyBlock knowledgeBackingProperties;
        Renderer savedCaptionRenderer;
        bool savedCaptionWasVisible;

        void ShowSavedCaption(bool visible)
        {
            if (savedCaptionRenderer) savedCaptionRenderer.enabled = visible && savedCaptionWasVisible;
        }

        void BuildFasciaLabels(Renderer plinth)
        {
            var fascia = new GameObject("Front fascia labels").transform;
            fascia.SetParent(pedestalUI, false);
            var content = FindObjectOfType<HelmetEraVisitorContent>();
            var savedCaption = content ? content.Instructions : null;
            if (savedCaption)
            {
                // Use the existing sign's front plane. The top lip projects
                // farther toward the visitor than the plinth body's bounds.
                var anchor = savedCaption.transform;
                fascia.SetPositionAndRotation(anchor.position + anchor.up * .045f - anchor.forward * .014f,
                    anchor.rotation);
                savedCaptionRenderer = savedCaption.GetComponent<Renderer>();
                savedCaptionWasVisible = savedCaptionRenderer && savedCaptionRenderer.enabled;
                ShowSavedCaption(false);
            }
            else
            {
                float y = plinth ? plinth.bounds.max.y - .045f : pedestalUI.position.y - .205f;
                fascia.SetPositionAndRotation(new Vector3(pedestalUI.position.x, y, pedestalUI.position.z),
                    Quaternion.identity);
            }
            eraTitle = Label(fascia, "Era caption", Vector3.zero, new Vector2(.65f, .046f), .29f);
            instruction = Label(fascia, "Station invitation", new Vector3(0, -.066f, 0), new Vector2(.72f, .080f), .25f);
            instruction.enableAutoSizing = true;
            instruction.fontSizeMin = .21f;
            instruction.fontSizeMax = .25f;
            MuseumText.Set(instruction, VisitInvitation);
        }

        void SetFrontText(TMP_Text label)
        {
            if (!label) return;
            var source = label.fontSharedMaterial;
            if (!source || !source.HasProperty(CullModeProperty) || source.GetFloat(CullModeProperty) == (float)CullMode.Back) return;
            if (!frontTextMaterials.TryGetValue(source, out var front))
            {
                // The saved font and atlas stay shared by other exhibits and legacy UI.
                front = new Material(source) { name = source.name + " (front side)" };
                front.SetInt(CullModeProperty, (int)CullMode.Back);
                frontTextMaterials.Add(source, front);
            }
            label.fontSharedMaterial = front;
        }

        // Match the readable local -Z side used by the shader. Do not depend on the visitor's gaze direction.
        internal bool IsViewingControlFront(Transform surface) =>
            !viewer || Vector3.Dot(surface.forward, viewer.position - surface.position) <= 0;

        internal static void SetFrontSurface(Renderer surface)
        {
            if (!surface || !surface.sharedMaterial || !surface.sharedMaterial.HasProperty(FrontOnly)) return;
            var properties = new MaterialPropertyBlock();
            surface.GetPropertyBlock(properties);
            if (surface is LineRenderer line)
            {
                properties.SetFloat(FrontOnly, 2);
                SetFrontLineNormal(line, properties);
            }
            else properties.SetFloat(FrontOnly, 1);
            surface.SetPropertyBlock(properties);
        }

        internal static void SetFrontLineNormal(LineRenderer line, MaterialPropertyBlock properties)
        {
            Vector3 normal = -line.transform.forward;
            properties.SetVector(FrontLineNormal, new Vector4(normal.x, normal.y, normal.z, 0));
        }

        void ApplyKnowledgeCardMaterials()
        {
            foreach (var label in knowledgeCard.GetComponentsInChildren<TMP_Text>(true))
            {
                knowledgeTextMaterials.Add(label, label.fontSharedMaterial);
                SetFrontText(label);
            }

            var backing = knowledgeCard.Find("Panel Backing");
            if (!backing || !uiMaterial.HasProperty(FrontOnly)) return;
            knowledgeBacking = backing.GetComponent<Renderer>();
            if (!knowledgeBacking || !knowledgeBacking.sharedMaterial ||
                !knowledgeBacking.sharedMaterial.HasProperty(BaseColorProperty)) return;

            knowledgeBackingMaterial = knowledgeBacking.sharedMaterial;
            knowledgeBackingProperties = new MaterialPropertyBlock();
            knowledgeBacking.GetPropertyBlock(knowledgeBackingProperties);
            var properties = new MaterialPropertyBlock();
            knowledgeBacking.GetPropertyBlock(properties);
            properties.SetColor(BaseColorProperty, knowledgeBackingMaterial.GetColor(BaseColorProperty));
            properties.SetFloat(FrontOnly, 1);
            knowledgeBacking.sharedMaterial = uiMaterial;
            knowledgeBacking.SetPropertyBlock(properties);
        }

        void RestoreKnowledgeCardMaterials()
        {
            foreach (var item in knowledgeTextMaterials)
                if (item.Key) item.Key.fontSharedMaterial = item.Value;
            knowledgeTextMaterials.Clear();
            if (knowledgeBacking && knowledgeBackingMaterial)
            {
                knowledgeBacking.sharedMaterial = knowledgeBackingMaterial;
                knowledgeBacking.SetPropertyBlock(knowledgeBackingProperties);
            }
        }
    }
}

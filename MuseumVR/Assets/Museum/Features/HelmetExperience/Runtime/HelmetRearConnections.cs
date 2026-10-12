using UnityEngine;

namespace Museum.Experience
{
    /// <summary>Saved, helmet-local fittings. Geometry is an illustrative addition, not measured manufacturing data.</summary>
    [DisallowMultipleComponent]
    public sealed class HelmetRearConnections : MonoBehaviour
    {
        [SerializeField] Renderer airConnector;
        [SerializeField] Transform airOutlet;
        [SerializeField] Renderer[] telephoneSurfaces = new Renderer[0];
        [SerializeField] Transform telephoneOutlet;
        [SerializeField] Transform voiceResponse;
        MaterialPropertyBlock block;
        Museum.Inspection.HelmetInspectionController inspection;
        static readonly int FocusId = Shader.PropertyToID("_Focus");
        public Renderer AirConnector => airConnector;
        public Transform AirOutlet => airOutlet;
        public Transform TelephoneOutlet => telephoneOutlet;
        public Renderer[] TelephoneSurfaces => telephoneSurfaces;
        public bool TelephoneLocationIsIllustrative { get; private set; }
        // Append-on fittings can be clipped without moving the established specimen/tool reference frame.
        public bool IsTelephoneSurface(Renderer surface) => System.Array.IndexOf(telephoneSurfaces,surface)>=0;
        public bool VoiceResponseVisible => voiceResponse && voiceResponse.gameObject.activeInHierarchy;
        public float VoiceFocus { get; private set; }

        public void Configure(Renderer air, Transform airEnd, Renderer[] telephone, Transform telephoneEnd, Transform response,
            bool telephoneLocationIsIllustrative = false)
        {
            // Clear the previous era's surfaces before replacing the renderer list.
            ClearVoice();
            airConnector=air;airOutlet=airEnd;telephoneSurfaces=telephone ?? System.Array.Empty<Renderer>();telephoneOutlet=telephoneEnd;voiceResponse=response;
            TelephoneLocationIsIllustrative = telephoneLocationIsIllustrative;
            ClearVoice();
        }
        public void ShowVoice(float emphasis, bool responding, bool moving, float phaseTime)
        {
            VoiceFocus=emphasis;
            if(block==null)block=new MaterialPropertyBlock();
            foreach(var surface in telephoneSurfaces)
            {
                if(!surface || !surface.sharedMaterial || !surface.sharedMaterial.HasProperty(FocusId))continue;
                surface.GetPropertyBlock(block);block.SetFloat(FocusId,emphasis);surface.SetPropertyBlock(block);
            }
            if(!voiceResponse)return;
            if(!inspection)inspection=GetComponent<Museum.Inspection.HelmetInspectionController>();
            bool removed=false;
            if(inspection && inspection.IsCutaway)
            {
                Vector4 plane=inspection.WorldSectionPlane;
                removed=Vector3.Dot(new Vector3(plane.x,plane.y,plane.z),voiceResponse.position)+plane.w>0;
            }
            voiceResponse.gameObject.SetActive(responding && !removed);
            // A local visual response, not a claim about the motion of a real telephone fitting.
            float size=moving ? 1f+.18f*(.5f+.5f*Mathf.Sin(phaseTime*7f)) : 1f;
            voiceResponse.localScale=Vector3.one*size;
        }
        public void ClearVoice() => ShowVoice(0,false,false,0);
        void OnDisable() => ClearVoice();
    }
}

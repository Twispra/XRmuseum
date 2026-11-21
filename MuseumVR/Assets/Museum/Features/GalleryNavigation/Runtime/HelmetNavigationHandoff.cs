namespace Museum.Diegetic
{
    public sealed partial class HelmetDiegeticStation
    {
        // Moving the rig must not become a drag, keep old narration playing behind the
        // visitor, or clear the exhibit's discoveries, era, section or rotation.
        public void PrepareForGalleryNavigation()
        {
            StopNarration();
            CancelGestures();
            if (exploration) exploration.Stop();
        }
    }
}

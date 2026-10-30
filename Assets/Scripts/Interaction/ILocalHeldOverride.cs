namespace UntitledPoolGame.Interaction
{
    // A held object that decides for itself what Attack and Interact do
    // (Play Anywhere's props are placed, not thrown or dropped), and who may
    // pick it up. On the same object as its LocalGrabbable.
    public interface ILocalHeldOverride
    {
        // While true, LocalPlayerHandController neither throws nor drops it.
        bool OverridesRelease { get; }
        bool CanBePickedBy(int playerIndex);
    }
}

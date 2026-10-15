namespace domain.Enums;

// Several devices sharing one ignition: the master owns the button and
// broadcasts its state, followers switch their outputs from that broadcast
public enum IgnitionRole
{
    Standalone,
    Master,
    Follower
}

namespace Hawa.Core.Model;

public abstract record Transition;
public sealed record LidOpened : Transition;
public sealed record Connected : Transition;
public sealed record Disconnected : Transition;
public sealed record PodRemoved(Side Side) : Transition;
public sealed record PodInserted(Side Side) : Transition;
public sealed record LowBattery(Side Side, int Percent) : Transition;

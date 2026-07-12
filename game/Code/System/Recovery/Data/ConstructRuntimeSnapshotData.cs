namespace Dxura.RP.Game;

/// <summary>
/// Runtime state for a construct in a server snapshot. This is intentionally separate from ConstructDupeItem.
/// </summary>
public class ConstructRuntimeSnapshotData
{
	public Guid ConstructId { get; set; }
	public ConstructType Type { get; set; }
	public string StateJson { get; set; } = string.Empty;
}

namespace Dxura.RP.Game;

/// <summary>
/// Runtime state that is persisted as part of a server snapshot, but never included in player dupes.
/// </summary>
public interface IConstructRuntimeState
{
	string SaveRuntimeState();
	void LoadRuntimeState( string stateJson );
}

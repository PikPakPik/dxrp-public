namespace Dxura.RP.Game;

public interface IInputHints
{
	IEnumerable<(string Action, string Label)> GetInputHints();
	int GetInputHintsHash() => 0;
}

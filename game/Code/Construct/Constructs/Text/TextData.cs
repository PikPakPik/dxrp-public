namespace Dxura.RP.Game;

public struct TextData() : IConstructData
{
	public readonly uint SchemaVersion => 2;
	public List<TextLineData> Lines { get; set; } = new List<TextLineData> { new TextLineData() };
}

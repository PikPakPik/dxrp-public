namespace Dxura.RP.Game.Wire;

public record MoneyPotWireData : IConstructData, IWireLabelData
{
	public uint SchemaVersion => 1;
	public string Label { get; set; } = string.Empty;
}

public record MoneyPotWireRuntimeState
{
	public uint Amount { get; set; }
}

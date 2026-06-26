using Dxura.RP.Game.Wire;

namespace Dxura.RP.Game.Tools;

public abstract class BaseWireConstructTool<TData>( ConstructType type ) : BaseConstructTool<TData>( type )
	where TData : IConstructData, IWireLabelData, new()
{
	[Property]
	[Title( "Label" )]
	[Description( "Optional tag to identify this wire construct in-world" )]
	[Range( 0, WireLabelHelper.MaxWireLabelLength )]
	public string Label
	{
		get => Data.Label;
		set => Data.Label = value;
	}
}

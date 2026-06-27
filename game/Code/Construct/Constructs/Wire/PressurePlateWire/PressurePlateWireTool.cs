using Dxura.RP.Game.Wire;
using Dxura.RP.Shared;

namespace Dxura.RP.Game.Tools;

[Tool( "#tool.wire.pressureplate.name", "#tool.wire.pressureplate.description", "#tool.group.sensor", Category = ToolCategory.Wire, MinimumLevel = 1 )]
public class PressurePlateWireTool() : BaseConstructTool<PressurePlateWireData>( ConstructType.PressurePlateWire )
{
	protected override Rotation FlatSurfaceRotationOffset => Rotation.From( -90, 0, 0 );

	[Property]
	[Title( "Width" )]
	[Description( "Width of the plate in units" )]
	[Range( PressurePlateWireDefinition.MinSize, PressurePlateWireDefinition.MaxSize )]
	public int Width
	{
		get => Data.Width;
		set => Data = Data with { Width = value };
	}

	[Property]
	[Title( "Length" )]
	[Description( "Length of the plate in units" )]
	[Range( PressurePlateWireDefinition.MinSize, PressurePlateWireDefinition.MaxSize )]
	public int Length
	{
		get => Data.Length;
		set => Data = Data with { Length = value };
	}

	[Property]
	[Title( "Depth" )]
	[Description( "Thickness of the plate in units" )]
	[Range( PressurePlateWireDefinition.MinDepth, PressurePlateWireDefinition.MaxDepth )]
	public int Depth
	{
		get => Data.Depth;
		set => Data = Data with { Depth = value };
	}

	[Property]
	[Title( "Filter" )]
	[Description( "What types of objects should trigger this plate" )]
	public TriggerFilterType FilterType
	{
		get => Data.FilterType;
		set => Data = Data with { FilterType = value };
	}
}

using Dxura.RP.Game.Wire;

namespace Dxura.RP.Game.Tools;

[Tool( "#tool.wire.meta.name", "#tool.wire.meta.description", "#tool.group.logic", Category = ToolCategory.Wire, MinimumLevel = 1 )]
public class MetaWireTool() : BaseConstructTool<MetaWireData>( ConstructType.MetaWire )
{
	protected override Rotation FlatSurfaceRotationOffset => Rotation.From( -90, 0, 0 );
}

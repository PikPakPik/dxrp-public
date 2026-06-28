namespace Dxura.RP.Game;

public enum SpawnType
{
	Default,
	Job,
	JobGroup,
	Tag
}

/// <summary>
/// Dictates where players will spawn when they join DXRP
/// </summary>
[Title( "DXRP Spawn Point" )]
[Category( "Game" )]
[Icon( "accessibility_new" )]
[EditorHandle( "materials/gizmo/spawnpoint.png" )]
public sealed class WorldSpawnPoint : Component
{
	public const string JailTag = "jail";

	[Property] public SpawnType Type { get; set; } = SpawnType.Default;

	[Property, Group( "Job" )] public string? JobIdentifier { get; set; }

	[Property, Group( "Job Group" )] public string? GroupIdentifier { get; set; }

	[Property, Group( "Tag" )] public new string[] Tags { get; set; } = [];

	public GameModeJobDto? Job => Type == SpawnType.Job
		? GameModeJobs.FindByReference( JobIdentifier )
		: null;

	public GameModeJobGroupDto? Group => Type == SpawnType.JobGroup
		? GameModeJobs.FindGroupByReference( GroupIdentifier )
		: null;

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Color color = Type switch
		{
			SpawnType.Job => Job?.ColorValue() ?? (Color)"#E3510D",
			SpawnType.JobGroup => Group?.ColorValue() ?? (Color)"#E3510D",
			SpawnType.Tag => Tags.Contains( JailTag ) ? (Color)"#FF4444" : (Color)"#E3510D",
			_ => (Color)"#AAAAAA"
		};

		var spawnpointModel = Model.Load( "models/editor/spawnpoint.vmdl" );
		Gizmo.Hitbox.Model( spawnpointModel );
		Gizmo.Draw.Color = color.WithAlpha( Gizmo.IsHovered || Gizmo.IsSelected ? 0.7f : 0.5f );
		var so = Gizmo.Draw.Model( spawnpointModel );
		if ( so is not null )
		{
			so.Flags.CastShadows = true;
		}
	}
}

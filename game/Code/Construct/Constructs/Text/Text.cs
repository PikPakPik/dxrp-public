using Sandbox.Diagnostics;

namespace Dxura.RP.Game;

/// <summary>
///     A component that represents a text prop in the game world.
/// </summary>
public sealed class Text() : BaseConstruct( ConstructType.Text )
{
	[Property]
	[RequireComponent]
	public required TextRenderer TextRenderer { get; set; }

	private const string RedactedText = "#generic.redacted";
	private readonly List<GameObject> _lineObjects = new();

	protected override void OnDataChanged( IConstructData oldData, IConstructData newData )
	{
		var textData = newData is TextData data ? data : default;
		var oldTextData = oldData is TextData old ? old : default;
		var lines = textData.Lines ?? new List<TextLineData> { new() };

		GameObject.Name = $"Text ({lines[0].Text})";

		// Disable the root renderer — all lines are rendered via child GameObjects
		if ( TextRenderer.IsValid() )
			TextRenderer.Enabled = false;

		// Clean up previous line child objects
		foreach ( var obj in _lineObjects )
		{
			if ( obj.IsValid() ) obj.Destroy();
		}

		_lineObjects.Clear();

		// Only render lines with content — empty lines are invisible placeholders
		var visibleLines = lines.Where( l => !string.IsNullOrEmpty( l.Text ) ).ToList();

		const float lineGap = 2f;
		var lineHeights = visibleLines.Select( l => l.FontSize * 0.1f ).ToList();
		float totalHeight = visibleLines.Count > 0
			? lineHeights.Sum() + ( visibleLines.Count - 1 ) * lineGap
			: 1f;
		float maxWidth = visibleLines.Count > 0
			? visibleLines.Max( l => MathF.Max( 1f, l.Text.Length * l.FontSize * 0.06f ) )
			: 1f;

		( Collider as BoxCollider )!.Scale = new Vector3( 2f, maxWidth, totalHeight );

		float zOffset = totalHeight / 2f;

		for ( int i = 0; i < visibleLines.Count; i++ )
		{
			var line = visibleLines[i];
			float lineH = lineHeights[i];
			float lineCenter = zOffset - lineH / 2f;

			var child = new GameObject();
			child.Parent = GameObject;
			child.Name = $"line_{i}";
			child.LocalPosition = new Vector3( 0f, 0f, lineCenter );
			_lineObjects.Add( child );

			var renderer = child.AddComponent<TextRenderer>();
			renderer.Enabled = true;
			renderer.Scale = 0.1f;
			renderer.TextScope = renderer.TextScope with
			{
				Text = line.Text,
				TextColor = line.Color,
				FontSize = line.FontSize,
				FontItalic = line.Italic,
				FontWeight = line.FontWeight,
				Outline = new TextRendering.Outline
				{
					Enabled = line.Outline, Color = line.OutlineColor, Size = line.OutlineSize
				}
			};

			zOffset -= lineH + lineGap;
		}

		if ( Networking.IsHost && !IsPreview )
		{
			ModerateLines( lines, oldTextData.Lines ?? new List<TextLineData>() );
		}
	}

	private void ModerateLines( List<TextLineData> lines, List<TextLineData> oldLines )
	{
		var player = GameUtils.GetPlayerById( Owner );
		var moderatedLines = new List<TextLineData>( lines );
		bool anyModerated = false;

		for ( int i = 0; i < lines.Count; i++ )
		{
			var line = lines[i];
			if ( string.IsNullOrEmpty( line.Text ) || line.Text == RedactedText ) continue;

			var oldText = i < oldLines.Count ? oldLines[i].Text : null;
			if ( line.Text == oldText ) continue;

			var filtered = GameManager.ModerateText( player?.SteamId ?? 0, "TEXT", line.Text );
			if ( filtered == line.Text ) continue;

			moderatedLines[i] = new TextLineData { Text = RedactedText, Color = Color.Red, FontSize = 60 };
			anyModerated = true;
		}

		if ( !anyModerated ) return;

		var serResult = Construct.Current.Serializer.Serialize( ConstructType.Text, new TextData { Lines = moderatedLines } );
		BroadcastData( serResult.IsSuccess ? serResult.Value : string.Empty );
	}
}

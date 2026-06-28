namespace Dxura.RP.Game.Tools;

[Tool( "#tool.text.name", "#tool.text.description", "#tool.group.construction" )]
public class TextTool() : BaseConstructTool<TextData>( ConstructType.Text )
{
	protected override bool FlatSurface => true;

	private readonly List<TextLineProxy> _proxies = new();

	protected override TextData GetPreviewDisplayData()
	{
		var lines = Lines;
		if ( lines.Any( l => !string.IsNullOrEmpty( l.Text ) ) ) return Data;

		var key = Input.GetButtonOrigin( "BuildMenu" );
		var previewLines = new List<TextLineData>( lines );

		if ( previewLines.Count == 0 )
			previewLines.Add( new TextLineData() );

		previewLines[0] = previewLines[0] with
		{
			Text = string.Format( Language.GetPhrase( "tool.text.preview_placeholder" ), key )
		};

		return Data with { Lines = previewLines };
	}

	public int SelectedLine { get; set; } = 0;

	public List<TextLineData> Lines => Data.Lines ?? new List<TextLineData> { new() };
	public int LineCount => Lines.Count;

	public TextLineData GetLine( int index )
	{
		var lines = Lines;
		return index >= 0 && index < lines.Count ? lines[index] : new TextLineData();
	}

	public void UpdateLine( int index, TextLineData line )
	{
		var newLines = new List<TextLineData>( Lines );
		if ( index >= 0 && index < newLines.Count )
		{
			newLines[index] = line;
			Data = Data with { Lines = newLines };
		}
	}

	public void AddLine()
	{
		if ( LineCount >= TextDefinition.MaxLines ) return;

		var newLines = new List<TextLineData>( Lines ) { new TextLineData() };
		Data = Data with { Lines = newLines };
	}

	public void RemoveLine( int index )
	{
		if ( LineCount <= 1 || index < 0 || index >= LineCount ) return;

		var newLines = new List<TextLineData>( Lines );
		newLines.RemoveAt( index );
		Data = Data with { Lines = newLines };
		_proxies.Clear();
	}

	public TextLineProxy GetLineProxy( int index )
	{
		while ( _proxies.Count <= index )
			_proxies.Add( new TextLineProxy( this, _proxies.Count ) );
		return _proxies[index];
	}

	public void ResetLines()
	{
		Data = new TextData();
		_proxies.Clear();
	}

	public override void PrimaryUseStart()
	{
		if ( Lines.All( l => string.IsNullOrEmpty( l.Text ) ) )
		{
			Notify.Error( "#notify.construct.text_empty" );
			return;
		}

		base.PrimaryUseStart();
	}
}

namespace Dxura.RP.Game.Tools;

public class TextLineProxy( TextTool tool, int index )
{
	[Property]
	[Title( "Text" )]
	[Range( TextDefinition.MinTextLength, TextDefinition.MaxTextLength )]
	public string Text
	{
		get => tool.GetLine( index ).Text;
		set => tool.UpdateLine( index, tool.GetLine( index ) with { Text = value } );
	}

	[Property]
	[Title( "Size" )]
	[Range( TextDefinition.MinFontSize, TextDefinition.MaxFontSize )]
	public float Size
	{
		get => tool.GetLine( index ).FontSize;
		set => tool.UpdateLine( index, tool.GetLine( index ) with { FontSize = value } );
	}

	[Property]
	[Title( "Color" )]
	public Color Color
	{
		get => tool.GetLine( index ).Color;
		set => tool.UpdateLine( index, tool.GetLine( index ) with { Color = value } );
	}

	[Property]
	[Title( "Italic" )]
	public bool Italic
	{
		get => tool.GetLine( index ).Italic;
		set => tool.UpdateLine( index, tool.GetLine( index ) with { Italic = value } );
	}

	[Property]
	[Title( "Font Weight" )]
	[Range( TextDefinition.MinFontWeight, TextDefinition.MaxFontWeight )]
	[Step( 100 )]
	public int FontWeight
	{
		get => tool.GetLine( index ).FontWeight;
		set => tool.UpdateLine( index, tool.GetLine( index ) with { FontWeight = value } );
	}

	[Property]
	[Title( "Outline" )]
	public bool Outline
	{
		get => tool.GetLine( index ).Outline;
		set => tool.UpdateLine( index, tool.GetLine( index ) with { Outline = value } );
	}

	[Property]
	[Title( "Outline Color" )]
	public Color OutlineColor
	{
		get => tool.GetLine( index ).OutlineColor;
		set => tool.UpdateLine( index, tool.GetLine( index ) with { OutlineColor = value } );
	}

	[Property]
	[Title( "Outline Size" )]
	[Range( TextDefinition.MinOutlineSize, TextDefinition.MaxOutlineSize )]
	[Step( 1 )]
	public float OutlineSize
	{
		get => tool.GetLine( index ).OutlineSize;
		set => tool.UpdateLine( index, tool.GetLine( index ) with { OutlineSize = value } );
	}
}

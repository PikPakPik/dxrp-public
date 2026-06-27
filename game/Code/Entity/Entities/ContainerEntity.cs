namespace Dxura.RP.Game.Entities;

public class ContainerEntityConfig
{
	public string ResourceId { get; init; } = string.Empty;
	public string IconPath { get; init; } = string.Empty;
	public string Unit { get; init; } = "units";
	public ContainerType ContainerType { get; init; } = ContainerType.Bag;
	public int DefaultQuantity { get; init; }
	public bool DestroyOnEmpty { get; init; } = true;
	public string? Tint { get; init; }
	public string? UseSound { get; init; }
}


[Title( "Container" )]
[Category( "Entities" )]
public sealed class ContainerEntity : BaseEntity
{
	[Property]
	[Sync( SyncFlags.FromHost )]
	[Change( nameof( OnQuantityChanged ) )]
	public int Quantity { get; set; }

	[Property]
	private ModelRenderer ModelRenderer { get; set; } = null!;

	[Property]
	private TextRenderer? TextRenderer { get; set; }

	[Property]
	[Group( "Effects" )]
	private Decal? Decal { get; set; }

	private ContainerEntityConfig _config = new();

	public string ResourceId => _config.ResourceId;

	public bool IsEmpty => Quantity <= 0;

	protected override void OnStart()
	{
		base.OnStart();

		_config = GetConfig( new ContainerEntityConfig() );

		UpdateState();
	}

	private void OnQuantityChanged( int oldValue, int newValue )
	{
		if ( newValue < oldValue && !string.IsNullOrEmpty( _config.UseSound ) )
		{
			Sound.Play( _config.UseSound, WorldPosition );
		}

		if ( newValue <= 0 )
		{
			if ( _config.DestroyOnEmpty )
			{
				GameObject.Destroy();
				return;
			}

			Quantity = 0;
		}

		UpdateText();
	}

	private void UpdateState()
	{
		if ( Networking.IsHost && Quantity <= 0 )
		{
			Quantity = _config.DefaultQuantity;
		}

		if ( Decal.IsValid() && !string.IsNullOrWhiteSpace( _config.IconPath ) )
		{
			var icon = Texture.LoadFromFileSystem( _config.IconPath, FileSystem.Mounted );
			if ( icon != null )
			{
				Decal.Decals = [new DecalDefinition { ColorTexture = icon }];
			}
		}

		if ( ModelRenderer.IsValid() && Color.TryParse( _config.Tint, out var tint ) )
		{
			ModelRenderer.Tint = tint;
		}

		UpdateText();
	}

	private void UpdateText()
	{
		if ( TextRenderer.IsValid() )
		{
			TextRenderer.Text = $"{_config.ResourceId} \n {Quantity} {_config.Unit}";
		}
	}

}

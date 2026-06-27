using Sandbox.Diagnostics;
namespace Dxura.RP.Game.Entities;

[Title( "Container" )]
[Category( "Entities" )]
public sealed class ContainerEntity : BaseEntity, IDescription
{
	[Property]
	[Sync( SyncFlags.FromHost )]
	[Change( nameof( OnQuantityChanged ) )]
	public int Quantity { get; set; }

	[Property]
	public ContainerType ContainerType { get; set; } = ContainerType.Bag;

	[Property]
	public int DefaultQuantity { get; set; }

	[Property]
	public string Unit { get; set; } = "units";

	[Property]
	public bool DestroyOnEmpty { get; set; } = true;

	[Property]
	public Color? Tint { get; set; }

	[Property]
	private ModelRenderer ModelRenderer { get; set; } = null!;

	[Property]
	private TextRenderer? TextRenderer { get; set; }

	[Property]
	[Group( "Effects" )]
	private SoundEvent? UseSound { get; set; }

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
		if ( newValue < oldValue )
		{
			UseSound.Play( WorldPosition );
		}

		if ( newValue <= 0 )
		{
			if ( DestroyOnEmpty )
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
			Quantity = DefaultQuantity;
		}

		if ( Decal.IsValid() && !string.IsNullOrWhiteSpace( _config.IconPath ) )
		{
			var icon = Texture.Load( FileSystem.Mounted, _config.IconPath );
			if ( icon != null )
			{
				Decal.Decals = [new DecalDefinition { ColorTexture = icon }];
			}
		}

		if ( ModelRenderer.IsValid() && Tint.HasValue )
		{
			ModelRenderer.Tint = Tint.Value;
		}

		UpdateText();
	}

	private void UpdateText()
	{
		if ( TextRenderer.IsValid() )
		{
			TextRenderer.Text = $"{_config.ResourceId} \n {Quantity} {Unit}";
		}
	}

}

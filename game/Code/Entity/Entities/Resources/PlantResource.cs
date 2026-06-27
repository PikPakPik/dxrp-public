[AssetType( Name = "Plant", Extension = "plant", Category = "DXRP" )]
public class PlantResource : GameResource
{
	public static HashSet<PlantResource> All { get; set; } = new();

	[Property]
	public string ResourceId { get; set; } = string.Empty;

	[Property]
	public Texture? Icon { get; set; }

	[Property]
	public List<int> Stages { get; set; } = new();

	[Property]
	public int TimeToGrow { get; set; }

	[Property]
	public GameObject? Harvest { get; set; } = null!;

	protected override void PostLoad()
	{
		All.Add( this );
	}
}

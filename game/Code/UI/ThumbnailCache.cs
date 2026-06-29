using System.Threading.Tasks;

namespace Dxura.RP.Game;

public static class ThumbnailCache
{
	private static readonly Dictionary<Model, Texture> ModelCache = new();
	private static readonly Dictionary<Material, Texture> MaterialCache = new();
	private static readonly Dictionary<string, Texture> CharacterPortraitCache = new();
	private static readonly LinkedList<string> CharacterPortraitOrder = new();
	private static readonly Queue<(string Key, Model Model, Action<SkinnedModelRenderer> Setup)> PortraitQueue = new();
	private static readonly HashSet<string> PendingPortraitKeys = new();
	private static readonly HashSet<string> PendingAsyncPortraitKeys = new();

	private static readonly Dictionary<string, Texture?> UrlCache = new();
	private static readonly LinkedList<string> UrlOrder = new();
	private static readonly HashSet<string> UrlLoading = new();
	private static readonly HashSet<string> UrlFailed = new();
	private static readonly Dictionary<string, List<Action>> UrlWaiters = new();

	private const int MaxUrlCacheSize = 500;
	private const int MaxCharacterPortraitCacheSize = 150;
	private const int MaxUrlBytes = 1024 * 1024 * 2;

	public static void Clear()
	{
		foreach ( var texture in ModelCache.Values )
			texture?.Dispose();

		foreach ( var texture in MaterialCache.Values )
			texture?.Dispose();

		foreach ( var texture in CharacterPortraitCache.Values )
			texture?.Dispose();

		foreach ( var texture in UrlCache.Values )
			texture?.Dispose();

		ModelCache.Clear();
		MaterialCache.Clear();
		CharacterPortraitCache.Clear();
		CharacterPortraitOrder.Clear();
		PortraitQueue.Clear();
		PendingPortraitKeys.Clear();
		PendingAsyncPortraitKeys.Clear();
		UrlCache.Clear();
		UrlOrder.Clear();
		UrlLoading.Clear();
		UrlFailed.Clear();
		UrlWaiters.Clear();
	}

	public static Texture Get( Model model )
	{
		if ( ModelCache.TryGetValue( model, out var tex ) )
			return tex;

		return GenerateTexture( model );
	}

	public static Texture Get( Material material )
	{
		if ( MaterialCache.TryGetValue( material, out var tex ) )
			return tex;

		return GenerateTexture( material );
	}

	public static bool IsPortraitCached( string key ) =>
		!string.IsNullOrWhiteSpace( key ) && CharacterPortraitCache.ContainsKey( key );

	public static Texture GetCharacterPortrait( string key, Model model, Action<SkinnedModelRenderer> setupRenderer )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return Texture.Transparent;

		if ( CharacterPortraitCache.TryGetValue( key, out var tex ) )
			return tex;

		if ( PendingPortraitKeys.Add( key ) )
			PortraitQueue.Enqueue( (key, model, setupRenderer) );

		return Texture.Transparent;
	}

	public static Texture GetCharacterPortrait( string key, GameModeJobDto job, Action<SkinnedModelRenderer> setupRenderer )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return Texture.Transparent;

		if ( CharacterPortraitCache.TryGetValue( key, out var tex ) )
			return tex;

		if ( !job.HasCloudModel() )
		{
			if ( PendingPortraitKeys.Add( key ) )
				PortraitQueue.Enqueue( (key, job.GetPrimaryModel(), setupRenderer) );
			return Texture.Transparent;
		}

		if ( PendingAsyncPortraitKeys.Add( key ) )
			_ = GenerateCloudPortraitAsync( key, job, setupRenderer );

		return Texture.Transparent;
	}

	private static async Task GenerateCloudPortraitAsync( string key, GameModeJobDto job,
		Action<SkinnedModelRenderer> setupRenderer )
	{
		var model = await job.GetPrimaryModelAsync();
		await GameTask.MainThread();

		if ( !CharacterPortraitCache.ContainsKey( key ) )
			GenerateCharacterPortrait( key, model, setupRenderer );

		PendingAsyncPortraitKeys.Remove( key );
	}

	public static void ProcessPortraitQueue( int maxCount = 1 )
	{
		var processed = 0;
		while ( processed < maxCount && PortraitQueue.Count > 0 )
		{
			var (key, model, setup) = PortraitQueue.Dequeue();
			PendingPortraitKeys.Remove( key );

			if ( CharacterPortraitCache.ContainsKey( key ) )
				continue;

			GenerateCharacterPortrait( key, model, setup );
			processed++;
		}
	}

	/// <summary>
	/// Returns the cached texture for a URL, or null if not yet loaded.
	/// Pass onLoaded to register a callback fired once when the texture becomes available.
	/// Passing the same callback on subsequent calls is safe — it is only registered once per load.
	/// </summary>
	public static Texture? GetUrl( string? url, Action? onLoaded = null )
	{
		if ( string.IsNullOrWhiteSpace( url ) )
			return null;

		if ( UrlCache.TryGetValue( url, out var cached ) )
			return cached;

		if ( UrlFailed.Contains( url ) )
			return null;

		if ( onLoaded != null )
		{
			if ( !UrlWaiters.TryGetValue( url, out var list ) )
			{
				list = new List<Action>();
				UrlWaiters[url] = list;
			}

			list.Add( onLoaded );
		}

		if ( !UrlLoading.Contains( url ) )
		{
			UrlLoading.Add( url );
			_ = LoadUrlAsync( url );
		}

		return null;
	}

	private static async Task LoadUrlAsync( string url )
	{
		try
		{
			var bytes = await Http.RequestBytesAsync( url );

			if ( bytes.Length <= 0 || bytes.Length > MaxUrlBytes )
			{
				MarkUrlFailed( url );
				return;
			}

			await GameTask.MainThread();

			var texture = Bitmap.CreateFromBytes( bytes )?.ToTexture();
			if ( texture == null )
			{
				MarkUrlFailed( url );
				return;
			}

			if ( UrlCache.Count >= MaxUrlCacheSize )
			{
				var oldest = UrlOrder.First!.Value;
				UrlOrder.RemoveFirst();
				if ( UrlCache.TryGetValue( oldest, out var old ) )
				{
					old?.Dispose();
					UrlCache.Remove( oldest );
				}
			}

			UrlCache[url] = texture;
			UrlOrder.AddLast( url );
			UrlLoading.Remove( url );
			FireWaiters( url );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"ThumbnailCache: failed to load URL ({url}): {ex.Message}" );
			MarkUrlFailed( url );
		}
	}

	private static void MarkUrlFailed( string url )
	{
		UrlLoading.Remove( url );
		UrlFailed.Add( url );
		FireWaiters( url );
	}

	private static void FireWaiters( string url )
	{
		if ( !UrlWaiters.TryGetValue( url, out var waiters ) )
			return;

		UrlWaiters.Remove( url );
		foreach ( var cb in waiters )
			cb();
	}

	private static Texture GenerateTexture( Model? model )
	{
		if ( model is null || model.IsError )
		{
			if ( model != null )
				ModelCache[model] = Texture.Invalid;

			return Texture.Transparent;
		}

		var texture = Texture.CreateRenderTarget().WithSize( 128, 128 ).Create();
		var scene = new Scene();
		using ( scene.Push() )
		{
			var modelGo = new GameObject();
			var modelRenderer = modelGo.AddComponent<ModelRenderer>();
			modelRenderer.Model = model;

			var cameraGo = new GameObject();
			var camera = cameraGo.AddComponent<CameraComponent>();
			camera.FieldOfView = 50;
			camera.BackgroundColor = Color.Transparent;

			var bounds = model.Bounds;
			var center = bounds.Center;
			var distance = bounds.Size.Length * 1.3f;
			var lightRadius = MathF.Max( bounds.Size.Length * 2.5f, 100f );

			cameraGo.WorldRotation = Rotation.From( 25, -45, 0 );
			cameraGo.WorldPosition = center + cameraGo.WorldRotation.Backward * distance;

			var lightGo = new GameObject();
			lightGo.WorldPosition = cameraGo.WorldPosition;
			lightGo.WorldRotation = Rotation.LookAt( center - lightGo.WorldPosition );

			var spotLight = lightGo.AddComponent<SpotLight>();
			spotLight.LightColor = Color.White * 3.0f;
			spotLight.Radius = lightRadius;
			spotLight.Attenuation = 0.5f;
			spotLight.ConeOuter = 60f;
			spotLight.Shadows = false;

			camera.RenderToTexture( texture );
		}

		ModelCache[model] = texture;
		return texture;
	}

	private static Texture GenerateTexture( Material? material )
	{
		if ( !material.IsValid() )
		{
			if ( material != null )
				MaterialCache[material] = Texture.Invalid;

			return Texture.Transparent;
		}

		var texture = Texture.CreateRenderTarget().WithSize( 128, 128 ).Create();
		var scene = new Scene();
		using ( scene.Push() )
		{
			var modelGo = new GameObject();
			var modelRenderer = modelGo.AddComponent<ModelRenderer>();
			modelRenderer.Model = Model.Sphere;
			modelRenderer.SetMaterialOverride( material, "" );

			var cameraGo = new GameObject();
			var camera = cameraGo.AddComponent<CameraComponent>();
			camera.FieldOfView = 60;
			camera.BackgroundColor = new Color( 0.15f, 0.15f, 0.15f );

			cameraGo.WorldPosition = new Vector3( 45, 15, 45 );
			cameraGo.WorldRotation = Rotation.LookAt( Vector3.Zero - cameraGo.WorldPosition );

			var mainLight = new GameObject();
			var directLight = mainLight.AddComponent<DirectionalLight>();
			mainLight.WorldRotation = Rotation.From( 60, -60, 0 );
			directLight.LightColor = Color.White * 1.5f;

			camera.RenderToTexture( texture );
		}

		MaterialCache[material] = texture;
		return texture;
	}

	private static Texture GenerateCharacterPortrait( string key, Model? model, Action<SkinnedModelRenderer> setupRenderer )
	{
		if ( model is null || model.IsError )
		{
			StoreCharacterPortrait( key, Texture.Transparent );
			return Texture.Transparent;
		}

		var texture = Texture.CreateRenderTarget().WithSize( 128, 128 ).Create();
		var scene = new Scene();

		using ( scene.Push() )
		{
			var modelGo = new GameObject();
			var renderer = modelGo.AddComponent<SkinnedModelRenderer>();
			renderer.Model = model;
			setupRenderer( renderer );

			var bounds = model.Bounds;
			if ( bounds.Size.LengthSquared <= 1f )
			{
				bounds = renderer.Bounds;
			}

			var height = MathF.Max( bounds.Size.z, 48f );
			var centerX = (bounds.Mins.x + bounds.Maxs.x) * 0.5f;
			var centerY = (bounds.Mins.y + bounds.Maxs.y) * 0.5f;
			modelGo.LocalPosition = new Vector3( -centerX, -centerY, -bounds.Mins.z );
			modelGo.WorldRotation = Rotation.From( 0f, 15f, 0f );

			var focusPoint = Vector3.Up * (height * 0.94f);
			foreach ( var boneName in new[] { "eyes", "eye_right", "eye_left", "eye_l", "eye_r" } )
			{
				var bone = renderer.GetBoneObject( boneName );
				if ( !bone.IsValid() )
					continue;

				var boneZ = bone.Transform.World.Position.z;
				if ( boneZ > height * 0.9f )
				{
					focusPoint = Vector3.Up * boneZ;
					break;
				}
			}

			var cameraGo = new GameObject();
			var camera = cameraGo.AddComponent<CameraComponent>();
			camera.FieldOfView = 35f;
			camera.BackgroundColor = Color.Transparent;
			camera.ZNear = 0.5f;
			camera.ZFar = 256f;

			var distance = MathF.Min( 44f, MathF.Max( 34f, height * 0.48f ) );
			cameraGo.WorldPosition = focusPoint + new Vector3( distance, -0.5f, 0f );
			cameraGo.WorldRotation = Rotation.LookAt( focusPoint - cameraGo.WorldPosition, Vector3.Up );

			var lightGo = new GameObject();
			lightGo.WorldPosition = focusPoint + Vector3.Forward * 28f + Vector3.Left * 12f + Vector3.Up * 16f;
			lightGo.WorldRotation = Rotation.LookAt( focusPoint - lightGo.WorldPosition, Vector3.Up );

			var spotLight = lightGo.AddComponent<SpotLight>();
			spotLight.LightColor = Color.White * 2f;
			spotLight.Radius = 180f;
			spotLight.Attenuation = 0.55f;
			spotLight.ConeOuter = 70f;
			spotLight.Shadows = false;

			camera.RenderToTexture( texture );
		}

		StoreCharacterPortrait( key, texture );
		return texture;
	}

	private static void StoreCharacterPortrait( string key, Texture texture )
	{
		if ( CharacterPortraitCache.ContainsKey( key ) )
		{
			CharacterPortraitCache[key] = texture;
			return;
		}

		if ( CharacterPortraitCache.Count >= MaxCharacterPortraitCacheSize )
		{
			var oldest = CharacterPortraitOrder.First!.Value;
			CharacterPortraitOrder.RemoveFirst();
			if ( CharacterPortraitCache.TryGetValue( oldest, out var old ) )
			{
				old?.Dispose();
				CharacterPortraitCache.Remove( oldest );
			}
		}

		CharacterPortraitCache[key] = texture;
		CharacterPortraitOrder.AddLast( key );
	}

	[ConCmd( "dx_clear_thumbnail_cache" )]
	public static void ToggleSoundScape()
	{
		Clear();
	}
}

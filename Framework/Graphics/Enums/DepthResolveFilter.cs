namespace Foster.Framework;

/// <summary>
/// How a multisampled depth attachment is resolved into its single-sample copy, which is what
/// shaders sample (<see cref="Target"/> with <see cref="SampleCount"/> above one). Mapped one
/// to one onto the SDL_GPU kit's render pass property. Pick by the depth convention of the
/// game: with standard Z (near = small) <see cref="Min"/> keeps edge pixels on the foreground,
/// with reverse Z <see cref="Max"/> does; <see cref="Sample0"/> takes the first sample.
/// </summary>
public enum DepthResolveFilter
{
	Sample0,
	Min,
	Max,
}

using System.Runtime.CompilerServices;

// The audio's tests read a voice's AudioSource (AudioService.SourceOf), which nothing else should touch.
[assembly: InternalsVisibleTo("MeridianMotion.Showcase.Tests.Audio")]

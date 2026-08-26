using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Sdk.Features;

public interface IAvailableFeatures
{
    public bool HasFeature(ExternalFeatureSupport feature);
}
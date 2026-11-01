using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.Sdk.Tests;

public sealed class GalleryUpdateSupportTest
{
    private abstract class BaseUpdatableParser : IUpdatableHtmlParser
    {
        public static GalleryUpdateStrategy DefaultUpdateStrategy => GalleryUpdateStrategy.FullDiff;

        public static GalleryUpdateStrategy[] SupportedUpdateStrategies =>
            [GalleryUpdateStrategy.NewItemsOnly, GalleryUpdateStrategy.FullDiff, GalleryUpdateStrategy.NewItemsOnly];

        public GalleryUpdateContext? UpdateContext { get; set; }
    }

    private sealed class DerivedUpdatableParser : BaseUpdatableParser;

    private sealed class SingleStrategyParser : IUpdatableHtmlParser
    {
        public static GalleryUpdateStrategy DefaultUpdateStrategy => GalleryUpdateStrategy.NewItemsOnly;
        public static GalleryUpdateStrategy[] SupportedUpdateStrategies => [];
        public GalleryUpdateContext? UpdateContext { get; set; }
    }

    [Fact]
    public void NonUpdatableTypeHasNoSupport()
    {
        Assert.Null(GalleryUpdateSupport.For(typeof(string)));
        Assert.Null(GalleryUpdateSupport.For(null));
    }

    [Fact]
    public void ReadsMembersDeclaredOnABaseParser()
    {
        var support = GalleryUpdateSupport.For(typeof(DerivedUpdatableParser));

        Assert.NotNull(support);
        Assert.Equal(GalleryUpdateStrategy.FullDiff, support.Default);
        Assert.Equal([GalleryUpdateStrategy.FullDiff, GalleryUpdateStrategy.NewItemsOnly], support.Supported);
    }

    [Fact]
    public void DefaultIsSupportedEvenWhenNotListed()
    {
        var support = GalleryUpdateSupport.For(typeof(SingleStrategyParser))!;

        Assert.Equal([GalleryUpdateStrategy.NewItemsOnly], support.Supported);
    }

    [Fact]
    public void ResolveUsesASupportedOverride()
    {
        var support = GalleryUpdateSupport.For(typeof(DerivedUpdatableParser))!;

        Assert.Equal(GalleryUpdateStrategy.NewItemsOnly, support.Resolve(GalleryUpdateStrategy.NewItemsOnly));
        Assert.Equal(GalleryUpdateStrategy.FullDiff, support.Resolve(null));
    }

    [Fact]
    public void ResolveFallsBackToDefaultForUnsupportedOverride()
    {
        var support = GalleryUpdateSupport.For(typeof(SingleStrategyParser))!;

        Assert.Equal(GalleryUpdateStrategy.NewItemsOnly, support.Resolve(GalleryUpdateStrategy.FullDiff));
    }
}

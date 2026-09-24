using StrBits.Core;
using Xunit;

namespace StrBits.Tests;

public sealed class ViewModelTests
{
    [Fact]
    public void InvalidInputClearsPriorResultsAndRecovers()
    {
        var vm = new MainViewModel { SelectedType = BinaryConverter.Types.Single(t => t.Kind == DataKind.Int8), Input = "42" };
        Assert.True(vm.IsValid);
        vm.Input = "999";
        Assert.True(vm.HasError);
        Assert.Null(vm.Result);
        Assert.Empty(vm.BinaryPreview);
        Assert.Empty(vm.GetCopyText());
        Assert.Equal("—", vm.PayloadSize);
        vm.LoadExample();
        Assert.True(vm.IsValid);
        Assert.Equal("11010110", vm.BinaryPreview);
    }

    [Fact]
    public void PreviewIsBoundedButCopyReportAndStatsIncludeEverything()
    {
        var vm = new MainViewModel { Input = new string('A', 1000) };
        Assert.Equal(256, vm.BinaryPreview.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(1000, vm.GetCopyText().Split(' ').Length);
        Assert.Equal(1000, vm.Result!.Bytes.Length);
        Assert.Equal(2000, vm.Result.Ones);
        Assert.Contains("Payload bytes: 1000", vm.CreateReport());
        Assert.Contains(vm.GetCopyText(), vm.CreateReport());
        vm.OutputTab = 1;
        Assert.Equal(2000, vm.GetCopyText().Length);
        vm.OutputTab = 2;
        Assert.Equal(1000, Convert.FromBase64String(vm.GetCopyText()).Length);
        Assert.StartsWith(vm.Base64Preview, vm.GetCopyText());
    }

    [Fact]
    public void EveryTypeHasAValidExample()
    {
        var vm = new MainViewModel();
        foreach (var type in vm.Types)
        {
            vm.SelectedType = type;
            vm.LoadExample();
            Assert.True(vm.IsValid, $"{type.Name}: {vm.Error}");
        }
    }

    [Fact]
    public void EmptyTextHasNoUndefinedRatios()
    {
        var vm = new MainViewModel { Input = "" };
        Assert.True(vm.IsValid);
        Assert.Equal(0, vm.OnesPercent);
        Assert.Contains("ratio n/a", vm.PayloadDetail);
        Assert.Equal("(empty)", vm.StoredValue);
    }

    [Fact]
    public void ByteOrderControlsApplyOnlyToRelevantTypes()
    {
        var vm = new MainViewModel();
        Assert.False(vm.HasByteOrder);
        vm.EncodingIndex = 1;
        Assert.True(vm.HasByteOrder);
        vm.SelectedType = vm.Types.Single(t => t.Kind == DataKind.Hex);
        Assert.False(vm.IsText);
        Assert.False(vm.HasByteOrder);
    }
}
